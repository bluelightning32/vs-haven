using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace Haven;

/// <summary>
/// A json configurable set of blocks.
/// </summary>
public class BlockConfig {
  /// <summary>
  /// When structures are auto generated in the plot zone, they can replace
  /// these blocks. TerrainReplace is merged into this.
  /// </summary>
  [JsonProperty]
  public BlockSet PlotZoneReplace = new();

  /// <summary>
  /// These blocks are removed from the surface of the plot zone when a plot is
  /// claimed. ResourceZoneClear is merged into this.
  /// </summary>
  [JsonProperty]
  public BlockSet PlotZoneClear = new();

  /// <summary>
  /// Avoid placing structures on top of these blocks. Internally if one of
  /// these blocks is found on the surface, then that position is marked as
  /// non-solid. TerrainAvoid is merged into this.
  /// </summary>
  [JsonProperty]
  public BlockSet PlotZoneAvoid = new();

  /// <summary>
  /// When structures are auto generated in the resource zone, they can replace
  /// these blocks.
  /// </summary>
  [JsonProperty]
  public BlockSet TerrainReplace = new();

  /// <summary>
  /// These blocks are removed from the surface of the resource zone.
  /// </summary>
  [JsonProperty]
  public BlockSet ResourceZoneClear = new();

  /// <summary>
  /// Avoid placing structures on top of these blocks. Internally if one of
  /// these blocks is found on the surface, then that position is marked as
  /// non-solid.
  /// </summary>
  [JsonProperty]
  public BlockSet TerrainAvoid = new();
  /// <summary>
  /// These blocks are solid and count as the surface. They can be raised with
  /// the terrain, but the cannot be duplicated to raise the terrain (only
  /// moved).
  /// </summary>
  [JsonProperty]
  public BlockSet TerrainSolid = new();
  /// <summary>
  /// These blocks are solid, count as the surface, and can be duplicated to
  /// raise the terrain.
  /// </summary>
  [JsonProperty]
  public BlockSet TerrainRaiseStart = new();
  /// <summary>
  /// If there are two of these blocks stacked up, then it is marked as a cliff.
  /// The player can place another block (from this set) next to the cliff to
  /// smooth it out, even if the haven otherwise would prevent the block
  /// placement.
  /// </summary>
  [JsonProperty]
  public BlockSet Cliff = new();

  public void Merge(BlockConfig other) {
    PlotZoneReplace.Merge(other.PlotZoneReplace);
    PlotZoneClear.Merge(other.PlotZoneClear);
    PlotZoneAvoid.Merge(other.PlotZoneAvoid);

    TerrainReplace.Merge(other.TerrainReplace);
    ResourceZoneClear.Merge(other.ResourceZoneClear);
    TerrainAvoid.Merge(other.TerrainAvoid);
    TerrainSolid.Merge(other.TerrainSolid);
    TerrainRaiseStart.Merge(other.TerrainRaiseStart);
    Cliff.Merge(other.Cliff);
  }

  public static BlockConfig Load(ILogger logger, IAssetManager assetManager) {
    List<IAsset> assets =
        assetManager.GetManyInCategory("worldgen", "haven/blockconfig/");
    BlockConfig result = new();
    foreach (IAsset asset in assets) {
      try {
        result.Merge(asset.ToObject<BlockConfig>());
      } catch (JsonReaderException val) {
        JsonReaderException e = val;
        logger.Error("Syntax error in json file '{0}': {1}", asset, e.Message);
      }
    }
    return result;
  }

  public Dictionary<int, TerrainCategory>
  ResolveResourceZoneCategories(MatchResolver resolver) {
    Dictionary<int, TerrainCategory> result = [];
    foreach (int id in TerrainSolid.Resolve(resolver)) {
      result[id] = TerrainCategory.Solid;
    }
    foreach (int id in TerrainRaiseStart.Resolve(resolver)) {
      result[id] = TerrainCategory.RaiseStart;
    }
    foreach (int id in TerrainAvoid.Resolve(resolver)) {
      result[id] = TerrainCategory.Nonsolid;
    }
    foreach (int id in TerrainReplace.Resolve(resolver)) {
      result[id] = TerrainCategory.Skip;
    }
    foreach (int id in ResourceZoneClear.Resolve(resolver)) {
      result[id] = TerrainCategory.Clear;
    }
    return result;
  }

  public Dictionary<int, TerrainCategory>
  ResolvePlotZoneCategories(MatchResolver resolver) {
    PlotZoneReplace.Merge(TerrainReplace);
    PlotZoneClear.Merge(ResourceZoneClear);
    PlotZoneAvoid.Merge(TerrainAvoid);

    Dictionary<int, TerrainCategory> result = [];
    foreach (int id in TerrainSolid.Resolve(resolver)) {
      result[id] = TerrainCategory.Solid;
    }
    foreach (int id in TerrainRaiseStart.Resolve(resolver)) {
      result[id] = TerrainCategory.RaiseStart;
    }
    foreach (int id in PlotZoneAvoid.Resolve(resolver)) {
      result[id] = TerrainCategory.Nonsolid;
    }
    foreach (int id in PlotZoneReplace.Resolve(resolver)) {
      result[id] = TerrainCategory.Skip;
    }
    foreach (int id in PlotZoneClear.Resolve(resolver)) {
      result[id] = TerrainCategory.Clear;
    }
    return result;
  }

  public HashSet<int> ResolveCliff(MatchResolver resolver) {
    return Cliff.Resolve(resolver);
  }
}
