using System;
using System.Collections.Generic;
using System.Linq;

using Haven.BlockBehaviors;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Haven;

public class HavenRegionIntersectionUpdate {
  public BlockPos OldCenter;
  public HavenRegionIntersection New;
}

public class HavenSystem : ModSystem {
  private ICoreAPI _api;

  public static string Domain { get; private set; }
  public static ILogger Logger { get; private set; }
  public ServerConfig ServerConfig { get; private set; }
  private BlockConfig _blockConfig = null;
  public readonly CallbackScheduler Scheduler = new();

  private IBlockAccessorRevertable _revertable = null;
  private HavenGenerator _activeRevertableGenerator = null;
  private ChunkLoader _loader = null;
  private PrunedTerrainHeightReader _terrain = null;
  private ServerCommands _commands = null;
  private readonly Dictionary<Vec2i, List<HavenRegionIntersection>>
      _loadedHavenIntersections = [];
  private readonly Dictionary<Vec2i, List<HavenRegionIntersectionUpdate>>
      _pendingIntersectionUpdates = [];

  public override double ExecuteOrder() { return 1.0; }

  public override void Start(ICoreAPI api) {
    Domain = Mod.Info.ModID;
    base.Start(api);
    Logger = Mod.Logger;

    _api = api;

    api.RegisterBlockBehaviorClass(nameof(Dispenser), typeof(Dispenser));
    api.RegisterBlockEntityBehaviorClass(
        nameof(BlockEntityBehaviors.Dispenser),
        typeof(BlockEntityBehaviors.Dispenser));
    api.RegisterBlockClass(nameof(Blocks.Delegate), typeof(Blocks.Delegate));

    Scheduler.Start(api);
    api.Event.MapRegionLoaded += MapRegionLoaded;
    api.Event.MapRegionUnloaded += MapRegionUnloaded;
  }

  public override void StartServerSide(ICoreServerAPI sapi) {
    base.StartServerSide(sapi);
    MatchResolver resolver = new(sapi.World, Logger);
    LoadConfigFile(sapi, resolver);
    _revertable = sapi.World.GetBlockAccessorRevertable(true, true);
    _loader = new(sapi.Event, sapi.WorldManager, sapi.World.BlockAccessor,
                  ChunksLoaded);
    _terrain = new PrunedTerrainHeightReader(
        new TerrainHeightReader(_loader, false),
        ServerConfig.ResourceZone.TerrainCategories,
        ServerConfig.ResourceZone.TerrainRaise);

    // This is normally set by GenStructures.initWorldGen, but that isn't called
    // in flat worlds. So set the filler block directly here instead.
    Block fillerBlock =
        sapi.World.BlockAccessor.GetBlock(new AssetLocation("meta-filler"));
    BlockSchematic.FillerBlockId = fillerBlock?.Id ?? 0;

    _commands = new(sapi, this);

    sapi.Event.SaveGameLoaded += OnSaveGameLoading;
    sapi.Event.GameWorldSave += OnSaveGameSaving;
  }

  public override void Dispose() {
    _revertable = null;
    _commands = null;
    Scheduler.Dispose();
    base.Dispose();
  }

  public bool GenerateHaven(BlockPos center) {
    if (_activeRevertableGenerator != null) {
      return false;
    }
    Logger.Build("Manual haven generation started.");
    _activeRevertableGenerator =
        new(_api.World, _loader, Logger, _terrain, UpdateHavenIntersection,
            center, ServerConfig.HavenRadius, ServerConfig.ResourceZone);
    ProcessRevertableGenerator();
    return true;
  }

  private void ProcessRevertableGenerator() {
    if (_activeRevertableGenerator != null) {
      if (!_activeRevertableGenerator.GenerationDone) {
        if (_activeRevertableGenerator.Generate(_revertable)) {
          _revertable.Commit();
        }
      }
      if (_activeRevertableGenerator.GenerationDone) {
        if (_activeRevertableGenerator.Commit(_revertable)) {
          Logger.Build(
              $"Manual haven generation done with center at {_activeRevertableGenerator.Center}.");
          _activeRevertableGenerator = null;
        }
      }
    }
  }

