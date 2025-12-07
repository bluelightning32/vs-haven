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
  private readonly ServerMain _server;
  public PrunedTerrainHeightReader() { _server = Framework.Server; }

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
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, true), [], []);
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
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), [granite.Id], []);
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
    Real.PrunedTerrainHeightReader reader =
        new(new Real.TerrainHeightReader(loader, false), [], [andesite.Id]);
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
        new(new Real.TerrainHeightReader(loader, false), [], []);
    Assert.AreEqual(2, ((ITerrainHeightReader)reader)
                           .GetHeight(_server.World.BlockAccessor, new(0, 0)));
    Assert.AreEqual(0, ((ITerrainHeightReader)reader)
                           .IsSolid(_server.World.BlockAccessor, new(0, 0)));
  }
}
