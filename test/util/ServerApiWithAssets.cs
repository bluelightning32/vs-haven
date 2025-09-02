using System.Reflection;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace Haven.Test;

public class FilterOrigin : IAssetOrigin {
  private readonly IAssetOrigin _original;
  private readonly Dictionary<AssetCategory, HashSet<string>> _allow;

  public FilterOrigin(IAssetOrigin original,
                      Dictionary<AssetCategory, HashSet<string>> allow) {
    _original = original;
    _allow = allow;
  }

  public string OriginPath => _original.OriginPath;

  public List<IAsset> GetAssets(AssetCategory category,
                                bool shouldLoad = true) {
    List<IAsset> assets = _original.GetAssets(category, shouldLoad);
    if (_allow.TryGetValue(category, out HashSet<string> allow)) {
      assets.RemoveAll((IAsset asset) => !allow.Contains(asset.Name));
    }
    return assets;
  }

  public List<IAsset> GetAssets(AssetLocation baseLocation,
                                bool shouldLoad = true) {
    List<IAsset> assets = _original.GetAssets(baseLocation, shouldLoad);
    if (_allow.TryGetValue(baseLocation.Category, out HashSet<string> allow)) {
      assets.RemoveAll((IAsset asset) => !allow.Contains(asset.Name));
    }
    return assets;
  }

  public bool
  IsAllowedToAffectGameplay() => _original.IsAllowedToAffectGameplay();

  public void LoadAsset(IAsset asset) => _original.LoadAsset(asset);

  public bool TryLoadAsset(IAsset asset) => _original.TryLoadAsset(asset);
}

class ServerApiWithAssets {
  public static string HavenResourcesPath {
    get {
      string path = AppDomain.CurrentDomain.BaseDirectory;
      return Path.Join(path, "..", "..", "..", "..", "resources");
    }
  }

