using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven;

public interface ITerrainHeightReader {
  /// <summary>
  /// Get the y position of the surface at a location
  /// </summary>
  /// <param name="accessor">An accessor for reading map chunks</param>
  /// <param name="pos">The X Z block position to query</param>
  /// <returns>
  /// The y position of the surface block if available, or -1 if the accessor
  /// cannot find the map chunk (load the chunk and try again)</returns>
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
  /// Determine whether the surface is solid
  /// </summary>
  /// <param name="accessor">An accessor for reading map chunks</param>
  /// <param name="pos">The X Z block position to query</param>
  /// <returns>1 if the block is solid, 0 if it is not, or -1 if the chunk is
  /// unloaded</returns>
  public int IsSolid(IBlockAccessor accessor, Vec2i pos) {
    (ushort[] heights, bool[] solid) =
        GetHeightsAndSolid(accessor, pos.X / GlobalConstants.ChunkSize,
                           pos.Y / GlobalConstants.ChunkSize);
    if (solid == null) {
      return -1;
    }
    return solid[pos.X % GlobalConstants.ChunkSize +
                 pos.Y % GlobalConstants.ChunkSize * GlobalConstants.ChunkSize]
               ? 1
               : 0;
  }

  /// <summary>
  /// Query the surface heights of all blocks in the column
  /// </summary>
  /// <param name="accessor">An accessor for loading map chunks</param>
  /// <param name="chunkX"></param>
  /// <param name="chunkZ"></param>
  /// <returns></returns>
  ushort[] GetHeights(IBlockAccessor accessor, int chunkX, int chunkZ) {
    return GetHeightsAndSolid(accessor, chunkX, chunkZ).Item1;
  }

  /// <summary>
  /// Query the surface heights of all blocks in the column along with whether
  /// they are solid (as opposed to water)
  /// </summary>
  /// <param name="accessor">An accessor for loading map chunks</param>
  /// <param name="chunkX"></param>
  /// <param name="chunkZ"></param>
  /// <returns></returns>
  (ushort[], bool[])
      GetHeightsAndSolid(IBlockAccessor accessor, int chunkX, int chunkZ);
}

public class TerrainHeightReader : ITerrainHeightReader {
  private readonly IChunkLoader _loader;
  private readonly bool _useWorldGenHeight = true;
  private readonly HashSet<int> _replace;

  public TerrainHeightReader(IChunkLoader loader, bool useWorldGenHeight,
                             HashSet<int> replace) {
    _loader = loader;
    _useWorldGenHeight = useWorldGenHeight;
    _replace = replace;
  }

  public (ushort[], bool[])
      GetHeightsAndSolid(IBlockAccessor accessor, int chunkX, int chunkZ) {
    IMapChunk chunk = accessor.GetMapChunk(chunkX, chunkZ);
    if (chunk == null) {
      _loader.LoadChunkColumn(chunkX, chunkZ);
      return (null, null);
    }
    ushort[] heightsOriginal = _useWorldGenHeight
                                   ? chunk.WorldGenTerrainHeightMap
                                   : chunk.RainHeightMap;
    const int blocks = GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
    bool[] solid = new bool[blocks];
    ushort[] heights = new ushort[blocks];
    int offset = 0;
    BlockPos pos =
        new(Dimensions.NormalWorld) { Z = chunkZ * GlobalConstants.ChunkSize };
    int xOffset = chunkX * GlobalConstants.ChunkSize;
    for (int z = 0; z < GlobalConstants.ChunkSize; ++z, ++pos.Z) {
      pos.X = xOffset;
      for (int x = 0; x < GlobalConstants.ChunkSize; ++x, ++pos.X, ++offset) {
        pos.Y = heightsOriginal[offset];
        Block surface = accessor.GetBlock(pos);
        while (pos.Y > 0 && _replace.Contains(surface.Id)) {
          --pos.Y;
          surface = accessor.GetBlock(pos);
        }
        surface = accessor.GetBlock(pos, BlockLayersAccess.Solid);
        solid[offset] = surface.Id != 0;
        heights[offset] = (ushort)pos.Y;
      }
    }
    return (heights, solid);
  }
}
