using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Haven.BlockBehaviors;
using Haven.EntityBehaviors;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Haven;

public class HavenRegionIntersectionUpdate {
  public BlockPos OldCenter;
  public HavenRegionIntersection New;
}

public class HavenUpdate {
  public BlockPos OldCenter;
  public Haven New;
}

public class HavenSystem : ModSystem {
  private static IModLoader s_lastLoader;
  private static HavenSystem s_instance;
  public static string Domain { get; private set; }
  public static ILogger Logger { get; private set; }

  private ICoreAPI _api;
  public ServerConfig ServerConfig { get; private set; }
  private BlockConfig _blockConfig = null;
  private HashSet<int> _cliffBlocks = [];
  public readonly CallbackScheduler Scheduler = new();

  private IBlockAccessorRevertable _revertable = null;
  private HavenGenerator _activeRevertableGenerator = null;
  private ChunkLoader _loader = null;
  private PrunedTerrainHeightReader _terrain = null;
  private ServerCommands _commands = null;
  private readonly Dictionary<Vec2i, List<HavenRegionIntersection>>
      _loadedHavenIntersections = [];
  private readonly Dictionary<Vec2i, List<Haven>> _loadedHavens = [];
  private readonly Dictionary<Vec2i, List<HavenRegionIntersectionUpdate>>
      _pendingIntersectionUpdates = [];
  private readonly Dictionary<Vec2i, List<HavenUpdate>> _pendingHavenUpdates =
      [];

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

