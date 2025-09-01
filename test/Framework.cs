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

  [AssemblyInitialize()]
  public static void AssemblyInitialize(TestContext context) {
    Dictionary<AssetCategory, HashSet<string>> allow =
        new() { [AssetCategory.itemtypes] = new() { "fruit.json" },
                [AssetCategory.blocktypes] = new() { "rock.json" },
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

    Server.UnloadChunkColumn(0, 0);
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
    Block block = Server.World.BlockAccessor.GetBlock(pos);
    Assert.IsNotNull(block);

    Block newBlock = block == granite ? andesite : granite;
    Server.World.BlockAccessor.SetBlock(newBlock.Id, pos);
    Assert.AreEqual(newBlock, Server.World.BlockAccessor.GetBlock(pos));
  }
}
