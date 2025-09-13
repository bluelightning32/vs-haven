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

  /// <summary>
  /// Gets the distance between a rectangle centered inside a circle the border
  /// of the circle. The distance is measured along the given unit vector.
  /// </summary>
  /// <param name="rWidth">width of the rectangle</param>
  /// <param name="rHeight">height of the rectangle</param>
  /// <param name="u">unit vector to measure the distance along</param>
  /// <param name="radius">radius of the circle</param>
  /// <returns>the distance between the rectangle and the circle. If the
  /// rectangle is translated this distance along the unit vector, then one of
  /// its corners will touch the circle boundary.</returns>
  public static double GetRectToCircleDist(double rWidth, double rHeight,
                                           Vec2d u, double radius) {
    // Find the rectangle corner which is closest to the circle along the unit
    // vector.
    double x = u.X < 0 ? -rWidth / 2 : rWidth / 2;
    double y = u.Y < 0 ? -rHeight / 2 : rHeight / 2;
    return GetPointToCircleDist(x, y, u, radius);
  }

  /// <summary>
  /// Gets the distance between a rectangle centered inside a circle the border
  /// of the circle. The distance is measured along the given unit vector.
  /// </summary>
  /// <param name="rWidth">width of the rectangle</param>
  /// <param name="rHeight">height of the rectangle</param>
  /// <param name="angle">angle to measure the distance along</param>
  /// <param name="radius">radius of the circle</param>
  /// <returns>the distance between the rectangle and the circle. If the
  /// rectangle is translated this distance along the unit vector, then one of
  /// its corners will touch the circle boundary. Or if the rectangle is larger
  /// than the circle, a negative number is returned.</returns>
  public static double GetRectToCircleDist(double rWidth, double rHeight,
                                           double angle, double radius) {
    (double sin, double cos) = Math.SinCos(angle);
    return GetRectToCircleDist(rWidth, rHeight, new Vec2d(cos, sin), radius);
  }
}
