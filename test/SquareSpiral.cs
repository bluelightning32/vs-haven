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
      first.Add(spiral.GetOffset());
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
      Assert.IsLessThan(3, spiral.GetOffset().X);
      Assert.IsGreaterThan(-3, spiral.GetOffset().X);
      Assert.IsLessThan(3, spiral.GetOffset().Y);
      Assert.IsGreaterThan(-3, spiral.GetOffset().Y);
      found.Add(spiral.GetOffset());
      spiral.Next();
    }
    Assert.AreEqual(25, found.Count);
  }

  [TestMethod]
  public void Serialization() {
    Real.SquareSpiral spiral = new();
    for (int i = 0; i < 9; ++i) {
      Vec2i p = spiral.GetOffset();
      byte[] data = SerializerUtil.Serialize(spiral);
      Real.SquareSpiral copy =
          SerializerUtil.Deserialize<Real.SquareSpiral>(data);
      Assert.AreEqual(p, copy.GetOffset());

      spiral.Next();
    }
  }
}