  public static ServerMain
  Create(Dictionary<AssetCategory, HashSet<string>> allowAssetFiles,
         bool disallowByDefault = true, bool logDebug = false) {
    string vsPath = Environment.GetEnvironmentVariable("VINTAGE_STORY");

    PropertyInfo assetsPathProp = typeof(GamePaths).GetProperty("AssetsPath");
    assetsPathProp.GetSetMethod(true).Invoke(
        null, new object[] { Path.Combine(vsPath, "assets") });
    // Even the NullLogger tries to archive old log files in the log directory.
    Directory.CreateDirectory(GamePaths.Logs);
    if (!logDebug && ServerMain.Logger == null) {
      ServerMain.Logger = new NullLogger();
    }

    StartServerArgs serverArgs = new();
    string[] rawArgs = Array.Empty<string>();
    ServerProgramArgs programArgs = new();
    ServerMain server = new(serverArgs, rawArgs, programArgs, false);
    // This would normally be set by ServerProgram.Main.
    server.exit = new GameExit();

    // This creates a ServerCoreAPI and sets server.api.
    _ = new ServerSystemModHandler(server);
    ServerCoreAPI api = (ServerCoreAPI)server.Api;

    FieldInfo serverChunkDataPoolField = server.GetType().GetField(
        "serverChunkDataPool", BindingFlags.Instance | BindingFlags.NonPublic);
    serverChunkDataPoolField.SetValue(
        server, new ChunkDataPool(MagicNum.ServerChunkSize, server));

    server.ModEventManager = new ServerEventManager(server);
    server.EventManager =
        new CoreServerEventManager(server, server.ModEventManager);
    server.PlayerDataManager = new(server);

    // Even though this is called a ChunkServerThread, it doesn't directly
    // launch a system thread. Instead, ServerMain.Launch would create a real
    // Thread that calls chunkThread.Process. However, our fake server will not
    // start the real background thread. Instead, unit tests will call
    // LoadChunksInline.
    ChunkServerThread chunkThread =
        new(server, "chunkdbthread", server.ServerThreadsCts.Token);
    FieldInfo chunkThreadField = server.GetType().GetField(
        "chunkThread", BindingFlags.Instance | BindingFlags.NonPublic);
    chunkThreadField.SetValue(server, chunkThread);

    Type serverSystemSupplyChunksType = server.GetType().Assembly.GetType(
        "Vintagestory.Server.ServerSystemSupplyChunks");
    ServerSystem supplyChunks = (ServerSystem)Activator.CreateInstance(
        serverSystemSupplyChunksType, [server, chunkThread]);

    Type serverSystemLoadAndSaveGameType = server.GetType().Assembly.GetType(
        "Vintagestory.Server.ServerSystemLoadAndSaveGame");
    ServerSystem loadsavegame = (ServerSystem)Activator.CreateInstance(
        serverSystemLoadAndSaveGameType, [server, chunkThread]);

    FieldInfo systemsField = server.GetType().GetField(
        "Systems", BindingFlags.Instance | BindingFlags.NonPublic);
    systemsField.SetValue(server,
                          new ServerSystem[] { supplyChunks, loadsavegame });

    server.ModEventManager = new ServerEventManager(server);
    server.AssetManager =
        new AssetManager(GamePaths.AssetsPath, EnumAppSide.Server);
    server.AssetManager.InitAndLoadBaseAssets(logDebug ? api.Logger : null);

    Lang.Load(api.Logger, server.AssetManager);
    server.Config = new();
    SaveGame saveGame = SaveGame.CreateNew(server.Config);
    FieldInfo saveGameDataField = server.GetType().GetField(
        "SaveGameData", BindingFlags.Instance | BindingFlags.NonPublic);
    saveGameDataField.SetValue(server, saveGame);

    // The mod loader is hardcoded to load libraries from
    // AppDomain.CurrentDomain.BaseDirectory. The only way to change that
    // property in .net 7 is to set the following variable.
    AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", vsPath);
    ModLoader loader =
        new(api, Array.Empty<string>(), server.progArgs.TraceLog);
    // Reset the AppDomain.CurrentDomain.BaseDirectory.
    AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", null);

    FieldInfo modLoaderField = api.GetType().GetField(
        "modLoader", BindingFlags.Instance | BindingFlags.NonPublic);
    modLoaderField.SetValue(api, loader);

    Type[] loadMods = new Type[] { typeof(Vintagestory.ServerMods.Core),
                                   typeof(SurvivalCoreSystem) };

    MethodInfo loadModInfoFromModInfoAttribute =
        typeof(ModContainer)
            .GetMethod("LoadModInfoFromModInfoAttribute",
                       BindingFlags.Instance | BindingFlags.NonPublic);
    PropertyInfo infoProperty = typeof(Mod).GetProperty("Info");
    List<ModContainer> mods = new();
    foreach (Type modType in loadMods) {
      ModInfoAttribute info =
          modType.Assembly.GetCustomAttribute<ModInfoAttribute>();

      ModContainer modContainer =
          new(new FileInfo(modType.Assembly.Location), api.Logger, logDebug);
      object[] args = new object[] {
        info, new List<Vintagestory.API.Common.ModDependency>(), null
      };
      ModInfo modInfo =
          (ModInfo)loadModInfoFromModInfoAttribute.Invoke(modContainer, args);
      infoProperty.GetSetMethod(true).Invoke(modContainer,
                                             new object[] { modInfo });
      mods.Add(modContainer);
    }

    loader.LoadMods(mods);

    FieldInfo enabledSystemsField = loader.GetType().GetField(
        "enabledSystems", BindingFlags.Instance | BindingFlags.NonPublic);
    List<ModSystem> enabledSystems =
        (List<ModSystem>)enabledSystemsField.GetValue(loader);

    // Skip because it tries to register a game tick listener.
    enabledSystems.Remove(
        loader.GetModSystem<ModSystemItemRendererOptimizer>());
    // Skip because it tries to register a game tick listener.
    enabledSystems.Remove(loader.GetModSystem<Vintagestory.GameContent.Mechanics
                                                  .MechanicalPowerMod>());
    // Skip because it tries to use the world block accessor.
    enabledSystems.Remove(loader.GetModSystem<ClothManager>());

    loader.RunModPhase(ModRunPhase.Pre);
    loader.RunModPhase(ModRunPhase.Start);

    server.WorldMap = new ServerWorldMap(server);
    server.WorldMap.Init(1000, 32 * 5, 1000);

    // This would usually be created and set by
    // ServerSystemLoadAndSaveGame.OnBeginConfiguration.
    GameDatabase gameDatabase = new(ServerMain.Logger);
    FieldInfo gameDatabaseField = chunkThread.GetType().GetField(
        "gameDatabase", BindingFlags.Instance | BindingFlags.NonPublic);
    gameDatabaseField.SetValue(chunkThread, gameDatabase);

    // This would usually be set by GameDatabase.OpenConnection.
    FakeGameDbConnection conn = new();
    FieldInfo connField = gameDatabase.GetType().GetField(
        "conn", BindingFlags.Instance | BindingFlags.NonPublic);
    connField.SetValue(gameDatabase, conn);

    MethodInfo loadSaveGameMethod = loadsavegame.GetType().GetMethod(
        "LoadSaveGame", BindingFlags.Instance | BindingFlags.NonPublic);
    loadSaveGameMethod.Invoke(loadsavegame, []);

    if (disallowByDefault) {
      foreach (AssetCategory category in AssetCategory.categories.Values) {
        if (!allowAssetFiles.ContainsKey(category) &&
            category != AssetCategory.worldproperties &&
            category != AssetCategory.config) {
          // Match nothing
          allowAssetFiles.Add(category, new());
        }
      }
    }
    for (int i = 0; i < server.AssetManager.CustomModOrigins.Count; ++i) {
      server.AssetManager.CustomModOrigins[i] = new FilterOrigin(
          server.AssetManager.CustomModOrigins[i], allowAssetFiles);
    }
    server.AssetManager.CustomAppOrigins.Add(
        new FolderOrigin(HavenResourcesPath));

    server.AssetManager.AddExternalAssets(api.Logger, loader);

    loader.RunModPhase(ModRunPhase.AssetsLoaded);

    // OnLoadedNative is usually called by ServerSystemBlockSimulation, but this
    // simplified server does not start that.
    foreach (Item item in server.World.Items) {
      item.OnLoadedNative(api);
    }
    foreach (Block block in server.World.Blocks) {
      block.OnLoadedNative(api);
    }

    IAsset sunlight =
        server.AssetManager.Get("textures/environment/sunlight.png");
    FieldInfo gameWorldCalendarField = server.GetType().GetField(
        "GameWorldCalendar", BindingFlags.Instance | BindingFlags.NonPublic);
    GameCalendar calendar = new(sunlight, 0, 1L);
    gameWorldCalendarField.SetValue(server, calendar);

    // This is normally set by
    // ServerSystemSupplyChunks.InitWorldgenAndSpawnChunks.
    FieldInfo worldgenHandlerField = supplyChunks.GetType().GetField(
        "worldgenHandler", BindingFlags.Instance | BindingFlags.NonPublic);
    WorldGenHandler worldgenHandler = new();
    worldgenHandlerField.SetValue(supplyChunks, worldgenHandler);

    MethodInfo enterRunPhaseMethod = server.GetType().GetMethod(
        "EnterRunPhase", BindingFlags.Instance | BindingFlags.NonPublic);
    enterRunPhaseMethod.Invoke(server, [EnumServerRunPhase.RunGame]);

    // This registers the save game callback.
    loadsavegame.OnBeginRunGame();
    return server;
  }
}
