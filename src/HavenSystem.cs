using System;

using Haven.BlockBehaviors;

using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Haven;

public class HavenSystem : ModSystem {
  private ICoreAPI _api;

  public static string Domain { get; private set; }
  public static ILogger Logger { get; private set; }
  public ServerConfig ServerConfig { get; private set; }
  public readonly CallbackScheduler Scheduler = new();

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
    LoadConfigFile(sapi);
  }

  public override void Dispose() {
    Scheduler.Dispose();
    base.Dispose();
  }

  private void LoadConfigFile(ICoreServerAPI api) {
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
  }
}
