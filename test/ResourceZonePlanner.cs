using PrefixClassName.MsTest;

using Vintagestory.API.MathTools;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ResourceZonePlanner {
  private static void TestGetPointToCircleDist(double x, double y, double angle,
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

  private void TestGetRectToCircleDist(int rWidth, int rHeight, double angle,
                                       double radius) {
    double dist = Real.ResourceZonePlanner.GetRectToCircleDist(rWidth, rHeight,
                                                               angle, radius);
    int touching = 0;
    (double sin, double cos) = Math.SinCos(angle);
    foreach (double y in new double[] { rHeight / 2, -rHeight / 2 }) {
      foreach (double x in new double[] { rWidth / 2, -rWidth / 2 }) {
        double shiftedX = x + cos * dist;
        double shiftedY = y + sin * dist;
        double pDist = Math.Sqrt(shiftedX * shiftedX + shiftedY * shiftedY);
        if (Math.Abs(pDist - radius) < 0.0001) {
          ++touching;
        }
        Assert.IsLessThanOrEqualTo(radius, pDist);
      }
    }
  }

  [TestMethod]
  public void GetRectToCircleDistRight() {
    TestGetRectToCircleDist(4, 2, 0 * Math.PI, 20);
  }

  [TestMethod]
  public void GetRectToCircleDistLeft() {
    TestGetRectToCircleDist(4, 2, 1 * Math.PI, 20);
  }

  [TestMethod]
  public void GetRectToCircleDistQ1() {
    TestGetRectToCircleDist(4, 2, Math.PI / 2, 20);
  }

  [TestMethod]
  public void GetRectToCircleDistOverSized() {
    Assert.IsLessThan(
        0, Real.ResourceZonePlanner.GetRectToCircleDist(50, 40, 0, 20));
  }
}
