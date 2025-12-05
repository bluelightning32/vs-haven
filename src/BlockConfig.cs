using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace Haven;

/// <summary>
/// A json configurable set of blocks.
/// </summary>
public class BlockConfig {
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

  public void Merge(BlockConfig other) {
    TerrainReplace.Merge(other.TerrainReplace);
    ResourceZoneClear.Merge(other.ResourceZoneClear);
    TerrainAvoid.Merge(other.TerrainAvoid);
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
}
