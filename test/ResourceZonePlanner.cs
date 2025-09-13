using PrefixClassName.MsTest;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ResourceZonePlanner {
  public static void TestGetPointToCircleDist(double x, double y, double angle,
                                              double radius) {
    double rAdjusted =
        Real.ResourceZonePlanner.GetPointToCircleDist(x, y, angle, radius);
    double xShifted = x + rAdjusted * Math.Cos(angle);
    double yShifted = y + rAdjusted * Math.Sin(angle);
    Assert.AreEqual(
        radius, Math.Sqrt(xShifted * xShifted + yShifted * yShifted), 0.0001);
  }

  [TestMethod]
  public void GetPointToCircleDistHorizontal() {
    TestGetPointToCircleDist(3, 3.5, 0, 20);
  }

  [TestMethod]
  public void GetPointToCircleDistVertical() {
    TestGetPointToCircleDist(3, 3.5, Math.PI / 2, 20);
  }

  [TestMethod]
  public void GetPointToCircleDistQ1() {
    TestGetPointToCircleDist(3, 3.5, Math.PI / 3, 20);
  }

  [TestMethod]
  public void GetPointToCircleDistQ2() {
    TestGetPointToCircleDist(3, 3.5, 2 * Math.PI / 3, 20);
  }

  [TestMethod]
  public void GetPointToCircleDistQ3() {
    TestGetPointToCircleDist(3, 3.5, 5 * Math.PI / 4, 20);
  }

  [TestMethod]
  public void GetPointToCircleDistQ4() {
    TestGetPointToCircleDist(3, 3.5, 7 * Math.PI / 4, 20);
  }
}
