using PrefixClassName.MsTest;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class SquareSpiral {
  [TestMethod]
  public void First9() {
    Real.SquareSpiral spiral = new();
    List<Vec2i> first = new();
    for (int i = 0; i < 9; ++i) {
      first.Add(spiral.Offset);
      spiral.Next();
    }
    CollectionAssert.AreEqual(
        new Vec2i[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
                      new(-1, 0), new(-1, -1), new(0, -1), new(1, -1) },
        first);
  }

  [TestMethod]
  public void First25() {
    Real.SquareSpiral spiral = new();
    HashSet<Vec2i> found = new();
    for (int i = 0; i < 25; ++i) {
      Assert.IsLessThan(3, spiral.Offset.X);
      Assert.IsGreaterThan(-3, spiral.Offset.X);
      Assert.IsLessThan(3, spiral.Offset.Y);
      Assert.IsGreaterThan(-3, spiral.Offset.Y);
      found.Add(spiral.Offset);
      spiral.Next();
    }
    Assert.HasCount(25, found);
  }

  [TestMethod]
  public void SquareOffsetFirst9() {
    Real.SquareSpiral spiral = new();
    List<Vec2i> first = [];
    for (int i = 0; i < 9; ++i) {
      first.Add(spiral.SquareOffset);
      spiral.Next();
    }
    CollectionAssert.AreEqual(
        new Vec2i[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
                      new(-1, 0), new(-1, -1), new(0, -1), new(1, -1) },
        first);
  }

  [TestMethod]
  public void SquareOffsetFirst25() {
    Real.SquareSpiral spiral = new();
    HashSet<Vec2i> found = [];
    for (int i = 0; i < 25; ++i) {
      Assert.IsLessThan(5, spiral.SquareOffset.X);
      Assert.IsGreaterThan(-5, spiral.SquareOffset.X);
      Assert.IsLessThan(5, spiral.SquareOffset.Y);
      Assert.IsGreaterThan(-5, spiral.SquareOffset.Y);
      found.Add(spiral.SquareOffset);
      spiral.Next();
    }
    Assert.HasCount(25, found);
  }

  [TestMethod]
  public void Serialization() {
    Real.SquareSpiral spiral = new();
    for (int i = 0; i < 9; ++i) {
      Vec2i p = spiral.Offset;
      byte[] data = SerializerUtil.Serialize(spiral);
      Real.SquareSpiral copy =
          SerializerUtil.Deserialize<Real.SquareSpiral>(data);
      Assert.AreEqual(p, copy.Offset);

      spiral.Next();
    }
  }
}
