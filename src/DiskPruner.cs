using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Haven;

/// <summary>
/// Removes blocks between the terrain survey's surface and the block accessor
/// surface, if they are in the remove set.
/// </summary>
[ProtoContract]
public class DiskPruner : IWorldGenerator {
  [ProtoMember(1)]
  readonly Vec2i _center;
  [ProtoMember(2)]
  int _finishedRadius;
  [ProtoMember(4)]
  int _activeRadius;
  [ProtoMember(5)]
  int _nextRadius;
  [ProtoMember(6)]
  readonly HashSet<(int, int)> _finishedChunks = [];
  [ProtoMember(7)]
  readonly Dictionary<BlockPos, TreeAttribute> _queuedBlockEntities = [];
  private IWorldAccessor _worldForResolve;
  private IChunkLoader _loader;
  private TerrainSurvey _terrain;
  private PrunedTerrainHeightReader _pruneConfig;

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private DiskPruner() {}

  public DiskPruner(IWorldAccessor worldForResolve, IChunkLoader loader,
                    TerrainSurvey terrain,
                    PrunedTerrainHeightReader pruneConfig, Vec2i center,
                    int radius) {
    _worldForResolve = worldForResolve;
    _loader = loader;
    _terrain = terrain;
    _pruneConfig = pruneConfig;
    _center = center;
    _finishedRadius = -1;
    _activeRadius = radius;
    _nextRadius = _activeRadius;
  }

  public bool GenerateDone {
    get { return _activeRadius == _finishedRadius; }
  }

  /// <summary>
  /// Expand the radius of pruned blocks
  /// </summary>
  /// <param name="radius">the new radius, which must be larger than the
  /// previous radius</param>
  public void Expand(int radius) {
    if (_activeRadius == _finishedRadius) {
      // The new radius can be started immediately.
      _activeRadius = radius;
    } else {
      // Finish the current annulus before starting a new one. This is done to
      // prevent clearing the same block column twice, in case something else
      // was placed there in the meantime.
      _nextRadius = radius;
    }
  }

  public bool Generate(IBlockAccessor accessor) {
    if (_finishedRadius == _activeRadius) {
      return true;
    }
    bool incomplete = false;
    int maxRadiusSq = _activeRadius * _activeRadius;
    int minRadiusSq =
        _finishedRadius < 0 ? -1 : _finishedRadius * _finishedRadius;
    void FullChunk(int chunkX, int chunkZ, ChunkColumnSurvey survey) {
      if (_finishedChunks.Contains((chunkX, chunkZ))) {
        return;
      }
      ushort[] sourceHeights =
          _pruneConfig.Source.GetHeights(accessor, chunkX, chunkZ);
      if (sourceHeights == null) {
        incomplete = true;
        _loader.LoadChunkColumn(chunkX, chunkZ);
        return;
      }
      int chunkXOffset = chunkX * GlobalConstants.ChunkSize;
      int chunkZOffset = chunkZ * GlobalConstants.ChunkSize;
      int offset = 0;
      BlockPos pos = new(Dimensions.NormalWorld);
      for (int z = 0; z < GlobalConstants.ChunkSize; ++z) {
        for (int x = 0; x < GlobalConstants.ChunkSize; ++x, ++offset) {
          pos.X = chunkXOffset + x;
          pos.Z = chunkZOffset + z;
          _pruneConfig.ApplyColumnChanges(accessor, sourceHeights, pos, offset,
                                          _queuedBlockEntities);
        }
      }
      _finishedChunks.Add((chunkX, chunkZ));
    }
    void PartialChunk(int chunkX, int chunkZ, ChunkColumnSurvey survey) {
      if (_finishedChunks.Contains((chunkX, chunkZ))) {
        return;
      }
      if (_finishedChunks.Contains((chunkX, chunkZ))) {
        return;
      }
      ushort[] sourceHeights =
          _pruneConfig.Source.GetHeights(accessor, chunkX, chunkZ);
      if (sourceHeights == null) {
        incomplete = true;
        _loader.LoadChunkColumn(chunkX, chunkZ);
        return;
      }
      int chunkXOffset = chunkX * GlobalConstants.ChunkSize;
      int chunkZOffset = chunkZ * GlobalConstants.ChunkSize;
      int centerXOffset = chunkXOffset - _center.X;
      int centerZOffset = chunkZOffset - _center.Y;
      int offset = 0;
      BlockPos pos = new(Dimensions.NormalWorld);
      for (int z = 0; z < GlobalConstants.ChunkSize; ++z) {
        int zOffset = centerZOffset + z;
        int zOffsetSq = zOffset * zOffset;
        for (int x = 0; x < GlobalConstants.ChunkSize; ++x, ++offset) {
          int xOffset = centerXOffset + x;
          int distSq = zOffsetSq + xOffset * xOffset;
          if (distSq > maxRadiusSq || distSq <= minRadiusSq) {
            continue;
          }
          pos.X = chunkXOffset + x;
          pos.Z = chunkZOffset + z;
          _pruneConfig.ApplyColumnChanges(accessor, sourceHeights, pos, offset,
                                          _queuedBlockEntities);
        }
      }
      _finishedChunks.Add((chunkX, chunkZ));
    }
    _terrain.TraverseAnnulus(accessor, _center, _finishedRadius, _activeRadius,
                             FullChunk, PartialChunk, ref incomplete);
    if (incomplete) {
      return false;
    }
    _finishedChunks.Clear();
    _finishedRadius = _activeRadius;
    if (_nextRadius != _activeRadius) {
      _activeRadius = _nextRadius;
    }
    return Generate(accessor);
  }

  private TerrainCategory GetCategory(int blockId) {
    return _pruneConfig.GetCategory(blockId);
  }

  public bool Commit(IBlockAccessor accessor) {
    List<BlockPos> finished = [];
    foreach ((BlockPos pos, TreeAttribute tree) in _queuedBlockEntities) {
      if (CommitBlockEntity(accessor, pos, tree)) {
        finished.Add(pos);
      }
    }
    foreach (BlockPos pos in finished) {
      _queuedBlockEntities.Remove(pos);
    }
    return _queuedBlockEntities.Count == 0;
  }

  private bool CommitBlockEntity(IBlockAccessor accessor, BlockPos pos,
                                 TreeAttribute tree) {
    string treeBlockCode = tree.GetString("blockCode");
    if (treeBlockCode == null) {
      return true;
    }
    Block block = accessor.GetBlock(pos);
    if (block.Code != treeBlockCode) {
      if (block.Id == 0) {
        if (accessor.GetChunkAtBlockPos(pos) == null) {
          _loader.LoadChunkColumnByBlockXZ(pos.X, pos.Z);
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
    be.FromTreeAttributes(tree, _worldForResolve);
    if (accessor is not IWorldGenBlockAccessor) {
      be.MarkDirty();
    }
    return true;
  }

  /// <summary>
  /// Call this to initialize the remaining fields after the object has been
  /// deserialized.
  /// </summary>
  /// <param name="worldForResolve"></param>
  /// <param name="reader"></param>
  /// <param name="terrain"></param>
  /// <param name="blocks"></param>
  public void Restore(IWorldAccessor worldForResolve, IChunkLoader loader,
                      TerrainSurvey terrain,
                      PrunedTerrainHeightReader pruneConfig) {
    _worldForResolve = worldForResolve;
    _loader = loader;
    _terrain = terrain;
    _pruneConfig = pruneConfig;
  }
}
