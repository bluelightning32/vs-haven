using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
  RaiseStart,
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
    return category == TerrainCategory.RaiseStart ||
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
  public readonly ITerrainHeightReader Source;
  private readonly Dictionary<int, TerrainCategory> _terrainCategories;
  public ushort Raise;

  public PrunedTerrainHeightReader(
      ITerrainHeightReader source,
      Dictionary<int, TerrainCategory> terrainCategories, ushort raise) {
    Source = source;
    _terrainCategories = terrainCategories ?? [];
    _terrainCategories.TryAdd(0, TerrainCategory.Skip);
    Raise = raise;
  }

  /// <summary>
  /// Gets the block at the surface and the surface position
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="pos">
  /// On input, this is the surface according to the source reader. On output,
  /// this is the surface after taking the Clear and Skip blocks into account.
  /// </param>
  /// <returns>the block at the new surface</returns>
  internal Block GetSurfaceBeforeRaise(IBlockAccessor accessor, BlockPos pos) {
    Block surface = accessor.GetBlock(pos);
    TerrainCategory category = GetCategory(surface.Id);
    while (pos.Y > 0 && !category.IsSurface()) {
      --pos.Y;
      surface = accessor.GetBlock(pos);
      category = GetCategory(surface.Id);
    }
    return surface;
  }

  /// <summary>
  /// Finds the block at or below the surface where the terrain raise should
  /// start.
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="pos">
  /// On input, this is the surface according to GetSurfaceBeforeRaise. On
  /// output, this is the raise start, if any.
  /// </param>
  /// <param name="surface">the block at the surface</param>
  /// <returns>the block at the raise start, or null if the column should not be
  /// raised</returns>
  internal Block GetRaiseStart(IBlockAccessor accessor, BlockPos pos,
                               Block surface) {
    bool belowSurface = false;
    TerrainCategory category = GetCategory(surface.Id);
    while (pos.Y >= 0 && category.CanRaise()) {
      if (category.IsRaiseStart(belowSurface)) {
        return surface;
      }
      belowSurface = true;
      --pos.Y;
      surface = accessor.GetBlock(pos);
      category = GetCategory(surface.Id);
    }
    return null;
  }

  public (ushort[], bool[])
      GetHeightsAndSolid(IBlockAccessor accessor, int chunkX, int chunkZ) {
    ushort[] heightsSource = Source.GetHeights(accessor, chunkX, chunkZ);
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
        Block surface = GetSurfaceBeforeRaise(accessor, pos);

        Block surfaceSolid = accessor.GetBlock(pos, BlockLayersAccess.Solid);
        solid[offset] = GetCategory(surfaceSolid.Id).IsSolid();
        heights[offset] = (ushort)pos.Y;

        if (GetRaiseStart(accessor, pos, surface) != null) {
          heights[offset] += Raise;
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

  /// <summary>
  /// Modify the terrain as described by this reader. Note that reading the
  /// terrain again after this may return a different result (it may say to
  /// raise the terrain again).
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="sourceHeights"></param>
  /// <param name="pos"></param>
  /// <param name="offset"></param>
  public void
  ApplyColumnChanges(IBlockAccessor accessor, ushort[] sourceHeights,
                     BlockPos pos, int offset,
                     Dictionary<BlockPos, TreeAttribute> queuedBlockEntities) {
    int mapHeight = sourceHeights[offset];
    pos.Y = mapHeight;
    Block surface = GetSurfaceBeforeRaise(accessor, pos);
    // First prune the blocks above the surface.
    int prunedHeight = pos.Y;
    for (int y = mapHeight; y > prunedHeight; --y) {
      pos.Y = y;
      int existing = accessor.GetBlockId(pos);
      if (GetCategory(existing).ShouldClear()) {
        accessor.SetBlock(0, pos);
      }
    }

    if (Raise <= 0) {
      // None of the columns should be raised.
      return;
    }

    // Now possibly raise the column.
    Block raiseBlock = GetRaiseStart(accessor, pos, surface);
    if (raiseBlock == null) {
      // Don't raise this column.
      return;
    }
    int raiseStart = pos.Y;

    // Check if there are more blocks above the surface that the terrain reader
    // missed.
    pos.Y = mapHeight;
    surface = accessor.GetBlock(pos, BlockLayersAccess.Solid);
    while (surface.Id != 0 &&
           GetCategory(surface.BlockId) != TerrainCategory.SolidHold) {
      ++mapHeight;
      pos.Y = mapHeight;
      surface = accessor.GetBlock(pos, BlockLayersAccess.Solid);
    }

    BlockPos moveTo = new(pos.X, 0, pos.Z, pos.dimension);
    for (int y = mapHeight; y > raiseStart; --y) {
      pos.Y = y;
      moveTo.Y = y + Raise;
      CopyBlock(accessor, pos, moveTo, queuedBlockEntities);
    }
    if (GetCategory(raiseBlock.Id) == TerrainCategory.RaiseStart) {
      pos.Y = raiseStart;
      for (int y = raiseStart + Raise; y > raiseStart; --y) {
        moveTo.Y = y;
        CopyBlock(accessor, pos, moveTo, queuedBlockEntities);
      }
    } else {
      for (int y = raiseStart + Raise; y > raiseStart; --y) {
        moveTo.Y = y;
        accessor.SetBlock(0, moveTo);
      }
    }
  }

  private void
  CopyBlock(IBlockAccessor accessor, BlockPos pos, BlockPos moveTo,
            Dictionary<BlockPos, TreeAttribute> queuedBlockEntities) {
    CopyBlock(accessor, pos, moveTo, BlockLayersAccess.Solid);
    CopyBlock(accessor, pos, moveTo, BlockLayersAccess.Fluid);
    BlockEntity be = accessor.GetBlockEntity(pos);
    if (be != null) {
      TreeAttribute tree = new();
      be.ToTreeAttributes(tree);
      queuedBlockEntities.Add(moveTo.Copy(), tree);
    }
    Dictionary<int, Block> decors = accessor.GetSubDecors(pos);
    if (decors != null) {
      foreach ((int face, Block decor) in decors) {
        accessor.SetDecor(decor, pos, face);
      }
    }
  }

  private void CopyBlock(IBlockAccessor accessor, BlockPos pos, BlockPos moveTo,
                         int layer) {
    Block source = accessor.GetBlock(pos, layer);
    accessor.SetBlock(source.Id, moveTo, layer);
  }

  public static bool CommitBlockEntity(IWorldAccessor worldForResolve,
                                       IChunkLoader loader,
                                       IBlockAccessor accessor, BlockPos pos,
                                       TreeAttribute tree) {
    string treeBlockCode = tree.GetString("blockCode");
    if (treeBlockCode == null) {
      return true;
    }
    Block block = accessor.GetBlock(pos);
    if (block.Code != treeBlockCode) {
      if (block.Id == 0) {
        if (accessor.GetChunkAtBlockPos(pos) == null) {
          loader.LoadChunkColumnByBlockXZ(pos.X, pos.Z);
          // Try again when the chunk is loaded.
          return false;
        }
      }
      // Some other block was placed at the target location in the meantime. So
      // give up on updating the block entity for the old block.
      HavenSystem.Logger.Warning(
          "A block of {0} was placed at {1} before the block entity for " +
              "block {2} could be restored.",
          block.Code, pos, treeBlockCode);
      return true;
    }
    if (block.EntityClass == null) {
      // Something went wrong. This block was not supposed to have a block
      // entity.
      HavenSystem.Logger.Error(
          "Tried to restore a block entity for block {0}, but that block " +
              "does not have a block entity.",
          block.Code);
      return true;
    }
    BlockEntity be = accessor.GetBlockEntity(pos);
    if (be == null) {
      accessor.SpawnBlockEntity(block.EntityClass, pos);
      be = accessor.GetBlockEntity(pos);
      if (be == null) {
        // Failed to spawn the block entity. Give up.
        HavenSystem.Logger.Error("Failed to spawn a block entity of type {0}",
                                 block.EntityClass);
        return true;
      }
    }
    tree.SetInt("posx", pos.X);
    tree.SetInt("posy", pos.InternalY);
    tree.SetInt("posz", pos.Z);
    be.FromTreeAttributes(tree, worldForResolve);
    if (accessor is not IWorldGenBlockAccessor) {
      be.MarkDirty();
    }
    return true;
  }
}
