using PrefixClassName.MsTest;

using Vintagestory.API.Common;
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
        CreateGraniteBox(1, 1, 1, 0, new BlockPos(0, 0, 0), supervisor);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(0, 0, 0, 0, 0);

    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(new BlockPos[] { new(0, 0, 0) },
                                   accessor.StagedBlocks.Keys);
  }

  [TestMethod]
  public void OnlyPlacesBlocksOnce() {
    MockSchematicPlacerSupervisor supervisor = new();
    Real.SchematicPlacer placer =
        CreateGraniteBox(1, 1, 1, 0, new BlockPos(0, 0, 0), supervisor);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(0, 0, 0, 0, 0);

    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(new BlockPos[] { new(0, 0, 0) },
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
  public void SerializationRemembersPlacedBlocks() {
    MockSchematicPlacerSupervisor supervisor = new();
    Real.SchematicPlacer placer =
        CreateGraniteBox(1, 1, 1, 0, new BlockPos(0, 0, 0), supervisor);
    IBulkBlockAccessor accessor =
        Framework.Server.GetBlockAccessorBulkUpdate(false, false);

    // Mark the chunk as loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    supervisor.FakeTerrain.FillChunk(0, 0, 0, 0, 0);

    Assert.IsTrue(placer.Generate(accessor));
    CollectionAssert.AreEquivalent(new BlockPos[] { new(0, 0, 0) },
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
