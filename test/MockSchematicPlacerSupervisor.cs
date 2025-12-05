using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using static Haven.ISchematicPlacerSupervisor;

using Real = Haven;

namespace Haven.Test;

/// <summary>
/// A fake terrain height reader that returns the heights that are internally
/// stored in RAM.
/// </summary>
public class MockSchematicPlacerSupervisor : ISchematicPlacerSupervisor {
  public Real.TerrainSurvey Terrain { get; set; }
  public IChunkLoader Loader { get; set; }
  public IWorldAccessor WorldForResolve { get; set; }

  public FakeChunkLoader FakeLoader = new();
  public MemoryTerrainHeightReader FakeTerrain = new();
  public System.Func<IBlockAccessor, Real.SchematicPlacer, BlockPos,
                     LocationResult> TryFinalizeLocationMock = null;

  public MockSchematicPlacerSupervisor() {
    WorldForResolve = Framework.Server;
    Loader = FakeLoader;
    Terrain = new(FakeTerrain);
  }

  public LocationResult TryFinalizeLocation(IBlockAccessor accessor,
                                            Real.SchematicPlacer placer,
                                            BlockPos offset) {
    if (TryFinalizeLocationMock != null) {
      return TryFinalizeLocationMock(accessor, placer, offset);
    }
    return LocationResult.Accepted;
  }
}
