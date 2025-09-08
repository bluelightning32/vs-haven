using PrefixClassName.MsTest;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class HavenSystem {
  [TestMethod]
  public void Logger() {
    // Checking the logger also verifies that the mod was loaded.
    Assert.IsNotNull(Real.HavenSystem.Logger);
  }
}
