using System;
using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Haven;

[ProtoContract]
public class HavenRegionIntersection {
  [ProtoMember(1)]
  public BlockPos Center;
  [ProtoMember(2)]
  public int ResourceZoneRadius;
  [ProtoMember(3)]
  public int Radius;

  public HavenRegionIntersection Copy() {
    byte[] data = SerializerUtil.Serialize(this);
    return SerializerUtil.Deserialize<HavenRegionIntersection>(data);
  }

  public override string ToString() {
    return $"center: {Center}, resourceZoneRadius: {ResourceZoneRadius}, radius: {Radius}";
  }

  public static bool CircleIntersectsRect(int cx, int cy, int radius,
                                          Rectanglei rect) {
    // Half the size of the rectangle.
    double rHalfSizeX = (rect.X2 - rect.X1) / 2.0d;
    double rHalfSizeY = (rect.Y2 - rect.Y1) / 2.0d;

    // The rectangle's center position.
    double rCenterX = rect.X1 + rHalfSizeX;
    double rCenterY = rect.Y1 + rHalfSizeY;

    // Distance from the circle's center to the nearest north-south edge of the
    // rectangle.
    double dx = Math.Abs(cx - rCenterX) - rHalfSizeX;
    // Distance from the circle's center to the nearest east-west edge of the
    // rectangle.
    double dy = Math.Abs(cy - rCenterY) - rHalfSizeY;

    if (dx > 0) {
      // The circle's center is outside of the rectangle in the x coordinate.
      if (dy > 0) {
        // The circle's center is outside of the rectangle in the y coordinate.
        // See if the rectangle's corner intersects the circle.
        return dx * dx + dy * dy <= radius * radius;
      } else {
        // Check if the circle's left side intersects the rectangle.
        return dx <= radius;
      }
    } else {
      // Check if the circle's south side intersects the rectangle, or if the
      // circle's center is inside the rectangle.
      return dy <= radius;
    }
  }

  public bool IntersectsMapRegion(int regionX, int regionZ, int regionSize) {
    int zBlock = regionZ * regionSize;
    int xBlock = regionX * regionSize;
    return CircleIntersectsRect(
        Center.X, Center.Z, Radius,
        new(xBlock, zBlock, xBlock + regionSize, zBlock + regionSize));
  }

  public static IEnumerable<Vec2i> GetRegions(BlockPos center, int radius,
                                              int regionSize) {
    int startX = (center.X - radius) / regionSize;
    int endX = (center.X + radius) / regionSize;
    int startZ = (center.Z - radius) / regionSize;
    int endZ = (center.Z + radius) / regionSize;
    for (int z = startZ; z <= endZ; ++z) {
      int zBlock = z * regionSize;
      for (int x = startX; x <= endX; ++x) {
        int xBlock = x * regionSize;
        if (CircleIntersectsRect(center.X, center.Z, radius,
                                 new(xBlock, zBlock, xBlock + regionSize,
                                     zBlock + regionSize))) {
          yield return new Vec2i(x, z);
        }
      }
    }
  }

  public IEnumerable<Vec2i> GetRegions(int regionSize) {
    return GetRegions(Center, Radius, regionSize);
  }

  public bool Contains(BlockPos pos, int havenAboveHeight,
                       int havenBelowHeight) {
    int dx = pos.X - Center.X;
    int dz = pos.Z - Center.Z;
    if (dx * dx + dz * dz > Radius * Radius) {
      return false;
    }
    if (pos.Y < Center.Y - havenBelowHeight) {
      return false;
    }
    if (pos.Y >= Center.Y + havenAboveHeight) {
      return false;
    }
    return true;
  }
}
