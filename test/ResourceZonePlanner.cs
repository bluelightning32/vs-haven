using PrefixClassName.MsTest;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ResourceZonePlanner {
  public void TestGetPointToCircleDist(double angle, double radius, double x,
                                      double y) {
    double rAdjusted =
        Real.ResourceZonePlanner.GetPointToCircleDist(angle, radius, x, y);
    double xShifted = x + rAdjusted * Math.Cos(angle);
    double yShifted = y + rAdjusted * Math.Sin(angle);
    Assert.AreEqual(
        radius, Math.Sqrt(xShifted * xShifted + yShifted * yShifted), 0.0001);
  }

  [TestMethod]
  public void GetPointToCircleDistHorizontal() {
    TestGetPointToCircleDist(0, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetPointToCircleDistVertical() {
    TestGetPointToCircleDist(Math.PI / 2, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetPointToCircleDistQ1() {
    TestGetPointToCircleDist(Math.PI / 3, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetPointToCircleDistQ2() {
    TestGetPointToCircleDist(2 * Math.PI / 3, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetPointToCircleDistQ3() {
    TestGetPointToCircleDist(5 * Math.PI / 4, 20, 3, 3.5);
  }

  [TestMethod]
  public void GetPointToCircleDistQ4() {
    TestGetPointToCircleDist(7 * Math.PI / 4, 20, 3, 3.5);
  }
}
