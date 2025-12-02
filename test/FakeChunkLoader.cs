using Vintagestory.API.MathTools;

namespace Haven.Test;

/// <summary>
/// A chunk loader that simply remembers which chunks were requested.
/// </summary>
public class FakeChunkLoader : IChunkLoader {
  public readonly HashSet<Vec3i> Requested = [];
  public FakeChunkLoader() {}

  public void LoadChunk(int chunkX, int chunkY, int chunkZ) {
    Requested.Add(new Vec3i(chunkX, chunkY, chunkZ));
  }
}
