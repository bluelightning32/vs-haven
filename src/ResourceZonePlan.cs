using System;
using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

public class ResourceZonePlan {
  public ResourceZonePlan(BlockPos center, double minRadius,
                          IAssetManager assetManager, IRandom rand,
                          IEnumerable<OffsetBlockSchematic> structures) {}

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
  /// Gets the distance between a rectangle centered inside a circle and the
  /// border of the circle. The distance is measured along the given unit
  /// vector.
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
  /// Gets the distance between a rectangle centered inside a circle and the
  /// border of the circle. The distance is measured along the given unit
  /// vector.
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

  /// <summary>
  /// Randomly select a point within the circle such that the rectangle fits
  /// within the circle with that point as its center. The given rectangle must
  /// fit within the circle.
  /// </summary>
  /// <param name="rand">random number generator</param>
  /// <param name="rWidth">width of the rectangle</param>
  /// <param name="rHeight">height of the rectangle</param>
  /// <param name="radius">radius of the circle</param>
  /// <returns>randomly selected center position for the rectangle</returns>
  public static Vec2d GetRandomRectCenterInCircle(IRandom rand, double rWidth,
                                                  double rHeight,
                                                  double radius) {
    // Let x be a uniformly random number between 0 to 1. If every point within
    // a circle of area a could be enumerated in order, then P(x*a) would be a
    // uniformly random point in the circle.
    //
    // Let's say the points of the circle are enumerated starting from the
    // center of the circle and spirling out. Then the following equation shows
    // P(x*a)'s distance from the center of the circle.
    //   x * a = pi * s^2
    //   x * a / pi = s^2
    //   s = sqrt(x * a / pi)
    // Now if a is calculated as a = pi * r^2, then s is:
    //   s = sqrt(x * (pi * r^2) / pi)
    //   s = sqrt(x * r^2)
    //   s = sqrt(x) * r
    //
    // So a random point in the circle can be selected by first picking a random
    // angle, then picking a distance from the center of the circle with sqrt(x)
    // * r.
    double angle = rand.NextDouble() * Math.Tau;
    (double sin, double cos) = Math.SinCos(angle);
    Vec2d u = new(cos, sin);
    // Find how far the rectangle can be moved in that direction without leaving
    // the circle. This is used to effectively shrink the radius used to select
    // the rectangle center position.
    double adjustedRadius = GetRectToCircleDist(rWidth, rHeight, u, radius);
    double centerDist = Math.Sqrt(rand.NextDouble()) * adjustedRadius;
    return u * centerDist;
  }
}
