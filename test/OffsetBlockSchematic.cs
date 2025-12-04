using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class OffsetBlockSchematic {
  public static Real.Structure Load(string name) {
    AssetLocation location = AssetLocation.Create(name, "haven");
    location.WithPathPrefixOnce("worldgen/havenstructures/")
        .WithPathAppendixOnce(".json");
    return Framework.Server.AssetManager.Get<Real.Structure>(location);
  }

  public static Real.OffsetBlockSchematic
  CreateGraniteBox(int sx, int sy, int sz, int offsetY) {
    Block granite =
        Framework.Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    BlockPos pos = new(Dimensions.NormalWorld);
    for (int z = 100; z < 100 + sz; ++z) {
      pos.Z = z;
      for (int y = 100; y < 100 + sy; ++y) {
        pos.Y = y;
        for (int x = 100; x < 100 + sx; ++x) {
          pos.X = x;
          if (Framework.Server.World.BlockAccessor.GetChunkAtBlockPos(pos) ==
              null) {
            Framework.Api.WorldManager.LoadChunkColumnPriority(
                pos.X / GlobalConstants.ChunkSize,
                pos.Z / GlobalConstants.ChunkSize);
            Framework.Server.LoadChunksInline();
          }
          Framework.Server.World.BlockAccessor.SetBlock(granite.Id, pos);
          Assert.AreEqual(granite.Id,
                          Framework.Server.World.BlockAccessor.GetBlockId(pos));
        }
      }
    }
    BlockSchematic schematic =
        new(Framework.Server, new BlockPos(100, 100, 100),
            new BlockPos(100 + sx, 100 + sy, 100 + sz), false);
    string s = JsonUtil.ToString(schematic);
    Real.OffsetBlockSchematic result =
        JsonUtil.FromString<Real.OffsetBlockSchematic>(s);
    result.OffsetY = offsetY;
    result.UpdateOutline();
    return result;
  }

  /// <summary>
  /// Creates a shape with a flat top and a 1x1 spindle below it. The shape
  /// looks a T from the side.
  /// </summary>
  /// <param name="spindleHeight"></param>
  /// <param name="radius">the number of blocks (measured in manhattan distance)
  /// in each direction beyond the spindle. A value of 0 creates a 1x1
  /// top.</param> <param name="topHeight"></param> <param
  /// name="offsetY"></param> <returns></returns>
  public static Real.OffsetBlockSchematic
  CreateGraniteTop(int radius, int spindleHeight, int topHeight, int offsetY) {
    Block granite =
        Framework.Server.World.GetBlock(new AssetLocation("game:rock-granite"));
    BlockPos pos = new(Dimensions.NormalWorld);
    // Create the top.
    for (int z = 0; z <= 1 + 2 * radius; ++z) {
      pos.Z = 100 + z;
      for (int y = 0; y < spindleHeight + topHeight; ++y) {
        pos.Y = 100 + y;
        for (int x = 0; x <= 1 + 2 * radius; ++x) {
          pos.X = 100 + x;
          if (Framework.Server.World.BlockAccessor.GetChunkAtBlockPos(pos) ==
              null) {
            Framework.Api.WorldManager.LoadChunkColumnPriority(
                pos.X / GlobalConstants.ChunkSize,
                pos.Z / GlobalConstants.ChunkSize);
            Framework.Server.LoadChunksInline();
          }
          int blockId;
          if (y < spindleHeight) {
            if (x == radius && z == radius) {
              // This part of the spindle.
              blockId = granite.Id;
            } else {
              // This is the air surrounding the spindle. It needs to be cleared
              // in case another unit test left it filled in.
              blockId = 0;
            }
          } else {
            // This is part of the top.
            blockId = granite.Id;
          }
          Framework.Server.World.BlockAccessor.SetBlock(blockId, pos);
          Assert.AreEqual(blockId,
                          Framework.Server.World.BlockAccessor.GetBlockId(pos));
        }
      }
    }
    BlockSchematic schematic =
        new(Framework.Server, new BlockPos(100, 100, 100),
            new BlockPos(101 + 2 * radius, 100 + spindleHeight + topHeight,
                         101 + 2 * radius),
            false);
    string s = JsonUtil.ToString(schematic);
    Real.OffsetBlockSchematic result =
        JsonUtil.FromString<Real.OffsetBlockSchematic>(s);
    result.OffsetY = offsetY;
    result.UpdateOutline();
    return result;
  }

  [TestMethod]
  public void Intersects() {
    Real.Structure stone = Load("stone");
    NormalRandom rand = new(0);
    Real.OffsetBlockSchematic schematic =
        stone.Select(Framework.Server.AssetManager, rand).First();
    BlockPos start = new(0, 0, 0);
    Assert.IsNull(
        schematic.Intersects(start, schematic, new BlockPos(1000, 0, 0)));
    Cuboidi cube = schematic.Intersects(start, schematic, start);
    Assert.IsNotNull(cube);
  }

  [TestMethod]
  public void AvoidIntersection() {
    Real.Structure stone = Load("stone");
    NormalRandom rand = new(0);
    Real.OffsetBlockSchematic schematic =
        stone.Select(Framework.Server.AssetManager, rand).First();

    BlockPos start = new(0, 0, 0);
    BlockPos otherStart = new(0, 0, 0);
    Assert.IsFalse(schematic.AvoidIntersection(start, schematic, otherStart,
                                               new Vec3d(0, 1, 0)));
    Assert.IsGreaterThan(otherStart.Y, start.Y);
    Assert.AreEqual(otherStart.X, start.X);
    Assert.AreEqual(otherStart.Z, start.Z);

    Assert.IsNull(schematic.Intersects(start, schematic, otherStart));
  }

  [TestMethod]
  public void AutoConfigureProbesCornersAndMid() {
    int sideLen = Real.OffsetBlockSchematic.AutoProbeSpacing + 2;
    Real.OffsetBlockSchematic box = CreateGraniteBox(sideLen, 1, sideLen, 0);
    box.AutoConfigureProbes();
    (int, int)[] expectedPositions = [
      new(0, 0),
      new(sideLen - 1, 0),
      new(0, sideLen - 1),
      new(sideLen - 1, sideLen - 1),
      new(Real.OffsetBlockSchematic.AutoProbeSpacing, 0),
      new(0, Real.OffsetBlockSchematic.AutoProbeSpacing),
      new(Real.OffsetBlockSchematic.AutoProbeSpacing, sideLen - 1),
      new(sideLen - 1, Real.OffsetBlockSchematic.AutoProbeSpacing),
    ];
    List<TerrainProbe> expected = [];
    foreach ((int x, int z) in expectedPositions) {
      expected.Add(new() {
        X = x,
        Z = z,
        YMin = -1,
        YMax = 1,
      });
    }
    CollectionAssert.AreEquivalent(expected, box.Probes);
  }

  [TestMethod]
  public void AutoConfigureProbesNeg1YOffset() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, -1);
    box.AutoConfigureProbes();
    CollectionAssert.AreEquivalent(
        new TerrainProbe[] { new() { X = 0, Z = 0, YMin = -2, YMax = 0 } },
        box.Probes);
  }

  [TestMethod]
  public void AutoConfigureProbesTallNeg2YOffset() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 3, 1, -2);
    box.AutoConfigureProbes();
    CollectionAssert.AreEquivalent(
        new TerrainProbe[] { new() { X = 0, Z = 0, YMin = -3, YMax = 1 } },
        box.Probes);
  }

  [TestMethod]
  public void AutoConfigureProbesPos2YOffset() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, 2);
    box.AutoConfigureProbes();
    CollectionAssert.AreEquivalent(
        new TerrainProbe[] { new() { X = 0, Z = 0, YMin = -1, YMax = 0 } },
        box.Probes);
  }

  [TestMethod]
  public void AutoConfigureProbesOnlySurface0YOffset() {
    Real.OffsetBlockSchematic top = CreateGraniteTop(1, 1, 1, 0);
    top.AutoConfigureProbes();
    CollectionAssert.AreEquivalent(
        new TerrainProbe[] { new() { X = 1, Z = 1, YMin = -1, YMax = 2 } },
        top.Probes);
  }

  [TestMethod]
  public void AutoConfigureProbesOnlySurfaceNeg1YOffset() {
    Real.OffsetBlockSchematic top = CreateGraniteTop(1, 1, 1, -1);
    top.AutoConfigureProbes();
    (int, int)[] expectedPositions = [
      new(0, 0),
      new(2, 0),
      new(0, 2),
      new(2, 2),
    ];
    List<TerrainProbe> expected = [];
    foreach ((int x, int z) in expectedPositions) {
      expected.Add(new() {
        X = x,
        Z = z,
        YMin = -2,
        YMax = 1,
      });
    }
    CollectionAssert.AreEquivalent(expected, top.Probes);
  }

  [TestMethod]
  public void ProbeTerrainUnloadedChunk() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, 0);
    box.AutoConfigureProbes();
    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);

    Assert.AreEqual(
        -1, box.ProbeTerrain(
                survey, null,
                new(Framework.UnloadedMapChunkX * GlobalConstants.ChunkSize,
                    Framework.UnloadedMapChunkZ * GlobalConstants.ChunkSize)));
  }

  [TestMethod]
  public void ProbeTerrainAverageTwoHeights() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, 0);
    box.Probes = [
      new() {
        X = 0,
        Z = 0,
        YMin = -100,
        YMax = 100,
      },
      new() {
        X = 1,
        Z = 0,
        YMin = -100,
        YMax = 100,
      }
    ];

    MemoryTerrainHeightReader reader = new();
    reader.SetHeight(0, 0, 2);
    reader.SetHeight(1, 0, 4);
    Real.TerrainSurvey survey = new(reader);
    // The terrain is easily within the probe's allowed range. So the preferred
    // height should be an average of the surface height at both locations, plus
    // 1 so that the schematic is placed on top of the surface.
    Assert.AreEqual(4, box.ProbeTerrain(survey, null, new(0, 0)));
  }

  [TestMethod]
  public void ProbeTerrainRestrictiveProbeWinsHigh() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, 0);
    box.Probes = [
      new() {
        X = 0,
        Z = 0,
        YMin = -100,
        YMax = 100,
      },
      new() {
        X = 1,
        Z = 0,
        YMin = 0,
        YMax = 1,
      }
    ];

    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);
    // This probe easily passes.
    reader.SetHeight(0, 0, 2);
    // This probe has no margin of error. So it should dictate the final result.
    reader.SetHeight(1, 0, 4);
    Assert.AreEqual(4, box.ProbeTerrain(survey, null, new(0, 0)));
  }

  [TestMethod]
  public void ProbeTerrainRestrictiveProbeWinsLow() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, 0);
    box.Probes = [
      new() {
        X = 0,
        Z = 0,
        YMin = -100,
        YMax = 100,
      },
      new() {
        X = 1,
        Z = 0,
        YMin = 0,
        YMax = 1,
      }
    ];

    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);
    // This probe easily passes.
    reader.SetHeight(0, 0, 4);
    // This probe has no margin of error. So it should dictate the final result.
    reader.SetHeight(1, 0, 2);
    Assert.AreEqual(2, box.ProbeTerrain(survey, null, new(0, 0)));
  }

  [TestMethod]
  public void ProbeTerrainFailure() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, 0);
    box.Probes = [
      new() {
        X = 0,
        Z = 0,
        YMin = 0,
        YMax = 1,
      },
      new() {
        X = 1,
        Z = 1,
        YMin = 0,
        YMax = 1,
      }
    ];

    MemoryTerrainHeightReader reader = new();
    Real.TerrainSurvey survey = new(reader);
    // This probe easily passes.
    reader.SetHeight(0, 0, 2);
    // This probe cannot pass along with the prior one.
    reader.SetHeight(1, 1, 3);
    Assert.AreEqual(-2, box.ProbeTerrain(survey, null, new(0, 0)));
  }

  [TestMethod]
  public void ProtoSerialization() {
    Real.OffsetBlockSchematic box = CreateGraniteBox(1, 1, 1, -1);
    byte[] data = SerializerUtil.Serialize(box);
    Real.OffsetBlockSchematic copy =
        SerializerUtil.Deserialize<Real.OffsetBlockSchematic>(data);
    Assert.AreEqual(box.OffsetY, copy.OffsetY);
    CollectionAssert.AreEquivalent(
        box.GetJustPositions(new BlockPos(0, 0, 0)),
        copy.GetJustPositions(new BlockPos(0, 0, 0)));
  }
}
