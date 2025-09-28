using PrefixClassName.MsTest;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class Structure {
  public static Real.Structure Load(string name) {
    AssetLocation location = AssetLocation.Create(name, "haven");
    location.WithPathPrefixOnce("worldgen/havenstructures/")
        .WithPathAppendixOnce(".json");
    return Framework.Server.AssetManager.Get<Real.Structure>(location);
  }

  [TestMethod]
  public void Select() {
    Real.Structure stone = Load("stone");
    // This test is going to verify that at least one schematic is returned. So
    // the structure must be configured to always return at least one schematic.
    Assert.IsGreaterThanOrEqualTo(1, stone.Count.avg - stone.Count.var);
    NormalRandom rand = new(0);
    List<OffsetBlockSchematic> schematics =
        stone.Select(Framework.Server.AssetManager, rand).ToList();
    Assert.IsGreaterThanOrEqualTo(1, schematics.Count);
  }

  [TestMethod]
  public void SelectCountRoundDown() {
    Real.Structure stone = Load("stone");
    stone.Count.dist = EnumDistribution.UNIFORM;
    stone.Count.avg = 1.0f;
    stone.Count.var = 0.5f;
    NormalRandom rand = new(0);
    int count = 0;
    int attempts = 10;
    for (int i = 0; i < attempts; ++i) {
      count += stone.Select(Framework.Server.AssetManager, rand).Count();
    }
    Assert.IsLessThanOrEqualTo(0.7 * attempts, count);
    Assert.IsGreaterThanOrEqualTo(0.3 * attempts, count);
  }

  [TestMethod]
  public void Intersects() {
    Real.Structure stone = Load("stone");
    NormalRandom rand = new(0);
    OffsetBlockSchematic schematic =
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
    OffsetBlockSchematic schematic =
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
}
