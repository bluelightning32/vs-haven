using System;
using System.Collections.Generic;
using System.Diagnostics;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven;

public class ResourceZonePlan {
  public class ChunkColumnStatus {
    /// <summary>
    /// Indicates whether this column has alread been generated. If it has been
    /// generated, then new structures cannot be moved into this chunk if the
    /// plan needs to be adjusted for some reason.
    /// </summary>
    internal bool Generated = false;
    /// <summary>
    /// The list of structures that need to be generated in this chunk column.
    /// Each entry is a structure start position, which also serves as a key to
    /// Structures.
    /// </summary>
    internal List<BlockPos> StructureStarts = new();
  }
  public BlockPos Center { get; private set; }
  public double Radius { get; private set; }
  private readonly Dictionary<BlockPos, OffsetBlockSchematic> _structures =
      new();
  public IReadOnlyDictionary<BlockPos, OffsetBlockSchematic> Structures {
    get { return _structures; }
  }
  private readonly Dictionary<Vec2i, ChunkColumnStatus> _chunkColumns = new();
  /// <summary>
  /// This is a sparse dictionary of the status of the chunk columns. It is
  /// indexed by chunk coordinates (block x z position divided by 32). If a
  /// column is missing from the dictionary, that means that either it has not
  /// been generated yet and will not have any structures in it, or the column
  /// is not part of the resource zone.
  /// </summary>
  public IReadOnlyDictionary<Vec2i, ChunkColumnStatus> ChunkColumns {
    get { return _chunkColumns; }
  }
  /// <summary>
  /// The number of chunk columns that still need to be generated and contain
  /// part of a structure.
  /// </summary>
  private int _remainingColumns = 0;

  /// <summary>
  /// Construct a resource zone plan. The initial plan lays out the structures
  /// in the XZ plane. The Y coordinates of the structures are not set, because
  /// the terrain height is not available at this stage.
  /// </summary>
  /// <param name="center"></param>
  /// <param name="minRadius"></param>
  /// <param name="rand"></param>
  /// <param name="structures"></param>
  public ResourceZonePlan(BlockPos center, double minRadius, IRandom rand,
                          IEnumerable<OffsetBlockSchematic> structures) {
    Center = center;
    Radius = minRadius;
    double radiusSq = Radius * Radius;
    List<KeyValuePair<Vec2d, OffsetBlockSchematic>> placement = new();
    // Select 2d random positions for each structure. These positions are stored
    // as rectangle centers relative to the center of the resource zone.
    foreach (OffsetBlockSchematic structure in structures) {
      // GetRandomRectCenterInCircle can't handle the case where the rectangle
      // does not fit in the circle. So expand the circle first if necessary.
      double structHalfWidth = structure.SizeX / 2.0;
      double structHalfDepth = structure.SizeZ / 2.0;
      double structRadiusSq =
          structHalfWidth * structHalfWidth + structHalfDepth * structHalfDepth;
      if (structRadiusSq > radiusSq) {
        radiusSq = structRadiusSq;
        Radius = Math.Sqrt(radiusSq);
      }
      Vec2d location = GetRandomRectCenterInCircle(rand, structure.SizeX,
                                                   structure.SizeZ, Radius);
      Debug.Assert(!Double.IsNaN(location.X));
      Debug.Assert(!Double.IsNaN(location.Y));
      placement.Add(new(location, structure));
    }
    // Sort the structures by their distance from the center of the zone. This
    // is done so that as structure overlaps are resolved by moving structures
    // away from the center, it minimizes the chances of the new structure
    // overlapping with any already checked structures.
    placement.Sort(CompareDistance);
    // Start resolving overlaps. For all structures at or after i, their
    // position is a structure center offset relative to the zone center. For
    // all structures before i, their position is a structure start (lower
    // position) relative to the zone center. As long as the positions are
    // doubles, they are kept as relative positions to avoid floating point
    // rounding problems.
    for (int i = 0; i < placement.Count; ++i) {
      BlockPos iPos =
          StructureCenterToStart(placement[i].Key, placement[i].Value);
      // moveDir is the position vector of the structure at i relative to the
      // center of the zone. If this structure overlaps with any others, it will
      // be moved in this direction (away from the zone center) until the
      // overlap is resolved.
      Vec3d moveDir = new(placement[i].Key.X, 0, placement[i].Key.Y);
      if (moveDir.X == 0 && moveDir.Z == 0) {
        // Since the vector is 0, set it to something non-zero before trying to
        // normalize it.
        moveDir.X = 1;
        moveDir.Y = 1;
      }
      moveDir.Normalize();
      Debug.Assert(!Double.IsNaN(moveDir.X));
      Debug.Assert(!Double.IsNaN(moveDir.Z));
      bool avoidedIntersection = true;
      // Keep iterating through all the previously placed structures until the
      // structure at i does not overlap with any of them.
      do {
        avoidedIntersection = true;
        for (int j = 0; j < i; ++j) {
          BlockPos jPos = new((int)placement[j].Key.X + Center.X, 0,
                              (int)placement[j].Key.Y + Center.Z);
          // Even if avoidedIntersection becomes false, continue through the
          // rest of the placed structures, because the i structure is more
          // likely to overlap with them than the already iterated structures.
          // The outer loop will still take another pass through everything, but
          // by continuing this inner loop, the next pass is more likely to
          // succeed.
          avoidedIntersection &= placement[i].Value.AvoidIntersection(
              iPos, placement[j].Value, jPos, moveDir);
        }
      } while (!avoidedIntersection);
      // This structure is now considered placed. So convert its location into a
      // relative lower bound offset.
      placement[i].Key.X = iPos.X - Center.X;
      placement[i].Key.Y = iPos.Z - Center.Z;
      // Also add the structure to the official dictionary. This for loop
      // continues to iterate through placement instead of _structures, because
      // placement is sorted.
      _structures[iPos] = placement[i].Value;
      ExpandRadiusIfNecessary(iPos, placement[i].Value);
    }

    InitializeChunkColumns();
  }

