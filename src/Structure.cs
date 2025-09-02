using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

public class OffsetBlockSchematic : BlockSchematic {
  [JsonProperty]
  public int OffsetY = 0;
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
