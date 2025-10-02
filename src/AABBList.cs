using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.MathTools;

namespace Haven;

/// <summary>
/// Contains a list of block positions. This data structure is optimized for the
/// case where most block positions are adjacent such that multiple can be
/// stored with an adjacent aligned bounding box.
/// </summary>
public class AABBList {
  public List<Cuboidi> Regions { get; private set; }
  /// <summary>
  /// The tightest upper bound on the Y coordinates of the region.
  /// </summary>
  public int MaxY {
    get {
      int result = 0;
      foreach (Cuboidi c in Regions) {
        result = Math.Max(result, c.Y2);
      }
      return result;
    }
  }
  /// <summary>
  /// The tightest lower bound on the Y coordinates of the region.
  /// </summary>
  public int MinY {
    get {
      int result = int.MaxValue;
      foreach (Cuboidi c in Regions) {
        result = Math.Min(result, c.Y1);
      }
      return result;
    }
  }
  public AABBList() { Regions = []; }

  public AABBList(IEnumerable<BlockPos> positions) {
    HashSet<BlockPos> remainingPositions = [..positions];
    Regions = [];
    while (remainingPositions.Count > 0) {
      BlockPos first = remainingPositions.First();
      Cuboidi region = new(first, 1);
      remainingPositions.Remove(first);
      int availableDirections = 0x3F;
      while (availableDirections > 0) {
        if ((availableDirections & 0x1) != 0) {
          if (!TryGrowX(region, true, remainingPositions)) {
            availableDirections &= ~0x1;
          }
        }
        if ((availableDirections & 0x2) != 0) {
          if (!TryGrowX(region, false, remainingPositions)) {
            availableDirections &= ~2;
          }
        }
        if ((availableDirections & 0x4) != 0) {
          if (!TryGrowY(region, true, remainingPositions)) {
            availableDirections &= ~0x4;
          }
        }
        if ((availableDirections & 0x8) != 0) {
          if (!TryGrowY(region, false, remainingPositions)) {
            availableDirections &= ~0x8;
          }
        }
        if ((availableDirections & 0x10) != 0) {
          if (!TryGrowZ(region, true, remainingPositions)) {
            availableDirections &= ~0x10;
          }
        }
        if ((availableDirections & 0x20) != 0) {
          if (!TryGrowZ(region, false, remainingPositions)) {
            availableDirections &= ~0x20;
          }
        }
      }
      for (int x = region.MinX; x < region.MaxX; ++x) {
        for (int y = region.MinY; y < region.MaxY; ++y) {
          for (int z = region.MinZ; z < region.MaxZ; ++z) {
            remainingPositions.Remove(new BlockPos(x, y, z));
          }
        }
      }
      Regions.Add(region);
    }
  }

  public bool Contains(BlockPos pos) {
    foreach (Cuboidi region in Regions) {
      if (region.Contains(pos)) {
        return true;
      }
    }
    return false;
  }

  /// <summary>
  /// Determines if this AABB list contains (fully encloses) another one.
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset">an offset to translate other when doing the
  /// intersection</param>
  /// <returns>true if this contains the other region</returns>
  public bool Contains(AABBList with, Vec3i withOffset) {
    foreach (Cuboidi region in with.Regions) {
      if (!Contains(region, withOffset)) {
        return false;
      }
    }
    return true;
  }

  /// <summary>
  /// Determines if this AABB list contains (fully encloses) another one.
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset">an offset to translate other when doing the
  /// intersection</param>
  /// <returns>true if this contains the other region</returns>
  public bool Contains(Cuboidi with, Vec3i withOffset) {
    List<Cuboidi> remaining = [with.OffsetCopy(withOffset)];
    while (remaining.Count > 0) {
      Cuboidi check = remaining.Last();
      remaining.RemoveAt(remaining.Count - 1);
      bool intersected = false;
      foreach (Cuboidi region in Regions) {
        if (region.Intersects(check)) {
          // The region overlaps the check region. Add any remaining parts of
          // check back into the list.
          if (check.X1 < region.X1) {
            remaining.Add(new(check.X1, check.Y1, check.Z1, region.X1, check.Y2,
                              check.Z2));
          }
          if (check.X2 > region.X2) {
            remaining.Add(new(region.X2, check.Y1, check.Z1, check.X2, check.Y2,
                              check.Z2));
          }
          check.X1 = Math.Max(check.X1, region.X1);
          check.X2 = Math.Min(check.X2, region.X2);
          if (check.Y1 < region.Y1) {
            remaining.Add(new(check.X1, check.Y1, check.Z1, check.X1, region.Y1,
                              check.Z2));
          }
          if (check.Y2 > region.Y2) {
            remaining.Add(new(check.X1, region.Y2, check.Z1, check.X2, check.Y2,
                              check.Z2));
          }
          check.Y1 = Math.Max(check.Y1, region.Y1);
          check.Y2 = Math.Min(check.Y2, region.Y2);
          if (check.Z1 < region.Z1) {
            remaining.Add(new(check.X1, check.Y1, check.Z1, check.X1, check.Y1,
                              region.Z1));
          }
          if (check.Z2 > region.Z2) {
            remaining.Add(new(check.X1, check.Y1, region.Z2, check.X2, check.Y2,
                              check.Z2));
          }
          intersected = true;
          break;
        }
      }
      if (!intersected) {
        return false;
      }
    }
    return true;
  }

