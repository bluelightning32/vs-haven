using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using Real = Haven;

namespace Haven.Test;

/// <summary>
/// A fake terrain height reader that returns the heights that are internally
/// stored in RAM.
/// </summary>
public class MockSchematicPlacerSupervisor : ISchematicPlacerSupervisor {
  public ITerrainHeightReader Terrain { get; set; }
  public IChunkLoader Loader { get; set; }
  public IWorldAccessor WorldForResolve { get; set; }

  public FakeChunkLoader FakeLoader = new();
  public MemoryTerrainHeightReader FakeTerrain = new();
  public System
      .Func<Real.SchematicPlacer, BlockPos, bool> TryFinalizeLocationMock =
      null;

  public MockSchematicPlacerSupervisor() {
    WorldForResolve = Framework.Server;
    Loader = FakeLoader;
    Terrain = FakeTerrain;
  }

  public bool TryFinalizeLocation(Real.SchematicPlacer placer,
                                  BlockPos offset) {
    if (TryFinalizeLocationMock != null) {
      return TryFinalizeLocationMock(placer, offset);
    }
    return true;
  }
}
