using System;

namespace Haven;

public class ResourceZonePlanner {
  /// <summary>
  /// Find the adjusted radius of the circle such that moving the rectangle out
  /// that far in the given angle results in the given point on the rectangle
  /// touching the circle.
  /// </summary>
  /// <param name="angle">angle to move the rectangle</param>
  /// <param name="radius">radius of the circle</param>
  /// <param name="x">x coordinate of a point on the rectangle</param>
  /// <param name="y">y coordinate of a point on the rectangle</param>
  /// <returns></returns>
  public static double GetAdjustedRadius(double angle, double radius, double x,
                                         double y) {
    // This is based on
    // https://mathworld.wolfram.com/Circle-LineIntersection.html.
    (double sin, double cos) = Math.SinCos(angle);
    double d = x * sin - y * cos;
    return Math.Sqrt(radius * radius - d * d) - x * cos - y * sin;
  }
}