  /// <summary>
  /// Initialize _chunkColumns. _structures must be initialized beforehand.
  /// </summary>
  private void InitializeChunkColumns() {
    Debug.Assert(_chunkColumns.Count == 0);
    foreach (KeyValuePair<BlockPos, OffsetBlockSchematic> structure in
                 _structures) {
      int minX = structure.Key.X / GlobalConstants.ChunkSize;
      int maxX = structure.Key.Z / GlobalConstants.ChunkSize;
      int minZ =
          (structure.Key.X + structure.Value.SizeX) / GlobalConstants.ChunkSize;
      int maxZ =
          (structure.Key.Z + structure.Value.SizeZ) / GlobalConstants.ChunkSize;
      for (int z = minZ; z <= maxZ; ++z) {
        for (int x = minX; x <= maxX; ++x) {
          Vec2i chunkKey = new(x, z);
          if (!_chunkColumns.TryGetValue(chunkKey,
                                         out ChunkColumnStatus status)) {
            status = new();
            _chunkColumns.Add(chunkKey, status);
          }
          status.StructureStarts.Add(structure.Key);
        }
      }
    }
    _remainingColumns = _chunkColumns.Count;
  }

  /// <summary>
  /// Expands the zone radius to include a structure
  /// </summary>
  /// <param name="startPos">the start position (lower bound) for the
  /// structure</param> <param name="structure"></param>
  private void ExpandRadiusIfNecessary(BlockPos startPos,
                                       OffsetBlockSchematic structure) {
    double radiusSq = Radius * Radius;
    // Calculate the rectangle upper bound position of the structure, then move
    // it to the first quadrant using abs.
    double x = Math.Abs(startPos.X - Center.X + structure.SizeX / 2.0) +
               structure.SizeX / 2.0;
    double z = Math.Abs(startPos.Z - Center.Z + structure.SizeZ / 2.0) +
               structure.SizeZ / 2.0;
    double needRadiusSq = x * x + z * z;
    if (needRadiusSq > radiusSq) {
      Radius = Math.Sqrt(needRadiusSq);
    }
  }

  /// <summary>
  /// Converts a structure center position relative to the zone center into a
  /// structure start position relative to the zone center.
  /// </summary>
  /// <param name="center"></param>
  /// <param name="structure"></param>
  /// <returns></returns>
  private BlockPos StructureCenterToStart(Vec2d center,
                                          OffsetBlockSchematic structure) {
    return new((int)(center.X - structure.SizeX / 2.0) + Center.X, 0,
               (int)(center.Y - structure.SizeZ / 2.0) + Center.Z);
  }

  private int CompareDistance(KeyValuePair<Vec2d, OffsetBlockSchematic> a,
                              KeyValuePair<Vec2d, OffsetBlockSchematic> b) {
    double aDist = a.Key.LengthSq();
    double bDist = b.Key.LengthSq();
    if (aDist < bDist) {
      return -1;
    } else if (aDist == bDist) {
      return 0;
    } else {
      return 1;
    }
  }

  /// <summary>
  /// Finds the distance between a point (x,y) inside a circle and the boundary
  /// of the circle, when the point is translated at the given angle.
  /// </summary>
  /// <param name="angle">angle to translate the point</param>
  /// <param name="radius">radius of the circle</param>
  /// <param name="x">x coordinate of the point to translate</param>
  /// <param name="y">y coordinate of the point to translate</param>
  /// <returns>distance</returns>
  public static double GetPointToCircleDist(double x, double y, double angle,
                                            double radius) {
    (double sin, double cos) = Math.SinCos(angle);
    return GetPointToCircleDist(x, y, new Vec2d(cos, sin), radius);
  }

