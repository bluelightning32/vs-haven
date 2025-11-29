using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven.Test;

/// <summary>
/// A fake terrain height reader that returns the heights that are internally
/// stored in RAM.
/// </summary>
public class MemoryTerrainHeightReader : ITerrainHeightReader {
  private readonly HashSet<Vec2i> _requestedChunks = [];
  private readonly Dictionary<Vec2i, ushort[]> _chunkHeights = [];

  public MemoryTerrainHeightReader() {}

  public bool WasChunkRequested(int chunkX, int chunkZ) {
    return _requestedChunks.Contains(new Vec2i(chunkX, chunkZ));
  }

  public void ClearRequestedChunks() { _requestedChunks.Clear(); }

  public ushort[] GetHeights(IBlockAccessor accessor, int chunkX, int chunkZ) {
    Vec2i key = new(chunkX, chunkZ);
    _requestedChunks.Add(key);
    return _chunkHeights.GetValueOrDefault(key);
  }

  public void FillChunk(int chunkX, int chunkZ, int intercept, double xslope,
                        double zslope) {
    Vec2i key = new(chunkX, chunkZ);
    if (!_chunkHeights.TryGetValue(key, out ushort[] heights)) {
      heights =
          new ushort[GlobalConstants.ChunkSize * GlobalConstants.ChunkSize];
      _chunkHeights.Add(key, heights);
    }
    for (int z = 0; z < GlobalConstants.ChunkSize; ++z) {
      for (int x = 0; x < GlobalConstants.ChunkSize; ++x) {
        heights[x + z * GlobalConstants.ChunkSize] =
            (ushort)(intercept + x * xslope + z * zslope);
      }
    }
  }

  public void SetHeight(int x, int z, ushort height) {
    Vec2i key =
        new(x / GlobalConstants.ChunkSize, z / GlobalConstants.ChunkSize);
    if (!_chunkHeights.TryGetValue(key, out ushort[] heights)) {
      heights =
          new ushort[GlobalConstants.ChunkSize * GlobalConstants.ChunkSize];
      _chunkHeights.Add(key, heights);
    }
    heights[x % GlobalConstants.ChunkSize +
            z % GlobalConstants.ChunkSize * GlobalConstants.ChunkSize] = height;
  }
}
