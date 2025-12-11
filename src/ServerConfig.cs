using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace Haven;

public class ServerConfig {
  [JsonProperty]
  public double HavenChancePerRegion = 0.05;

  [JsonProperty]
  public int HavenAboveHeight = 50;
  [JsonProperty]
  public int HavenBelowHeight = 50;

  [JsonProperty]
  public int HavenRadius = 30;

  [JsonProperty]
  public int BlocksPerPlot = 10;
  /// <summary>
  /// The number of blocks between plots. This setting exists to ensure there is
  /// enough space to walk to and from the haven even if players put fences in
  /// their plots.
  /// </summary>
  [JsonProperty]
  public int PlotBorderWidth = 2;

  [JsonProperty]
  public ResourceZoneConfig ResourceZone = new();

  /// <summary>
  /// The number of plots each player can claim in each haven
  /// </summary>
  [JsonProperty]
  public int PlotsPerPlayer = 1;

  public void Resolve(ILogger logger, IWorldAccessor worldForResolve,
                      MatchResolver resolver, BlockConfig config) {
    ResourceZone.Resolve(logger, worldForResolve, resolver, config);
  }
}
