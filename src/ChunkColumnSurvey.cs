using System;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Haven;

[ProtoContract]
public class ChunkColumnSurvey {
  /// <summary>
  /// The sum of the X and Z step heights for all surface blocks in the chunk
  /// plus the north and west neighbors
  /// </summary>
  [ProtoMember(1)]
  public readonly int TotalRoughness = 0;
  /// <summary>
  /// The number of surface blocks at or above sea level in the chunk
  /// </summary>
  [ProtoMember(2)]
  public readonly int TotalAboveSea = 0;
  /// <summary>
  /// The height of every surface block in the chunk
  /// </summary>
  [ProtoMember(3)]
  public readonly ushort[] Heights;

  private ChunkColumnSurvey(ushort[] heights, ushort[] westHeights,
                            ushort[] northHeights) {
    Heights = heights;
    // Calculate the roughness of the northern border.
    for (int offset = 0, northOffset = (GlobalConstants.ChunkSize - 1) *
                                       GlobalConstants.ChunkSize;
         offset < GlobalConstants.ChunkSize; ++offset, ++northOffset) {
      TotalRoughness += Math.Abs(northHeights[northOffset] - heights[offset]);
    }
    const int chunkBlocks =
        GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
    // Calculate the roughness of the western border.
    for (int offset = 0, westOffset = GlobalConstants.ChunkSize - 1;
         offset < chunkBlocks; offset += GlobalConstants.ChunkSize,
             westOffset += GlobalConstants.ChunkSize) {
      TotalRoughness += Math.Abs(westHeights[westOffset] - heights[offset]);
    }
    // Calculate the west-east roughness inside the chunk
    for (int offset = 0; offset < chunkBlocks; ++offset) {
      for (int x = 1; x < GlobalConstants.ChunkSize; ++x, ++offset) {
        TotalRoughness += Math.Abs(Heights[offset] - heights[offset + 1]);
      }
    }
    // Calculate the north-south roughness inside the chunk
    for (int offset1 = 0, offset2 = GlobalConstants.ChunkSize;
         offset2 < chunkBlocks; ++offset1, ++offset2) {
      TotalRoughness += Math.Abs(Heights[offset1] - heights[offset2]);
    }
    for (int offset = 0; offset < chunkBlocks; ++offset) {
      if (heights[offset] >= Climate.Sealevel) {
        ++TotalAboveSea;
      }
    }
  }

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private ChunkColumnSurvey() {}

  public ushort GetHeight(int relX, int relZ) {
    return Heights[relZ * GlobalConstants.ChunkSize + relX];
  }

  /// <summary>
  /// Create a ChunkSurvey
  /// </summary>
  /// <param name="accessor">
  /// accessor for reading the terrain height
  /// </param>
  /// <param name="reader">
  /// reader that knows how to get the terrain height and trigger loading new
  /// chunks if necessary
  /// </param>
  /// <param name="westNeighbor">
  /// an already completed survey for the chunk to the north, or null if that
  /// survey is not completed yet
  /// </param>
  /// <param name="northNeighbor">
  /// an already completed survey for the chunk to the west, or null if that
  /// survey is not completed yet
  /// </param>
  /// <returns>
  /// a completed chunk survey, or null if the block accessor does not have one
  /// of the necessary chunks loaded
  /// </returns>
  public static ChunkColumnSurvey Create(IBlockAccessor accessor,
                                         ITerrainHeightReader reader,
                                         int chunkX, int chunkZ,
                                         ChunkColumnSurvey westNeighbor,
                                         ChunkColumnSurvey northNeighbor) {
    ushort[] westHeights;
    if (westNeighbor != null) {
      westHeights = westNeighbor.Heights;
    } else {
      westHeights = reader.GetHeights(accessor, chunkX - 1, chunkZ);
    }
    ushort[] northHeights;
    if (northNeighbor != null) {
      northHeights = northNeighbor.Heights;
    } else {
      northHeights = reader.GetHeights(accessor, chunkX, chunkZ - 1);
    }
    ushort[] heights = reader.GetHeights(accessor, chunkX, chunkZ);
    // All of the chunk heights are requested above, even if some of the heights
    // are null. This is because GetHeights can trigger loading the chunks, and
    // all chunk load requests should be triggered in one pass.
    if (westHeights == null || northHeights == null || heights == null) {
      return null;
    }
    return new ChunkColumnSurvey(heights, westHeights, northHeights);
  }
}
