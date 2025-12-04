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
                                              Vec2i center, int radius,
                                              int chunkY, int yThickest,
                                              ref int chunkCount,
                                              ref bool incomplete) {
    int yoffset = yThickest - center.Y;
    int xoffset = (int)Math.Sqrt(radius * radius - yoffset * yoffset);
    int chunkXBegin = BlockStartToChunk(center.X - xoffset);
    int chunkXEnd = BlockEndToChunk(center.X + xoffset);
    chunkCount += chunkXEnd - chunkXBegin;
    return GetColumnRowStats(accessor, chunkXBegin, chunkXEnd, chunkY,
                             ref incomplete);
  }

  /// <summary>
  /// Gets the stats for all chunks that intersect the given circle. If a chunk
  /// intersects the circle at all, then the stats for the whole chunk are
  /// included.
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="center">circle center</param>
  /// <param name="radius">radius of circle</param>
  /// <param name="chunkCount">
  /// the number of chunks the circle overlaps with, including missing chunks
  /// </param>
  /// <param name="incomplete">
  /// set to true if one or more of the requested chunks was unavailable
  /// </param>
  /// <returns>the stats for all intersected chunks</returns>
  public TerrainStats GetRoughCircleStats(IBlockAccessor accessor, Vec2i center,
                                          int radius, out int chunkCount,
                                          ref bool incomplete) {
    chunkCount = 0;
    TerrainStats results = new();
    int y = center.Y - radius;
    // Move y to the north end of the following chunk so that it is in a known
    // location.
    y += GlobalConstants.ChunkSize - y % GlobalConstants.ChunkSize;
    // Process the northern half of the circle.
    for (; y <= center.Y; y += GlobalConstants.ChunkSize) {
      // y points to one chunk south of what needs to be added in this
      // iteration. Here, each row is thicker on the southern side of the chunk.
      results.Add(GetStatsForRowOfCircle(accessor, center, radius,
                                         BlockStartToChunk(y) - 1, y,
                                         ref chunkCount, ref incomplete));
    }

    // Process the center of the circle.
    results.Add(GetStatsForRowOfCircle(accessor, center, radius,
                                       BlockStartToChunk(center.Y), center.Y,
                                       ref chunkCount, ref incomplete));

    // Process the southern half of the circle.
    for (; y < center.Y + radius; y += GlobalConstants.ChunkSize) {
      // y points to the northmost block of the chunk row to be added in this
      // iteration. Here, each row is thicker on the northern side of the chunk.
      results.Add(GetStatsForRowOfCircle(accessor, center, radius,
                                         BlockStartToChunk(y), y,
                                         ref chunkCount, ref incomplete));
    }
    return results;
  }

  private static int BlockStartToChunk(int pos) {
    return pos / GlobalConstants.ChunkSize;
  }

  private static int BlockEndToChunk(int pos) {
    return BlockStartToChunk(pos + GlobalConstants.ChunkSize - 1);
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
