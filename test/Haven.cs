using PrefixClassName.MsTest;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class Haven {
  [TestMethod]
  public void Serialization() {
    Real.HavenRegionIntersection intersection =
        new() { Center = new BlockPos(10, 10, 10, Dimensions.NormalWorld),
                Radius = 31, ResourceZoneRadius = 11, SafeZoneRadius = 11 };
    Real.Haven haven = new(intersection, 1, 5);
    byte[] data = SerializerUtil.Serialize(haven);
    Real.Haven copy = SerializerUtil.Deserialize<Real.Haven>(data);

    Assert.AreEqual(haven.GetIntersection(), copy.GetIntersection());
  }

  [TestMethod]
  public void ExpandOnConstruction() {
    Real.HavenRegionIntersection intersection =
        new() { Center = new BlockPos(10, 10, 10, Dimensions.NormalWorld),
                Radius = 5, ResourceZoneRadius = 1, SafeZoneRadius = 1 };
    Real.Haven haven = new(intersection, 0, 1);

    // The haven has this shape:
    //
    //     2
    //   3 R 1
    // 4 R R R 0
    //   5 R 7
    //     6
    Assert.AreEqual(2, haven.SafeZoneRadius);

    // It already expanded.
    Assert.IsFalse(haven.TryExpand(0, 1));
  }

  [TestMethod]
  public void GetPlotNoBorder() {
    Real.HavenRegionIntersection intersection =
        new() { Center = new BlockPos(10, 10, 10, Dimensions.NormalWorld),
                Radius = 5, ResourceZoneRadius = 1, SafeZoneRadius = 1 };
    Real.Haven haven = new(intersection, 0, 1);

    // The haven has this shape:
    //
    //     2
    //   3 R 1
    // 4 R R R 0
    //   5 R 7
    //     6

    (int, int, int)[] expectedPlots = new(int, int, int)[] {
      (2, 0, 0),  (1, 1, 1),   (0, 2, 2),  (-1, 1, 3),
      (-2, 0, 4), (-1, -1, 5), (0, -2, 6), (1, -1, 7),
    };

    foreach (var expected in expectedPlots) {
      (Real.PlotRing ring, int plot) = haven.GetPlot(
          new BlockPos(10 + expected.Item1, 10, 10 + expected.Item2,
                       Dimensions.NormalWorld),
          1, 1);
      Assert.IsNotNull(ring);
      Assert.AreEqual(expected.Item3, plot);
    }

    // Try a position outside the plot zone
    {
      (Real.PlotRing ring, _) =
          haven.GetPlot(new BlockPos(13, 10, 10, Dimensions.NormalWorld), 1, 1);
      Assert.IsNull(ring);
    }
  }
}