  public bool UndoHaven() {
    if (_revertable.CurrentHistoryState >= _revertable.AvailableHistoryStates) {
      return false;
    }
    _revertable.ChangeHistoryState(1);
    Logger.Build("Manual haven reverted.");
    return true;
  }

  private void LoadConfigFile(ICoreServerAPI api, MatchResolver resolver) {
    string configFile = $"{Domain}.json";
    try {
      ServerConfig = api.LoadModConfig<ServerConfig>(configFile);
    } catch (Exception e) {
      api.Logger.Fatal(
          "Error parsing '{0}': {1}. Using default config instead.", configFile,
          e.Message);
      ServerConfig = new();
    }
    if (ServerConfig == null) {
      // The file doesn't exist. So create it.
      ServerConfig = new();
      api.StoreModConfig(ServerConfig, configFile);
    }
    _blockConfig = BlockConfig.Load(Logger, api.Assets);

    ServerConfig.Resolve(Logger, api.World, resolver, _blockConfig);
  }

  private void ChunksLoaded(List<IWorldChunk> list) {
    Logger.Build("ChunksLoaded.");
    ProcessRevertableGenerator();
  }

  private void MapRegionLoaded(Vec2i mapCoord, IMapRegion region) {
    List<HavenRegionIntersection> intersections =
        region.GetModdata<List<HavenRegionIntersection>>("haven:intersections");
    intersections ??= [];
    lock (_pendingIntersectionUpdates) {
      if (_pendingIntersectionUpdates.TryGetValue(
              mapCoord, out List<HavenRegionIntersectionUpdate> updates)) {
        ApplyUpdatesForRegion(region, intersections, updates);
        _pendingIntersectionUpdates.Remove(mapCoord);
      }
    }
    if (intersections.Count > 0) {
      _loadedHavenIntersections[mapCoord] = intersections;
    } else {
      _loadedHavenIntersections.Remove(mapCoord);
    }
  }

  private void MapRegionUnloaded(Vec2i mapCoord, IMapRegion region) {
    _loadedHavenIntersections.Remove(mapCoord);
  }

  /// <summary>
  /// Returns a copy of all loaded haven region intersections
  /// </summary>
  /// <returns></returns>
  public Dictionary<Vec2i, List<HavenRegionIntersection>>
  GetLoadedIntersections() {
    Dictionary<Vec2i, List<HavenRegionIntersection>> result = [];
    foreach ((Vec2i pos, List<HavenRegionIntersection> intersections)
                 in _loadedHavenIntersections) {
      result.Add(pos, [..intersections.Select(i => i.Copy())]);
    }
    return result;
  }

  public void UpdateHavenIntersection(BlockPos oldCenter, int oldRadius,
                                      HavenRegionIntersection intersection) {
    if (_api is not ICoreServerAPI sapi) {
      Logger.Error("UpdateHavenIntersection called on the client side");
      return;
    }
    HashSet<Vec2i> updateRegions = [];
    if (intersection != null) {
      updateRegions.UnionWith(
          intersection.GetRegions(sapi.WorldManager.RegionSize));
    }
    if (oldCenter != null) {
      updateRegions.UnionWith(HavenRegionIntersection.GetRegions(
          oldCenter, oldRadius, sapi.WorldManager.RegionSize));
    }

    lock (_pendingIntersectionUpdates) {
      foreach (Vec2i region in updateRegions) {
        if (!_pendingIntersectionUpdates.TryGetValue(
                region, out List<HavenRegionIntersectionUpdate> updates)) {
          updates = [];
          _pendingIntersectionUpdates.Add(region, updates);
        }
        bool found = false;
        if (oldCenter != null) {
          foreach (HavenRegionIntersectionUpdate update in updates) {
            if (update.New.Center == oldCenter) {
              found = true;
              update.New = intersection;
              break;
            }
          }
        }
        if (!found) {
          updates.Add(new() { OldCenter = oldCenter, New = intersection });
        }
      }
      sapi.Event.EnqueueMainThreadTask(ProcessUpdates, "haven:processupdates");
    }
  }

  public void RegisterHavenIntersection(HavenRegionIntersection intersection) {
    UpdateHavenIntersection(null, 0, intersection);
  }

  public void
  UnregisterHavenIntersection(HavenRegionIntersection intersection) {
    UpdateHavenIntersection(intersection.Center, intersection.Radius, null);
  }

