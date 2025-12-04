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
  private readonly Dictionary<Vec2i, (ushort[], bool[])> _chunkData = [];

  public MemoryTerrainHeightReader() {}

  public bool WasChunkRequested(int chunkX, int chunkZ) {
    return _requestedChunks.Contains(new Vec2i(chunkX, chunkZ));
  }

  public void ClearRequestedChunks() { _requestedChunks.Clear(); }

  public (ushort[], bool[])
      GetHeightsAndSolid(IBlockAccessor accessor, int chunkX, int chunkZ) {
    Vec2i key = new(chunkX, chunkZ);
    _requestedChunks.Add(key);
    return _chunkData.GetValueOrDefault(key, (null, null));
  }

  private (ushort[], bool[]) GetOrCreateChunkData(int chunkX, int chunkZ) {
    Vec2i key = new(chunkX, chunkZ);
    if (!_chunkData.TryGetValue(key, out(ushort[], bool[]) data)) {
      data =
          new(new ushort[GlobalConstants.ChunkSize * GlobalConstants.ChunkSize],
              new bool[GlobalConstants.ChunkSize * GlobalConstants.ChunkSize]);
      _chunkData.Add(key, data);
    }
    return data;
  }

  public void FillChunk(int chunkX, int chunkZ, int intercept, double xslope,
                        double zslope, bool solid = true) {
    (ushort[] heights, bool[] solids) = GetOrCreateChunkData(chunkX, chunkZ);
    for (int z = 0; z < GlobalConstants.ChunkSize; ++z) {
      for (int x = 0; x < GlobalConstants.ChunkSize; ++x) {
        heights[x + z * GlobalConstants.ChunkSize] =
            (ushort)(intercept + x * xslope + z * zslope);
        solids[x + z * GlobalConstants.ChunkSize] = solid;
      }
    }
  }

  public void SetHeight(int x, int z, ushort height) {
    (ushort[] heights, bool[] solid) = GetOrCreateChunkData(
        x / GlobalConstants.ChunkSize, z / GlobalConstants.ChunkSize);
    heights[x % GlobalConstants.ChunkSize +
            z % GlobalConstants.ChunkSize * GlobalConstants.ChunkSize] = height;
  }

  public void SetSolid(int x, int z, bool solid) {
    (ushort[] heights, bool[] solids) = GetOrCreateChunkData(
        x / GlobalConstants.ChunkSize, z / GlobalConstants.ChunkSize);
    solids[x % GlobalConstants.ChunkSize +
           z % GlobalConstants.ChunkSize * GlobalConstants.ChunkSize] = solid;
  }
}
