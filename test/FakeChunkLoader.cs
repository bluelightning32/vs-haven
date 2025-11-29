using Vintagestory.API.MathTools;

namespace Haven.Test;

/// <summary>
/// A chunk loader that simply remembers which chunks were requested.
/// </summary>
public class FakeChunkLoader : IChunkLoader {
  public readonly HashSet<Vec2i> Requested = [];
  public FakeChunkLoader() {}

  public void LoadChunkColumn(int chunkX, int chunkZ) {
    Requested.Add(new Vec2i(chunkX, chunkZ));
  }
}
