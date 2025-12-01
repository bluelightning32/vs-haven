using System;

using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace Haven;

public class TerrainProbe : IEquatable<TerrainProbe> {
  [JsonProperty]
  public int X;
  [JsonProperty]
  public int Z;

  /// <summary>
  /// Minimum allowed Y value of the surface of the terrain at the X, Z
  /// location. This is measured after the OffsetY is applied.
  /// </summary>
  [JsonProperty]
  public int YMin;
  /// <summary>
  /// One greater than the maximum allowed Y value of the surface of the terrain
  /// at the X, Z location. This is measured after the OffsetY is applied.
  /// </summary>
  [JsonProperty]
  public int YMax;

  public bool Equals(TerrainProbe other) {
    return X == other.X && Z == other.Z && YMin == other.YMin &&
           YMax == other.YMax;
  }

  public override bool Equals(object obj) {
    if (obj is TerrainProbe other) {
      return Equals(other);
    } else {
      return false;
    }
  }

  public override int GetHashCode() { return X ^ (Z << 10); }

  public override string ToString() { return JsonUtil.ToPrettyString(this); }
}

public class SchematicData {
  [JsonProperty]
  public AssetLocation Schematic;
  [JsonProperty]
  public int OffsetY = 0;
  /// <summary>
  /// Manually specified terrain probe points. These are used to ensure that
  /// the terrain where the structure is placed is sufficently flat.
  ///
  /// If this is null, then probes are automatically selected. The automatically
  /// selected probes will be on the perimeter of the object. This perimeter
  /// only counts blocks at the y=0 level (after OffsetY is applied) and below.
  ///
  /// YMin is set to min(-1, OffsetY-1). So when yoffset=0, the terrain must be
  /// at least 1 block below where the structure will be placed. When
  /// yoffset=-1, the terrain must be at least 1 block below the surface level.
  ///
  /// YMax is set to one plus the y coordinate of the highest block in that
  /// contiguous column (starting at y=0 after OffsetY is applied) of the
  /// structure, or max(0, OffsetY) if there is no block at y=0.
  ///
  /// Example. D is dirt. * are blocks from the schematic.
  /// <pre>
  ///   Y
  ///   1    *
  ///   0   **
  ///  -1 D***D
  ///  -2 *DDDD <-- OffsetY=-2
  ///  -3 DDDDD
  ///     |||\---- YMin=-3, YMax=2
  ///     || \---- YMin=-3, YMax=1
  ///     | \----- YMin=-3, YMax=0
  ///      \------ YMin=-3, YMax=0
  /// </pre>
  /// </summary>
  [JsonProperty]
  public TerrainProbe[] Probes = null;

  public OffsetBlockSchematic Resolve(IAssetManager assetManager) {
    string path = Schematic.WithPathPrefixOnce("worldgen/schematics/")
                      .WithPathAppendixOnce(".json");
    OffsetBlockSchematic resolved =
        assetManager.Get(path).ToObject<OffsetBlockSchematic>();
    if (resolved == null) {
      return null;
    }
    resolved.OffsetY = OffsetY;
    resolved.UpdateOutline();
    if (Probes != null) {
      resolved.Probes = Probes;
    } else {
      resolved.AutoConfigureProbes();
    }
    return resolved;
  }
}
