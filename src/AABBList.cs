using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.MathTools;

namespace Haven;

/// <summary>
/// Contains a list of block positions. This data structure is optimized for the
/// case where most block positions are adjacent such that multiple can be
/// stored with an adjacent aligned bounding box.
/// </summary>
public class AABBList {
  public List<Cuboidi> Regions { get; private set; }
  public AABBList() { Regions = []; }

  public AABBList(IEnumerable<BlockPos> positions) {
    HashSet<BlockPos> remainingPositions = [..positions];
    Regions = [];
    while (remainingPositions.Count > 0) {
      BlockPos first = remainingPositions.First();
      Cuboidi region = new(first, 1);
      remainingPositions.Remove(first);
      int availableDirections = 0x3F;
      while (availableDirections > 0) {
        if ((availableDirections & 0x1) != 0) {
          if (!TryGrowX(region, true, remainingPositions)) {
            availableDirections &= ~0x1;
          }
        }
        if ((availableDirections & 0x2) != 0) {
          if (!TryGrowX(region, false, remainingPositions)) {
            availableDirections &= ~2;
          }
        }
        if ((availableDirections & 0x4) != 0) {
          if (!TryGrowY(region, true, remainingPositions)) {
            availableDirections &= ~0x4;
          }
        }
        if ((availableDirections & 0x8) != 0) {
          if (!TryGrowY(region, false, remainingPositions)) {
            availableDirections &= ~0x8;
          }
        }
        if ((availableDirections & 0x10) != 0) {
          if (!TryGrowZ(region, true, remainingPositions)) {
            availableDirections &= ~0x10;
          }
        }
        if ((availableDirections & 0x20) != 0) {
          if (!TryGrowZ(region, false, remainingPositions)) {
            availableDirections &= ~0x20;
          }
        }
      }
      for (int x = region.MinX; x < region.MaxX; ++x) {
        for (int y = region.MinY; y < region.MaxY; ++y) {
          for (int z = region.MinZ; z < region.MaxZ; ++z) {
            remainingPositions.Remove(new BlockPos(x, y, z));
          }
        }
      }
      Regions.Add(region);
    }
  }

  public bool Contains(BlockPos pos) {
    foreach (Cuboidi region in Regions) {
      if (region.Contains(pos)) {
        return true;
      }
    }
    return false;
  }

  /// <summary>
  /// Determines if this AABB list intersects with another one.
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset">an offset to translate other when doing the
  /// intersection</param> <returns>the intersecting AABB from this AABB list
  /// that intersects, or null if there is no intersection</returns>
  public Cuboidi Intersects(Cuboidi with, Vec3i withOffset) {
    foreach (Cuboidi region in Regions) {
      if (region.Intersects(with, withOffset)) {
        return region;
      }
    }
    return null;
  }

  /// <summary>
  /// Determines if this AABB list intersects with another one.
  /// </summary>
  /// <param name="with"></param>
  /// <param name="withOffset">an offset to translate other when doing the
  /// intersection</param> <returns>the intersecting AABB from this AABB list
  /// that intersects, or null if there is no intersection</returns>
  public Cuboidi Intersects(AABBList with, Vec3i withOffset) {
    foreach (Cuboidi withRegion in with.Regions) {
      Cuboidi intersection = Intersects(withRegion, withOffset);
      if (intersection != null) {
        return intersection;
      }
    }
    return null;
  }

  private static bool TryGrowX(Cuboidi region, bool grow,
                               HashSet<BlockPos> remaining) {
    int x = grow ? region.MaxX : region.MinX - 1;
    for (int y = region.MinY; y < region.MaxY; ++y) {
      for (int z = region.MinZ; z < region.MaxZ; ++z) {
        if (!remaining.Contains(new BlockPos(x, y, z))) {
          return false;
        }
      }
    }
    if (grow) {
      region.X2++;
    } else {
      region.X1--;
    }
    return true;
  }

  private static bool TryGrowY(Cuboidi region, bool grow,
                               HashSet<BlockPos> remaining) {
    int y = grow ? region.MaxY : region.MinY - 1;
    for (int x = region.MinX; x < region.MaxX; ++x) {
      for (int z = region.MinZ; z < region.MaxZ; ++z) {
        if (!remaining.Contains(new BlockPos(x, y, z))) {
          return false;
        }
      }
    }
    if (grow) {
      region.Y2++;
    } else {
      region.Y1--;
    }
    return true;
  }

  private static bool TryGrowZ(Cuboidi region, bool grow,
                               HashSet<BlockPos> remaining) {
    int z = grow ? region.MaxZ : region.MinZ - 1;
    for (int x = region.MinX; x < region.MaxX; ++x) {
      for (int y = region.MinY; y < region.MaxY; ++y) {
        if (!remaining.Contains(new BlockPos(x, y, z))) {
          return false;
        }
      }
    }
    if (grow) {
      region.Z2++;
    } else {
      region.Z1--;
    }
    return true;
  }
}
