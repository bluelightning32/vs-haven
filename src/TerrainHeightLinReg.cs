using System;

namespace Haven;

/// <summary>
/// Performs a linear regression on the terrain height. The linear regression
/// produces the constants c1, c2, and c3, such that the following formula
/// approximates the height of the terrain:
///   y = c1 + c2*x + c3*z
///
/// The linear regression tries to minimize the error of the block locations it
/// was trained on. It can also return the R^2 value to describe how well the
/// linear regression fits the data. The R^2 value is between 0 and 1, with 1
/// meaning the model perfectly fits the data.
///
/// The class can handle at least 10,000 samples with X and Z coords up to the
/// game max of 67108864. However, the samples should not be more than 1,000,000
/// apart, or rounding errors may occur.
/// </summary>
public class TerrainHeightLinReg {
  // With a linear regression, each sample has an observed value and predictor
  // variables. For estimating the terrain height, the y coordinate of the
  // terrain surface is the observed value. The x and z coordinates of the
  // terrain surface are the predictor variables. Additionally, the constant 1
  // is a predictor variable (called an intercept term) so that the output of
  // the linear regression includes an offset constant.
  //
  // Each surface location passed to this object is a sample. The mathematical
  // descriptions of a linear regression put all the samples into matrices.
  //
  // The matrix of observed values is called the regressand. It is commonly
  // represented with the variable y. For the terrain height, this is a column
  // vector, with one row for the height of each sample.
  //
  // The matrix of predictor values is called the regressor. It is commonly
  // represented with the variable X. Each row is a sample. The columns are the
  // intercept term (1), the sample x coordinate, and the sample z coordinate.
  // Note that X (the regressor) is distinct from x (the east/west coordinate of
  // a single sample).
  //
  // The output of the linear regression is B, a column vector of parameters
  // that can be combined with the predictor variables to predict the height of
  // a sample.
  //
  // The following formula describes how to calculate B using matrix operations.
  // Note that ^T means matrix transpose and ^-1 means matrix inverse.
  //   B = (X^T * X)^-1 * X^T * y
  //
  // The above formula works when the linear regression is only performed once
  // when all the samples are available. However, TerrainHeightLinReg supports
  // quickly performing the linear regression after each sample is added. So the
  // linear regression formula has to be incrementally computed as each sample
  // is added.
  //
  // (X^T * X) symmetric 3x3 matrix that can be incrementally calculated. It
  // contains the following values:
  // | num samples | x coord sum   |   z coord sum |
  // | x coord sum | x^2 coord sum |  xz coord sum |
  // | z coord sum |  xz coord sum | z^2 coord sum |
  //
  // (X^T * y) column vector that can be incrementally calculated. It contains
  // the following values:
  // |  y coord sum |
  // | xy coord sum |
  // | yz coord sum |
  //
  // Additionally this class needs to calculate R^2. One formula for R^2 is:
  //   P = X * (X^T * X)^-1 * X^T
  //   M = I_n - P
  //   TSS = variance(y) * num samples.
  //   R^2 = 1 - (y^T * M * y) / TSS
  //
  // The formula can be simplified so that the matrices can be incrementally
  // calculated.
  //   R^2 = 1 - (y^T * (I_n - P) * y) / variance(y)
  //   R^2 = 1 - (y^T * I_n * y - y^T * P * y) / variance(y)
  //   R^2 = 1 - (y^T * y - y^T * (X * (X^T * X)^-1 * X^T) * y) / variance(y)
  //   R^2 = 1 - (y^T * y - y^T * X * B) / variance(y)
  //   R^2 = 1 - (y^T * y - (X^T * y)^T * B) / variance(y)
  //
  // (y^T * y) is a column vector with just one row that can be incrementally
  // calculated:
  // | y^2 coord sum |
  //
  //
  // variance(y) can be calculated with the population variance shortcut
  // formula:
  //   variance(y) = (y^2 coord sum)/num_samples - (y coord sum/num_samples)^2
  //
  // So in this class, the non-duplicate cells listed above are calculated and
  // stored as individual variables.
  //
  // x and z coordinates are in the range [0, 67108864). This class has to sum
  // two coordinates multiplied together. The class needs to support at least
  // 10,000 samples. So many of the variables have to use Int128.
  private int _numSamples = 0;
  private long _xSum = 0;
  private long _ySum = 0;
  private long _zSum = 0;
  private Int128 _xxSum = 0;
  private Int128 _xySum = 0;
  private Int128 _xzSum = 0;
  private Int128 _yySum = 0;
  private Int128 _yzSum = 0;
  private Int128 _zzSum = 0;

  public TerrainHeightLinReg() {}

  public void Add(int x, int y, int z) {
    ++_numSamples;
    _xSum += x;
    _ySum += y;
    _zSum += z;
    _xxSum += (long)x * x;
    _xySum += (long)x * y;
    _xzSum += (long)x * z;
    _yySum += (long)y * y;
    _yzSum += (long)y * z;
    _zzSum += (long)z * z;
  }

  public void Remove(int x, int y, int z) {
    --_numSamples;
    _xSum -= x;
    _ySum -= y;
    _zSum -= z;
    _xxSum -= (long)x * x;
    _xySum -= (long)x * y;
    _xzSum -= (long)x * z;
    _yySum -= (long)y * y;
    _yzSum -= (long)y * z;
    _zzSum -= (long)z * z;
  }

