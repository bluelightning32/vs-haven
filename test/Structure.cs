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
}
