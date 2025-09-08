using PrefixClassName.MsTest;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ResourceZonePlanner {
  public void TestGetAdjustedRadius(double angle, double radius, double x,
                                    double y) {
    double rAdjusted =
        Real.ResourceZonePlanner.GetAdjustedRadius(angle, radius, x, y);
    double xShifted = x + rAdjusted * Math.Cos(angle);
    double yShifted = y + rAdjusted * Math.Sin(angle);
    Assert.AreEqual(
        radius, Math.Sqrt(xShifted * xShifted + yShifted * yShifted), 0.0001);
  }

  [TestMethod]
  public void GetAdjustedRadiusHorizontal() {
    TestGetAdjustedRadius(0, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetAdjustedRadiusVertical() {
    TestGetAdjustedRadius(Math.PI / 2, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetAdjustedRadiusQ1() {
    TestGetAdjustedRadius(Math.PI / 3, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetAdjustedRadiusQ2() {
    TestGetAdjustedRadius(2 * Math.PI / 3, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetAdjustedRadiusQ3() {
    TestGetAdjustedRadius(5 * Math.PI / 4, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetAdjustedRadiusQ4() {
    TestGetAdjustedRadius(7 * Math.PI / 4, 20, 3, 3.5);
  }
}
