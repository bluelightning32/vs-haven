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
  public double HavenRadius = 100.0;

  [JsonProperty]
  public int BlocksPerPlot = 10;
  [JsonProperty]
  public string PlotBlockDenylist = "";

  [JsonProperty]
  public ResourceZoneConfig ResourceZone = new();

  public void Resolve(ILogger logger, IWorldAccessor worldForResolve) {
    ResourceZone.Resolve(logger, worldForResolve);
  }
}
