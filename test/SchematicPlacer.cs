using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class SchematicPlacer {
  public static Real.SchematicPlacer
  CreateGraniteBox(int sx, int sy, int sz, int offsetY, BlockPos offset,
                   ISchematicPlacerSupervisor supervisor) {
    Real.OffsetBlockSchematic schematic =
        OffsetBlockSchematic.CreateGraniteBox(sx, sy, sz, offsetY);
    schematic.AutoConfigureProbes();
    return new Real.SchematicPlacer(schematic, offset, supervisor);
  }

  [TestMethod]
  public void PlacesOnLoadedChunks() {
    MockSchematicPlacerSupervisor supervisor = new();
    Real.SchematicPlacer placer =
        CreateGraniteBox(1, 1, 1, 0, new BlockPos(0, 1, 0), supervisor);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(0, 0, 0, 0, 0);

    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(new BlockPos[] { new(0, 1, 0) },
                                   accessor.StagedBlocks.Keys);
  }

  [TestMethod]
  public void OnlyPlacesBlocksOnce() {
    MockSchematicPlacerSupervisor supervisor = new();
    Real.SchematicPlacer placer =
        CreateGraniteBox(1, 1, 1, 0, new BlockPos(0, 1, 0), supervisor);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(0, 0, 0, 0, 0);

    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(new BlockPos[] { new(0, 1, 0) },
                                   accessor.StagedBlocks.Keys);

    // Call Generate again and verify it does not modify any more blocks,
    // because Generated already completed.
    accessor.StagedBlocks.Clear();
    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(Array.Empty<BlockPos>(),
                                   accessor.StagedBlocks.Keys);

    // Verify that Commit does not modify any blocks either.
    Assert.IsTrue(placer.Commit(accessor));
    CollectionAssert.AreEquivalent(Array.Empty<BlockPos>(),
                                   accessor.StagedBlocks.Keys);
  }

  [TestMethod]
  public void KeepsGoodStructurePosition() {
    MockSchematicPlacerSupervisor supervisor = new();
    bool locationSelected = false;
    Real.SchematicPlacer placer = null;
    BlockPos pos =
        new(3 * GlobalConstants.ChunkSize, 101, 3 * GlobalConstants.ChunkSize);
    bool FinalizeLocation(Real.SchematicPlacer placer2, BlockPos pos2) {
      Assert.IsFalse(locationSelected);
      locationSelected = true;
      Assert.AreEqual(pos, pos2);
      Assert.AreEqual(placer, placer2);
      return true;
    }
    supervisor.TryFinalizeLocationMock = FinalizeLocation;
    placer = CreateGraniteBox(1, 1, 1, 0, pos, supervisor);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(3, 3);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(3, 3, 100, 0, 0);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    Assert.IsTrue(placer.Generate(accessor));
    Assert.IsTrue(locationSelected);
  }

  [TestMethod]
  public void FindsGoodStructurePosition() {
    MockSchematicPlacerSupervisor supervisor = new();
    bool locationSelected = false;
    Real.SchematicPlacer placer = null;
    BlockPos pos =
        new(3 * GlobalConstants.ChunkSize, 100, 3 * GlobalConstants.ChunkSize);
    bool FinalizeLocation(Real.SchematicPlacer placer2, BlockPos pos2) {
      Assert.IsFalse(locationSelected);
      locationSelected = true;
      Assert.AreNotEqual(pos, pos2);
      Assert.IsTrue(pos.X != pos2.X || pos.Z != pos2.Z);
      Assert.IsLessThan(GlobalConstants.ChunkSize * 2,
                        pos2.ManhattenDistance(pos));
      Assert.AreEqual(placer, placer2);
      return true;
    }
    supervisor.TryFinalizeLocationMock = FinalizeLocation;
    placer = CreateGraniteBox(2, 1, 2, 0, pos, supervisor);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(3, 3);
    Framework.Server.LoadChunksInline();
    for (int z = 1; z < 4; ++z) {
      for (int x = 1; x < 4; ++x) {
        supervisor.FakeTerrain.FillChunk(x, z, 100, 0, 0);
      }
    }
    // Make the initial location rough so that the placer has to find a new
    // location.
    supervisor.FakeTerrain.FillChunk(3, 3, 100, 4, 0);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);
    Assert.IsTrue(placer.Generate(accessor));
    Assert.IsTrue(locationSelected);
  }

  [TestMethod]
  public void SerializationRemembersPlacedBlocks() {
    MockSchematicPlacerSupervisor supervisor = new();
    Real.SchematicPlacer placer =
        CreateGraniteBox(1, 1, 1, 0, new BlockPos(0, 1, 0), supervisor);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(0, 0, 0, 0, 0);

    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(new BlockPos[] { new(0, 1, 0) },
                                   accessor.StagedBlocks.Keys);

    byte[] data = SerializerUtil.Serialize(placer);
    Real.SchematicPlacer copy =
        SerializerUtil.Deserialize<Real.SchematicPlacer>(data);
    copy.Restore(supervisor);

    // Call Generate on the copy and verify it does not modify any more blocks,
    // because Generated already completed on the original placer.
    accessor.StagedBlocks.Clear();
    Assert.IsTrue(copy.Generate(accessor));
    CollectionAssert.AreEquivalent(Array.Empty<BlockPos>(),
                                   accessor.StagedBlocks.Keys);
  }
}
