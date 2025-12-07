using System;
using System.Collections.Generic;

using Haven.BlockBehaviors;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Haven;

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
        _blockConfig.ResolveTerrainReplace(resolver),
        _blockConfig.TerrainAvoid.Resolve(resolver));

    // This is normally set by GenStructures.initWorldGen, but that isn't called
    // in flat worlds. So set the filler block directly here instead.
    Block fillerBlock =
        sapi.World.BlockAccessor.GetBlock(new AssetLocation("meta-filler"));
    BlockSchematic.FillerBlockId = fillerBlock?.Id ?? 0;

    _commands = new(sapi, this);
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
    _activeRevertableGenerator = new(_api.World, _loader, Logger, _terrain,
                                     center, ServerConfig.ResourceZone);
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
      api.Logger.Fatal("Error parsing '{0}': {1}", configFile, e.Message);
      throw;
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
}
