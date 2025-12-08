using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven.EntityBehaviors;

/// <summary>
/// Sets the animalSeekingRange stat to a negative value while the entity is in
/// a haven safe zone.
/// </summary>
public class SafeZone : EntityBehavior {
  private readonly BlockPos lastUpdate =
      new BlockPos(0, 0, 0, Dimensions.NormalWorld);
  private readonly HavenSystem _system;
  private bool _wasInSafeZone = false;

  public SafeZone(Entity entity) : base(entity) {
    _system = HavenSystem.GetSystem(entity.Api.ModLoader);
  }

  public override string PropertyName() { return "safeZone"; }

  public override void OnGameTick(float deltaTime) {
    base.OnGameTick(deltaTime);
    int curX = (int)entity.ServerPos.X;
    int curY = (int)entity.ServerPos.Y;
    int curZ = (int)entity.ServerPos.Z;
    int curDim = entity.ServerPos.Dimension;
    if (lastUpdate.X == curX && lastUpdate.Y == curY && lastUpdate.Z == curZ &&
        lastUpdate.dimension == curDim) {
      // The entity is still at the same position. Do not update the stats.
      return;
    }
    lastUpdate.Set(curX, curY, curZ);
    lastUpdate.SetDimension(curDim);

    UpdateStats(lastUpdate);
  }

  private void UpdateStats(BlockPos pos) {
    HavenRegionIntersection intersection = _system.GetHavenIntersection(pos);
    bool inSafeZone =
        intersection?.InSafeZone(pos, _system.ServerConfig.HavenBelowHeight,
                                 _system.ServerConfig.HavenAboveHeight) ??
        false;
    if (_wasInSafeZone == inSafeZone) {
      return;
    }
    _wasInSafeZone = inSafeZone;
    if (inSafeZone) {
      entity.Stats.Set("animalSeekingRange", "haven:safezone", -100);
      HavenSystem.Logger.Audit(
          "Setting animalSeekingRange to -100 for {0} at {1}", entity.GetName(),
          lastUpdate);
    } else {
      entity.Stats.Remove("animalSeekingRange", "haven:safezone");
      HavenSystem.Logger.Audit("Restoring animalSeekingRange for {0} at {1}",
                               entity.GetName(), lastUpdate);
    }
  }
}
