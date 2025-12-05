using System;
using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven;

[ProtoContract]
public class TerrainSurvey {
  [ProtoMember(1)]
  private readonly Dictionary<Vec2i, ChunkColumnSurvey> _chunks = [];

  private ITerrainHeightReader _reader = null;

  public TerrainSurvey(ITerrainHeightReader reader) {
    ArgumentNullException.ThrowIfNull(reader);
    _reader = reader;
  }

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private TerrainSurvey() {}

  /// <summary>
  /// Returns the height at the location, if it can be immediately loaded
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="blockX"></param>
  /// <param name="blockZ"></param>
  /// <returns>the height if the chunk is available, or -1 if the chunk is
  /// unavailable</returns>
  public int GetHeight(IBlockAccessor accessor, int blockX, int blockZ) {
    int chunkX = blockX / GlobalConstants.ChunkSize;
    int chunkZ = blockZ / GlobalConstants.ChunkSize;
    ChunkColumnSurvey column = GetColumn(accessor, chunkX, chunkZ);
    if (column == null) {
      return -1;
    }
    return column.GetHeight(blockX % GlobalConstants.ChunkSize,
                            blockZ % GlobalConstants.ChunkSize);
  }

  /// <summary>
  /// Determine whether the surface is solid
  /// </summary>
  /// <param name="accessor">An accessor for reading map chunks</param>
  /// <param name="blockX"></param>
  /// <param name="blockZ"></param>
  /// <returns>1 if the block is solid, 0 if it is not, or -1 if the chunk is
  /// unloaded</returns>
  public int IsSolid(IBlockAccessor accessor, int blockX, int blockZ) {
    int chunkX = blockX / GlobalConstants.ChunkSize;
    int chunkZ = blockZ / GlobalConstants.ChunkSize;
    ChunkColumnSurvey column = GetColumn(accessor, chunkX, chunkZ);
    if (column == null) {
      return -1;
    }
    return column.IsSolid(blockX % GlobalConstants.ChunkSize,
                          blockZ % GlobalConstants.ChunkSize)
               ? 1
               : 0;
  }

  public ChunkColumnSurvey GetColumn(IBlockAccessor accessor, int chunkX,
                                     int chunkZ) {
    return GetColumn(accessor, new Vec2i(chunkX, chunkZ));
  }

  internal ChunkColumnSurvey
  GetColumnWithoutRoughnessUpdate(IBlockAccessor accessor, Vec2i pos) {
    if (_chunks.TryGetValue(pos, out ChunkColumnSurvey result)) {
      return result;
    } else {
      result =
          ChunkColumnSurvey.Create(accessor, _reader, pos.X, pos.Y, null, null);
      if (result != null) {
        _chunks.Add(pos, result);
      }
      return result;
    }
  }

  public ChunkColumnSurvey GetColumn(IBlockAccessor accessor, Vec2i pos) {
    if (_chunks.TryGetValue(pos, out ChunkColumnSurvey result)) {
      if (result.Stats.Roughness == -1) {
        // The neighbors were not available when this chunk was first loaded.
        // Try again now.
        ChunkColumnSurvey west = GetColumnWithoutRoughnessUpdate(
            accessor, new Vec2i(pos.X - 1, pos.Y));
        ChunkColumnSurvey north = GetColumnWithoutRoughnessUpdate(
            accessor, new Vec2i(pos.X, pos.Y - 1));
        result.CalculateRoughness(west, north);
      }
      return result;
    } else {
      ChunkColumnSurvey west = GetColumnWithoutRoughnessUpdate(
          accessor, new Vec2i(pos.X - 1, pos.Y));
      ChunkColumnSurvey north = GetColumnWithoutRoughnessUpdate(
          accessor, new Vec2i(pos.X, pos.Y - 1));
      result = ChunkColumnSurvey.Create(accessor, _reader, pos.X, pos.Y, west,
                                        north);
      if (result != null) {
        _chunks.Add(pos, result);
      }
      return result;
    }
  }

  /// <summary>
  /// Call this to initialize the remaining fields after the object has been
  /// deserialized.
  /// </summary>
  /// <param name="reader"></param>
  public void Restore(ITerrainHeightReader reader) {
    ArgumentNullException.ThrowIfNull(reader);
    if (_reader != null) {
      throw new InvalidOperationException(
          "The TerrainSurvey is already initialized.");
    }
    _reader = reader;
  }

