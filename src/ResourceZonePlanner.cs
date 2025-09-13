using System;

using Vintagestory.API.MathTools;

namespace Haven;

public class ResourceZonePlanner {
  /// <summary>
  /// Finds the distance between a point (x,y) inside a circle and the boundary
  /// of the circle, when the point is translated at the given angle.
  /// </summary>
  /// <param name="angle">angle to translate the point</param>
  /// <param name="radius">radius of the circle</param>
  /// <param name="x">x coordinate of the point to translate</param>
  /// <param name="y">y coordinate of the point to translate</param>
  /// <returns>distance</returns>
  public static double GetPointToCircleDist(double x, double y, double angle,
                                            double radius) {
    // This is based on
    // https://mathworld.wolfram.com/Circle-LineIntersection.html.
    (double sin, double cos) = Math.SinCos(angle);
    return GetPointToCircleDist(x, y, new Vec2d(cos, sin), radius);
  }

  /// <summary>
  /// Finds the distance between a point (x,y) inside a circle and the boundary
  /// of the circle, when the point is translated along the given unit vector.
  /// </summary>
  /// <param name="u">unit vector to translate the point along</param>
  /// <param name="radius">radius of the circle</param>
  /// <param name="x">x coordinate of the point to translate</param>
  /// <param name="y">y coordinate of the point to translate</param>
  /// <returns>distance</returns>
  public static double GetPointToCircleDist(double x, double y, Vec2d u,
                                            double radius) {
    // This is based on
    // https://mathworld.wolfram.com/Circle-LineIntersection.html.
    double d = x * u.Y - y * u.X;
    return Math.Sqrt(radius * radius - d * d) - x * u.X - y * u.Y;
  }
}
