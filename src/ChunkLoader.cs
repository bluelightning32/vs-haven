using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Haven;

public interface IChunkLoader {
  /// <summary>
  /// Triggers the requested chunk column to be asynchronously loaded. The
  /// accessor will call the generator's Process method after the chunk column
  /// is loaded. The accessor will internally deduplicate multiple requests for
  /// the same chunk column.
  /// </summary>
  /// <param name="pos">A block X Z position (not chunk index)</param>
  void LoadChunkColumnByBlockXZ(int blockX, int blockZ) {
    LoadChunkColumn(blockX / GlobalConstants.ChunkSize,
                    blockZ / GlobalConstants.ChunkSize);
  }

  /// <summary>
  /// Triggers the requested chunk column to be asynchronously loaded. The
  /// accessor will call the generator's Process method after the chunk column
  /// is loaded (or sometime soon if the chunk is already loaded). The accessor
  /// will internally deduplicate multiple requests for the same chunk column.
  /// </summary>
  /// <param name="chunkX"></param>
  /// <param name="chunkZ"></param>
  void LoadChunkColumn(int chunkX, int chunkZ) { LoadChunk(chunkX, 0, chunkZ); }

  void LoadChunk(int chunkX, int chunkY, int chunkZ);
}

public class ChunkLoader : IChunkLoader {
  private readonly IEventAPI _eventAPI;
  private readonly IWorldManagerAPI _world;
  private readonly IBlockAccessor _accessor;
  private readonly ConcurrentDictionary<Vec3i, bool> _requestedChunks = [];
  readonly Action<List<IWorldChunk>> _chunksLoaded;
  /// <summary>
  /// Set to 1 when the main thread task is scheduled. This is used to avoid
  /// calling the main thread task too often.
  /// </summary>
  private int _taskScheduled = 0;

  /// <summary>
  /// Constructs a chunk loader
  /// </summary>
  /// <param name="eventAPI">used enqueue main thread tasks</param>
  /// <param name="world">used to schedule chunk column loads</param>
  /// <param name="accessor">used to verify that chunks are loaded. This is only
  /// accessed on the main thread.</param> <param name="chunksLoaded">called on
  /// the main thread after chunks are loaded</param>
  public ChunkLoader(IEventAPI eventAPI, IWorldManagerAPI world,
                     IBlockAccessor accessor,
                     Action<List<IWorldChunk>> chunksLoaded) {
    _eventAPI = eventAPI;
    _world = world;
    _accessor = accessor;
    _accessor = accessor;
    _chunksLoaded = chunksLoaded;
  }

  public void LoadChunk(int chunkX, int chunkY, int chunkZ) {
    Vec3i key = new(chunkX, chunkY, chunkZ);
    if (_requestedChunks.TryAdd(key, true)) {
      _world.LoadChunkColumnPriority(
          chunkX, chunkZ, new ChunkLoadOptions() { OnLoaded = ChunkLoaded });
    }
  }

  private void ChunkLoaded() {
    int original = Interlocked.Or(ref _taskScheduled, 1);
    if (original == 0) {
      // This call caused _taskScheduled to transition from 0 to 1. It is
      // responsible for scheduling the task.
      _eventAPI.EnqueueMainThreadTask(ChunkLoadedTask, "Haven.ChunkLoadedTask");
    }
  }

  private void ChunkLoadedTask() {
    _taskScheduled = 0;
    List<IWorldChunk> loaded = [];
    foreach (Vec3i c in _requestedChunks.Keys) {
      IWorldChunk chunk = _accessor.GetChunk(c.X, c.Y, c.Z);
      if (chunk != null) {
        loaded.Add(chunk);
        _requestedChunks.Remove(c, out bool unused);
      }
    }
    if (loaded.Count > 0) {
      _chunksLoaded(loaded);
    }
  }
}
