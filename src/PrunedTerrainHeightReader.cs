using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven;

/// <summary>
/// How a block should be treated towards the terrain surface. The enum values
/// are ordered from least hard to most hard.
/// </summary>
public enum TerrainCategory {
  // Does not count towards the surface and should be removed
  Clear,
  // Does not count towards the surface, can be overwritten as necessary, but it
  // should not be proactively removed. It will be elevated with the terrain.
  Skip,
  // Counts towards the surface level, but it is a non-solid surface.
  Nonsolid,
  // Counts towards the surface level.
  Solid,
  Default = Solid
}

public static class TerrainCategoryExtensions {
  public static bool ShouldClear(this TerrainCategory category) {
    return category == TerrainCategory.Clear;
  }

  public static bool IsSurface(this TerrainCategory category) {
    return category >= TerrainCategory.Nonsolid;
  }

  public static bool IsSolid(this TerrainCategory category) {
    return category >= TerrainCategory.Solid;
  }
}

public class PrunedTerrainHeightReader : ITerrainHeightReader {
  private readonly ITerrainHeightReader _source;
  private readonly Dictionary<int, TerrainCategory> _blocks;

  public PrunedTerrainHeightReader(ITerrainHeightReader source,
                                   Dictionary<int, TerrainCategory> blocks) {
    _source = source;
    _blocks = blocks ?? [];
    _blocks.TryAdd(0, TerrainCategory.Skip);
  }

  public (ushort[], bool[])
      GetHeightsAndSolid(IBlockAccessor accessor, int chunkX, int chunkZ) {
    ushort[] heightsSource = _source.GetHeights(accessor, chunkX, chunkZ);
    if (heightsSource == null) {
      return (null, null);
    }
    const int blocks = GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
    bool[] solid = new bool[blocks];
    ushort[] heights = new ushort[blocks];
    int offset = 0;
    BlockPos pos =
        new(Dimensions.NormalWorld) { Z = chunkZ * GlobalConstants.ChunkSize };
    int xOffset = chunkX * GlobalConstants.ChunkSize;
    for (int z = 0; z < GlobalConstants.ChunkSize; ++z, ++pos.Z) {
      pos.X = xOffset;
      for (int x = 0; x < GlobalConstants.ChunkSize; ++x, ++pos.X, ++offset) {
        pos.Y = heightsSource[offset];
        Block surface = accessor.GetBlock(pos);
        TerrainCategory category = GetCategory(surface.Id);
        while (pos.Y > 0 && !category.IsSurface()) {
          --pos.Y;
          surface = accessor.GetBlock(pos);
          category = GetCategory(surface.Id);
        }
        surface = accessor.GetBlock(pos, BlockLayersAccess.Solid);
        solid[offset] = GetCategory(surface.Id).IsSolid();
        heights[offset] = (ushort)pos.Y;
      }
    }
    return (heights, solid);
  }

  public TerrainCategory GetCategory(int blockId) {
    if (!_blocks.TryGetValue(blockId, out TerrainCategory category)) {
      return TerrainCategory.Default;
    }
    return category;
  }
}
