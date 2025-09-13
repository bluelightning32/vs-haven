using System;

namespace Haven;

public class ResourceZonePlanner {
  /// <summary>
  /// Finds the distance between a point (x,y) inside a circle and the boundary
  /// of the circle, when the point is moved at the given angle.
  /// </summary>
  /// <param name="angle">angle to move the rectangle</param>
  /// <param name="radius">radius of the circle</param>
  /// <param name="x">x coordinate of a point on the rectangle</param>
  /// <param name="y">y coordinate of a point on the rectangle</param>
  /// <returns></returns>
  public static double GetPointToCircleDist(double angle, double radius,
                                           double x, double y) {
    // This is based on
    // https://mathworld.wolfram.com/Circle-LineIntersection.html.
    (double sin, double cos) = Math.SinCos(angle);
    double d = x * sin - y * cos;
    return Math.Sqrt(radius * radius - d * d) - x * cos - y * sin;
  }
}