  private void
  TraverseAnnulusRow(IBlockAccessor accessor, Vec2i center, int holeRadiusSq,
                     int radiusSq, int chunkZ, int zThickest, int zThinnest,
                     Action<int, int, ChunkColumnSurvey> traverseFullChunk,
                     Action<int, int, ChunkColumnSurvey> traversePartialChunk,
                     ref bool incomplete) {
    // Calculate the x coordinates for the part of the row that intersects the
    // disk.
    int zPartialOffset = zThickest - center.Y;
    int xOuterPartialOffset =
        (int)Math.Sqrt(radiusSq - zPartialOffset * zPartialOffset);
    int chunkXOuterPartialBegin =
        BlockStartToChunk(center.X - xOuterPartialOffset);
    int chunkXOuterPartialEnd = BlockEndToChunk(center.X + xOuterPartialOffset);
    // Calculate the x coordinates for the part of the row that are fully
    // covered by the disk.
    int zFullOffset = zThinnest - center.Y;
    int xOuterFullOffsetSq = radiusSq - zFullOffset * zFullOffset;
    if (xOuterFullOffsetSq <
        GlobalConstants.ChunkSize * GlobalConstants.ChunkSize) {
      // No part of the row is fully covered by the disk.
      TraverseRow(accessor, chunkXOuterPartialBegin, chunkXOuterPartialEnd,
                  chunkZ, traversePartialChunk, ref incomplete);
      return;
    }
    int xOuterFullOffset = (int)Math.Sqrt(xOuterFullOffsetSq);
    int chunkXOuterFullBegin = BlockEndToChunk(center.X - xOuterFullOffset);
    int chunkXOuterFullEnd = BlockStartToChunk(center.X + xOuterFullOffset);

    int xInnerPartialOffsetSq = holeRadiusSq - zPartialOffset * zPartialOffset;
    if (xInnerPartialOffsetSq < 0) {
      // No part of the row is covered by the hole.
      TraverseRow(accessor, chunkXOuterPartialBegin, chunkXOuterFullBegin,
                  chunkZ, traversePartialChunk, ref incomplete);
      TraverseRow(accessor, chunkXOuterFullBegin, chunkXOuterFullEnd, chunkZ,
                  traverseFullChunk, ref incomplete);
      TraverseRow(accessor, chunkXOuterFullEnd, chunkXOuterPartialEnd, chunkZ,
                  traversePartialChunk, ref incomplete);
      return;
    }
    int xInnerPartialOffset = (int)Math.Sqrt(xInnerPartialOffsetSq);
    int chunkXInnerPartialBegin =
        BlockStartToChunk(center.X - xInnerPartialOffset);
    int chunkXInnerPartialEnd = BlockEndToChunk(center.X + xInnerPartialOffset);

    // Handle the outer sections of the annulus.
    if (chunkXOuterFullBegin < chunkXInnerPartialBegin) {
      TraverseRow(accessor, chunkXOuterPartialBegin, chunkXOuterFullBegin,
                  chunkZ, traversePartialChunk, ref incomplete);
      TraverseRow(accessor, chunkXOuterFullBegin, chunkXInnerPartialBegin,
                  chunkZ, traverseFullChunk, ref incomplete);
    } else {
      TraverseRow(accessor, chunkXOuterPartialBegin, chunkXInnerPartialBegin,
                  chunkZ, traversePartialChunk, ref incomplete);
    }
    if (chunkXOuterFullEnd > chunkXInnerPartialEnd) {
      TraverseRow(accessor, chunkXInnerPartialEnd, chunkXOuterFullEnd, chunkZ,
                  traverseFullChunk, ref incomplete);
      TraverseRow(accessor, chunkXOuterFullEnd, chunkXOuterPartialEnd, chunkZ,
                  traversePartialChunk, ref incomplete);
    } else {
      TraverseRow(accessor, chunkXInnerPartialEnd, chunkXOuterPartialEnd,
                  chunkZ, traversePartialChunk, ref incomplete);
    }

    // Handle the inner edge of the annulus.
    int xInnerFullOffsetSq = holeRadiusSq - zFullOffset * zFullOffset;
    if (xInnerFullOffsetSq <
        GlobalConstants.ChunkSize * GlobalConstants.ChunkSize) {
      // No part of the row is fully covered by the hole.
      TraverseRow(accessor, chunkXInnerPartialBegin, chunkXInnerPartialEnd,
                  chunkZ, traversePartialChunk, ref incomplete);
      return;
    }
    int xInnerFullOffset = (int)Math.Sqrt(xInnerFullOffsetSq);
    int chunkXInnerFullBegin = BlockEndToChunk(center.X - xInnerFullOffset);
    int chunkXInnerFullEnd = BlockStartToChunk(center.X + xInnerFullOffset);

    TraverseRow(accessor, chunkXInnerPartialBegin, chunkXInnerFullBegin, chunkZ,
                traversePartialChunk, ref incomplete);
    TraverseRow(accessor, chunkXInnerFullEnd, chunkXInnerPartialEnd, chunkZ,
                traversePartialChunk, ref incomplete);
  }

