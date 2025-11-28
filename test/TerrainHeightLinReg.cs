using PrefixClassName.MsTest;

using Real = Haven;

namespace Haven.Test;

[PrefixTestClass]
public class TerrainHeightLinReg {
  [TestMethod]
  public void FourFlat() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 1);
    linReg.Add(2, 10, 1);
    linReg.Add(1, 10, 2);
    linReg.Add(2, 10, 2);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept);
    Assert.AreEqual(0, xparam);
    Assert.AreEqual(0, zparam);

    Assert.AreEqual(10, linReg.Mean);
    Assert.AreEqual(0, linReg.PopulationVariance);
    Assert.AreEqual(1, linReg.RSquared);
    Assert.AreEqual(0, linReg.MeanSquareErrorN);
  }

  [TestMethod]
  public void FourXSlope1() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 11, 1);
    linReg.Add(2, 12, 1);
    linReg.Add(1, 11, 2);
    linReg.Add(2, 12, 2);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept);
    Assert.AreEqual(1, xparam);
    Assert.AreEqual(0, zparam);

    Assert.AreEqual(11.5, linReg.Mean);
    Assert.AreEqual(0.25, linReg.PopulationVariance);
    Assert.AreEqual(1, linReg.RSquared);
    Assert.AreEqual(0, linReg.MeanSquareErrorN);
  }

  [TestMethod]
  public void FourZSlope1() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 11, 1);
    linReg.Add(2, 11, 1);
    linReg.Add(1, 12, 2);
    linReg.Add(2, 12, 2);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept);
    Assert.AreEqual(0, xparam);
    Assert.AreEqual(1, zparam);
  }

  [TestMethod]
  public void FourZSlope2() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 11, 1);
    linReg.Add(2, 11, 1);
    linReg.Add(1, 13, 2);
    linReg.Add(2, 13, 2);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(9, intercept);
    Assert.AreEqual(0, xparam);
    Assert.AreEqual(2, zparam);
  }

  [TestMethod]
  public void NoSamples() {
    Real.TerrainHeightLinReg linReg = new();
    // Just make sure it doesn't crash.
    (double intercept, double xparam, double zparam) = linReg.Beta;
    double _ = linReg.Mean;

    Assert.AreEqual(0, linReg.PopulationVariance);
    Assert.AreEqual(1, linReg.RSquared);
    Assert.AreEqual(0, linReg.MeanSquareErrorN);
  }

  [TestMethod]
  public void OneSample() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 2);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 1 * xparam + 2 * zparam, 0.0001);

    Assert.AreEqual(10, linReg.Mean);
    Assert.AreEqual(0, linReg.PopulationVariance);
    Assert.AreEqual(1, linReg.RSquared);
    Assert.AreEqual(0, linReg.MeanSquareErrorN);
  }

  [TestMethod]
  public void TwoSampleCopies() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 2);
    linReg.Add(1, 10, 2);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 1 * xparam + 2 * zparam, 0.0001);

    Assert.AreEqual(10, linReg.Mean);
    Assert.AreEqual(0, linReg.PopulationVariance);
    Assert.AreEqual(1, linReg.RSquared);
    Assert.AreEqual(0, linReg.MeanSquareErrorN);
  }

  [TestMethod]
  public void TwoSamplesInXLineFlat() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 1);
    linReg.Add(2, 10, 1);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 1 * xparam + 1 * zparam, 0.0001);
    Assert.AreEqual(10, intercept + 2 * xparam + 1 * zparam, 0.0001);
  }

  [TestMethod]
  public void TwoSamplesInXLineSlope2() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 1);
    linReg.Add(2, 12, 1);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 1 * xparam + 1 * zparam, 0.0001);
    Assert.AreEqual(12, intercept + 2 * xparam + 1 * zparam, 0.0001);
  }

  [TestMethod]
  public void TwoSamplesInZLineFlat() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 3);
    linReg.Add(1, 10, 4);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 1 * xparam + 3 * zparam, 0.0001);
    Assert.AreEqual(10, intercept + 1 * xparam + 4 * zparam, 0.0001);
  }

  [TestMethod]
  public void TwoSamplesInZLineSlope2() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(1, 10, 3);
    linReg.Add(1, 12, 4);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 1 * xparam + 3 * zparam, 0.0001);
    Assert.AreEqual(12, intercept + 1 * xparam + 4 * zparam, 0.0001);
  }

  [TestMethod]
  public void TwoSamplesInXZLineSlope2() {
    Real.TerrainHeightLinReg linReg = new();
    linReg.Add(3, 10, 3);
    linReg.Add(4, 12, 4);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(10, intercept + 3 * xparam + 3 * zparam, 0.0001);
    Assert.AreEqual(12, intercept + 4 * xparam + 4 * zparam, 0.0001);
  }

  [TestMethod]
  public void Balanced16() {
    Real.TerrainHeightLinReg linReg = new();
    // 12 10 10 12
    // 10 12 12 10
    // 10 12 12 10
    // 12 10 10 12
    linReg.Add(100, 12, 100);
    linReg.Add(101, 10, 100);
    linReg.Add(102, 10, 100);
    linReg.Add(103, 12, 100);
    linReg.Add(100, 10, 101);
    linReg.Add(101, 12, 101);
    linReg.Add(102, 12, 101);
    linReg.Add(103, 10, 101);
    linReg.Add(100, 10, 102);
    linReg.Add(101, 12, 102);
    linReg.Add(102, 12, 102);
    linReg.Add(103, 10, 102);
    linReg.Add(100, 12, 103);
    linReg.Add(101, 10, 103);
    linReg.Add(102, 10, 103);
    linReg.Add(103, 12, 103);
    (double intercept, double xparam, double zparam) = linReg.Beta;
    Assert.AreEqual(11, intercept);
    Assert.AreEqual(0, xparam);
    Assert.AreEqual(0, zparam);

    Assert.AreEqual(11, linReg.Mean);
    Assert.AreEqual(1, linReg.PopulationVariance);
    Assert.AreEqual(0, linReg.RSquared);
    Assert.AreEqual(1, linReg.MeanSquareErrorN);
  }
}
