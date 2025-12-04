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

  private TerrainStats GetStatsForRowOfCircle(IBlockAccessor accessor,
                                              Vec2i center, int radiusSq,
                                              int chunkZ, int zThickest,
                                              int zThinnest, ref int area,
                                              ref bool incomplete) {
    // Calculate the x coordinates for the part of the row that intersects the
    // circle.
    int zPartialOffset = zThickest - center.Y;
    int xPartialOffset =
        (int)Math.Sqrt(radiusSq - zPartialOffset * zPartialOffset);
    int chunkXPartialBegin = BlockStartToChunk(center.X - xPartialOffset);
    int chunkXPartialEnd = BlockEndToChunk(center.X + xPartialOffset);
    // Calculate the x coordinates for the part of the row that are fully
    // covered by the circle.
    int zFullOffset = zThinnest - center.Y;
    int xFullOffsetSq = radiusSq - zFullOffset * zFullOffset;
    if (xFullOffsetSq < GlobalConstants.ChunkSize * GlobalConstants.ChunkSize) {
      // No part of the row is fully covered by the circle.
      return GetStatsForCirclePartialCover(accessor, center, radiusSq,
                                           chunkXPartialBegin, chunkXPartialEnd,
                                           chunkZ, ref area, ref incomplete);
    }
    int xFullOffset = (int)Math.Sqrt(xFullOffsetSq);
    int chunkXFullBegin = BlockEndToChunk(center.X - xFullOffset);
    int chunkXFullEnd = BlockStartToChunk(center.X + xFullOffset);
    TerrainStats results = GetStatsForCirclePartialCover(
        accessor, center, radiusSq, chunkXPartialBegin, chunkXFullBegin, chunkZ,
        ref area, ref incomplete);
    area += (chunkXFullEnd - chunkXFullBegin) * GlobalConstants.ChunkSize *
            GlobalConstants.ChunkSize;
    results.Add(GetColumnRowStats(accessor, chunkXFullBegin, chunkXFullEnd,
                                  chunkZ, ref incomplete));
    results.Add(GetStatsForCirclePartialCover(
        accessor, center, radiusSq, chunkXFullEnd, chunkXPartialEnd, chunkZ,
        ref area, ref incomplete));
    return results;
  }

  private TerrainStats
  GetStatsForCirclePartialCover(IBlockAccessor accessor, Vec2i center,
                                int radiusSq, int chunkXBegin, int chunkXEnd,
                                int chunkZ, ref int area, ref bool incomplete) {
    TerrainStats results = new();
    for (int chunkX = chunkXBegin; chunkX < chunkXEnd; ++chunkX) {
      ChunkColumnSurvey chunk = GetColumn(accessor, new Vec2i(chunkX, chunkZ));
      if (chunk == null) {
        incomplete = true;
        continue;
      }
      int offset = 0;
      for (int z = 0; z < GlobalConstants.ChunkSize; ++z) {
        int zOffset = chunkZ * GlobalConstants.ChunkSize + z - center.Y;
        int zOffsetSq = zOffset * zOffset;
        for (int x = 0; x < GlobalConstants.ChunkSize; ++x, ++offset) {
          int xOffset = chunkX * GlobalConstants.ChunkSize + x - center.X;
          if (zOffsetSq + xOffset * xOffset <= radiusSq) {
            int height = chunk.Heights[offset];
            int northHeight;
            if (z == 0) {
              northHeight =
                  GetHeight(accessor, chunkX * GlobalConstants.ChunkSize + x,
                            chunkZ * GlobalConstants.ChunkSize + z - 1);
              if (northHeight == -1) {
                incomplete = true;
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
                incomplete = true;
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
            ++area;
          }
        }
      }
    }
    return results;
  }

  /// <summary>
  /// Gets the stats for all blocks covered by the given circle. A block is
  /// considered to be covered if (dist(pos,center) &lt;= radius).
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="center">circle center</param>
  /// <param name="radius">radius of circle</param>
  /// <param name="area">
  /// the number of blocks covered by the circle
  /// </param>
  /// <param name="incomplete">
  /// set to true if one or more of the requested chunks was unavailable
  /// </param>
  /// <returns>the stats for all blocks in the circle</returns>
  public TerrainStats GetCircleStats(IBlockAccessor accessor, Vec2i center,
                                     int radius, out int area,
                                     ref bool incomplete) {
    area = 0;
    int radiusSq = radius * radius;
    TerrainStats results = new();
    int y = center.Y - radius;
    // Move y to the north end of the following chunk so that it is in a known
    // location.
    y += GlobalConstants.ChunkSize - y % GlobalConstants.ChunkSize;
    // Process the northern half of the circle.
    for (; y <= center.Y; y += GlobalConstants.ChunkSize) {
      // y points to one chunk south of what needs to be added in this
      // iteration. Here, each row is thicker on the southern side of the chunk.
      results.Add(GetStatsForRowOfCircle(
          accessor, center, radiusSq, BlockStartToChunk(y) - 1, y,
          y - GlobalConstants.ChunkSize, ref area, ref incomplete));
    }

    // Process the center of the circle. y points to the row to the south of the
    // center row.
    int yThinnest = center.Y > y - GlobalConstants.ChunkSize / 2
                        ? y - GlobalConstants.ChunkSize
                        : y;
    results.Add(GetStatsForRowOfCircle(accessor, center, radiusSq,
                                       BlockStartToChunk(center.Y), center.Y,
                                       yThinnest, ref area, ref incomplete));

    // Process the southern half of the circle.
    for (; y <= center.Y + radius; y += GlobalConstants.ChunkSize) {
      // y points to the northmost block of the chunk row to be added in this
      // iteration. Here, each row is thicker on the northern side of the chunk.
      results.Add(GetStatsForRowOfCircle(
          accessor, center, radiusSq, BlockStartToChunk(y), y,
          y + GlobalConstants.ChunkSize, ref area, ref incomplete));
    }
    return results;
  }

  private static int BlockStartToChunk(int pos) {
    return pos / GlobalConstants.ChunkSize;
  }

  private static int BlockEndToChunk(int pos) {
    return BlockStartToChunk(pos + GlobalConstants.ChunkSize);
  }

  /// <summary>
  /// Sum the stats for a range of columns adjacent in the east-west direction.
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="chunkXBegin">
  /// the X coordinate for the first chunk to include
  /// </param>
  /// <param name="chunkXEnd">
  /// the X coordinate for one after the last chunk to include
  /// </param>
  /// <param name="chunkZ">the Z coordinate for all chunks in the row
  /// </param>
  /// <param name="incomplete">
  /// set to true if one or more of the requested chunks was unavailable
  /// </param>
  /// <returns>the stats for all covered chunks</returns>
  public TerrainStats GetColumnRowStats(IBlockAccessor accessor,
                                        int chunkXBegin, int chunkXEnd,
                                        int chunkZ, ref bool incomplete) {
    TerrainStats results = new();
    for (int x = chunkXBegin; x < chunkXEnd; ++x) {
      ChunkColumnSurvey chunk = GetColumn(accessor, new Vec2i(x, chunkZ));
      if (chunk != null) {
        results.Add(chunk.Stats);
        if (chunk.Stats.Roughness == -1) {
          incomplete = true;
        }
      } else {
        incomplete = true;
      }
    }
    return results;
  }
}
