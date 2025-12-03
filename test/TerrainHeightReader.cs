using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class TerrainHeightReader {
  private readonly ServerMain _server;
  public TerrainHeightReader() { _server = Framework.Server; }

  [TestMethod]
  public void RequestUnloadedChunk() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(
        Framework.UnloadedMapChunkX, Framework.UnloadedMapChunkZ);
    Assert.IsNull(chunk);

    FakeChunkLoader loader = new();
    Real.TerrainHeightReader reader = new(loader, true);
    Assert.IsNull(((ITerrainHeightReader)reader)
                      .GetHeights(_server.World.BlockAccessor,
                                  Framework.UnloadedMapChunkX,
                                  Framework.UnloadedMapChunkZ));
    CollectionAssert.AreEquivalent(
        new Vec3i[] { new(Framework.UnloadedMapChunkX, 0,
                          Framework.UnloadedMapChunkZ) },
        loader.Requested.ToList());
  }

  [TestMethod]
  public void RequestLoadedChunk() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    _server.LoadChunksInline();
    IServerMapChunk chunk = sapi.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    FakeChunkLoader loader = new();
    Real.TerrainHeightReader reader = new(loader, true);
    Assert.IsNotNull(((ITerrainHeightReader)reader)
                         .GetHeights(_server.World.BlockAccessor, 0, 0));
    CollectionAssert.AreEquivalent(Array.Empty<Vec2i>(),
                                   loader.Requested.ToList());
  }

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
    Real.TerrainHeightReader reader = new(loader, true);
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
}
