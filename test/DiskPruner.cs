using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class DiskPruner {
  [TestMethod]
  public void MissingChunk() {
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));
    MemoryTerrainHeightReader reader = new();
    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories =
        new() { { granite.Id, TerrainCategory.Clear } };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 100);
    Real.TerrainSurvey terrain = new(pruneConfig);

    Real.DiskPruner pruner =
        new(loader, terrain, pruneConfig, new Vec2i(1, 1), 0);
    Assert.IsFalse(pruner.Done);
    Assert.IsFalse(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsFalse(pruner.Done);

    // Ensure the chunk is loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    IServerMapChunk chunk = Framework.Api.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    reader.FillChunk(0, 0, 2, 0, 0);
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(1, 2, 1, Dimensions.NormalWorld));

    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Done);

    Assert.AreEqual(0, Framework.Api.World.BlockAccessor.GetBlockId(
                           new BlockPos(1, 2, 1, Dimensions.NormalWorld)));
  }

  [TestMethod]
  public void SkipsIgnoredBlocks() {
    Block andesite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-andesite"));
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));
    MemoryTerrainHeightReader reader = new();
    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories = new() {
      { granite.Id, TerrainCategory.Clear },
      { andesite.Id, TerrainCategory.Skip },
    };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 100);
    Real.TerrainSurvey terrain = new(pruneConfig);

    reader.FillChunk(0, 0, 5, 0, 0);

    for (int y = 1; y <= 5; ++y) {
      Framework.Api.World.BlockAccessor.SetBlock(
          granite.Id, new BlockPos(1, y, 1, Dimensions.NormalWorld));
    }
    Framework.Api.World.BlockAccessor.SetBlock(
        andesite.Id, new BlockPos(1, 3, 1, Dimensions.NormalWorld));

    Real.DiskPruner pruner =
        new(loader, terrain, pruneConfig, new Vec2i(1, 1), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Done);

    for (int y = 2; y <= 5; ++y) {
      int expected = y == 3 ? andesite.Id : 0;
      Assert.AreEqual(
          expected, Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(1, y, 1, Dimensions.NormalWorld))
                        .Id);
    }
  }

  [TestMethod]
  public void ExpandKeepsFinishedColumns() {
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));
    MemoryTerrainHeightReader reader = new();
    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories =
        new() { { granite.Id, TerrainCategory.Clear } };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 100);
    Real.TerrainSurvey terrain = new(pruneConfig);

    reader.FillChunk(0, 0, 2, 0, 0);

    IMapChunk chunk = Framework.Api.World.BlockAccessor.GetMapChunk(0, 0);
    for (int z = 0; z < 5; ++z) {
      for (int x = 0; x < 5; ++x) {
        Framework.Api.World.BlockAccessor.SetBlock(
            granite.Id, new BlockPos(x, 2, z, Dimensions.NormalWorld));
      }
    }

    Real.DiskPruner pruner =
        new(loader, terrain, pruneConfig, new Vec2i(2, 2), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Done);

    Assert.AreEqual(0,
                    Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(2, 2, 2, Dimensions.NormalWorld))
                        .Id);
    Assert.AreEqual(granite.Id,
                    Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(1, 2, 2, Dimensions.NormalWorld))
                        .Id);

    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(2, 2, 2, Dimensions.NormalWorld));
    reader.SetHeight(2, 2, 2);

    pruner.Expand(1);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Done);

    Assert.AreEqual(granite.Id,
                    Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(2, 2, 2, Dimensions.NormalWorld))
                        .Id);
    Assert.AreEqual(0,
                    Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(1, 2, 2, Dimensions.NormalWorld))
                        .Id);
  }

  [TestMethod]
  public void Serialization() {
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey terrain = new(reader);
    FakeChunkLoader loader = new();
    Dictionary<int, TerrainCategory> terrainCategories =
        new() { { granite.Id, TerrainCategory.Clear } };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 100);
    reader.FillChunk(0, 0, 1, 0, 0);

    Real.DiskPruner pruner =
        new(loader, terrain, pruneConfig, new Vec2i(1, 1), 0);
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(1, 2, 1, Dimensions.NormalWorld));
    reader.SetHeight(1, 1, 2);

    byte[] data = SerializerUtil.Serialize(pruner);
    Real.DiskPruner copy = SerializerUtil.Deserialize<Real.DiskPruner>(data);
    copy.Restore(loader, terrain, pruneConfig);
    Assert.IsFalse(pruner.Done);
    Assert.IsFalse(copy.Done);

    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));

    data = SerializerUtil.Serialize(pruner);
    copy = SerializerUtil.Deserialize<Real.DiskPruner>(data);
    copy.Restore(loader, terrain, pruneConfig);
    Assert.IsTrue(pruner.Done);
    Assert.IsTrue(copy.Done);
  }
}
