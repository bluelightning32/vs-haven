using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

public class OffsetBlockSchematic : BlockSchematic {
  [JsonProperty]
  public int OffsetY = 0;

  public AABBList _outline = null;

  public void UpdateOutline() {
    _outline = new AABBList(GetJustPositions(new BlockPos(0, 0, 0)));
  }

  public Cuboidi GetOffsetBoundingBox(BlockPos startPos) {
    Cuboidi result =
        new Cuboidi(startPos, startPos.AddCopy(SizeX, SizeY, SizeZ));
    result.Y1 += OffsetY;
    result.Y2 += OffsetY;
    return result;
  }

  /// <summary>
  /// Determines if there is an intersection between two schematics.
  /// </summary>
  /// <param name="startPos">the start location of this schematic</param>
  /// <param name="with">the other schematic to perform the intersection test
  /// with</param>
  /// <param name="withStartPos">the start location of the other
  /// schematic</param>
  /// <returns>a cuboid from the other schematic that intersects with this one,
  /// or null.</returns>
  public Cuboidi Intersects(BlockPos startPos, OffsetBlockSchematic with,
                            BlockPos withStartPos) {
    Vec3i withOffset = withStartPos.SubCopy(startPos).AsVec3i;
    withOffset.Y += with.OffsetY - OffsetY;
    if (!GetOffsetBoundingBox(startPos).Intersects(
            with.GetOffsetBoundingBox(withStartPos))) {
      return null;
    }
    Cuboidi result = _outline.Intersects(with._outline, withOffset);
    if (result == null) {
      return result;
    }
    return result.OffsetCopy(0, with.OffsetY, 0);
  }

  /// <summary>
  /// Moves startPos in direction to avoid an intersection with another
  /// schematic
  /// </summary>
  /// <param name="startPos"></param>
  /// <param name="with"></param>
  /// <param name="withStartPos"></param>
  /// <param name="direction">direction to move the schematic to avoid an
  /// intersection. This should be a unit vector</param>
  /// <returns>true if there was no intersection, or false if startPos was
  /// updated to avoid an intersection</returns>
  public bool AvoidIntersection(BlockPos startPos, OffsetBlockSchematic with,
                                BlockPos withStartPos, Vec3d direction) {
    Vec3i withOffset = withStartPos.SubCopy(startPos).AsVec3i;
    withOffset.Y += with.OffsetY - OffsetY;
    if (!GetOffsetBoundingBox(startPos).Intersects(
            with.GetOffsetBoundingBox(withStartPos))) {
      return true;
    }
    if (_outline.AvoidIntersection(with._outline, withOffset, direction * -1)) {
      return true;
    }
    withOffset.Y -= with.OffsetY - OffsetY;
    withOffset.X -= withStartPos.X;
    withOffset.Y -= withStartPos.Y;
    withOffset.Z -= withStartPos.Z;
    startPos.X = -withOffset.X;
    startPos.Y = -withOffset.Y;
    startPos.Z = -withOffset.Z;
    return false;
  }
}

public class SchematicData {
  [JsonProperty]
  public AssetLocation Schematic;
  [JsonProperty]
  public int OffsetY = 0;

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
    return resolved;
  }
}

public class Structure {
  // Name of this structure
  [JsonProperty]
  public readonly AssetLocation Code;
  [JsonProperty]
  public readonly Dictionary<string, SchematicData> Schematics;
  [JsonProperty]
  public readonly NatFloat Count;

  private List<SchematicData> Resolve(IAssetManager assetManager) {
    List<SchematicData> result = [];
    List<string> remove = null;
    foreach (KeyValuePair<string, SchematicData> entry in Schematics) {
      if (entry.Value.Resolve(assetManager) == null) {
        HavenSystem.Logger.Error(
            $"Unable to resolve schematic {entry.Key} referenced in {Code}.");
        remove ??= [];
        remove.Add(entry.Key);
      } else {
        result.Add(entry.Value);
      }
    }
    if (remove != null) {
      foreach (string key in remove) {
        Schematics.Remove(key);
      }
    }
    return result;
  }

  public IEnumerable<OffsetBlockSchematic> Select(IAssetManager assetManager,
                                                  IRandom rand) {
    List<SchematicData> available = Resolve(assetManager);
    int remaining = (int)Count.nextFloat(1, rand);
    while (remaining > 0) {
      int index = rand.NextInt(Schematics.Count);
      SchematicData schematic = available[index];
      OffsetBlockSchematic resolved = schematic.Resolve(assetManager);
      if (resolved == null) {
        HavenSystem.Logger.Error(
            $"Unable to resolve schematic {schematic.Schematic} referenced in {Code}.");
        // This continue is completely unexpected. The Resolve method already
        // verified that this entry was resolvable. So just exit here even if
        // other structures could be generated.
        yield break;
      }
      yield return resolved;
      --remaining;
    }
  }
}
