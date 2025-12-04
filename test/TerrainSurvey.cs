using PrefixClassName.MsTest;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class TerrainSurvey {
  [TestMethod]
  public void GetDiskStatsMissingChunk() {
    MemoryTerrainHeightReader reader = new();

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    TerrainStats stats = survey.GetDiskStats(null, new Vec2i(center, center),
                                             GlobalConstants.ChunkSize / 2,
                                             out int area, ref incomplete);
    Assert.IsTrue(incomplete);
    Assert.AreEqual(0, stats.SolidCount);
    Assert.AreEqual(0, stats.Roughness);
  }

  [TestMethod]
  public void GetColumnNeighborsLoadedLater() {
    MemoryTerrainHeightReader reader = new();
    const int surroundingHeight = 50;

    // Fill the requested chunk
    const int centerHeight = 200;
    reader.FillChunk(1, 1, centerHeight, 0, 0);

    Real.TerrainSurvey survey = new(reader);
    TerrainStats stats = survey.GetColumn(null, new Vec2i(1, 1)).Stats;
    Assert.AreEqual(-1, stats.Roughness);

    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        if (x != 1 || z != 1) {
          reader.FillChunk(x, z, surroundingHeight, 0, 0);
        }
      }
    }

    stats = survey.GetColumn(null, new Vec2i(1, 1)).Stats;
    // The border between the requested chunk and the neighboring chunks is
    // rough. Only the north and west borders are counted.
    Assert.AreEqual((centerHeight - surroundingHeight) *
                        GlobalConstants.ChunkSize * 2,
                    stats.Roughness);
  }

  [TestMethod]
  public void GetDiskStatsR1ChunkCenter() {
    MemoryTerrainHeightReader reader = new();

    // Fill the chunk that holds the disk
    const int diskHeight = 200;
    reader.FillChunk(1, 1, diskHeight, 1, 1);

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    // A disk of radius 1 covers 5 blocks in this shape:
    //   x
    // x x x
    //   x
    TerrainStats stats = survey.GetDiskStats(null, new Vec2i(center, center), 1,
                                             out int area, ref incomplete);

    Assert.IsFalse(incomplete);
    Assert.AreEqual(5, stats.SolidCount);
    Assert.AreEqual(5, area);
    Assert.AreEqual(GetDiskExpectedArea(1), stats.SolidCount);
    int chunkCenter = GlobalConstants.ChunkSize / 2;
    Assert.AreEqual(5 * diskHeight + 2 * (chunkCenter - 1) + 6 * chunkCenter +
                        2 * (chunkCenter + 1),
                    stats.SumHeight);
    Assert.AreEqual(10, stats.Roughness);
  }

  [TestMethod]
  public void GetDiskStatsR1CrossSouthEastBorder() {
    MemoryTerrainHeightReader reader = new();

    // Fill the chunk that holds the disk
    const int diskHeight = 200;
    reader.FillChunk(0, 0, diskHeight, 1, 1);
    reader.FillChunk(1, 0, diskHeight + 32, 1, 1);
    reader.FillChunk(0, 1, diskHeight + 32, 1, 1);

    Real.TerrainSurvey survey = new(reader);
    bool incomplete = false;
    // A disk of radius 1 covers 5 blocks in this shape:
    //   x
    // x x x
    //   x
    TerrainStats stats = survey.GetDiskStats(null, new Vec2i(31, 31), 1,
                                             out int area, ref incomplete);

    Assert.IsFalse(incomplete);
    Assert.AreEqual(5, stats.SolidCount);
    Assert.AreEqual(5, area);
    Assert.AreEqual(5 * diskHeight + 2 * 30 + 6 * 31 + 2 * 32, stats.SumHeight);
    Assert.AreEqual(10, stats.Roughness);
  }

  [TestMethod]
  public void GetDiskStatsR1SweepSouth() {
    MemoryTerrainHeightReader reader = new();

    // Fill the chunk that holds the disk
    const int diskHeight = 200;
    for (int z = 1; z < 5; ++z) {
      reader.FillChunk(1, z, diskHeight, 0, 0);
    }

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    for (int zOffset = 0; zOffset < 2 * GlobalConstants.ChunkSize; ++zOffset) {
      bool incomplete = false;
      // A disk of radius 1 covers 5 blocks in this shape:
      //   x
      // x x x
      //   x
      TerrainStats stats =
          survey.GetDiskStats(null, new Vec2i(center, center + zOffset), 1,
                              out int area, ref incomplete);

      Assert.IsFalse(
          incomplete,
          $"The survey was unexpectedly incomplete at z={center + zOffset}");
      Assert.AreEqual(5, area);
      Assert.AreEqual(
          5, stats.SolidCount,
          $"Solid count was {stats.SolidCount} instead of 5 at zOffset={zOffset}");
      Assert.AreEqual(5 * diskHeight, stats.SumHeight);
      Assert.AreEqual(0, stats.Roughness);
    }
  }

  /// <summary>
  /// This is a slow but very safe way to calculate the disk area
  /// </summary>
  /// <param name="radius"></param>
  /// <returns></returns>
  private int GetDiskExpectedArea(int radius) {
    int count = 0;
    for (int z = 0; z <= 2 * radius; ++z) {
      for (int x = 0; x <= 2 * radius; ++x) {
        if ((z - radius) * (z - radius) + (x - radius) * (x - radius) <=
            radius * radius) {
          ++count;
        }
      }
    }
    return count;
  }

  [TestMethod]
  public void GetDiskStatsSingleChunk() {
    MemoryTerrainHeightReader reader = new();
    const int diskHeight = 50;
    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z,
                         diskHeight + x * GlobalConstants.ChunkSize +
                             z * GlobalConstants.ChunkSize,
                         1, 1);
      }
    }

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    TerrainStats stats = survey.GetDiskStats(null, new Vec2i(center, center),
                                             GlobalConstants.ChunkSize / 2,
                                             out int area, ref incomplete);
    Assert.IsFalse(incomplete);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize / 2), area);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize / 2),
                    stats.SolidCount);
    // Every block has 2 roughness.
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize / 2) * 2,
                    stats.Roughness);
  }

  [TestMethod]
  public void GetDiskStats4Chunks() {
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
    TerrainStats stats = survey.GetDiskStats(null, new Vec2i(center, center),
                                             GlobalConstants.ChunkSize / 2,
                                             out int area, ref incomplete);
    Assert.IsFalse(incomplete);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize / 2),
                    stats.SolidCount);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize / 2), area);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize / 2),
                    stats.Roughness);
  }

  [TestMethod]
  public void GetDiskStats9Chunks() {
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
    TerrainStats stats = survey.GetDiskStats(null, new Vec2i(center, center),
                                             GlobalConstants.ChunkSize,
                                             out int area, ref incomplete);
    Assert.IsFalse(incomplete);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize),
                    stats.SolidCount);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize), area);
    Assert.AreEqual(GetDiskExpectedArea(GlobalConstants.ChunkSize),
                    stats.Roughness);
  }

  [TestMethod]
  public void GetDiskStats2CRSweepDiag() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 7; ++x) {
      for (int z = 0; z < 7; ++z) {
        reader.FillChunk(x, z, 200 + x * GlobalConstants.ChunkSize, 1, 0);
      }
    }
    Real.TerrainSurvey survey = new(reader);

    for (int center = GlobalConstants.ChunkSize * 3;
         center < GlobalConstants.ChunkSize * 4; ++center) {
      bool incomplete = false;
      TerrainStats stats = survey.GetDiskStats(null, new Vec2i(center, center),
                                               2 * GlobalConstants.ChunkSize,
                                               out int area, ref incomplete);
      Assert.IsFalse(incomplete);
      Assert.AreEqual(GetDiskExpectedArea(2 * GlobalConstants.ChunkSize),
                      stats.SolidCount);
      Assert.AreEqual(GetDiskExpectedArea(2 * GlobalConstants.ChunkSize), area);
      Assert.AreEqual(GetDiskExpectedArea(2 * GlobalConstants.ChunkSize),
                      stats.Roughness);
    }
  }

  [TestMethod]
  public void GetAnnulusStatsR0_1ChunkCenter() {
    MemoryTerrainHeightReader reader = new();

    // Fill the chunk that holds the annulus
    const int diskHeight = 200;
    reader.FillChunk(1, 1, diskHeight, 1, 1);

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    // A annulus of [0,1] covers 4 blocks in this shape:
    //   x
    // x o x
    //   x
    TerrainStats stats = survey.GetAnnulusStats(
        null, new Vec2i(center, center), 0, 1, out int area, ref incomplete);

    Assert.IsFalse(incomplete);
    Assert.AreEqual(4, stats.SolidCount);
    Assert.AreEqual(4, area);
    int chunkCenter = GlobalConstants.ChunkSize / 2;
    Assert.AreEqual(4 * diskHeight + 2 * (chunkCenter - 1) + 4 * chunkCenter +
                        2 * (chunkCenter + 1),
                    stats.SumHeight);
    Assert.AreEqual(8, stats.Roughness);
  }

  [TestMethod]
  public void GetAnnulusStatsR1_2ChunkCenter() {
    MemoryTerrainHeightReader reader = new();

    // Fill the chunk that holds the annulus
    const int diskHeight = 200;
    reader.FillChunk(1, 1, diskHeight, 1, 1);

    Real.TerrainSurvey survey = new(reader);
    int center = (int)(GlobalConstants.ChunkSize * 1.5);
    bool incomplete = false;
    // A annulus of [1,2] covers 4 blocks in this shape:
    //     x
    //   x o x
    // x o o o x
    //   x o x
    //     x
    TerrainStats stats = survey.GetAnnulusStats(
        null, new Vec2i(center, center), 1, 2, out int area, ref incomplete);

    Assert.IsFalse(incomplete);
    Assert.AreEqual(8, stats.SolidCount);
    Assert.AreEqual(8, area);
    Assert.AreEqual(16, stats.Roughness);
  }

  [TestMethod]
  public void GetAnnulusStatsR1_2SweepDiag() {
    MemoryTerrainHeightReader reader = new();

    // Fill the chunk that holds the annulus
    const int diskHeight = 200;
    // Fill nearby chunks
    for (int x = 0; x < 3; ++x) {
      for (int z = 0; z < 3; ++z) {
        reader.FillChunk(x, z, diskHeight, 0, 0);
      }
    }

    Real.TerrainSurvey survey = new(reader);
    for (int i = 0; i < GlobalConstants.ChunkSize; ++i) {
      int center = (int)(GlobalConstants.ChunkSize * 1.5) + i;
      bool incomplete = false;
      // A annulus of [1,2] covers 4 blocks in this shape:
      //     x
      //   x o x
      // x o o o x
      //   x o x
      //     x
      TerrainStats stats = survey.GetAnnulusStats(
          null, new Vec2i(center, center), 1, 2, out int area, ref incomplete);

      Assert.IsFalse(incomplete);
      Assert.AreEqual(8, stats.SolidCount);
      Assert.AreEqual(8, area);
    }
  }

  [TestMethod]
  public void TraverseAnnulusLargeHoleSweepEast() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 6; ++x) {
      for (int z = 0; z < 5; ++z) {
        reader.FillChunk(x, z, 200, 0, 0);
      }
    }
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 2.5);
    int radius = GlobalConstants.ChunkSize * 2;
    int holeRadius = GlobalConstants.ChunkSize * 2 - 1;
    for (int offset = 0; offset < GlobalConstants.ChunkSize; ++offset) {
      bool incomplete = false;
      TerrainStats annulusStats = survey.GetAnnulusStats(
          null, new Vec2i(center + offset, center), holeRadius, radius,
          out int annulusArea, ref incomplete);
      Assert.IsFalse(incomplete);
      Assert.AreEqual(400, annulusArea);
      Assert.AreEqual(400, annulusStats.SolidCount);
    }
  }

  [TestMethod]
  public void TraverseAnnulusLargeSweepEast() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 6; ++x) {
      for (int z = 0; z < 5; ++z) {
        reader.FillChunk(x, z, 200, 0, 0);
      }
    }
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 2.5);
    int radius = GlobalConstants.ChunkSize * 2;
    int holeRadius = 1;
    for (int offset = 0; offset < GlobalConstants.ChunkSize; ++offset) {
      bool incomplete = false;
      TerrainStats annulusStats = survey.GetAnnulusStats(
          null, new Vec2i(center + offset, center), holeRadius, radius,
          out int annulusArea, ref incomplete);
      Assert.IsFalse(incomplete);
      Assert.AreEqual(12848, annulusArea);
      Assert.AreEqual(12848, annulusStats.SolidCount);
    }
  }

  [TestMethod]
  public void TraverseAnnulus2CRCentered() {
    MemoryTerrainHeightReader reader = new();
    // Fill nearby chunks
    for (int x = 0; x < 5; ++x) {
      for (int z = 0; z < 5; ++z) {
        reader.FillChunk(x, z, 200 + x * GlobalConstants.ChunkSize, 0, 0);
      }
    }
    Real.TerrainSurvey survey = new(reader);

    int center = (int)(GlobalConstants.ChunkSize * 2.5);
    Dictionary<int, int> areaByRadius = [];
    for (int radius = 0; radius <= GlobalConstants.ChunkSize * 2; ++radius) {
      bool incomplete = false;
      TerrainStats diskStats =
          survey.GetDiskStats(null, new Vec2i(center, center), radius,
                              out int diskArea, ref incomplete);
      Assert.IsFalse(incomplete);
      areaByRadius.Add(radius, diskArea);
      for (int holeRadius = 0; holeRadius <= radius; ++holeRadius) {
        TerrainStats annulusStats =
            survey.GetAnnulusStats(null, new Vec2i(center, center), holeRadius,
                                   radius, out int annulusArea, ref incomplete);
        Assert.IsFalse(incomplete);
        int holeArea = areaByRadius[holeRadius];
        Assert.AreEqual(diskArea - holeArea, annulusArea,
                        $"area mismatch at {holeRadius}, {radius}");
        Assert.AreEqual(diskArea - holeArea, annulusStats.SolidCount);
      }
    }
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
