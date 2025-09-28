using Vintagestory.API.MathTools;

namespace Haven;

public static class CuboidiExtensions {
  public static bool Intersects(this Cuboidi cube, Cuboidi with,
                                Vec3i withOffset) {
    if (with.MaxX + withOffset.X <= cube.MinX ||
        with.MinX + withOffset.X >= cube.MaxX) {
      return false;
    }

    if (with.MaxY + withOffset.Y <= cube.MinY ||
        with.MinY + withOffset.Y >= cube.MaxY) {
      return false;
    }

    if (with.MaxZ + withOffset.Z > cube.MinZ) {
      return with.MinZ + withOffset.Z < cube.MaxZ;
    }

    return false;
  }
}
