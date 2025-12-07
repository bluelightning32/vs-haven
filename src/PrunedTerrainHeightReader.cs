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
  // should not be proactively removed. It will be raised with the terrain.
  Skip,
  // Counts towards the surface level, but it is a non-solid surface. Can be
  // raised.
  Nonsolid,
  // Counts towards the surface level. Can be raised.
  Solid,
  // Counts towards the surface level. Can be duplicated to raised the terrain.
  // Solid.
  Duplicate,
  // Counts towards the surface level. Cannot be raised.
  SolidHold,
  Default = SolidHold
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

  public static bool CanRaise(this TerrainCategory category) {
    return category < TerrainCategory.SolidHold;
  }

  public static bool IsRaiseStart(this TerrainCategory category,
                                  bool belowSurface) {
    return category == TerrainCategory.Duplicate ||
           (belowSurface && !IsSurface(category));
  }
}

/// <summary>
/// Reports the height of the terrain as if DiskPruner ran.
///
/// It roughly follows these steps to determine the terrain height.
/// 1. Start with the height reported by the source terrain height reader.
/// 2. Reduce the height as long as the surface block is a Clear or Skip. This
/// calculates the result of pruning the surface vegetation.
/// 3. Determine whether the surface is solid or not, based on whether the block
/// is in the Nonsolid category.
/// 4. Raise the terrain if there is a block in the Duplicate category below the
/// surface. This raise fails if a SolidHold block is found below the surface
/// before a Duplicate, Clear, or Skip block is found.
/// </summary>
public class PrunedTerrainHeightReader : ITerrainHeightReader {
  private readonly ITerrainHeightReader _source;
  private readonly Dictionary<int, TerrainCategory> _terrainCategories;
  private readonly ushort _raise;

  public PrunedTerrainHeightReader(
      ITerrainHeightReader source,
      Dictionary<int, TerrainCategory> terrainCategories, ushort raise) {
    _source = source;
    _terrainCategories = terrainCategories ?? [];
    _terrainCategories.TryAdd(0, TerrainCategory.Skip);
    _raise = raise;
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
        Block surfaceSolid = accessor.GetBlock(pos, BlockLayersAccess.Solid);
        solid[offset] = GetCategory(surfaceSolid.Id).IsSolid();
        heights[offset] = (ushort)pos.Y;

        bool canRaise = false;
        bool belowSurface = false;
        while (pos.Y >= 0 && category.CanRaise()) {
          if (category.IsRaiseStart(belowSurface)) {
            canRaise = true;
            break;
          }
          belowSurface = true;
          --pos.Y;
          surface = accessor.GetBlock(pos);
          category = GetCategory(surface.Id);
        }

        if (canRaise) {
          heights[offset] += _raise;
        }
      }
    }
    return (heights, solid);
  }

  public TerrainCategory GetCategory(int blockId) {
    if (!_terrainCategories.TryGetValue(blockId,
                                        out TerrainCategory category)) {
      return TerrainCategory.Default;
    }
    return category;
  }
}