  /// <summary>
  /// Determines if this AABB list intersects with another one.
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset">an offset to translate other when doing the
  /// intersection</param>
  /// <returns>the intersecting AABB from this AABB list that intersects, or
  /// null if there is no intersection</returns>
  public Cuboidi Intersects(Cuboidi with, Vec3i withOffset) {
    foreach (Cuboidi region in Regions) {
      if (region.Intersects(with, withOffset)) {
        return region;
      }
    }
    return null;
  }

  /// <summary>
  /// Determines if this AABB list intersects with another one.
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset">an offset to translate other when doing the
  /// intersection</param>
  /// <returns>the intersecting AABB from `with` that intersects, or null if
  /// there is no intersection</returns>
  public Cuboidi Intersects(AABBList with, Vec3i withOffset) {
    foreach (Cuboidi withRegion in with.Regions) {
      Cuboidi intersection = Intersects(withRegion, withOffset);
      if (intersection != null) {
        return withRegion;
      }
    }
    return null;
  }

  /// <summary>
  /// Moves withOffset in direction to avoid an intersection with another
  /// schematic
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset"></param>
  /// <param name="direction">direction to move with to avoid an
  /// intersection. This should be a unit vector</param>
  /// <returns>true if there was no intersection, or false if withOffset was
  /// updated to avoid an intersection</returns>
  public bool AvoidIntersection(AABBList with, Vec3i withOffset,
                                Vec3d direction) {
    double distance = 0.0;
    Vec3i initialOffset = withOffset.Clone();
    while (true) {
      Cuboidi withCube = Intersects(with, withOffset);
      if (withCube == null) {
        return distance == 0.0;
      }
      distance += 1;
      withOffset.X = (int)(initialOffset.X + direction.X * distance);
      withOffset.Y = (int)(initialOffset.Y + direction.Y * distance);
      withOffset.Z = (int)(initialOffset.Z + direction.Z * distance);
      while (true) {
        Cuboidi myCube = Intersects(withCube, withOffset);
        if (myCube == null) {
          break;
        }
        do {
          distance += 1;
          withOffset.X = (int)(initialOffset.X + direction.X * distance);
          withOffset.Y = (int)(initialOffset.Y + direction.Y * distance);
          withOffset.Z = (int)(initialOffset.Z + direction.Z * distance);
        } while (myCube.Intersects(withCube, withOffset));
      }
    }
  }

  private static bool TryGrowX(Cuboidi region, bool grow,
                               HashSet<BlockPos> remaining) {
    int x = grow ? region.MaxX : region.MinX - 1;
    for (int y = region.MinY; y < region.MaxY; ++y) {
      for (int z = region.MinZ; z < region.MaxZ; ++z) {
        if (!remaining.Contains(new BlockPos(x, y, z))) {
          return false;
        }
      }
    }
    if (grow) {
      region.X2++;
    } else {
      region.X1--;
    }
    return true;
  }

  private static bool TryGrowY(Cuboidi region, bool grow,
                               HashSet<BlockPos> remaining) {
    int y = grow ? region.MaxY : region.MinY - 1;
    for (int x = region.MinX; x < region.MaxX; ++x) {
      for (int z = region.MinZ; z < region.MaxZ; ++z) {
        if (!remaining.Contains(new BlockPos(x, y, z))) {
          return false;
        }
      }
    }
    if (grow) {
      region.Y2++;
    } else {
      region.Y1--;
    }
    return true;
  }

  private static bool TryGrowZ(Cuboidi region, bool grow,
                               HashSet<BlockPos> remaining) {
    int z = grow ? region.MaxZ : region.MinZ - 1;
    for (int x = region.MinX; x < region.MaxX; ++x) {
      for (int y = region.MinY; y < region.MaxY; ++y) {
        if (!remaining.Contains(new BlockPos(x, y, z))) {
          return false;
        }
      }
    }
    if (grow) {
      region.Z2++;
    } else {
      region.Z1--;
    }
    return true;
  }

  public void GrowUp(int v) {
    int maxY = MaxY;
    foreach (Cuboidi c in Regions) {
      if (c.Y2 == maxY) {
        c.Y2 += v;
      }
    }
  }

  public void GrowDown(int v) {
    int minY = MinY;
    foreach (Cuboidi c in Regions) {
      if (c.Y1 == minY) {
        c.Y1 -= v;
      }
    }
  }
}
