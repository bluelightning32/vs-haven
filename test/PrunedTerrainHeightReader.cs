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
  private static ServerMain s_server;
  private static Real.MatchResolver s_resolver;

  [ClassInitialize()]
  public static void ClassInit(TestContext testContext) {
    s_server = Framework.Server;
    s_resolver = new(s_server.World, s_server.Api.Logger);
  }

  public PrunedTerrainHeightReader() {}

  [TestMethod]
  public void SolidBlocksAreSolid() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    FakeChunkLoader loader = new();
    BlockConfig config = new();
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, true),
            config.ResolveTerrainCategories(s_resolver), 100);
    int y = ((ITerrainHeightReader)reader)
                .GetHeight(s_server.World.BlockAccessor, new(0, 0));
    s_server.World.BlockAccessor.SetBlock(
        granite.BlockId, new BlockPos(0, y, 0, Dimensions.NormalWorld));
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .IsSolid(s_server.World.BlockAccessor, new(0, 0)));

    y = ((ITerrainHeightReader)reader)
            .GetHeight(s_server.World.BlockAccessor, new(1, 0));
    for (; y >= 0; --y) {
      s_server.World.BlockAccessor.SetBlock(
          0, new BlockPos(1, y, 0, Dimensions.NormalWorld));
    }
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(s_server.World.BlockAccessor, new(1, 0)));
  }

  [TestMethod]
  public void IgnoresReplaceable() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    Block andesite =
        sapi.World.GetBlock(new AssetLocation("game:rock-andesite"));
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    for (int y = 0; y < 3; ++y) {
      s_server.World.BlockAccessor.SetBlock(
          granite.BlockId, new BlockPos(0, y, 0, Dimensions.NormalWorld));
      s_server.World.BlockAccessor.SetBlock(
          granite.BlockId, new BlockPos(1, y, 0, Dimensions.NormalWorld));
    }
    s_server.World.BlockAccessor.SetBlock(
        andesite.BlockId, new BlockPos(1, 1, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 2;
    chunk.RainHeightMap[1] = 2;

    FakeChunkLoader loader = new();
    BlockConfig config = new();
    config.TerrainReplace.Include = new() { { "game:rock-granite", 1 } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false),
            config.ResolveTerrainCategories(s_resolver), 100);
    // Should skip all the granite except at y=0.
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
    // Should skip all the granite down to the andesite at y=1.
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(1, 0)));
  }

  [TestMethod]
  public void HonorsNonsolid() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    Block andesite =
        sapi.World.GetBlock(new AssetLocation("game:rock-andesite"));
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    for (int y = 0; y < 3; ++y) {
      s_server.World.BlockAccessor.SetBlock(
          andesite.BlockId, new BlockPos(0, y, 0, Dimensions.NormalWorld));
      s_server.World.BlockAccessor.SetBlock(
          granite.BlockId, new BlockPos(1, y, 0, Dimensions.NormalWorld));
    }
    chunk.RainHeightMap[0] = 2;
    chunk.RainHeightMap[1] = 2;

    FakeChunkLoader loader = new();
    BlockConfig config = new();
    config.TerrainAvoid.Include = new() { { "game:rock-andesite", 1 } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false),
            config.ResolveTerrainCategories(s_resolver), 100);
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
    // The andesite was marked as non-solid.
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(s_server.World.BlockAccessor, new(0, 0)));
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(1, 0)));
    // Granite is solid by default.
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .IsSolid(s_server.World.BlockAccessor, new(1, 0)));
  }

  [TestMethod]
  public void WaterSurface() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    Block water = sapi.World.GetBlock(new AssetLocation("game:water-still-7"));
    Block granite = sapi.World.GetBlock(new AssetLocation("game:rock-granite"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    s_server.World.BlockAccessor.SetBlock(
        granite.BlockId, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    s_server.World.BlockAccessor.SetBlock(
        water.BlockId, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    s_server.World.BlockAccessor.SetBlock(
        water.BlockId, new BlockPos(0, 2, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 2;

    FakeChunkLoader loader = new();
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), [], 100);
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(s_server.World.BlockAccessor, new(0, 0)));
  }

  [TestMethod]
  public void RaiseDuplicateAtSurface() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    // Dirt block with no grass.
    Block soil =
        sapi.World.GetBlock(new AssetLocation("game:soil-medium-none"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    s_server.World.BlockAccessor.SetBlock(
        soil.BlockId, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 0;

    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories =
        new() { { soil.Id, TerrainCategory.Duplicate } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), terrainCategories, 3);
    Assert.AreEqual(3, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
  }

  [TestMethod]
  public void FailRaiseWithoutDuplicate() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    // Dirt block with grass.
    Block soil =
        sapi.World.GetBlock(new AssetLocation("game:soil-medium-normal"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    // Soil is marked as solid, but not duplicable. The terrain fails to get
    // raised, because there is no duplicable block below the soil.
    s_server.World.BlockAccessor.SetBlock(
        soil.BlockId, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    s_server.World.BlockAccessor.SetBlock(
        soil.BlockId, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 1;

    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories =
        new() { { soil.Id, TerrainCategory.Solid } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), terrainCategories, 3);
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
  }

  [TestMethod]
  public void FailRaiseHoldSolid() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    // Dirt block with grass.
    Block soil =
        sapi.World.GetBlock(new AssetLocation("game:soil-medium-normal"));
    Block planks =
        sapi.World.GetBlock(new AssetLocation("game:planks-aged-ns"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    // The soil block can be raised, but only if a duplicable block is found
    // below it. In this case a planks block is found instead, which is not
    // duplicable.
    s_server.World.BlockAccessor.SetBlock(
        planks.BlockId, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    s_server.World.BlockAccessor.SetBlock(
        soil.BlockId, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 1;

    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories = new() {
      { soil.Id, TerrainCategory.Solid },
      { planks.Id, TerrainCategory.SolidHold },
    };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), terrainCategories, 3);
    Assert.AreEqual(1, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
  }

  [TestMethod]
  public void RaiseSkip() {
    ICoreServerAPI sapi = (ICoreServerAPI)s_server.Api;
    // Dirt block with grass.
    Block soil =
        sapi.World.GetBlock(new AssetLocation("game:soil-medium-normal"));
    Block grass =
        sapi.World.GetBlock(new AssetLocation("game:tallgrass-short-free"));

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    s_server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    // The soil block can be raised, but only if a duplicable block is found
    // below it. In this case, grass is found below it. The grass cannot be
    // directly duplicated, but air can be duplicated in its place.
    s_server.World.BlockAccessor.SetBlock(
        grass.BlockId, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    s_server.World.BlockAccessor.SetBlock(
        soil.BlockId, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 1;

    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories =
        new() { { soil.Id, TerrainCategory.Solid },
                { grass.Id, TerrainCategory.Skip } };
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), terrainCategories, 3);
    Assert.AreEqual(4, ((ITerrainHeightReader)reader)
                           .GetHeight(s_server.World.BlockAccessor, new(0, 0)));
  }
}
