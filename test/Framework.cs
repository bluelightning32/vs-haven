using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace Haven.Test;

[PrefixTestClass]
public class Framework {
  // This property is set by the test framework:
  // https://learn.microsoft.com/en-us/visualstudio/test/how-to-create-a-data-driven-unit-test?view=vs-2022#add-a-testcontext-to-the-test-class
  public TestContext TestContext { get; set; } = null!;
  public static ServerMain Server = null;
  public static ServerCoreAPI Api {
    get { return (ServerCoreAPI)Server.Api; }
  }

  /// <summary>
  /// For performance reasons, all of the unit tests share the same game world
  /// (it is not reloaded between unit tests). This is the X coordinate of a
  /// chunk column that none of the unit tests should not be loaded by any of
  /// the tests.
  /// </summary>
  public const int UnloadedMapChunkX = 1005;
  /// <summary>
  /// Z coordinate of the unloaded chunk column.
  /// </summary>
  public const int UnloadedMapChunkZ = 1007;

  [AssemblyInitialize()]
  public static void AssemblyInitialize(TestContext context) {
    Dictionary<AssetCategory, HashSet<string>> allow =
        new() { [AssetCategory.itemtypes] = new() { "fruit.json" },
                [AssetCategory.blocktypes] = new() { "egg.json", "rock.json" },
                [AssetCategory.recipes] = new() {} };
    Server = ServerApiWithAssets.Create(allow);
  }

  [AssemblyCleanup()]
  public static void AssemblyCleanup() { Server?.Dispose(); }

  public static Item GetItem(string domain, string code) {
    return Server.World.GetItem(new AssetLocation(domain, code));
  }

  public static Block GetBlock(string domain, string code) {
    return Server.World.GetBlock(new AssetLocation(domain, code));
  }

  [TestMethod]
  public void CranberryItemLoaded() {
    Item cranberry =
        Server.World.GetItem(new AssetLocation("game:fruit-cranberry"));
    Assert.IsNotNull(cranberry);
    Assert.AreEqual(EnumFoodCategory.Fruit,
                    cranberry.NutritionProps.FoodCategory);
  }

  [TestMethod]
  public void GraniteBlockLoaded() {
    Block granite =
        Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    Assert.IsNotNull(granite);
    Assert.IsGreaterThanOrEqualTo(5, granite.Resistance);
  }

  [TestMethod]
  public void WorldMap() { Assert.IsNotNull(Server.WorldMap); }

  [TestMethod]
  public void LoadChunk() {
    ICoreServerAPI sapi = (ICoreServerAPI)Server.Api;
    sapi.WorldManager.UnloadChunkColumn(0, 0);
    IServerChunk chunk = sapi.WorldManager.GetChunk(0, 0, 0);
    Assert.IsNull(chunk);
    bool loaded = false;
    sapi.WorldManager.LoadChunkColumnPriority(
        0, 0, new ChunkLoadOptions() { OnLoaded = () => loaded = true });
    Server.LoadChunksInline();
    Assert.IsTrue(loaded);
    chunk = sapi.WorldManager.GetChunk(0, 0, 0);
    Assert.IsNotNull(chunk);
  }

  [TestMethod]
  public void ReloadChunk() {
    ICoreServerAPI sapi = (ICoreServerAPI)Server.Api;
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    Server.LoadChunksInline();
    IServerChunk chunk = sapi.WorldManager.GetChunk(0, 0, 0);
    Block granite =
        Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    chunk.Unpack();
    chunk.Data[0] = granite.Id;
    chunk.MarkModified();

    // UnloadChunkColumn arguably has a bug where it skips saving the chunk
    // before unloading it. So save all of the dirty chunks before unloading the
    // chunk so that the chunk modification above is not lost.
    Server.SaveGameInline();
    sapi.WorldManager.UnloadChunkColumn(0, 0);
    chunk = sapi.WorldManager.GetChunk(0, 0, 0);
    Assert.IsNull(chunk);

    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    Server.LoadChunksInline();

    chunk = sapi.WorldManager.GetChunk(0, 0, 0);
    chunk.Unpack();
    Assert.AreEqual(granite.Id, chunk.Data[0]);
  }

  [TestMethod]
  public void GetBlockAt() {
    Block granite =
        Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    Block andesite =
        Server.World.GetBlock(new AssetLocation("game:rock-andesite"));

    BlockPos pos = new(1, 1, 1);
    ICoreServerAPI sapi = (ICoreServerAPI)Server.Api;
    sapi.WorldManager.LoadChunkColumnPriority(
        pos.X / Server.WorldMap.ChunkSize, pos.Y / Server.WorldMap.ChunkSize);
    Server.LoadChunksInline();
    Block block = Server.World.BlockAccessor.GetBlock(pos);
    Assert.IsNotNull(block);

    Block newBlock = block == granite ? andesite : granite;
    Server.World.BlockAccessor.SetBlock(newBlock.Id, pos);
    Assert.AreEqual(newBlock, Server.World.BlockAccessor.GetBlock(pos));
  }

  [TestMethod]
  public void LoadMapChunk() {
    ICoreServerAPI sapi = (ICoreServerAPI)Server.Api;
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    Server.LoadChunksInline();
    IServerMapChunk mapChunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(mapChunk);
  }

  [TestMethod]
  public void LoadMapRegion() {
    ICoreServerAPI sapi = (ICoreServerAPI)Server.Api;
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    Server.LoadChunksInline();
    IMapRegion mapRegion = sapi.WorldManager.GetMapRegion(0, 0);
    Assert.IsNotNull(mapRegion);
  }
}
