using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Server;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ChunkLoader {
  private readonly ServerMain _server;
  public ChunkLoader() { _server = Framework.Server; }

  [TestMethod]
  public void RequestLoadedChunk() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    _server.LoadChunksInline();
    IServerChunk chunk = sapi.WorldManager.GetChunk(0, 0, 0);
    Assert.IsNotNull(chunk);

    int callbackCalled = 0;
    List<IWorldChunk> loaded = null;
    void Callback(List<IWorldChunk> chunks) {
      callbackCalled++;
      loaded = chunks;
    }

    Real.ChunkLoader loader =
        new(sapi.Event, sapi.WorldManager, sapi.World.BlockAccessor, Callback);
    loader.LoadChunk(0, 0, 0);

    _server.ProcessMainThreadTasks();

    Assert.AreEqual(1, callbackCalled);
    CollectionAssert.AreEquivalent(new[] { chunk }, loaded);
  }

  [TestMethod]
  public void LoadsDeduplicated() {
    ICoreServerAPI sapi = (ICoreServerAPI)_server.Api;

    // Ensure the chunk is loaded.
    sapi.WorldManager.LoadChunkColumnPriority(0, 0);
    sapi.WorldManager.LoadChunkColumnPriority(1, 0);
    _server.LoadChunksInline();
    IServerChunk chunk0 = sapi.WorldManager.GetChunk(0, 0, 0);
    Assert.IsNotNull(chunk0);
    IServerChunk chunk1 = sapi.WorldManager.GetChunk(1, 0, 0);
    Assert.IsNotNull(chunk1);

    int callbackCalled = 0;
    List<IWorldChunk> loaded = null;
    void Callback(List<IWorldChunk> chunks) {
      callbackCalled++;
      loaded = chunks;
    }

    Real.ChunkLoader loader =
        new(sapi.Event, sapi.WorldManager, sapi.World.BlockAccessor, Callback);
    loader.LoadChunk(0, 0, 0);
    loader.LoadChunk(1, 0, 0);
    loader.LoadChunk(0, 0, 0);
    loader.LoadChunk(1, 0, 0);

    _server.ProcessMainThreadTasks();

    Assert.AreEqual(1, callbackCalled);
    CollectionAssert.AreEquivalent(new[] { chunk0, chunk1 }, loaded);
  }
}