    api.RegisterEntityBehaviorClass("safezone", typeof(SafeZone));

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
    sapi.Event.OnTestBlockAccess += OnTestBlockAccess;
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
          RegisterHavenOnly(new Haven(
              _activeRevertableGenerator.RegionIntersection,
              ServerConfig.PlotBorderWidth, ServerConfig.BlocksPerPlot));
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
    _cliffBlocks = _blockConfig.ResolveCliff(resolver);
  }

  private void ChunksLoaded(List<IWorldChunk> list) {
    Logger.Build("ChunksLoaded.");
    ProcessRevertableGenerator();
  }

  private void MapRegionLoaded(Vec2i mapCoord, IMapRegion region) {
    if (_api is not ICoreServerAPI sapi) {
      Logger.Error("MapRegionLoaded called on the client side");
      return;
    }
    List<HavenRegionIntersection> intersections =
        region.GetModdata<List<HavenRegionIntersection>>("haven:intersections");
    intersections ??= [];
    List<Haven> havens = region.GetModdata<List<Haven>>("haven:havens");
    havens ??= [];
    lock (_pendingIntersectionUpdates) {
      if (_pendingIntersectionUpdates.TryGetValue(
              mapCoord, out List<HavenRegionIntersectionUpdate> updates)) {
        ApplyUpdatesForRegion(region, intersections, updates);
        _pendingIntersectionUpdates.Remove(mapCoord);
      }
      if (_pendingHavenUpdates.TryGetValue(
              mapCoord, out List<HavenUpdate> havenUpdates)) {
        ApplyUpdatesForRegion(region, havens, havenUpdates);
        _pendingHavenUpdates.Remove(mapCoord);
      }
    }
    // Look for any haven intersections that are in the map region (and are thus
    // authoritative) but are missing the corresponding haven. This can happen
    // when upgrading from version 0.4.0 or older.
    foreach (HavenRegionIntersection intersection in intersections) {
      if (intersection.Center.X / sapi.WorldManager.RegionSize != mapCoord.X) {
        continue;
      }
      if (intersection.Center.Z / sapi.WorldManager.RegionSize != mapCoord.Y) {
        continue;
      }
      if (_activeRevertableGenerator != null &&
          _activeRevertableGenerator.Center == intersection.Center) {
        // This haven is still being generated. It will create the haven object
        // when it's done.
        continue;
      }
      if (havens.Any((Haven haven) => haven.GetIntersection().Center ==
                                      intersection.Center)) {
        // This haven intersection already has a haven.
        continue;
      }
      Logger.Warning(
          $"Creating missing haven object for haven intersection at {intersection.Center}");
      havens.Add(new(intersection, ServerConfig.PlotBorderWidth,
                     ServerConfig.BlocksPerPlot));
    }
    foreach (Haven haven in havens) {
      if (haven.TryExpand(ServerConfig.PlotBorderWidth,
                          ServerConfig.BlocksPerPlot)) {
        UpdateHaven(haven.GetIntersection().Center,
                    haven.GetIntersection().Radius, haven);
      }
    }
    if (intersections.Count > 0) {
      _loadedHavenIntersections[mapCoord] = intersections;
    } else {
      _loadedHavenIntersections.Remove(mapCoord);
    }
    if (havens.Count > 0) {
      _loadedHavens[mapCoord] = havens;
    } else {
      _loadedHavens.Remove(mapCoord);
    }
  }

  private void MapRegionUnloaded(Vec2i mapCoord, IMapRegion region) {
    _loadedHavenIntersections.Remove(mapCoord);
    _loadedHavens.Remove(mapCoord);
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
            if (update.New != null && update.New.Center == oldCenter) {
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

  public void UpdateHaven(BlockPos oldCenter, int oldRadius, Haven haven) {
    if (_api is not ICoreServerAPI sapi) {
      Logger.Error("UpdateHaven called on the client side");
      return;
    }
    HashSet<Vec2i> updateRegions = [];
    if (haven != null) {
      updateRegions.UnionWith(
          haven.GetIntersection().GetRegions(sapi.WorldManager.RegionSize));
    }
    if (oldCenter != null) {
      updateRegions.UnionWith(HavenRegionIntersection.GetRegions(
          oldCenter, oldRadius, sapi.WorldManager.RegionSize));
    }

    lock (_pendingHavenUpdates) {
      foreach (Vec2i region in updateRegions) {
        if (!_pendingHavenUpdates.TryGetValue(region,
                                              out List<HavenUpdate> updates)) {
          updates = [];
          _pendingHavenUpdates.Add(region, updates);
        }
        bool found = false;
        if (oldCenter != null) {
          foreach (HavenUpdate update in updates) {
            if (update.New != null &&
                update.New.GetIntersection().Center == oldCenter) {
              found = true;
              update.New = haven;
              break;
            }
          }
        }
        if (!found) {
          updates.Add(new() { OldCenter = oldCenter, New = haven });
        }
      }
      sapi.Event.EnqueueMainThreadTask(ProcessUpdates, "haven:processupdates");
    }
  }

  private void RegisterHavenOnly(Haven haven) { UpdateHaven(null, 0, haven); }

  public void RegisterHaven(Haven haven) {
    UpdateHavenIntersection(null, 0, haven.GetIntersection());
    UpdateHaven(null, 0, haven);
  }

  public void UnregisterHaven(HavenRegionIntersection intersection) {
    UpdateHaven(intersection.Center, intersection.Radius, null);
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

  /// <summary>
  /// Find the haven that contains the given location
  /// </summary>
  /// <param name="pos">location to find a intersecting haven</param>
  /// <returns>the haven</returns>
  public Haven GetHaven(BlockPos pos) {
    int regionSize = _api.World.BlockAccessor.RegionSize;
    Vec2i region = new(pos.X / regionSize, pos.Z / regionSize);
    if (!_loadedHavens.TryGetValue(region, out List<Haven> havens)) {
      return null;
    }
    Haven best = null;
    double bestDist = double.PositiveInfinity;
    foreach (Haven haven in havens) {
      HavenRegionIntersection intersection = haven.GetIntersection();
      if (intersection.Contains(pos, ServerConfig.HavenBelowHeight,
                                ServerConfig.HavenAboveHeight)) {
        double dist =
            pos.DistanceSqTo(intersection.Center.X, intersection.Center.Y,
                             intersection.Center.Z);
        if (dist < bestDist) {
          bestDist = dist;
          best = haven;
        }
      }
    }
    return best;
  }

  private void ProcessUpdates() {
    lock (_pendingIntersectionUpdates) {
      List<Vec2i> remove = [];
      foreach ((Vec2i regionCoord, List<HavenRegionIntersectionUpdate> updates)
                   in _pendingIntersectionUpdates) {
        IMapRegion region =
            _api.World.BlockAccessor.GetMapRegion(regionCoord.X, regionCoord.Y);
        if (region != null) {
          if (!_loadedHavenIntersections.TryGetValue(
                  regionCoord,
                  out List<HavenRegionIntersection> intersections)) {
            intersections = [];
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
    lock (_pendingHavenUpdates) {
      List<Vec2i> remove = [];
      foreach ((Vec2i regionCoord, List<HavenUpdate> updates)
                   in _pendingHavenUpdates) {
        IMapRegion region =
            _api.World.BlockAccessor.GetMapRegion(regionCoord.X, regionCoord.Y);
        if (region != null) {
          if (!_loadedHavens.TryGetValue(regionCoord, out List<Haven> havens)) {
            havens = [];
            _loadedHavens.Add(regionCoord, havens);
          }
          ApplyUpdatesForRegion(region, havens, updates);
          remove.Add(regionCoord);
          if (havens.Count == 0) {
            _loadedHavens.Remove(regionCoord);
          }
        }
      }
      foreach (Vec2i v in remove) {
        _pendingHavenUpdates.Remove(v);
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

  private void ApplyUpdatesForRegion(IMapRegion region, List<Haven> havens,
                                     List<HavenUpdate> updates) {
    Dictionary<BlockPos, Haven> updatesDict = new();
    List<Haven> newHavens = [];
    foreach (HavenUpdate update in updates) {
      if (update.OldCenter != null) {
        updatesDict[update.OldCenter] = update.New;
      } else {
        newHavens.Add(update.New);
      }
    }
    if (updatesDict.Count > 0) {
      for (int i = 0; i < havens.Count;) {
        if (updatesDict.TryGetValue(havens[i].GetIntersection().Center,
                                    out Haven update)) {
          updatesDict.Remove(havens[i].GetIntersection().Center);
          if (update != null) {
            havens[i] = update;
          } else {
            havens.RemoveAt(i);
            continue;
          }
        }
        ++i;
      }
      if (updatesDict.Count > 0) {
        Logger.Error("Some haven updates could not be matched " +
                     "against existing havens and were dropped.");
      }
    }
    havens.AddRange(newHavens);
    region.SetModdata("haven:havens", havens);
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
    lock (_pendingHavenUpdates) {
      var updates = sapi.WorldManager.SaveGame
                        .GetData<Dictionary<Vec2i, List<HavenUpdate>>>(
                            "haven:havenupdates");
      _pendingHavenUpdates.Clear();
      if (updates != null) {
        _pendingHavenUpdates.AddRange(updates);
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
    lock (_pendingHavenUpdates) {
      sapi.WorldManager.SaveGame.StoreData("haven:havenupdates",
                                           _pendingHavenUpdates);
    }
  }

  public static HavenSystem GetSystem(IModLoader loader) {
    // IModLoader.GetModSystem is a little slow, because it walks all the mods.
    // So cache the result.
    if (loader == s_lastLoader) {
      return s_instance;
    }
    s_lastLoader = loader;
    s_instance = loader.GetModSystem<HavenSystem>();
    return s_instance;
  }

  private EnumWorldAccessResponse
  OnTestBlockAccess(IPlayer player, BlockSelection blockSel,
                    EnumBlockAccessFlags accessType, ref string claimant,
                    EnumWorldAccessResponse input) {
    HavenRegionIntersection intersection =
        GetHavenIntersection(blockSel.Position);
    if (intersection == null) {
      return input;
    }
    if (accessType != EnumBlockAccessFlags.BuildOrBreak) {
      return input;
    }
    if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) {
      return input;
    }
    Haven haven = GetHaven(blockSel.Position);
    // The build and break flags are combined together. This handler does not
    // get any direct indication on whether it is a build or break event.
    // Instead, it is inferred based on whether the left mouse is down (needed
    // to break blocks).
    //
    // The first time this is called for a place block event, blockSel.DidOffset
    // will be true. However, if that first check succeeds, a second check will
    // be run from Block.CanPlaceBlock, and it will have DidOffset to false. So
    // DidOffset cannot be used to reliably detect block placement events.
    if (!player.Entity.Controls.LeftMouseDown) {
      // This is probably placing a block.
      Block placing = player.Entity.ActiveHandItemSlot.Itemstack?.Block;
      if (placing != null) {
        string error = TestBlockPlacement(intersection, haven, player,
                                          blockSel.Position, placing);
        if (error != null) {
          claimant = error;
          return EnumWorldAccessResponse.DeniedByMod;
        }
        return input;
      }
      claimant = "custommessage-haven";
      return EnumWorldAccessResponse.DeniedByMod;
    } else {
      string error =
          TestBlockBreak(intersection, haven, player, blockSel.Position);
      if (error != null) {
        claimant = error;
        return EnumWorldAccessResponse.DeniedByMod;
      }
      return input;
    }
  }

  private string TestBlockPlacement(HavenRegionIntersection intersection,
                                    Haven haven, IPlayer player,
                                    BlockPos position, Block placing) {
    string defaultError = "custommessage-haven";
    if (haven != null) {
      (PlotRing ring, double radians) =
          haven.GetPlotRing(position, ServerConfig.HavenBelowHeight,
                            ServerConfig.HavenAboveHeight);
      if (ring != null) {
        int owner = ring.GetOwnerIndex(radians);
        if (owner > 0) {
          // For owned plots, only allow placement by the owner of the plot.
          if (ring.Plots[owner].OwnerUID == player.PlayerUID) {
            return null;
          } else if (ring.Plots[owner].OwnerUID != null) {
            return "custommessage-haven-plot-owned";
          }
        } else {
          defaultError = "custommessage-haven-plot-border";
        }
      }
    } else {
      // The haven intersection was loaded but not the haven itself. The haven
      // should be loaded soon. For now, block placements in the plot zone.
      if (intersection.InPlotZone(position, ServerConfig.HavenBelowHeight,
                                  ServerConfig.HavenAboveHeight)) {
        return defaultError;
      }
    }
    if (IsCliffBlock(placing)) {
      foreach (BlockFacing facing in BlockFacing.HORIZONTALS) {
        BlockPos test = position.AddCopy(facing);

        if (!IsCliffBlock(_api.World.BlockAccessor.GetBlock(test))) {
          continue;
        }
        if (!IsCliffBlock(_api.World.BlockAccessor.GetBlockAbove(test))) {
          continue;
        }
        return null;
      }
    }
    return defaultError;
  }

  private string TestBlockBreak(HavenRegionIntersection intersection,
                                Haven haven, IPlayer player,
                                BlockPos position) {
    string defaultError = "custommessage-haven";
    if (haven != null) {
      (PlotRing ring, double radians) =
          haven.GetPlotRing(position, ServerConfig.HavenBelowHeight,
                            ServerConfig.HavenAboveHeight);
      if (ring != null) {
        int owner = ring.GetOwnerIndex(radians);
        if (owner > 0) {
          if (ring.Plots[owner].OwnerUID == player.PlayerUID) {
            return null;
          } else if (ring.Plots[owner].OwnerUID != null) {
            return "custommessage-haven-plot-owned";
          }
        } else {
          defaultError = "custommessage-haven-plot-border";
        }
      }
    }
    return defaultError;
  }

  private bool IsCliffBlock(Block block) {
    if (block == null) {
      return false;
    }
    return _cliffBlocks.Contains(block.Id);
  }
}
