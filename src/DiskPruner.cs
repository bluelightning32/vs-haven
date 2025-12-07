using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

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

  IChunkLoader _loader;
  TerrainSurvey _terrain;
  PrunedTerrainHeightReader _pruneConfig;

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private DiskPruner() {}

  public DiskPruner(IChunkLoader loader, TerrainSurvey terrain,
                    PrunedTerrainHeightReader pruneConfig,
                    Vec2i center, int radius) {
    _loader = loader;
    _terrain = terrain;
    _pruneConfig = pruneConfig;
    _center = center;
    _finishedRadius = -1;
    _activeRadius = radius;
    _nextRadius = _activeRadius;
  }

  public bool Done {
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

  private void ProcessColumn(IBlockAccessor accessor, IMapChunk chunk,
                             ChunkColumnSurvey survey, BlockPos pos,
                             int offset) {
    int surveyHeight = survey.Heights[offset];
    int mapHeight = chunk.RainHeightMap[offset];
    for (int y = mapHeight; y > surveyHeight; --y) {
      pos.Y = y;
      int existing = accessor.GetBlockId(pos);
      if (GetCategory(existing).ShouldClear()) {
        accessor.SetBlock(0, pos);
      }
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
      IMapChunk chunk = accessor.GetMapChunk(chunkX, chunkZ);
      if (chunk == null) {
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
          ProcessColumn(accessor, chunk, survey, pos, offset);
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
      IMapChunk chunk = accessor.GetMapChunk(chunkX, chunkZ);
      if (chunk == null) {
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
          ProcessColumn(accessor, chunk, survey, pos, offset);
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

  public bool Commit(IBlockAccessor accessor) { return true; }

  /// <summary>
  /// Call this to initialize the remaining fields after the object has been
  /// deserialized.
  /// </summary>
  /// <param name="reader"></param>
  /// <param name="terrain"></param>
  /// <param name="blocks"></param>
  public void Restore(IChunkLoader loader, TerrainSurvey terrain,
                      PrunedTerrainHeightReader pruneConfig) {
    _loader = loader;
    _terrain = terrain;
    _pruneConfig = pruneConfig;
  }
}
