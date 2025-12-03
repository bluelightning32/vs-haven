using PrefixClassName.MsTest;

using Vintagestory.API.MathTools;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class ResourceZonePlan {
  private static void TestGetPointToCircleDist(double x, double y, double angle,
                                               double radius) {
    double rAdjusted =
        Real.ResourceZonePlan.GetPointToCircleDist(x, y, angle, radius);
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

  private bool IsRectInCircle(Vec2d rCenter, double rWidth, double rHeight,
                              double radius) {
    double radiusSquared = radius * radius;
    foreach (double y in new double[] { rCenter.Y - rHeight / 2,
                                        rCenter.Y + rHeight / 2 }) {
      foreach (double x in new double[] { rCenter.X - rWidth / 2,
                                          rCenter.X + rWidth / 2 }) {
        if (x * x + y * y > radiusSquared) {
          return false;
        }
      }
    }
    return true;
  }

  private void TestGetRectToCircleDist(int rWidth, int rHeight, double angle,
                                       double radius) {
    double dist = Real.ResourceZonePlan.GetRectToCircleDist(rWidth, rHeight,
                                                            angle, radius);
    int touching = 0;
    (double sin, double cos) = Math.SinCos(angle);
    Assert.IsTrue(IsRectInCircle(new Vec2d(cos * dist, sin * dist), rWidth,
                                 rHeight, radius));
    foreach (double dy in new double[] { rHeight / 2, -rHeight / 2 }) {
      foreach (double dx in new double[] { rWidth / 2, -rWidth / 2 }) {
        double shiftedX = dx + cos * dist;
        double shiftedY = dy + sin * dist;
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
  public void GetRectToCircleDistXX() {
    TestGetRectToCircleDist(1, 2, 1.8671859004511877, 4);
  }

  [TestMethod]
  public void GetRectToCircleDistOverSized() {
    Assert.IsLessThan(0,
                      Real.ResourceZonePlan.GetRectToCircleDist(50, 40, 0, 20));
  }

  [TestMethod]
  public void GetRandomRectCenterInCircleFairness() {
    NormalRandom rand = new(0);
    double rWidth = 1;
    double rHeight = 2;
    int radius = 4;
    Dictionary<Vec2i, int> selectedDistribution = new();
    for (int i = 0; i < 2000; ++i) {
      Vec2d center = Real.ResourceZonePlan.GetRandomRectCenterInCircle(
          rand, rWidth, rHeight, radius);
      Assert.IsTrue(
          IsRectInCircle(center, rWidth, rHeight, radius),
          $"Rectangle with center ({center.X}, {center.Y}) is outside the circle of radius {radius}");
      double x = Math.Floor(center.X);
      double y = Math.Floor(center.Y);
      bool allIn = true;
      foreach (double dy in new double[] { 0, 1 }) {
        foreach (double dx in new double[] { 0, 1 }) {
          if (!IsRectInCircle(new Vec2d(x + dx, y + dy), rWidth, rHeight,
                              radius)) {
            allIn = false;
            break;
          }
        }
      }
      if (allIn) {
        selectedDistribution.TryGetValue(new Vec2i((int)x, (int)y),
                                         out int oldCount);

        selectedDistribution[new Vec2i((int)x, (int)y)] = oldCount + 1;
      }
    }
    Assert.HasCount(16, selectedDistribution);

    int min = 10000;
    Vec2i minPlace = null;
    int max = 0;
    Vec2i maxPlace = null;
    foreach ((Vec2i key, int count) in selectedDistribution) {
      if (count < min) {
        min = count;
        minPlace = key;
      }
      if (count > max) {
        max = count;
        maxPlace = key;
      }
    }
    Assert.IsLessThan(
        30, max - min,
        $"Difference between min count ({min}) at " +
            $"({minPlace.X}, {minPlace.Y}) and max count ({max}) at " +
            $"({maxPlace.X}, {maxPlace.Y}) is greater than expected");
  }

  [TestMethod]
  public void ExpandRadiusIfNecessary() {
    List<Real.Structure> structures =
        [Structure.Load("stone"), Structure.Load("cattailtops")];
    List<Real.OffsetBlockSchematic> schematics = [];
    NormalRandom rand = new(0);
    while (schematics.Count < 5) {
      foreach (Real.Structure structure in structures) {
        schematics.AddRange(
            structure.Select(Framework.Server.AssetManager, rand));
      }
    }
    double minRadius = 1;
    BlockPos center = new(10000, 100, 10000);
    // Purposely use minRadius=1 to force a bunch of overlaps.
    Real.ResourceZonePlan plan = new(null, center, minRadius, rand, schematics);
    Assert.AreEqual(center, plan.Center);
    Assert.IsGreaterThan(2, plan.Radius);
    Assert.IsLessThan(100, plan.Radius);

    // Verify all of the structures fit within the final zone radius.
    Real.AABBList zone = AABBList.MakeCylinder(center, plan.Radius, 1000);
    foreach (Real.SchematicPlacer structure in plan.Structures) {
      Assert.IsTrue(
          zone.Contains(structure.Schematic.Outline, structure.Offset.AsVec3i));
    }

    // Verify that none of the structures intersect with each other.
    for (int i = 0; i < plan.Structures.Count; ++i) {
      for (int j = 0; j < i; ++j) {
        Assert.IsNull(plan.Structures[i].Schematic.Intersects(
            plan.Structures[i].Offset, plan.Structures[j].Schematic,
            plan.Structures[j].Offset));
      }
    }
  }

  [TestMethod]
  public void SetCenter() {
    List<Real.Structure> structures =
        [Structure.Load("stone"), Structure.Load("cattailtops")];
    List<Real.OffsetBlockSchematic> schematics = [];
    NormalRandom rand = new(0);
    while (schematics.Count < 5) {
      foreach (Real.Structure structure in structures) {
        schematics.AddRange(
            structure.Select(Framework.Server.AssetManager, rand));
      }
    }
    double minRadius = 100;
    BlockPos center = new(10000, 100, 10000);
    Real.ResourceZonePlan plan = new(null, center, minRadius, rand, schematics);
    Assert.AreEqual(center, plan.Center);

    // Verify all of the structures fit within the zone radius.
    Real.AABBList zone = AABBList.MakeCylinder(center, plan.Radius, 1000);
    foreach (Real.SchematicPlacer structure in plan.Structures) {
      Assert.IsTrue(
          zone.Contains(structure.Schematic.Outline, structure.Offset.AsVec3i));
    }

    // Update the center
    BlockPos newCenter = new(500, 100, 500);
    plan.Center = newCenter;
    Assert.AreEqual(newCenter, plan.Center);

    // Verify all of the structures fit within the zone radius.
    zone = AABBList.MakeCylinder(newCenter, plan.Radius, 1000);
    foreach (Real.SchematicPlacer structure in plan.Structures) {
      Assert.IsTrue(
          zone.Contains(structure.Schematic.Outline, structure.Offset.AsVec3i));
    }
  }
}
