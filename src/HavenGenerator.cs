using System;

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

  [ProtoMember(4)]
  private DiskPruner _pruneResourceZone = null;

  /// <summary>
  /// Radius of the entire haven.
  /// </summary>
  [ProtoMember(5)]
  private int _radius = 0;

  public IChunkLoader Loader { get; private set; }

  public ILogger Logger { get; private set; }

  public IWorldAccessor WorldForResolve { get; private set; }

  /// <summary>
  /// True if Generate has returned true. Commit may still need to be called.
  /// </summary>
  public bool GenerationDone { get; private set; }

  public BlockPos Center => _centerLocator.Center;

  private PrunedTerrainHeightReader _reader;
  private Action<BlockPos, int, HavenRegionIntersection> _havenUpdate;

  public HavenGenerator(
      IWorldAccessor worldForResolve, IChunkLoader loader, ILogger logger,
      PrunedTerrainHeightReader reader,
      Action<BlockPos, int, HavenRegionIntersection> havenUpdate,
      BlockPos center, int radius, ResourceZoneConfig config) {
    WorldForResolve = worldForResolve;
    Loader = loader;
    Logger = logger;
    _radius = radius;
    LCGRandom rand = new(WorldForResolve.Seed);
    rand.InitPositionSeed(center.X, center.Y, center.Z);
    _resourceZone = new(this, config, center, rand);
    Terrain = new(reader);
    _centerLocator = new(
        logger, Terrain, new(_resourceZone.Center.X, _resourceZone.Center.Z),
        (int)_resourceZone.Radius, config.MaxRoughnessPerimeter,
        config.MaxRoughnessArea, config.MinLandRatio);
    _reader = reader;
    _havenUpdate = havenUpdate;
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
    if (_resourceZone.ExpandRadiusIfNecessary(offset, placer.Schematic)) {
      _pruneResourceZone.Expand((int)_resourceZone.Radius);
      if (!_pruneResourceZone.Generate(accessor)) {
        return LocationResult.TryAgain;
      }
      int oldRadius = _radius;
      _radius = int.Max(_radius, (int)_resourceZone.Radius);
      HavenRegionIntersection intersection =
          new() { Center = _resourceZone.Center,
                  ResourceZoneRadius = (int)_resourceZone.Radius,
                  Radius = _radius };
      _havenUpdate.Invoke(_resourceZone.Center, oldRadius, intersection);
    }
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
        Logger.Error(
            $"Failed to find a suitable haven location near {_resourceZone.Center}");
        GenerationDone = true;
        return true;
      }
      _resourceZone.Center = _centerLocator.Center;
      HavenRegionIntersection intersection =
          new() { Center = _resourceZone.Center,
                  ResourceZoneRadius = (int)_resourceZone.Radius,
                  Radius = _radius };
      _havenUpdate.Invoke(null, 0, intersection);
      _pruneResourceZone =
          new(WorldForResolve, Loader, Terrain, _reader,
              new Vec2i(_resourceZone.Center.X, _resourceZone.Center.Z),
              (int)_resourceZone.Radius);
    }
    if (!_pruneResourceZone.Generate(accessor)) {
      return false;
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
    if (!_pruneResourceZone.Commit(accessor)) {
      return false;
    }
    bool structuresCommitted = true;
    foreach (SchematicPlacer placer in _resourceZone.Structures) {
      structuresCommitted &= placer.Commit(accessor);
    }
    return structuresCommitted;
  }

  public void
  Restore(IWorldAccessor worldForResolve, IChunkLoader loader, ILogger logger,
          PrunedTerrainHeightReader reader,
          Action<BlockPos, int, HavenRegionIntersection> havenUpdate) {
    WorldForResolve = worldForResolve;
    Loader = loader;
    Logger = logger;
    _reader = reader;
    _havenUpdate = havenUpdate;
    Terrain.Restore(reader);
    _centerLocator.Restore(logger, Terrain);
    if (_pruneResourceZone != null) {
      _pruneResourceZone.Restore(worldForResolve, Loader, Terrain, reader);
    }
  }
}