  /// <summary>
  /// The parameter vector. This is the main output of the linear regression.
  /// </summary>
  public (double, double, double) Beta {
    get {
      // B = (X^T * X)^-1 * X^T*y
      //
      // (X^T * X) =
      // | _numSamples  _xSum   _zSum  |
      // |   _xSum      _xxSum  _xzSum |
      // |   _zSum      _xzSum  _zzSum |
      double det = (double)(_numSamples * (_xxSum * _zzSum - _xzSum * _xzSum) +
                            _xSum * (_xzSum * _zSum - _xSum * _zzSum) +
                            _zSum * (_xSum * _xzSum - _xxSum * _zSum));
      if (det == 0) {
        if (_numSamples == 0) {
          // This should not happen, but these parameters are as good as for
          // estimating 0 samples.
          return (0, 0, 0);
        }
        // All of the points are in a straight line. Try performing a linear
        // regression with just the x coords.
        det = (double)(_numSamples * _xxSum - _xSum * _xSum);
        if (det != 0) {
          // (X^T * X)^-1 =
          //  1/det * | _xxSum  -_xSum      |
          //          | -_xSum  _numSamples |
          return ((double)(_xxSum * _ySum - _xSum * _xySum) / det,
                  (double)(_numSamples * _xySum - _xSum * _ySum) / det, 0);
        }
        // All of the x values must be the same. Try performing a linear
        // regression with just the z coords.
        det = (double)(_numSamples * _zzSum - _zSum * _zSum);
        if (det != 0) {
          // (X^T * X)^-1 =
          //  1/det * | _zzSum  -_zSum      |
          //          | -_zSum  _numSamples |
          return ((double)(_zzSum * _ySum - _zSum * _yzSum) / det, 0,
                  (double)(_numSamples * _yzSum - _zSum * _ySum) / det);
        }
        // All of the x and z coordinates must be the same. Just use the y
        // intercept.
        return (_ySum / (double)_numSamples, 0, 0);
      }
      // clang-format off
      // (X^T * X)^-1 =
      //  1/det * | _xxSum*_zzSum-_xzSum*_xzSum  _zSum*_xzSum-_xSum*_zzSum       _xSum*_xzSum-_zSum*_xxSum      |
      //          | _xzSum*_zSum-_xSum*_zzSum    _numSamples*_zzSum-_zSum*_zSum  _zSum*_xSum-_numSamples*_xzSum |
      //          | _xSum*_xzSum-_xxSum*_zSum    _xSum*_zSum-_numSamples*_xzSum  _numSamples*_xxSum-_xSum*_xSum |
      // clang-format on

      Int128 a = _xxSum * _zzSum - _xzSum * _xzSum;
      Int128 b = _zSum * _xzSum - _xSum * _zzSum;
      Int128 c = _xSum * _xzSum - _zSum * _xxSum;
      Int128 e = _numSamples * _zzSum - _zSum * _zSum;
      Int128 f = _zSum * _xSum - _numSamples * _xzSum;
      Int128 g = _numSamples * _xxSum - _xSum * _xSum;

      // X^T*y =
      // | _ySum  |
      // | _xySum |
      // | _yzSum |

      double intercept = (double)(a * _ySum + b * _xySum + c * _yzSum) / det;
      double xparam = (double)(b * _ySum + e * _xySum + f * _yzSum) / det;
      double zparam = (double)(c * _ySum + f * _xySum + g * _yzSum) / det;

      return (intercept, xparam, zparam);
    }
  }

  public double Mean {
    get {
      if (_numSamples == 0) {
        return 0;
      }
      return (double)_ySum / _numSamples;
    }
  }

  public double PopulationVariance {
    get {
      if (_numSamples == 0) {
        return 0;
      }
      // variance(y) = (y^2 coord sum)/num_samples - (y coord sum/num_samples)^2
      double m = Mean;
      return (double)_yySum / _numSamples - m * m;
    }
  }

  public double ResidualSumOfSquares {
    get {
      (double intercept, double xparam, double zparam) = Beta;
      return (double)_yySum - (_ySum * intercept + (double)_xySum * xparam +
                               (double)_yzSum * zparam);
    }
  }

  public double MeanSquareError {
    get {
      if (_numSamples < 2) {
        return 0;
      }
      return ResidualSumOfSquares / (_numSamples - 1);
    }
  }

  public double MeanSquareErrorN {
    get {
      if (_numSamples < 1) {
        return 0;
      }
      return ResidualSumOfSquares / _numSamples;
    }
  }

  public double RSquared {
    get {
      // R^2 = 1 - (y^T * y - (X^T * y)^T * B) / TSS
      //
      // y^T * y = | _yySum |
      //
      // X^T * y = | _ySum  |
      //           | _xySum |
      //           | _yzSum |
      if (_numSamples == 0) {
        return 1;
      }
      double totalSumOfSquares =
          (double)_yySum - (double)(_ySum * _ySum) / _numSamples;
      if (totalSumOfSquares == 0) {
        return 1;
      }

      return 1 - ResidualSumOfSquares / totalSumOfSquares;
    }
  }
}
