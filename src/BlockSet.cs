using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace Haven;

/// <summary>
/// A json configurable set of blocks.
/// </summary>
public class BlockSet {
  /// <summary>
  /// Include all blocks with a replaceable value at or above this. They are
  /// included at priority 0.
  ///
  /// When merging BlockSets, the lower value wins.
  /// </summary>
  [JsonProperty]
  public int IncludeReplaceable = int.MaxValue;
  /// <summary>
  /// Blocks to include in the set. Due to a bug in the VS json parser, the
  /// default domain is always "game", regardless of what default domain the
  /// json parser is instantiated with.
  /// </summary>
  [JsonProperty]
  public Dictionary<AssetLocation, int> Include = [];
  /// <summary>
  /// Blocks to exclude in the set. Due to a bug in the VS json parser, the
  /// default domain is always "game", regardless of what default domain the
  /// json parser is instantiated with.
  ///
  /// If a block is included and excluded at the same integer priority, then the
  /// exclusion has priority.
  /// </summary>
  [JsonProperty]
  public Dictionary<AssetLocation, int> Exclude = [];

  /// <summary>
  /// Merge the other block set into this one
  /// </summary>
  /// <param name="other">the block set to copy values from. it is not
  /// modified.</param>
  public void Merge(BlockSet other) {
    IncludeReplaceable = int.Min(IncludeReplaceable, other.IncludeReplaceable);
    foreach (KeyValuePair<AssetLocation, int> entry in other.Include) {
      if (Include.TryGetValue(entry.Key, out int existing)) {
        if (existing >= entry.Value) {
          continue;
        }
      }
      Include[entry.Key] = entry.Value;
    }
    foreach (KeyValuePair<AssetLocation, int> entry in other.Exclude) {
      if (Exclude.TryGetValue(entry.Key, out int existing)) {
        if (existing >= entry.Value) {
          continue;
        }
      }
      Exclude[entry.Key] = entry.Value;
    }
  }

  public HashSet<int> Resolve(MatchResolver resolver) {
    Dictionary<int, (bool, int)> blocks = [];
    if (IncludeReplaceable != int.MaxValue) {
      foreach (Block block in resolver.AllBlocks) {
        if (block.Replaceable >= IncludeReplaceable) {
          blocks.Add(block.Id, (true, 0));
        }
      }
    }
    foreach ((AssetLocation wildcard, int priority) in Include) {
      foreach (Block block in resolver.GetMatchingBlocks(wildcard)) {
        if (blocks.TryGetValue(block.Id, out(bool, int) existing)) {
          if (existing.Item2 >= priority) {
            continue;
          }
        }
        blocks[block.Id] = (true, priority);
      }
    }
    foreach ((AssetLocation wildcard, int priority) in Exclude) {
      foreach (Block block in resolver.GetMatchingBlocks(wildcard)) {
        if (blocks.TryGetValue(block.Id, out(bool, int) existing)) {
          if (existing.Item2 > priority) {
            continue;
          }
        }
        blocks[block.Id] = (false, priority);
      }
    }
    HashSet<int> result = [];
    foreach ((int blockId, (bool include, int priority)) in blocks) {
      if (include) {
        result.Add(blockId);
      }
    }
    return result;
  }
}
