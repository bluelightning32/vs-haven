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
    Real.TerrainSurvey terrain = new(reader);
    FakeChunkLoader loader = new();
    HashSet<int> removeSet = [granite.Id];

    Real.DiskPruner pruner =
        new(loader, terrain, removeSet, new Vec2i(1, 1), 0);
    Assert.IsFalse(pruner.Done);
    Assert.IsFalse(pruner.Generate(Framework.Api.World.BlockAccessor));
    Assert.IsFalse(pruner.Done);

    reader.FillChunk(0, 0, 1, 0, 0);
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(1, 2, 1, Dimensions.NormalWorld));
    IMapChunk chunk = Framework.Api.World.BlockAccessor.GetMapChunk(0, 0);
    chunk.RainHeightMap[1 * GlobalConstants.ChunkSize + 1] = 2;

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
    Real.TerrainSurvey terrain = new(reader);
    FakeChunkLoader loader = new();
    HashSet<int> removeSet = [granite.Id];

    reader.FillChunk(0, 0, 1, 0, 0);

    for (int y = 1; y <= 5; ++y) {
      Framework.Api.World.BlockAccessor.SetBlock(
          granite.Id, new BlockPos(1, y, 1, Dimensions.NormalWorld));
    }
    Framework.Api.World.BlockAccessor.SetBlock(
        andesite.Id, new BlockPos(1, 3, 1, Dimensions.NormalWorld));

    IMapChunk chunk = Framework.Api.World.BlockAccessor.GetMapChunk(0, 0);
    chunk.RainHeightMap[1 * GlobalConstants.ChunkSize + 1] = 5;

    Real.DiskPruner pruner =
        new(loader, terrain, removeSet, new Vec2i(1, 1), 0);
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
    Real.TerrainSurvey terrain = new(reader);
    FakeChunkLoader loader = new();
    HashSet<int> removeSet = [granite.Id];

    reader.FillChunk(0, 0, 1, 0, 0);

    IMapChunk chunk = Framework.Api.World.BlockAccessor.GetMapChunk(0, 0);
    for (int z = 0; z < 5; ++z) {
      for (int x = 0; x < 5; ++x) {
        Framework.Api.World.BlockAccessor.SetBlock(
            granite.Id, new BlockPos(x, 2, z, Dimensions.NormalWorld));
        chunk.RainHeightMap[z * GlobalConstants.ChunkSize + x] = 2;
      }
    }

    Real.DiskPruner pruner =
        new(loader, terrain, removeSet, new Vec2i(2, 2), 0);
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
    chunk.RainHeightMap[2 * GlobalConstants.ChunkSize + 2] = 2;

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
    HashSet<int> removeSet = [granite.Id];
    reader.FillChunk(0, 0, 1, 0, 0);

    Real.DiskPruner pruner =
        new(loader, terrain, removeSet, new Vec2i(1, 1), 0);
    Framework.Api.World.BlockAccessor.SetBlock(
        granite.Id, new BlockPos(1, 2, 1, Dimensions.NormalWorld));
    IMapChunk chunk = Framework.Api.World.BlockAccessor.GetMapChunk(0, 0);
    chunk.RainHeightMap[1 * GlobalConstants.ChunkSize + 1] = 2;

    byte[] data = SerializerUtil.Serialize(pruner);
    Real.DiskPruner copy = SerializerUtil.Deserialize<Real.DiskPruner>(data);
    copy.Restore(loader, terrain, removeSet);
    Assert.IsFalse(pruner.Done);
    Assert.IsFalse(copy.Done);

    Assert.IsTrue(pruner.Generate(Framework.Api.World.BlockAccessor));

    data = SerializerUtil.Serialize(pruner);
    copy = SerializerUtil.Deserialize<Real.DiskPruner>(data);
    copy.Restore(loader, terrain, removeSet);
    Assert.IsTrue(pruner.Done);
    Assert.IsTrue(copy.Done);
  }
}
