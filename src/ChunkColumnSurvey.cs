using System;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Haven;

[ProtoContract]
public struct TerrainStats {
  /// <summary>
  /// The sum of the X and Z step heights for all surface blocks in the area.
  /// This is set to -1 if the roughness could not be calculated because a
  /// neighbor chunk was not loaded.
  /// </summary>
  [ProtoMember(1)]
  public int Roughness = 0;
  /// <summary>
  /// The number of surface blocks not filled with water
  /// </summary>
  [ProtoMember(2)]
  public int SolidCount = 0;
  /// <summary>
  /// The sum of the y coordinate of the surface blocks in the area.
  /// </summary>
  [ProtoMember(3)]
  public int SumHeight = 0;

  public TerrainStats() {}

  public void Add(TerrainStats stats) {
    if ((Roughness | stats.Roughness) != -1) {
      Roughness += stats.Roughness;
    }
    SolidCount += stats.SolidCount;
    SumHeight += stats.SumHeight;
  }
}

[ProtoContract]
public class ChunkColumnSurvey {
  [ProtoMember(1)]
  private TerrainStats _stats = new();
  public TerrainStats Stats {
    get { return _stats; }
  }

  /// <summary>
  /// The height of every surface block in the chunk
  /// </summary>
  [ProtoMember(2)]
  public readonly ushort[] Heights;
  [ProtoMember(3)]
  public readonly bool[] Solid;

  private ChunkColumnSurvey(ushort[] heights, bool[] solid,
                            ushort[] westHeights, ushort[] northHeights) {
    Heights = heights;
    Solid = solid;
    const int chunkBlocks =
        GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
    int solidCount = 0;
    int sumHeight = 0;
    for (int offset = 0; offset < chunkBlocks; ++offset) {
      if (solid[offset]) {
        ++solidCount;
      }
      sumHeight += heights[offset];
    }
    _stats.SolidCount = solidCount;
    _stats.SumHeight = sumHeight;
    CalculateRoughness(westHeights, northHeights);
  }

  private void CalculateRoughness(ushort[] westHeights, ushort[] northHeights) {
    if (westHeights == null || northHeights == null) {
      _stats.Roughness = -1;
      return;
    }
    _stats.Roughness = 0;
    // Calculate the roughness of the northern border.
    for (int offset = 0, northOffset = (GlobalConstants.ChunkSize - 1) *
                                       GlobalConstants.ChunkSize;
         offset < GlobalConstants.ChunkSize; ++offset, ++northOffset) {
      _stats.Roughness += Math.Abs(northHeights[northOffset] - Heights[offset]);
    }
    const int chunkBlocks =
        GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
    // Calculate the roughness of the western border.
    for (int offset = 0, westOffset = GlobalConstants.ChunkSize - 1;
         offset < chunkBlocks; offset += GlobalConstants.ChunkSize,
             westOffset += GlobalConstants.ChunkSize) {
      _stats.Roughness += Math.Abs(westHeights[westOffset] - Heights[offset]);
    }
    // Calculate the west-east roughness inside the chunk
    for (int offset = 0; offset < chunkBlocks; ++offset) {
      for (int x = 1; x < GlobalConstants.ChunkSize; ++x, ++offset) {
        _stats.Roughness += Math.Abs(Heights[offset] - Heights[offset + 1]);
      }
    }
    // Calculate the north-south roughness inside the chunk
    for (int offset1 = 0, offset2 = GlobalConstants.ChunkSize;
         offset2 < chunkBlocks; ++offset1, ++offset2) {
      _stats.Roughness += Math.Abs(Heights[offset1] - Heights[offset2]);
    }
  }

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private ChunkColumnSurvey() {}

  public ushort GetHeight(int relX, int relZ) {
    return Heights[relZ * GlobalConstants.ChunkSize + relX];
  }

  public bool IsSolid(int relX, int relZ) {
    return Solid[relZ * GlobalConstants.ChunkSize + relX];
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
  /// a chunk survey with everything filled in except for possibly the
  /// Roughness, or null if the block accessor cannot currently load the chunk
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
    (ushort[] heights, bool[] solid) =
        reader.GetHeightsAndSolid(accessor, chunkX, chunkZ);

    // All of the chunk heights are requested above, even if some of the heights
    // are null. This is because GetHeights can trigger loading the chunks, and
    // all chunk load requests should be triggered in one pass.
    if (heights == null) {
      return null;
    }
    return new ChunkColumnSurvey(heights, solid, westHeights, northHeights);
  }

  /// <summary>
  /// Calculates the chunk roughness if the neighbor data was not available when
  /// the survey was first created.
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="reader"></param>
  /// <param name="chunkX"></param>
  /// <param name="chunkZ"></param>
  /// <param name="westNeighbor"></param>
  /// <param name="northNeighbor"></param>
  public void CalculateRoughness(ChunkColumnSurvey westNeighbor,
                                 ChunkColumnSurvey northNeighbor) {
    CalculateRoughness(westNeighbor?.Heights, northNeighbor?.Heights);
  }
}
