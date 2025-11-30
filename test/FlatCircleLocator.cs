using PrefixClassName.MsTest;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class FlatCircleLocator {
  [TestMethod]
  public void SingleChunkCircleInFirstLocation() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    Real.FlatCircleLocator locator = new(
        survey, new(center, center), GlobalConstants.ChunkSize / 2, 1, 1, 1.0);
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z, 200, 0, 0);
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, center), locator.Center);

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, center), locator.Center);
  }

  [TestMethod]
  public void InitialLocationNotEnoughLand() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    Real.FlatCircleLocator locator = new(
        survey, new(center, center), GlobalConstants.ChunkSize / 2, 1, 1, 1.0);

    // Set the initial location to height 1 so that it is not counted as land.
    reader.FillChunk(2, 2, 1, 0, 0);

    // The first attempt only has access to the chunk which fails. So the first
    // attempt will be incomplete.
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 5; ++x) {
      for (int z = 0; z < 5; ++z) {
        if (x != 2 || z != 2) {
          reader.FillChunk(x, z, 200, 0, 0);
        }
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);

    Vec2i found = locator.Center;
    Assert.IsLessThan(GlobalConstants.ChunkSize,
                      found.ManhattenDistance(center, center));

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(found, locator.Center);
  }

  [TestMethod]
  public void InitialLocationTooRough() {
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    Real.FlatCircleLocator locator = new(
        survey, new(center, center), GlobalConstants.ChunkSize / 2, 0, 0, 0);

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

    Vec2i found = locator.Center;
    Assert.IsLessThan(GlobalConstants.ChunkSize,
                      found.ManhattenDistance(center, center));

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
    Real.FlatCircleLocator locator = new(
        survey, new(center, center), GlobalConstants.ChunkSize / 2, 1, 1, 1.0);
    Assert.IsFalse(locator.Generate(null));

    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z, 200, 0, 0);
      }
    }

    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, center), locator.Center);

    // Calling Generate an extra time should not change the result.
    Assert.IsTrue(locator.Generate(null));
    Assert.IsFalse(locator.Failed);
    Assert.AreEqual(new(center, center), locator.Center);

    byte[] data = SerializerUtil.Serialize(locator);
    Real.FlatCircleLocator copy =
        SerializerUtil.Deserialize<Real.FlatCircleLocator>(data);
    copy.Restore(survey);
    Assert.AreEqual(new(center, center), locator.Center);
  }
}
