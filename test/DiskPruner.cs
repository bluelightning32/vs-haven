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

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(1, 1), 0);
    Assert.IsFalse(pruner.GenerateDone);
    Assert.IsFalse(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsFalse(pruner.GenerateDone);

    // Ensure the chunk is loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    IServerMapChunk chunk = Framework.Api.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    reader.FillChunk(0, 0, 2, 0, 0);
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(1, 2, 1, Dimensions.NormalWorld));

    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.GenerateDone);

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
        new(reader, terrainCategories, 0);
    Real.TerrainSurvey terrain = new(pruneConfig);

    reader.FillChunk(0, 0, 5, 0, 0);

    for (int y = 1; y <= 5; ++y) {
      Framework.Api.World.BlockAccessor.SetBlock(
          granite.Id, new BlockPos(1, y, 1, Dimensions.NormalWorld));
    }
    Framework.Api.World.BlockAccessor.SetBlock(
        andesite.Id, new BlockPos(1, 3, 1, Dimensions.NormalWorld));

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(1, 1), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.GenerateDone);

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

    for (int z = 0; z < 5; ++z) {
      for (int x = 0; x < 5; ++x) {
        Framework.Api.World.BlockAccessor.SetBlock(
            granite.Id, new BlockPos(x, 2, z, Dimensions.NormalWorld));
      }
    }

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(2, 2), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.GenerateDone);

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
    Assert.IsTrue(pruner.GenerateDone);

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
  public void RaiseStartAtSurface() {
    Block soil = Framework.Api.World.GetBlock(
        new AssetLocation("game:soil-medium-normal"));
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));

    FakeChunkLoader loader = new();
    Real.TerrainHeightReader reader = new(loader, false);
    Dictionary<int, TerrainCategory> terrainCategories = new() {
      { granite.Id, TerrainCategory.RaiseStart },
      { soil.Id, TerrainCategory.Solid },
    };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 2);
    Real.TerrainSurvey terrain = new(pruneConfig);

    // Ensure the chunk is loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    IServerMapChunk chunk = Framework.Api.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    Framework.Api.World.BlockAccessor.SetBlock(
        soil.Id, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        soil.Id, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(0, 2, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 2;

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(0, 0), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Commit(Framework.Api.World.BlockAccessor));

    for (int y = 0; y <= 4; ++y) {
      int expected = y <= 1 ? soil.Id : granite.Id;
      Assert.AreEqual(
          expected, Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(0, y, 0, Dimensions.NormalWorld))
                        .Id);
    }
  }

  [TestMethod]
  public void RaiseStartBelowSurface() {
    Block soil = Framework.Api.World.GetBlock(
        new AssetLocation("game:soil-medium-normal"));
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));

    FakeChunkLoader loader = new();
    Real.TerrainHeightReader reader = new(loader, false);
    Dictionary<int, TerrainCategory> terrainCategories = new() {
      { granite.Id, TerrainCategory.RaiseStart },
      { soil.Id, TerrainCategory.Solid },
    };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 2);
    Real.TerrainSurvey terrain = new(pruneConfig);

    // Ensure the chunk is loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    IServerMapChunk chunk = Framework.Api.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    Framework.Api.World.BlockAccessor.SetBlock(
        soil.Id, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        soil.Id, new BlockPos(0, 2, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 2;

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(0, 0), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Commit(Framework.Api.World.BlockAccessor));

    for (int y = 0; y <= 4; ++y) {
      int expected;
      if (y == 0) {
        expected = soil.Id;
      } else if (y <= 1 + 2) {
        expected = granite.Id;
      } else {
        expected = soil.Id;
      }
      Assert.AreEqual(
          expected, Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(0, y, 0, Dimensions.NormalWorld))
                        .Id);
    }
  }

  [TestMethod]
  public void RaiseVegetationAboveSurface() {
    Block grass = Framework.Api.World.GetBlock(
        new AssetLocation("game:tallgrass-short-free"));
    Block soil = Framework.Api.World.GetBlock(
        new AssetLocation("game:soil-medium-normal"));
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));

    FakeChunkLoader loader = new();
    Real.TerrainHeightReader reader = new(loader, false);
    Dictionary<int, TerrainCategory> terrainCategories = new() {
      { grass.Id, TerrainCategory.Skip },
      { granite.Id, TerrainCategory.RaiseStart },
      { soil.Id, TerrainCategory.Solid },
    };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 2);
    Real.TerrainSurvey terrain = new(pruneConfig);

    // Ensure the chunk is loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    IServerMapChunk chunk = Framework.Api.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    Framework.Api.World.BlockAccessor.SetBlock(
        soil.Id, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        soil.Id, new BlockPos(0, 2, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        grass.Id, new BlockPos(0, 3, 0, Dimensions.NormalWorld));
    // Grass does not block the rain
    chunk.RainHeightMap[0] = 2;

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(0, 0), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Commit(Framework.Api.World.BlockAccessor));

    for (int y = 0; y <= 4; ++y) {
      int expected;
      if (y == 0) {
        expected = soil.Id;
      } else if (y <= 1 + 2) {
        expected = granite.Id;
      } else {
        expected = soil.Id;
      }
      Assert.AreEqual(
          expected, Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(0, y, 0, Dimensions.NormalWorld))
                        .Id);
    }
    Assert.AreEqual(grass.Id,
                    Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(0, 5, 0, Dimensions.NormalWorld))
                        .Id);
  }

  [TestMethod]
  public void RaiseBlockEntity() {
    Block cranberryDispenser = Framework.Api.World.GetBlock(
        new AssetLocation("haven:berrybush-cranberry"));
    Block granite =
        Framework.Api.World.GetBlock(new AssetLocation("game:rock-granite"));

    FakeChunkLoader loader = new();
    Real.TerrainHeightReader reader = new(loader, false);
    Dictionary<int, TerrainCategory> terrainCategories = new() {
      { cranberryDispenser.Id, TerrainCategory.Nonsolid },
      { granite.Id, TerrainCategory.RaiseStart },
    };
    Real.PrunedTerrainHeightReader pruneConfig =
        new(reader, terrainCategories, 2);
    Real.TerrainSurvey terrain = new(pruneConfig);

    // Ensure the chunk is loaded.
    Framework.Api.WorldManager.LoadChunkColumnPriority(0, 0);
    Framework.Server.LoadChunksInline();
    IServerMapChunk chunk = Framework.Api.WorldManager.GetMapChunk(0, 0);
    Assert.IsNotNull(chunk);

    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(0, 0, 0, Dimensions.NormalWorld));
    Framework.Api.World.BlockAccessor.SetBlock(
        cranberryDispenser.Id, new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    chunk.RainHeightMap[0] = 1;

    BlockEntityBehaviors.Dispenser dispenser =
        cranberryDispenser.GetBEBehavior<BlockEntityBehaviors.Dispenser>(
            new BlockPos(0, 1, 0, Dimensions.NormalWorld));
    dispenser.SetRenewalHours("fake", 130);

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(0, 0), 0);
    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsTrue(pruner.Commit(Framework.Api.World.BlockAccessor));

    for (int y = 0; y <= 2; ++y) {
      Assert.AreEqual(granite.Id, Framework.Api.World.BlockAccessor
                                      .GetBlock(new BlockPos(
                                          0, 1, 0, Dimensions.NormalWorld))
                                      .Id);
    }
    Assert.AreEqual(cranberryDispenser.Id,
                    Framework.Api.World.BlockAccessor
                        .GetBlock(new BlockPos(0, 3, 0, Dimensions.NormalWorld))
                        .Id);
    dispenser =
        cranberryDispenser.GetBEBehavior<BlockEntityBehaviors.Dispenser>(
            new BlockPos(0, 3, 0, Dimensions.NormalWorld));
    Assert.AreEqual(130, dispenser.GetRenewalHours("fake"));
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

    Real.DiskPruner pruner = new(Framework.Api.World, loader, terrain,
                                 pruneConfig, new Vec2i(1, 1), 0);
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(1, 2, 1, Dimensions.NormalWorld));
    reader.SetHeight(1, 1, 2);

    byte[] data = SerializerUtil.Serialize(pruner);
    Real.DiskPruner copy = SerializerUtil.Deserialize<Real.DiskPruner>(data);
    copy.Restore(Framework.Api.World, loader, terrain, pruneConfig);
    Assert.IsFalse(pruner.GenerateDone);
    Assert.IsFalse(copy.GenerateDone);

    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));

    data = SerializerUtil.Serialize(pruner);
    copy = SerializerUtil.Deserialize<Real.DiskPruner>(data);
    copy.Restore(Framework.Api.World, loader, terrain, pruneConfig);
    Assert.IsTrue(pruner.GenerateDone);
    Assert.IsTrue(copy.GenerateDone);
  }
}
