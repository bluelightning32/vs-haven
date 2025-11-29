using PrefixClassName.MsTest;

using Vintagestory.API.Common;
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
    Assert.IsNull(reader.GetHeights(_server.World.BlockAccessor,
                                    Framework.UnloadedMapChunkX,
                                    Framework.UnloadedMapChunkZ));
    CollectionAssert.AreEquivalent(
        new Vec2i[] { new(Framework.UnloadedMapChunkX,
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
    Assert.IsNotNull(reader.GetHeights(_server.World.BlockAccessor, 0, 0));
    CollectionAssert.AreEquivalent(Array.Empty<Vec2i>(),
                                   loader.Requested.ToList());
  }
}
