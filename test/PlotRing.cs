using PrefixClassName.MsTest;

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

    Assert.IsNull(ring.ClaimPlot(Math.Tau / 4, "user2", "user2"));
    Assert.IsNull(ring.ClaimPlot(2 * Math.Tau / 4, "user3", "user3"));
    Assert.IsNull(ring.ClaimPlot(3 * Math.Tau / 4, "user3", "user3"));
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
}
