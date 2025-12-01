using PrefixClassName.MsTest;

using Vintagestory.API.MathTools;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class AABBList {
  private static List<BlockPos> MakeSphere(BlockPos center, double radius) {
    List<BlockPos> result = [];
    for (int x = (int)(center.X - radius); x <= (int)(center.X + radius); ++x) {
      for (int y = (int)(center.Y - radius); y <= (int)(center.Y + radius);
           ++y) {
        for (int z = (int)(center.Z - radius); z <= (int)(center.Z + radius);
             ++z) {
          if (center.DistanceSqTo(x, y, z) <= radius * radius) {
            result.Add(new BlockPos(x, y, z));
          }
        }
      }
    }
    return result;
  }

  public static Real.AABBList MakeCylinder(BlockPos center, double radius,
                                           double height) {
    List<BlockPos> circle = [];
    for (int x = (int)(center.X - radius); x <= (int)(center.X + radius); ++x) {
      for (int z = (int)(center.Z - radius); z <= (int)(center.Z + radius);
           ++z) {
        if (center.DistanceSqTo(x, center.Y, z) <= radius * radius) {
          circle.Add(new BlockPos(x, center.Y, z));
        }
      }
    }
    Real.AABBList result = new(circle);
    result.GrowUp((int)(height / 2) + 1);
    result.GrowDown((int)(height / 2) + 1);
    return result;
  }

  private List<BlockPos> MakeCube(BlockPos lower, BlockPos upper) {
    List<BlockPos> result = [];
    for (int x = lower.X; x < upper.X; ++x) {
      for (int y = lower.Y; y < upper.Y; ++y) {
        for (int z = lower.Z; z < upper.Z; ++z) {
          result.Add(new BlockPos(x, y, z));
        }
      }
    }
    return result;
  }

  [TestMethod]
  public void SphereContains() {
    BlockPos center = new(5, 5, 5);
    double radius = 3;
    List<BlockPos> blocks = MakeSphere(center, radius);
    Real.AABBList aabb = new(blocks);

    for (int x = (int)(center.X - radius - 1);
         x <= (int)(center.X + radius + 1); ++x) {
      for (int y = (int)(center.Y - radius - 1);
           y <= (int)(center.Y + radius + 1); ++y) {
        for (int z = (int)(center.Z - radius - 1);
             z <= (int)(center.Z + radius + 1); ++z) {
          if (center.DistanceSqTo(x, y, z) <= radius * radius) {
            Assert.IsTrue(blocks.Contains(new BlockPos(x, y, z)));
            Assert.IsTrue(aabb.Contains(new BlockPos(x, y, z)));
          } else {
            Assert.IsFalse(blocks.Contains(new BlockPos(x, y, z)));
            Assert.IsFalse(aabb.Contains(new BlockPos(x, y, z)));
          }
        }
      }
    }
  }

  [TestMethod]
  public void ReverseSphereContains() {
    BlockPos center = new(5, 5, 5);
    double radius = 3;
    List<BlockPos> blocks = MakeSphere(center, radius);
    blocks.Reverse();
    Real.AABBList aabb = new(blocks);

    for (int x = (int)(center.X - radius - 1);
         x <= (int)(center.X + radius + 1); ++x) {
      for (int y = (int)(center.Y - radius - 1);
           y <= (int)(center.Y + radius + 1); ++y) {
        for (int z = (int)(center.Z - radius - 1);
             z <= (int)(center.Z + radius + 1); ++z) {
          if (center.DistanceSqTo(x, y, z) <= radius * radius) {
            Assert.IsTrue(aabb.Contains(new BlockPos(x, y, z)));
          } else {
            Assert.IsFalse(aabb.Contains(new BlockPos(x, y, z)));
          }
        }
      }
    }
  }

  [TestMethod]
  public void TwoSpheresContains() {
    BlockPos center1 = new(1, 2, 3);
    BlockPos center2 = new(107, 106, 105);
    double radius = 3;
    List<BlockPos> blocks = MakeSphere(center1, radius);
    blocks.AddRange(MakeSphere(center2, radius));
    Real.AABBList aabb = new(blocks);

    for (int x = (int)(center1.X - radius - 1);
         x <= (int)(center1.X + radius + 1); ++x) {
      for (int y = (int)(center1.Y - radius - 1);
           y <= (int)(center1.Y + radius + 1); ++y) {
        for (int z = (int)(center1.Z - radius - 1);
             z <= (int)(center1.Z + radius + 1); ++z) {
          if (center1.DistanceSqTo(x, y, z) <= radius * radius) {
            Assert.IsTrue(aabb.Contains(new BlockPos(x, y, z)));
          } else {
            Assert.IsFalse(aabb.Contains(new BlockPos(x, y, z)));
          }
        }
      }
    }

    for (int x = (int)(center2.X - radius - 1);
         x <= (int)(center2.X + radius + 1); ++x) {
      for (int y = (int)(center2.Y - radius - 1);
           y <= (int)(center2.Y + radius + 1); ++y) {
        for (int z = (int)(center2.Z - radius - 1);
             z <= (int)(center2.Z + radius + 1); ++z) {
          if (center2.DistanceSqTo(x, y, z) <= radius * radius) {
            Assert.IsTrue(aabb.Contains(new BlockPos(x, y, z)));
          } else {
            Assert.IsFalse(aabb.Contains(new BlockPos(x, y, z)));
          }
        }
      }
    }
  }

  [TestMethod]
  public void SphereIntersectsBlock() {
    BlockPos center = new(5, 6, 7);
    double radius = 3;
    List<BlockPos> blocks = MakeSphere(center, radius);
    Real.AABBList aabb = new(blocks);

    Vec3i withOffset = new(center);
    for (int x = (int)(radius - 1); x < (int)(radius + 1); ++x) {
      for (int y = (int)(radius - 1); y < (int)(radius + 1); ++y) {
        for (int z = (int)(radius - 1); z < (int)(radius + 1); ++z) {
          if (center.DistanceSqTo(x, y, z) <= radius * radius) {
            Assert.IsNotNull(aabb.Intersects(
                new Cuboidi(new BlockPos(x, y, z), 1), withOffset));
          } else {
            Assert.IsNull(aabb.Intersects(new Cuboidi(new BlockPos(x, y, z), 1),
                                          withOffset));
          }
        }
      }
    }
  }

  [TestMethod]
  public void CubeRegionCount() {
    List<BlockPos> blocks =
        MakeCube(new BlockPos(1, 1, 1), new BlockPos(3, 4, 5));
    Real.AABBList aabb = new(blocks);
    Assert.HasCount(1, aabb.Regions);
  }

  [TestMethod]
  public void TwoCubesRegionCount() {
    List<BlockPos> blocks =
        MakeCube(new BlockPos(1, 1, 1), new BlockPos(3, 4, 5));
    blocks.AddRange(
        MakeCube(new BlockPos(10, 10, 10), new BlockPos(13, 14, 15)));
    Real.AABBList aabb = new(blocks);
    Assert.HasCount(2, aabb.Regions);
  }

  [TestMethod]
  public void AvoidIntersectionNoIntersection() {
    Real.AABBList cube =
        new(MakeCube(new BlockPos(1, 1, 1), new BlockPos(3, 4, 5)));
    Assert.IsTrue(
        cube.AvoidIntersection(cube, new Vec3i(100, 0, 0), new Vec3d(1, 0, 0)));
  }

  [TestMethod]
  public void AvoidIntersectionOverlappingCube() {
    Real.AABBList cube =
        new(MakeCube(new BlockPos(1, 2, 3), new BlockPos(4, 5, 6)));
    Vec3i offset = new(0, 1, 0);
    Assert.IsFalse(cube.AvoidIntersection(cube, offset, new Vec3d(1, 0, 0)));
    Assert.IsNull(cube.Intersects(cube, offset));
    Assert.IsGreaterThan(0, offset.X);
    Assert.AreEqual(1, offset.Y);
    Assert.AreEqual(0, offset.Z);

    Assert.IsNotNull(
        cube.Intersects(cube, new Vec3i(offset.X - 1, offset.Y, offset.Z)));
  }

  [TestMethod]
  public void AvoidIntersectionOverlappingSphere() {
    Real.AABBList sphere = new(MakeSphere(new BlockPos(1, 2, 3), 3));
    Vec3i offset = new(0, 1, 0);
    Assert.IsFalse(
        sphere.AvoidIntersection(sphere, offset, new Vec3d(1, 0, 0)));
    Assert.IsNull(sphere.Intersects(sphere, offset));
    Assert.IsGreaterThan(0, offset.X);
    Assert.AreEqual(1, offset.Y);
    Assert.AreEqual(0, offset.Z);

    Assert.IsNotNull(
        sphere.Intersects(sphere, new Vec3i(offset.X - 1, offset.Y, offset.Z)));
  }

  [TestMethod]
  public void CubeExactlyContains() {
    Real.AABBList cube =
        new(MakeCube(new BlockPos(1, 1, 1), new BlockPos(3, 4, 5)));
    Assert.IsTrue(cube.Contains(cube, new Vec3i(0, 0, 0)));
  }

  [TestMethod]
  public void CubeExactlyContainsWithOffset() {
    Real.AABBList cube1 =
        new(MakeCube(new BlockPos(1, 1, 1), new BlockPos(3, 4, 5)));
    Vec3i offset = new(10, 11, 12);
    Real.AABBList cube2 =
        new(MakeCube(new BlockPos(1 - offset.X, 1 - offset.Y, 1 - offset.Z),
                     new BlockPos(3 - offset.X, 4 - offset.Y, 5 - offset.Z)));

    Assert.IsFalse(cube1.Contains(cube2, new Vec3i(0, 0, 0)));
    Assert.IsTrue(cube1.Contains(cube2, offset));
  }

  [TestMethod]
  public void GetBoundingBoxForIntersectionEmpty() {
    Real.AABBList empty = new([]);
    Cuboidi bouding = empty.GetBoundingBoxForIntersection(
        new Cuboidi(-100, -100, -100, 100, 100, 100));
    Assert.IsGreaterThan(bouding.X2, bouding.X1);
    Assert.IsGreaterThan(bouding.Y2, bouding.Y1);
    Assert.IsGreaterThan(bouding.Z2, bouding.Z1);
  }

  [TestMethod]
  public void GetBoundingBoxForIntersectionEmptyInRange() {
    Real.AABBList cube =
        new(MakeCube(new BlockPos(1, 1, 1), new BlockPos(3, 4, 5)));
    Cuboidi bouding = cube.GetBoundingBoxForIntersection(
        new Cuboidi(-100, 100, -100, 100, 200, 100));
    Assert.IsGreaterThan(bouding.X2, bouding.X1);
    Assert.IsGreaterThan(bouding.Y2, bouding.Y1);
    Assert.IsGreaterThan(bouding.Z2, bouding.Z1);

    bouding = cube.GetBoundingBoxForIntersection(
        new Cuboidi(-100, -100, -100, 100, -200, 100));
    Assert.IsGreaterThan(bouding.X2, bouding.X1);
    Assert.IsGreaterThan(bouding.Y2, bouding.Y1);
    Assert.IsGreaterThan(bouding.Z2, bouding.Z1);
  }

  [TestMethod]
  public void GetBoundingBoxForIntersectionStaggeredCubes() {
    List<BlockPos> positions =
        MakeCube(new BlockPos(100, 0, 200), new BlockPos(101, 10, 201));
    positions.AddRange(
        MakeCube(new BlockPos(104, 10, 204), new BlockPos(105, 20, 205)));
    positions.AddRange(
        MakeCube(new BlockPos(114, 20, 214), new BlockPos(115, 30, 215)));
    Real.AABBList cube = new(positions);

    Cuboidi bouding = cube.GetBoundingBoxForIntersection(
        new Cuboidi(-100, 5, -100, 500, 15, 500));
    Assert.AreEqual(100, bouding.X1);
    Assert.AreEqual(5, bouding.Y1);
    Assert.AreEqual(200, bouding.Z1);
    Assert.AreEqual(105, bouding.X2);
    Assert.AreEqual(15, bouding.Y2);
    Assert.AreEqual(205, bouding.Z2);

    bouding = cube.GetBoundingBoxForIntersection(
        new Cuboidi(-100, -100, -100, 101, 200, 201));
    Assert.AreEqual(100, bouding.X1);
    Assert.AreEqual(0, bouding.Y1);
    Assert.AreEqual(200, bouding.Z1);
    Assert.AreEqual(101, bouding.X2);
    Assert.AreEqual(10, bouding.Y2);
    Assert.AreEqual(201, bouding.Z2);
  }
}