  /// <summary>
  /// Find the haven that contains the given location
  /// </summary>
  /// <param name="pos">location to find a intersecting haven</param>
  /// <returns>an intersecting haven, or null if there is no haven
  /// there</returns>
  public HavenRegionIntersection GetHavenIntersection(BlockPos pos) {
    int regionSize = _api.World.BlockAccessor.RegionSize;
    Vec2i region = new(pos.X / regionSize, pos.Z / regionSize);
    if (!_loadedHavenIntersections.TryGetValue(
            region, out List<HavenRegionIntersection> intersections)) {
      return null;
    }
    HavenRegionIntersection best = null;
    double bestDist = double.PositiveInfinity;
    foreach (HavenRegionIntersection intersection in intersections) {
      if (intersection.Contains(pos, ServerConfig.HavenBelowHeight,
                                ServerConfig.HavenAboveHeight)) {
        double dist =
            pos.DistanceSqTo(intersection.Center.X, intersection.Center.Y,
                             intersection.Center.Z);
        if (dist < bestDist) {
          bestDist = dist;
          best = intersection;
        }
      }
    }
    return best;
  }

  private void ProcessUpdates() {
    lock (_pendingIntersectionUpdates) {
      List<Vec2i> remove = new();
      foreach ((Vec2i regionCoord, List<HavenRegionIntersectionUpdate> updates)
                   in _pendingIntersectionUpdates) {
        IMapRegion region =
            _api.World.BlockAccessor.GetMapRegion(regionCoord.X, regionCoord.Y);
        if (region != null) {
          if (!_loadedHavenIntersections.TryGetValue(
                  regionCoord,
                  out List<HavenRegionIntersection> intersections)) {
            intersections = new();
            _loadedHavenIntersections.Add(regionCoord, intersections);
          }
          ApplyUpdatesForRegion(region, intersections, updates);
          remove.Add(regionCoord);
          if (intersections.Count == 0) {
            _loadedHavenIntersections.Remove(regionCoord);
          }
        }
      }
      foreach (Vec2i v in remove) {
        _pendingIntersectionUpdates.Remove(v);
      }
    }
  }

  private void
  ApplyUpdatesForRegion(IMapRegion region,
                        List<HavenRegionIntersection> intersections,
                        List<HavenRegionIntersectionUpdate> updates) {
    Dictionary<BlockPos, HavenRegionIntersection> updatesDict = new();
    List<HavenRegionIntersection> newIntersections = [];
    foreach (HavenRegionIntersectionUpdate update in updates) {
      if (update.OldCenter != null) {
        updatesDict[update.OldCenter] = update.New;
      } else {
        newIntersections.Add(update.New);
      }
    }
    if (updatesDict.Count > 0) {
      for (int i = 0; i < intersections.Count;) {
        if (updatesDict.TryGetValue(intersections[i].Center,
                                    out HavenRegionIntersection update)) {
          updatesDict.Remove(intersections[i].Center);
          if (update != null) {
            intersections[i] = update;
          } else {
            intersections.RemoveAt(i);
            continue;
          }
        }
        ++i;
      }
      if (updatesDict.Count > 0) {
        Logger.Error("Some haven intersection updates could not be matched " +
                     "against existing intersections and were dropped.");
      }
    }
    intersections.AddRange(newIntersections);
    region.SetModdata("haven:intersections", intersections);
  }

  private void OnSaveGameLoading() {
    if (_api is not ICoreServerAPI sapi) {
      return;
    }
    lock (_pendingIntersectionUpdates) {
      var updates =
          sapi.WorldManager.SaveGame
              .GetData<Dictionary<Vec2i, List<HavenRegionIntersectionUpdate>>>(
                  "haven:intersectionupdates");
      _pendingIntersectionUpdates.Clear();
      if (updates != null) {
        _pendingIntersectionUpdates.AddRange(updates);
      }
    }
  }

  private void OnSaveGameSaving() {
    if (_api is not ICoreServerAPI sapi) {
      return;
    }
    lock (_pendingIntersectionUpdates) {
      sapi.WorldManager.SaveGame.StoreData("haven:intersectionupdates",
                                           _pendingIntersectionUpdates);
    }
  }
}
