using PrefixClassName.MsTest;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class TerrainSurvey {
  [TestMethod]
  public void GetRoughCircleStatsMissingChunk() {
    MemoryTerrainHeightReader reader = new();

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    TerrainStats stats = survey.GetRoughCircleStats(
        null, new Vec2i(center, center), GlobalConstants.ChunkSize / 2,
        out int chunkCount, ref incomplete);
    Assert.IsTrue(incomplete);
    Assert.AreEqual(1, chunkCount);
    Assert.AreEqual(0, stats.SolidCount);
    Assert.AreEqual(0, stats.Roughness);
  }

  [TestMethod]
  public void GetRoughCircleStatsSingleChunk() {
    MemoryTerrainHeightReader reader = new();
    const int surroundingHeight = 50;
    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z, surroundingHeight, 0, 0);
      }
    }

    // Fill the chunk with the circle
    const int circleHeight = 200;
    reader.FillChunk(1, 1, circleHeight, 0, 0);

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    TerrainStats stats = survey.GetRoughCircleStats(
        null, new Vec2i(center, center), GlobalConstants.ChunkSize / 2,
        out int chunkCount, ref incomplete);
    Assert.IsFalse(incomplete);
    Assert.AreEqual(1, chunkCount);
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    stats.SolidCount);
    // The border between the circle's chunk and the neighboring chunks is
    // rough. Only the north and west borders are counted.
    Assert.AreEqual((circleHeight - surroundingHeight) *
                        GlobalConstants.ChunkSize * 2,
                    stats.Roughness);
  }

  [TestMethod]
  public void GetRoughCircleStats4Chunks() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 4; ++x) {
      for (int z = 0; z < 4; ++z) {
        reader.FillChunk(x, z, 200 + x * GlobalConstants.ChunkSize, 1, 0);
      }
    }

    Real.TerrainSurvey survey = new(reader);
    int center = GlobalConstants.ChunkSize * 2;
    bool incomplete = false;
    TerrainStats stats = survey.GetRoughCircleStats(
        null, new Vec2i(center, center), GlobalConstants.ChunkSize / 2,
        out int chunkCount, ref incomplete);
    Assert.IsFalse(incomplete);
    Assert.AreEqual(4, chunkCount);
    Assert.AreEqual(4 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    stats.SolidCount);
    Assert.AreEqual(4 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    stats.Roughness);
  }

  [TestMethod]
  public void GetRoughCircleStats9Chunks() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 4; ++x) {
      for (int z = 0; z < 4; ++z) {
        reader.FillChunk(x, z, 200 + x * GlobalConstants.ChunkSize, 1, 0);
      }
    }

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 2.5);
    bool incomplete = false;
    TerrainStats stats = survey.GetRoughCircleStats(
        null, new Vec2i(center, center), GlobalConstants.ChunkSize,
        out int chunkCount, ref incomplete);
    Assert.IsFalse(incomplete);
    Assert.AreEqual(9, chunkCount);
    Assert.AreEqual(9 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    stats.SolidCount);
    Assert.AreEqual(9 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    stats.Roughness);
  }

  [TestMethod]
  public void GetHeightUnloaded() {
    MemoryTerrainHeightReader reader = new();

    Real.TerrainSurvey survey = new(reader);
    Assert.AreEqual(-1, survey.GetHeight(null, GlobalConstants.ChunkSize,
                                         GlobalConstants.ChunkSize));
    Assert.IsTrue(reader.WasChunkRequested(1, 1));
    // It should have also requested the neighboring chunks to calculate the
    // roughness
    Assert.IsTrue(reader.WasChunkRequested(0, 1));
    Assert.IsTrue(reader.WasChunkRequested(1, 0));
  }

  [TestMethod]
  public void GetHeight() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 2; ++x) {
      for (int z = 0; z < 2; ++z) {
        reader.FillChunk(x, z, x * GlobalConstants.ChunkSize, 1, 0);
      }
    }

    Real.TerrainSurvey survey = new(reader);
    Assert.AreEqual(GlobalConstants.ChunkSize + 1,
                    survey.GetHeight(null, GlobalConstants.ChunkSize + 1,
                                     GlobalConstants.ChunkSize + 3));
  }

  [TestMethod]
  public void Serialization() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 2; ++x) {
      for (int z = 0; z < 2; ++z) {
        reader.FillChunk(x, z, x * GlobalConstants.ChunkSize, 1, 0);
      }
    }

    Real.TerrainSurvey survey = new(reader);
    int originalHeight = survey.GetHeight(null, GlobalConstants.ChunkSize + 1,
                                          GlobalConstants.ChunkSize + 3);

    // Restore the survey with a different reader that does not have the chunks
    // loaded. Verify that the restored survey uses the cached results.
    byte[] data = SerializerUtil.Serialize(survey);
    MemoryTerrainHeightReader reader2 = new();
    Real.TerrainSurvey copy =
        SerializerUtil.Deserialize<Real.TerrainSurvey>(data);
    copy.Restore(reader2);
    Assert.AreEqual(originalHeight,
                    copy.GetHeight(null, GlobalConstants.ChunkSize + 1,
                                   GlobalConstants.ChunkSize + 3));
  }
}
