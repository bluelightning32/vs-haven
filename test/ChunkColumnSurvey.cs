using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ChunkColumnSurvey {
  [TestMethod]
  public void NorthNeighborMissing() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100, 0, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, 100, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.IsNotNull(survey);
    Assert.AreEqual(-1, survey.Stats.Roughness);
    Assert.IsTrue(reader.WasChunkRequested(1, 0));
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.Stats.SolidCount);

    Real.ChunkColumnSurvey west =
        Real.ChunkColumnSurvey.Create(null, reader, 0, 1, null, null);
    survey.CalculateRoughness(west, null);
    Assert.AreEqual(-1, survey.Stats.Roughness);

    // Fill north chunk
    reader.FillChunk(1, 0, 100, 0, 0);
    Real.ChunkColumnSurvey north =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 0, null, null);
    survey.CalculateRoughness(west, north);
    Assert.AreEqual(0, survey.Stats.Roughness);
  }

  [TestMethod]
  public void WestNeighborMissing() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100, 1, 1);
    // Fill north chunk
    reader.FillChunk(1, 0, 100, 1, 1);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.IsNotNull(survey);
    Assert.AreEqual(-1, survey.Stats.Roughness);
    Assert.IsTrue(reader.WasChunkRequested(0, 1));
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.Stats.SolidCount);
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
    reader.FillChunk(1, 1, 100, 0, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, 100, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, 100, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(0, survey.Stats.Roughness);
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.Stats.SolidCount);
  }

  [TestMethod]
  public void XSlope1() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100 - 1, 1, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, 100 - 2, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, 100 - 1, 1, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.Stats.Roughness);
  }

  [TestMethod]
  public void XSlopeNeg2() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100 - 1, -2, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, 100 + 1, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, 100 - 1, -2, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(2 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.Stats.Roughness);
  }

  [TestMethod]
  public void ZSlope2() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100 - 2, 0, 2);
    // Fill west chunk
    reader.FillChunk(0, 1, 100 - 2, 0, 2);
    // Fill north chunk
    reader.FillChunk(1, 0, 100 - 4, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(2 * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize,
                    survey.Stats.Roughness);
  }

  [TestMethod]
  public void OneUnsolidBlock() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100, 0, 2);
    // Fill west chunk
    reader.FillChunk(0, 1, 100, 0, 2);
    // Fill north chunk
    reader.FillChunk(1, 0, 100, 0, 0);
    reader.SetSolid(GlobalConstants.ChunkSize, GlobalConstants.ChunkSize,
                    false);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize - 1,
                    survey.Stats.SolidCount);
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

  [TestMethod]
  public void SumHeight() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 200, 0, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);

    Assert.AreEqual(GlobalConstants.ChunkSize * GlobalConstants.ChunkSize * 200,
                    survey.Stats.SumHeight);
  }

  [TestMethod]
  public void Serialization() {
    MemoryTerrainHeightReader reader = new();
    // Fill requested chunk
    reader.FillChunk(1, 1, 100 - 1, 1, 0);
    // Fill west chunk
    reader.FillChunk(0, 1, 100 - 2, 0, 0);
    // Fill north chunk
    reader.FillChunk(1, 0, 100 - 1, 1, 0);
    Real.ChunkColumnSurvey survey =
        Real.ChunkColumnSurvey.Create(null, reader, 1, 1, null, null);
    byte[] data = SerializerUtil.Serialize(survey);
    Real.ChunkColumnSurvey copy =
        SerializerUtil.Deserialize<Real.ChunkColumnSurvey>(data);

    Assert.AreEqual(survey.Stats.Roughness, copy.Stats.Roughness);
    Assert.AreEqual(survey.Stats.SolidCount, copy.Stats.SolidCount);
    Assert.AreEqual(survey.Stats.SumHeight, copy.Stats.SumHeight);
    Assert.AreEqual(survey.GetHeight(3, 5), copy.GetHeight(3, 5));
  }
}
