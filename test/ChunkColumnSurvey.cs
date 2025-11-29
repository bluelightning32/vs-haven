using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ChunkColumnSurvey {
  [TestMethod]
  public void NorthNeighborMissing() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100, 1, 1);
    // Fill west chunk
    reader.FillChunk(0, 1, 100, 1, 1);
    Assert.IsNull(
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null));
    Assert.IsTrue(reader.WasChunkRequested(1, 0));
  }

  [TestMethod]
  public void WestNeighborMissing() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100, 1, 1);
    // Fill north chunk
    reader.FillChunk(1, 0, 100, 1, 1);
    Assert.IsNull(
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null));
    Assert.IsTrue(reader.WasChunkRequested(0, 1));
  }

  [TestMethod]
  public void RequestedChunkMissing() {
    MemoryTerrainHeightReader reader = new();
    // Fill west chunk
    reader.FillChunk(0, 1, 100, 1, 1);
    // Fill north chunk
    reader.FillChunk(1, 0, 100, 1, 1);
    Assert.IsNull(
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null));
    Assert.IsTrue(reader.WasChunkRequested(1, 1));
  }

  [TestMethod]
  public void AllMissing() {
    MemoryTerrainHeightReader reader = new();
    Assert.IsNull(
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null));
    Assert.IsTrue(reader.WasChunkRequested(1, 0));
    Assert.IsTrue(reader.WasChunkRequested(0, 1));
    Assert.IsTrue(reader.WasChunkRequested(1, 1));
  }

  [TestMethod]
  public void AllFlat() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, Climate.Sealevel, 0, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, Climate.Sealevel, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, Climate.Sealevel, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(0, survey.TotalRoughness);
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.TotalAboveSea);
  }

  [TestMethod]
  public void XSlope1() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, Climate.Sealevel - 1, 1, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, Climate.Sealevel - 2, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, Climate.Sealevel - 1, 1, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.TotalRoughness);
    Assert.AreEqual((GlobalConstants.ChunkSize - 1) * GlobalConstants.ChunkSize,
                    survey.TotalAboveSea);
  }

  [TestMethod]
  public void XSlopeNeg2() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, Climate.Sealevel - 1, -2, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, Climate.Sealevel + 1, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, Climate.Sealevel - 1, -2, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(2 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.TotalRoughness);
    Assert.AreEqual(0, survey.TotalAboveSea);
  }

  [TestMethod]
  public void ZSlope2() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, Climate.Sealevel - 2, 0, 2);
    // Fill west chunk
    reader.FillChunk(0, 1, Climate.Sealevel - 2, 0, 2);
    // Fill north chunk
    reader.FillChunk(1, 0, Climate.Sealevel - 4, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(2 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.TotalRoughness);
    Assert.AreEqual((GlobalConstants.ChunkSize - 1) * GlobalConstants.ChunkSize,
                    survey.TotalAboveSea);
  }

  [TestMethod]
  public void GetHeight() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 200, 1, 2);
    // Fill west chunk
    reader.FillChunk(0, 1, 100, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, 100, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);

    Assert.AreEqual(200 + 0 * 1 + 0 * 2, survey.GetHeight(0, 0));
    Assert.AreEqual(200 + 5 * 1 + 7 * 2, survey.GetHeight(5, 7));
  }
}
