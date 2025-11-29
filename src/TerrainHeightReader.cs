using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

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
  /// is loaded. The accessor will internally deduplicate multiple requests for
  /// the same chunk column.
  /// </summary>
  /// <param name="chunkX"></param>
  /// <param name="chunkZ"></param>
  void LoadChunkColumn(int chunkX, int chunkZ);
}

public interface ITerrainHeightReader {
  /// <summary>
  /// Get the world height at a location
  /// </summary>
  /// <param name="accessor">An accessor for loading map chunks</param>
  /// <param name="pos">The X Z block position to query</param>
  /// <returns>The height if available, or -1 if the accessor cannot find the
  /// map chunk (load the chunk and try again)</returns>
  public int GetHeight(IBlockAccessor accessor, Vec2i pos) {
    ushort[] heights = GetHeights(accessor, pos.X / GlobalConstants.ChunkSize,
                                  pos.Y / GlobalConstants.ChunkSize);
    if (heights == null) {
      return -1;
    }
    return heights[pos.X % GlobalConstants.ChunkSize +
                   pos.Y % GlobalConstants.ChunkSize *
                       GlobalConstants.ChunkSize];
  }

  /// <summary>
  /// Query the surface heights of all blocks in the column
  /// </summary>
  /// <param name="accessor">An accessor for loading map chunks</param>
  /// <param name="chunkX"></param>
  /// <param name="chunkZ"></param>
  /// <returns></returns>
  ushort[] GetHeights(IBlockAccessor accessor, int chunkX, int chunkZ);
}

public class TerrainHeightReader : ITerrainHeightReader {
  private readonly IChunkLoader _loader;
  private readonly bool _useWorldGenHeight = true;

  public TerrainHeightReader(IChunkLoader loader, bool useWorldGenHeight) {
    _loader = loader;
    _useWorldGenHeight = useWorldGenHeight;
  }

  public ushort[] GetHeights(IBlockAccessor accessor, int chunkX, int chunkZ) {
    IMapChunk chunk = accessor.GetMapChunk(chunkX, chunkZ);
    if (chunk == null) {
      _loader.LoadChunkColumn(chunkX, chunkZ);
      return null;
    }
    if (_useWorldGenHeight) {
      return chunk.WorldGenTerrainHeightMap;
    } else {
      return chunk.RainHeightMap;
    }
  }
}
