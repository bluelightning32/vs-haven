using PrefixClassName.MsTest;

using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class PlotRing {
  [TestMethod]
  public void SerializeUnclaimed() {
    Real.PlotRing ring = new(10, 100, 0, 1);
    Assert.IsGreaterThan(2, ring.Plots.Length);

    ring.Plots[0].OwnerUID = "test";

    byte[] data = SerializerUtil.Serialize(ring);
    Real.PlotRing copy = SerializerUtil.Deserialize<Real.PlotRing>(data);

    CollectionAssert.AreEquivalent(ring.Plots, copy.Plots);
  }

  [TestMethod]
  public void ClaimUnclaimed() {
    Real.PlotRing ring = new(10, 100, 0, 1);
    Assert.IsNull(ring.ClaimPlot(0, "myuid", "myname"));
  }

  [TestMethod]
  public void NoDoubleClaim() {
    Real.PlotRing ring = new(10, 100, 0, 1);
    Assert.IsNull(ring.ClaimPlot(0, "user1", "user1"));
    Assert.IsNotNull(ring.ClaimPlot(0, "user2", "user2"));
  }

  [TestMethod]
  public void Reclaim() {
    Real.PlotRing ring = new(10, 100, 0, 1);
    Assert.IsNull(ring.ClaimPlot(0, "user1", "user1"));
    Assert.IsNull(ring.UnclaimPlot(0, "user1"));
    Assert.IsNull(ring.ClaimPlot(0, "user1", "user1"));
  }

  [TestMethod]
  public void ShouldStartNewRing() {
    Real.PlotRing ring = new(10, 100, 0, Math.Tau / 4);
    Assert.IsFalse(ring.ShouldStartNewRing());

    Assert.IsNull(ring.ClaimPlot(0, "user1", "user1"));
    Assert.IsFalse(ring.ShouldStartNewRing());

    Assert.IsNull(ring.ClaimPlot(1, "user2", "user2"));
    Assert.IsNull(ring.ClaimPlot(2, "user3", "user3"));
    Assert.IsNull(ring.ClaimPlot(3, "user3", "user3"));
    Assert.IsTrue(ring.ShouldStartNewRing());
  }

  [TestMethod]
  public void PlotSizeNoBorder() {
    int expectedBlocksPerPlot = 10;
    Real.PlotRing ring = Real.PlotRing.Create(10, 20, 0, expectedBlocksPerPlot);
    double ringArea = Math.PI * ((ring.HoleRadius + ring.Width) *
                                     (ring.HoleRadius + ring.Width) -
                                 ring.HoleRadius * ring.HoleRadius);
    int numPlots = ring.Plots.Length;
    double plotBlocks = ringArea / numPlots;
    Assert.IsGreaterThanOrEqualTo(expectedBlocksPerPlot, plotBlocks);
    Assert.IsLessThan(expectedBlocksPerPlot * 2, plotBlocks);
  }

  private static void VerifyPlotBoundingBox(Real.Haven haven,
                                            Real.PlotRing ring, int plot) {
    BlockPos center = haven.GetIntersection().Center;
    Rectanglei box = ring.GetPlotBoundingBox(center.X, center.Z, plot);
    int x1 = int.MaxValue;
    int z1 = int.MaxValue;
    int x2 = int.MinValue;
    int z2 = int.MinValue;
    for (int z = box.Y1 - 2; z < box.Y2 + 2; ++z) {
      for (int x = box.X1 - 2; x < box.X2 + 2; ++x) {
        (Real.PlotRing foundRing, int foundPlot) = haven.GetPlot(
            new BlockPos(x, center.Y, z, center.dimension), 10, 10);
        Assert.AreEqual(foundPlot == plot,
                        ring.IsInPlot(center.X, center.Z, plot, x, z));
        if (foundPlot == plot) {
          Assert.IsGreaterThanOrEqualTo(box.X1, x, "x");
          Assert.IsGreaterThanOrEqualTo(box.Y1, z, "z");
          Assert.IsLessThanOrEqualTo(box.X2, x, "x");
          Assert.IsLessThanOrEqualTo(box.Y2, z, "z");
          x1 = int.Min(x1, x);
          z1 = int.Min(z1, z);
          x2 = int.Max(x2, x);
          z2 = int.Max(z2, z);
        }
      }
    }
    Assert.IsLessThan(3, x1 - box.X1);
    Assert.IsLessThan(3, z1 - box.Y1);
    Assert.IsLessThan(3, box.X2 - x2);
    Assert.IsLessThan(3, box.Y2 - z2);
  }

  [TestMethod]
  public void GetPlotBoundingBoxNormalSize() {
    Real.HavenRegionIntersection intersection =
        new() { Center = new BlockPos(100, 100, 100, Dimensions.NormalWorld),
                Radius = 30, ResourceZoneRadius = 10, SafeZoneRadius = 10 };
    Real.Haven haven = new(intersection, 2, 10);
    (Real.PlotRing ring, int firstPlot) = haven.GetPlot(
        intersection.Center.EastCopy(intersection.ResourceZoneRadius + 1), 10,
        10);
    Assert.IsNotNull(ring);
    Assert.AreEqual(0, firstPlot);
    for (int i = 0; i < ring.Plots.Length; ++i) {
      VerifyPlotBoundingBox(haven, ring, i);
    }
  }

  [TestMethod]
  public void GetPlotBoundingBoxManyPlots() {
    Real.HavenRegionIntersection intersection =
        new() { Center = new BlockPos(100, 100, 100, Dimensions.NormalWorld),
                Radius = 100, ResourceZoneRadius = 30, SafeZoneRadius = 30 };
    for (int w = 10; w < 50; ++w) {
      Real.Haven haven = new(intersection, 0, w);
      (Real.PlotRing ring, int firstPlot) = haven.GetPlot(
          intersection.Center.EastCopy(intersection.ResourceZoneRadius + 1), 10,
          10);
      Assert.IsNotNull(ring);
      Assert.AreEqual(0, firstPlot);
      for (int i = 0; i < ring.Plots.Length; ++i) {
        VerifyPlotBoundingBox(haven, ring, i);
      }
    }
  }
}
