using PrefixClassName.MsTest;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class FlatDiskLocator {
  [TestMethod]
  public void SingleChunkDiskInFirstLocation() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    Real.FlatDiskLocator locator =
        new(Framework.Api.Logger, survey, new(center, center),
            GlobalConstants.ChunkSize / 2, 1, 1, 1.0);
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z, 200, 0, 0);
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, 200, center), locator.Center);

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, 200, center), locator.Center);
  }

  [TestMethod]
  public void InitialLocationNotEnoughLand() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 2.5);
    Real.FlatDiskLocator locator =
        new(Framework.Api.Logger, survey, new(center, center),
            GlobalConstants.ChunkSize / 2, 1, 1, 1.0);

    // Make the initial chunk not count as land so that the locator has to
    // search other locations.
    reader.FillChunk(2, 2, 200, 0, 0, false);

    // The first attempt only has access to the chunk which fails. So the first
    // attempt will be incomplete.
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 5; ++x) {
      for (int z = 0; z < 5; ++z) {
        if (x != 2 || z != 2) {
          reader.FillChunk(x, z, 200, 0, 0, true);
        }
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);

    Assert.AreNotEqual(new Vec2i(center, center), locator.Center2D);
    Assert.IsLessThan(1.5 * GlobalConstants.ChunkSize,
                      locator.Center2D.ManhattenDistance(center, center));
    BlockPos found = locator.Center;

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(found, locator.Center);
  }

  [TestMethod]
  public void InitialLocationTooRough() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 2.5);
    Real.FlatDiskLocator locator =
        new(Framework.Api.Logger, survey, new(center, center),
            GlobalConstants.ChunkSize / 2, 0, 0, 0);

    // Give the initial location a slope so that it is too rough.
    reader.FillChunk(2, 2, 1, 1, 1);

    // The first attempt only has access to the chunk which fails. So the first
    // attempt will be incomplete.
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 5; ++x) {
      for (int z = 0; z < 5; ++z) {
        if (x != 2 || z != 2) {
          reader.FillChunk(x, z, 0, 0, 0);
        }
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);

    Assert.AreNotEqual(new Vec2i(center, center), locator.Center2D);
    Assert.IsLessThan(3 * GlobalConstants.ChunkSize,
                      locator.Center2D.ManhattenDistance(center, center));
    BlockPos found = locator.Center;

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(found, locator.Center);
  }

  [TestMethod]
  public void Serialization() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    Real.FlatDiskLocator locator =
        new(Framework.Api.Logger, survey, new(center, center),
            GlobalConstants.ChunkSize / 2, 1, 1, 1.0);
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z, 200, 0, 0);
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, 200, center), locator.Center);

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, 200, center), locator.Center);

    byte[] data = SerializerUtil.Serialize(locator);
    Real.FlatDiskLocator copy =
        SerializerUtil.Deserialize<Real.FlatDiskLocator>(data);
    copy.Restore(Framework.Api.Logger, survey);
    Assert.AreEqual(new(center, 200, center), locator.Center);
  }
}