  private void TraverseRow(IBlockAccessor accessor, int chunkXBegin,
                           int chunkXEnd, int chunkZ,
                           Action<int, int, ChunkColumnSurvey> traverseChunk,
                           ref bool incomplete) {
    for (int chunkX = chunkXBegin; chunkX < chunkXEnd; ++chunkX) {
      ChunkColumnSurvey chunk = GetColumn(accessor, new Vec2i(chunkX, chunkZ));
      if (chunk == null) {
        incomplete = true;
        continue;
      }
      traverseChunk(chunkX, chunkZ, chunk);
    }
  }

  public void
  TraverseAnnulus(IBlockAccessor accessor, Vec2i center, int holeRadius,
                  int radius,
                  Action<int, int, ChunkColumnSurvey> traverseFullChunk,
                  Action<int, int, ChunkColumnSurvey> traversePartialChunk,
                  ref bool incomplete) {
    int holeRadiusSq = holeRadius < 0 ? -1 : holeRadius * holeRadius;
    int radiusSq = radius * radius;
    int y = center.Y - radius;
    // Move y to the north end of the following chunk so that it is in a known
    // location.
    y += GlobalConstants.ChunkSize - y % GlobalConstants.ChunkSize;
    // Process the northern half of the disk.
    for (; y <= center.Y; y += GlobalConstants.ChunkSize) {
      // y points to one chunk south of what needs to be added in this
      // iteration. Here, each row is thicker on the southern side of the chunk.
      TraverseAnnulusRow(accessor, center, holeRadiusSq, radiusSq,
                         BlockStartToChunk(y) - 1, y,
                         y - GlobalConstants.ChunkSize, traverseFullChunk,
                         traversePartialChunk, ref incomplete);
    }

    // Process the center of the disk. y points to the row to the south of the
    // center row.
    int yThinnest = center.Y > y - GlobalConstants.ChunkSize / 2
                        ? y - GlobalConstants.ChunkSize
                        : y;
    TraverseAnnulusRow(accessor, center, holeRadiusSq, radiusSq,
                       BlockStartToChunk(center.Y), center.Y, yThinnest,
                       traverseFullChunk, traversePartialChunk, ref incomplete);

    // Process the southern half of the disk.
    for (; y <= center.Y + radius; y += GlobalConstants.ChunkSize) {
      // y points to the northmost block of the chunk row to be added in this
      // iteration. Here, each row is thicker on the northern side of the chunk.
      TraverseAnnulusRow(accessor, center, holeRadiusSq, radiusSq,
                         BlockStartToChunk(y), y, y + GlobalConstants.ChunkSize,
                         traverseFullChunk, traversePartialChunk,
                         ref incomplete);
    }
  }

  public void
  TraverseDisk(IBlockAccessor accessor, Vec2i center, int radius,
               Action<int, int, ChunkColumnSurvey> traverseFullChunk,
               Action<int, int, ChunkColumnSurvey> traversePartialChunk,
               ref bool incomplete) {
    TraverseAnnulus(accessor, center, 0, radius, traverseFullChunk,
                    traversePartialChunk, ref incomplete);
  }

