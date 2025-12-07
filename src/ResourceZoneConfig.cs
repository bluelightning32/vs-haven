using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

public class ResourceZoneConfig {
  [JsonProperty]
  public double MinRadius = 10.0;
  [JsonProperty]
  public double MaxRoughnessPerimeter = 2;
  [JsonProperty]
  public double MaxRoughnessArea = 0.5;
  [JsonProperty]
  public double MinLandRatio = 0.9;
  [JsonProperty]
  public ushort TerrainRaise = 1;

  private IWorldAccessor _worldForResolve = null;
  private ICollection<Structure> _structures = null;
  public Dictionary<int, TerrainCategory> TerrainCategories { get;
                                                              private set; }

  public ResourceZoneConfig() {}

  public void Resolve(ILogger logger, IWorldAccessor worldForResolve,
                      MatchResolver resolver, BlockConfig config) {
    _worldForResolve = worldForResolve;
    List<IAsset> structureAssets =
        _worldForResolve.AssetManager.GetManyInCategory("worldgen",
                                                        "haven/structures/");
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
    TerrainCategories = config.ResolveTerrainCategories(resolver);
  }

  public IEnumerable<OffsetBlockSchematic> SelectStructures(IRandom rand) {
    List<OffsetBlockSchematic> schematics = [];
    foreach (Structure structure in _structures) {
      schematics.AddRange(structure.Select(_worldForResolve, rand));
    }
    return schematics;
  }
}
