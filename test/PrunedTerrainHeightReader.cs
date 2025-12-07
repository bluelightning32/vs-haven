using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class PrunedTerrainHeightReader {
  private static ServerMain _server;
  private static Real.MatchResolver _resolver;

  [ClassInitialize()]
  public static void ClassInit(TestContext testContext) {
    _server = Framework.Server;
    _resolver = new(_server.World, _server.Api.Logger);
  }

  public PrunedTerrainHeightReader() {}

  [TestMethod]
  public void SolidBlocksAreSolid() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    _server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    FakeChunkLoader loader = new();
    BlockConfig config = new();
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, true),
            config.ResolveTerrainCategories(_resolver));
    int y = ((ITerrainHeightReader)reader)
                .GetHeight(_server.World.BlockAccessor, new(0, 0));
    _server.World.BlockAccessor.SetBlock(
        granite.BlockId, new BlockPos(0, y, 0, Dimensions.NormalWorld));
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .IsSolid(_server.World.BlockAccessor, new(0, 0)));

    y = ((ITerrainHeightReader)reader)
            .GetHeight(_server.World.BlockAccessor, new(1, 0));
    for (; y >= 0; --y) {
      _server.World.BlockAccessor.SetBlock(
          0, new BlockPos(1, y, 0, Dimensions.NormalWorld));
    }
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(_server.World.BlockAccessor, new(1, 0)));
  }

  [TestMethod]
  public void IgnoresReplaceable() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;
    Block andesite =
        sapi.World.GetBlock(new AssetLocation("game:rock-andesite"));
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    _server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    for (int y = 0; y < 3; ++y) {
      _server.World.BlockAccessor.SetBlock(
          granite.BlockId, new BlockPos(0, y, 0, Dimensions.NormalWorld));
      _server.World.BlockAccessor.SetBlock(
          granite.BlockId, new BlockPos(1, y, 0, Dimensions.NormalWorld));
    }
    _server.World.BlockAccessor.SetBlock(
        andesite.BlockId, new BlockPos(1, 1, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 2;
    chunk.RainHeightMap[1] = 2;

    FakeChunkLoader loader = new();
    BlockConfig config = new();
    config.TerrainReplace.Include = new() { { "game:rock-granite", 1 } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false),
            config.ResolveTerrainCategories(_resolver));
    // Should skip all the granite except at y=0.
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .GetHeight(_server.World.BlockAccessor, new(0, 0)));
    // Should skip all the granite down to the andesite at y=1.
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .GetHeight(_server.World.BlockAccessor, new(1, 0)));
  }

  [TestMethod]
  public void HonorsNonsolid() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;
    Block andesite =
        sapi.World.GetBlock(new AssetLocation("game:rock-andesite"));
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    _server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    for (int y = 0; y < 3; ++y) {
      _server.World.BlockAccessor.SetBlock(
          andesite.BlockId, new BlockPos(0, y, 0, Dimensions.NormalWorld));
      _server.World.BlockAccessor.SetBlock(
          granite.BlockId, new BlockPos(1, y, 0, Dimensions.NormalWorld));
    }
    chunk.RainHeightMap[0] = 2;
    chunk.RainHeightMap[1] = 2;

    FakeChunkLoader loader = new();
    BlockConfig config = new();
    config.TerrainAvoid.Include = new() { { "game:rock-andesite", 1 } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false),
            config.ResolveTerrainCategories(_resolver));
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(_server.World.BlockAccessor, new(0, 0)));
    // The andesite was marked as non-solid.
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(_server.World.BlockAccessor, new(0, 0)));
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(_server.World.BlockAccessor, new(1, 0)));
    // Granite is solid by default.
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .IsSolid(_server.World.BlockAccessor, new(1, 0)));
  }

  [TestMethod]
  public void WaterSurface() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;
    Block water = sapi.World.GetBlock(new AssetLocation("game:water-still-7"));
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    _server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    _server.World.BlockAccessor.SetBlock(
        granite.BlockId, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    _server.World.BlockAccessor.SetBlock(
        water.BlockId, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    _server.World.BlockAccessor.SetBlock(
        water.BlockId, new BlockPos(0, 2, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 2;

    FakeChunkLoader loader = new();
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), []);
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(_server.World.BlockAccessor, new(0, 0)));
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(_server.World.BlockAccessor, new(0, 0)));
  }
}
