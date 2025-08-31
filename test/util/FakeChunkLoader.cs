using System.Collections.Concurrent;
using System.Reflection;

using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.Server;

namespace Haven.Test;

public static class FakeChunkLoaderExtensions {

  static readonly FieldInfo systemsField =
      typeof(ServerMain)
          .GetField("Systems", BindingFlags.Instance | BindingFlags.NonPublic);

  public static void LoadChunksInline(this ServerMain serverMain) {
    ServerSystem[] systems = (ServerSystem[])systemsField.GetValue(serverMain);
    foreach (ServerSystem system in systems) {
      if (system is FakeChunkLoader loader) {
        loader.LoadChunksInline();
        return;
      }
    }
    throw new ArgumentException("Server is missing the FakeChunkLoader system",
                                nameof(serverMain));
  }

  public static void UnloadChunkColumn(this ServerMain serverMain, int chunkX,
                                       int chunkZ) {
    ServerSystem[] systems = (ServerSystem[])systemsField.GetValue(serverMain);
    foreach (ServerSystem system in systems) {
      if (system is FakeChunkLoader loader) {
        loader.UnloadChunkColumn(chunkX, chunkZ);
        return;
      }
    }
    throw new ArgumentException("Server is missing the FakeChunkLoader system",
                                nameof(serverMain));
  }
}

public class FakeChunkLoader : ServerSystem {
  private readonly ServerMain _server;
  /// <summary>
  /// Reference to _server.loadedChunksLock.
  /// </summary>
  private readonly FastRWLock _loadedChunksLock;
  /// <summary>
  /// Reference to _server.loadedChunks.
  /// </summary>
  private readonly Dictionary<long, ServerChunk> _loadedChunks;
  /// <summary>
  /// Reference to _server.fastChunkQueue.
  /// </summary>
  private readonly ConcurrentQueue<
      KeyValuePair<HorRectanglei, ChunkLoadOptions>> _fastChunkQueue;
  /// <summary>
  /// Reference to _server.serverChunkDataPool.
  /// </summary>
  private readonly ChunkDataPool _serverChunkDataPool;

  /// <summary>
  /// Serialized form of chunks that were once loaded but are now unloaded. The
  /// unloaded chunks are only saved in memory and never really saved to disk
  /// with this fake. The unloaded chunk data is lost when the test exits.
  /// </summary>
  private readonly Dictionary<long, byte[]> _unloadedChunks = [];

  private readonly FastMemoryStream _ms = new();

  public FakeChunkLoader(ServerMain server) : base(server) {
    _server = server;
    FieldInfo loadedChunksLockField = server.GetType().GetField(
        "loadedChunksLock", BindingFlags.Instance | BindingFlags.NonPublic);
    _loadedChunksLock = (FastRWLock)loadedChunksLockField.GetValue(server);
    FieldInfo loadedChunksField = server.GetType().GetField(
        "loadedChunks", BindingFlags.Instance | BindingFlags.NonPublic);
    _loadedChunks =
        (Dictionary<long, ServerChunk>)loadedChunksField.GetValue(server);
    FieldInfo fastChunkQueueField = server.GetType().GetField(
        "fastChunkQueue", BindingFlags.Instance | BindingFlags.NonPublic);
    _fastChunkQueue =
        (ConcurrentQueue<KeyValuePair<HorRectanglei, ChunkLoadOptions>>)
            fastChunkQueueField.GetValue(server);
    FieldInfo serverChunkDataPoolField = server.GetType().GetField(
        "serverChunkDataPool", BindingFlags.Instance | BindingFlags.NonPublic);
    _serverChunkDataPool =
        (ChunkDataPool)serverChunkDataPoolField.GetValue(server);
  }

  public void LoadChunksInline() {
    KeyValuePair<HorRectanglei, ChunkLoadOptions> loadRequest;
    while (!_fastChunkQueue.IsEmpty &&
           _fastChunkQueue.TryDequeue(out loadRequest)) {
      for (int chunkX = loadRequest.Key.X1; chunkX <= loadRequest.Key.X2;
           ++chunkX) {
        for (int chunkZ = loadRequest.Key.Z1; chunkZ <= loadRequest.Key.Z2;
             ++chunkZ) {
          for (int chunkY = 0; chunkY < _server.WorldMap.ChunkMapSizeY;
               ++chunkY) {
            long index3d =
                _server.WorldMap.ChunkIndex3D(chunkX, chunkY, chunkZ, 0);
            ServerChunk chunk =
                LoadOrCreateChunk(index3d, loadRequest.Value.ChunkGenParams);
            _loadedChunksLock.AcquireWriteLock();
            try {
              _loadedChunks[index3d] = chunk;
            } finally {
              _loadedChunksLock.ReleaseWriteLock();
            }
          }
        }
      }
      loadRequest.Value?.OnLoaded?.Invoke();
    }
  }

  private ServerChunk LoadOrCreateChunk(long index3d,
                                        ITreeAttribute chunkGenParams) {
    if (_unloadedChunks.TryGetValue(index3d, out byte[] serialized)) {
      return ServerChunk.FromBytes(serialized, _serverChunkDataPool, _server);
    }
    return ServerChunk.CreateNew(_serverChunkDataPool);
  }

  public bool UnloadChunkColumn(int chunkX, int chunkZ) {
    bool anyUnloaded = false;
    for (int chunkY = 0; chunkY < _server.WorldMap.ChunkMapSizeY; ++chunkY) {
      long index3d = _server.WorldMap.ChunkIndex3D(chunkX, chunkY, chunkZ, 0);
      anyUnloaded |= UnloadChunk(index3d);
    }
    return anyUnloaded;
  }

  public bool UnloadChunk(long index3d) {
    ServerChunk chunk;
    _loadedChunksLock.AcquireWriteLock();
    try {
      if (!_loadedChunks.Remove(index3d, out chunk)) {
        return false;
      }
    } finally {
      _loadedChunksLock.ReleaseWriteLock();
    }
    _unloadedChunks[index3d] = chunk.ToBytes(_ms);
    return true;
  }
}