  /// <summary>
  /// Gets the stats for all blocks covered by the given disk. A block is
  /// considered to be covered if (dist(pos,center) &lt;= radius).
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="center">disk center</param>
  /// <param name="radius">radius of disk</param>
  /// <param name="area">
  /// the number of blocks covered by the disk
  /// </param>
  /// <param name="incomplete">
  /// set to true if one or more of the requested chunks was unavailable
  /// </param>
  /// <returns>the stats for all blocks in the disk</returns>
  public TerrainStats GetDiskStats(IBlockAccessor accessor, Vec2i center,
                                   int radius, out int area,
                                   ref bool incomplete) {
    return GetAnnulusStats(accessor, center, -1, radius, out area,
                           ref incomplete);
  }

  /// <summary>
  /// Gets the stats for all blocks covered by the given annulus. A block is
  /// considered to be covered if (minRadius &lt;= dist(pos,center) &lt;=
  /// maxRadius).
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="center">annulus center</param>
  /// <param name="holeRadius">block closer to the center than this are part of
  /// the hole</param> <param name="radius">radius of the outer disk</param>
  /// <param name="area">
  /// the number of blocks covered by the annulus
  /// </param>
  /// <param name="incomplete">
  /// set to true if one or more of the requested chunks was unavailable
  /// </param>
  /// <returns>the stats for all blocks in the annulus</returns>
  public TerrainStats GetAnnulusStats(IBlockAccessor accessor, Vec2i center,
                                      int holeRadius, int radius, out int area,
                                      ref bool incomplete) {
    int localArea = 0;
    bool localIncomplete = false;
    TerrainStats results = new();
    int holeRadiusSq = holeRadius < 0 ? -1 : holeRadius * holeRadius;
    int radiusSq = radius * radius;
    void TraverseFullChunk(int chunkX, int chunkZ, ChunkColumnSurvey chunk) {
      localArea += GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
      results.Add(chunk.Stats);
      if (chunk.Stats.Roughness == -1) {
        localIncomplete = true;
      }
    }
    void TraversePartialChunk(int chunkX, int chunkZ, ChunkColumnSurvey chunk) {
      int offset = 0;
      for (int z = 0; z < GlobalConstants.ChunkSize; ++z) {
        int zOffset = chunkZ * GlobalConstants.ChunkSize + z - center.Y;
        int zOffsetSq = zOffset * zOffset;
        for (int x = 0; x < GlobalConstants.ChunkSize; ++x, ++offset) {
          int xOffset = chunkX * GlobalConstants.ChunkSize + x - center.X;
          int distSq = zOffsetSq + xOffset * xOffset;
          if (holeRadiusSq < distSq && distSq <= radiusSq) {
            int height = chunk.Heights[offset];
            int northHeight;
            if (z == 0) {
              northHeight =
                  GetHeight(accessor, chunkX * GlobalConstants.ChunkSize + x,
                            chunkZ * GlobalConstants.ChunkSize + z - 1);
              if (northHeight == -1) {
                localIncomplete = true;
                northHeight = height;
              }
            } else {
              northHeight = chunk.Heights[offset - GlobalConstants.ChunkSize];
            }
            results.Roughness += Math.Abs(northHeight - height);
            int westHeight;
            if (x == 0) {
              westHeight = GetHeight(accessor,
                                     chunkX * GlobalConstants.ChunkSize + x - 1,
                                     chunkZ * GlobalConstants.ChunkSize + z);
              if (westHeight == -1) {
                localIncomplete = true;
                westHeight = height;
              }
            } else {
              westHeight = chunk.Heights[offset - 1];
            }
            results.Roughness += Math.Abs(westHeight - height);
            if (chunk.Solid[offset]) {
              ++results.SolidCount;
            }
            results.SumHeight += height;
            ++localArea;
          }
        }
      }
    }
    TraverseAnnulus(accessor, center, holeRadius, radius, TraverseFullChunk,
                    TraversePartialChunk, ref incomplete);
    incomplete |= localIncomplete;
    area = localArea;
    return results;
  }

  private static int BlockStartToChunk(int pos) {
    return pos / GlobalConstants.ChunkSize;
  }

  private static int BlockEndToChunk(int pos) {
    return BlockStartToChunk(pos + GlobalConstants.ChunkSize);
  }
}