  /// <summary>
  /// Finds the distance between a point (x,y) inside a circle and the boundary
  /// of the circle, when the point is translated along the given unit vector.
  /// </summary>
  /// <param name="u">unit vector to translate the point along</param>
  /// <param name="radius">radius of the circle</param>
  /// <param name="x">x coordinate of the point to translate</param>
  /// <param name="y">y coordinate of the point to translate</param>
  /// <returns>distance</returns>
  public static double GetPointToCircleDist(double x, double y, Vec2d u,
                                            double radius) {
    // This is based on
    // https://mathworld.wolfram.com/Circle-LineIntersection.html.
    double d = x * u.Y - y * u.X;
    return Math.Sqrt(radius * radius - d * d) - x * u.X - y * u.Y;
  }

  /// <summary>
  /// Gets the distance between a rectangle centered inside a circle and the
  /// border of the circle. The distance is measured along the given unit
  /// vector.
  /// </summary>
  /// <param name="rWidth">width of the rectangle</param>
  /// <param name="rHeight">height of the rectangle</param>
  /// <param name="u">unit vector to measure the distance along</param>
  /// <param name="radius">radius of the circle</param>
  /// <returns>the distance between the rectangle and the circle. If the
  /// rectangle is translated this distance along the unit vector, then one of
  /// its corners will touch the circle boundary.</returns>
  public static double GetRectToCircleDist(double rWidth, double rHeight,
                                           Vec2d u, double radius) {
    // Find the rectangle corner which is closest to the circle along the unit
    // vector.
    double x = u.X < 0 ? -rWidth / 2 : rWidth / 2;
    double y = u.Y < 0 ? -rHeight / 2 : rHeight / 2;
    return GetPointToCircleDist(x, y, u, radius);
  }

  /// <summary>
  /// Gets the distance between a rectangle centered inside a circle and the
  /// border of the circle. The distance is measured along the given unit
  /// vector.
  /// </summary>
  /// <param name="rWidth">width of the rectangle</param>
  /// <param name="rHeight">height of the rectangle</param>
  /// <param name="angle">angle to measure the distance along</param>
  /// <param name="radius">radius of the circle</param>
  /// <returns>the distance between the rectangle and the circle. If the
  /// rectangle is translated this distance along the unit vector, then one of
  /// its corners will touch the circle boundary. Or if the rectangle is larger
  /// than the circle, a negative number is returned.</returns>
  public static double GetRectToCircleDist(double rWidth, double rHeight,
                                           double angle, double radius) {
    (double sin, double cos) = Math.SinCos(angle);
    return GetRectToCircleDist(rWidth, rHeight, new Vec2d(cos, sin), radius);
  }

  /// <summary>
  /// Randomly select a point within the circle such that the rectangle fits
  /// within the circle with that point as its center. The given rectangle must
  /// fit within the circle.
  /// </summary>
  /// <param name="rand">random number generator</param>
  /// <param name="rWidth">width of the rectangle</param>
  /// <param name="rHeight">height of the rectangle</param>
  /// <param name="radius">radius of the circle</param>
  /// <returns>randomly selected center position for the rectangle</returns>
  public static Vec2d GetRandomRectCenterInCircle(IRandom rand, double rWidth,
                                                  double rHeight,
                                                  double radius) {
    // Let x be a uniformly random number between 0 to 1. If every point within
    // a circle of area a could be enumerated in order, then P(x*a) would be a
    // uniformly random point in the circle.
    //
    // Let's say the points of the circle are enumerated starting from the
    // center of the circle and spirling out. Then the following equation shows
    // P(x*a)'s distance from the center of the circle.
    //   x * a = pi * s^2
    //   x * a / pi = s^2
    //   s = sqrt(x * a / pi)
    // Now if a is calculated as a = pi * r^2, then s is:
    //   s = sqrt(x * (pi * r^2) / pi)
    //   s = sqrt(x * r^2)
    //   s = sqrt(x) * r
    //
    // So a random point in the circle can be selected by first picking a random
    // angle, then picking a distance from the center of the circle with sqrt(x)
    // * r.
    double angle = rand.NextDouble() * Math.Tau;
    (double sin, double cos) = Math.SinCos(angle);
    Vec2d u = new(cos, sin);
    // Find how far the rectangle can be moved in that direction without leaving
    // the circle. This is used to effectively shrink the radius used to select
    // the rectangle center position.
    double adjustedRadius = GetRectToCircleDist(rWidth, rHeight, u, radius);
    double centerDist = Math.Sqrt(rand.NextDouble()) * adjustedRadius;
    return u * centerDist;
  }
}
