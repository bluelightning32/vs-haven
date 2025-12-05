using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using static Haven.ISchematicPlacerSupervisor;

namespace Haven;

/// <summary>
/// Generates a single haven. This class is thread safe so that it can be called
/// from both the main thread and world generation thread.
/// </summary>
[ProtoContract]
public class HavenGenerator : IWorldGenerator, ISchematicPlacerSupervisor {
  [ProtoMember(1)]
  private readonly ResourceZonePlan _resourceZone;

  [ProtoMember(2)]
  private readonly FlatDiskLocator _centerLocator;

  [field:ProtoMember(3)]
  public TerrainSurvey Terrain { get; }

  public IChunkLoader Loader { get; private set; }

  private ILogger _logger;

  public IWorldAccessor WorldForResolve { get; private set; }

  /// <summary>
  /// True if Generate has returned true. Commit may still need to be called.
  /// </summary>
  public bool GenerationDone { get; private set; }

  public BlockPos Center => _centerLocator.Center;

  public HavenGenerator(IWorldAccessor worldForResolve, IChunkLoader loader,
                        ILogger logger, ITerrainHeightReader reader,
                        BlockPos center, ResourceZoneConfig config) {
    WorldForResolve = worldForResolve;
    Loader = loader;
    _logger = logger;
    LCGRandom rand = new(WorldForResolve.Seed);
    rand.InitPositionSeed(center.X, center.Y, center.Z);
    _resourceZone = new(this, config, center, rand);
    Terrain = new(reader);
    _centerLocator = new(
        logger, Terrain, new(_resourceZone.Center.X, _resourceZone.Center.Z),
        (int)_resourceZone.Radius, config.MaxRoughnessPerimeter,
        config.MaxRoughnessArea, config.MinLandRatio);
  }

  public LocationResult TryFinalizeLocation(IBlockAccessor accessor,
                                            SchematicPlacer placer,
                                            BlockPos offset) {
    foreach (SchematicPlacer other in _resourceZone.Structures) {
      if (!other.IsOffsetFinal) {
        // This also excludes placer from checking itself for intersections.
        continue;
      }
      if (placer.Schematic.Intersects(offset, other.Schematic, other.Offset) !=
          null) {
        return LocationResult.Rejected;
      }
    }
    _resourceZone.ExpandRadiusIfNecessary(offset, placer.Schematic);
    // TODO: update haven intersection entries.
    return LocationResult.Accepted;
  }

  public bool Failed => _centerLocator.Failed;

  public bool Generate(IBlockAccessor accessor) {
    if (Failed) {
      return true;
    }
    if (!_centerLocator.Done) {
      if (!_centerLocator.Generate(accessor)) {
        return false;
      }
      if (_centerLocator.Failed) {
        _logger.Error(
            $"Failed to find a suitable haven location near {_resourceZone.Center}");
        GenerationDone = true;
        return true;
      }
      _resourceZone.Center = _centerLocator.Center;
      // TODO: update haven intersection entries.
    }
    bool structuresPlaced = true;
    foreach (SchematicPlacer placer in _resourceZone.Structures) {
      // Try to place all of the structures, even if some need chunks to be
      // loaded. This will enqueue as many chunk load requests as possible in
      // each iteration.
      structuresPlaced &= placer.Generate(accessor);
    }
    if (structuresPlaced) {
      GenerationDone = true;
    }
    return structuresPlaced;
  }

  public bool Commit(IBlockAccessor accessor) {
    if (Failed) {
      return true;
    }
    bool structuresCommitted = true;
    foreach (SchematicPlacer placer in _resourceZone.Structures) {
      structuresCommitted &= placer.Commit(accessor);
    }
    return structuresCommitted;
  }

  public void Restore(IWorldAccessor worldForResolve, IChunkLoader loader,
                      ILogger logger, ITerrainHeightReader reader) {
    WorldForResolve = worldForResolve;
    Loader = loader;
    _logger = logger;
    Terrain.Restore(reader);
    _centerLocator.Restore(logger, Terrain);
  }
}
