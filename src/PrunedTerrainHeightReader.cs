using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven;

public class PrunedTerrainHeightReader : ITerrainHeightReader {
  private readonly ITerrainHeightReader _source;
  private readonly HashSet<int> _replace;
  private readonly HashSet<int> _nonsolid;

  public PrunedTerrainHeightReader(ITerrainHeightReader source,
                                   HashSet<int> replace,
                                   HashSet<int> nonsolid) {
    _source = source;
    _replace = replace;
    _nonsolid = nonsolid;
    _nonsolid ??= [];
    _nonsolid.Add(0);
  }

  public (ushort[], bool[])
      GetHeightsAndSolid(IBlockAccessor accessor, int chunkX, int chunkZ) {
    ushort[] heightsSource = _source.GetHeights(accessor, chunkX, chunkZ);
    if (heightsSource == null) {
      return (null, null);
    }
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
        pos.Y = heightsSource[offset];
        Block surface = accessor.GetBlock(pos);
        while (pos.Y > 0 && _replace.Contains(surface.Id)) {
          --pos.Y;
          surface = accessor.GetBlock(pos);
        }
        surface = accessor.GetBlock(pos, BlockLayersAccess.Solid);
        solid[offset] = !_nonsolid.Contains(surface.Id);
        heights[offset] = (ushort)pos.Y;
      }
    }
    return (heights, solid);
  }
}
