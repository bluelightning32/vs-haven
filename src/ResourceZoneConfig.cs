using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

public class ResourceZoneConfig {
  [JsonProperty]
  public double MinRadius = 10.0;
  [JsonProperty]
  public double MaxRoughnessPerimeter = 3;
  [JsonProperty]
  public double MaxRoughnessArea = 0.75;
  [JsonProperty]
  public double MinAboveSea = 0.9;

  private IAssetManager _assetManager = null;
  private ICollection<Structure> _structures = null;

  public ResourceZoneConfig() {}

  public void Resolve(ILogger logger, IAssetManager assetManager) {
    _assetManager = assetManager;
    List<IAsset> structureAssets =
        assetManager.GetManyInCategory("worldgen", "havenstructures/");
    _structures = [];
    foreach (IAsset asset in structureAssets) {
      try {
        _structures.Add(asset.ToObject<Structure>());
      } catch (JsonReaderException val) {
        JsonReaderException e = val;
        logger.Error("Syntax error in json file '{0}': {1}", asset, e.Message);
      }
    }
    logger.Event($"Loaded {_structures.Count} haven structures.");
  }

  public IEnumerable<OffsetBlockSchematic> SelectStructures(IRandom rand) {
    List<OffsetBlockSchematic> schematics = [];
    foreach (Structure structure in _structures) {
      schematics.AddRange(structure.Select(_assetManager, rand));
    }
    return schematics;
  }
}
