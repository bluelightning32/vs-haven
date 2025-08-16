using System;
using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Haven.BlockEntityBehaviors;

/// <summary>
/// Tracks the per player status (harvested or ripe) of a dispenser block.
/// </summary>
public class Dispenser : BlockEntityBehavior {
  /// <summary>
  /// Dictionary of player uid string to renewal time in hours.
  ///
  /// This dictionary contains the players for which the dispener may be
  /// depleted. If the player is not in this dictionary, then the block is
  /// harvestable for them. If the player is in the dictionary, then the block
  /// is harvestable if the current game hour time is greater than the value in
  /// the dictionary.
  ///
  /// This is accessed from both the main thread and the render thread. So lock
  /// it before accessing it.
  /// </summary>
  readonly Dictionary<string, double> _renewalTimeByPlayer = [];
  readonly BlockBehaviors.Dispenser _blockBehavior = null;
  readonly Action<double> _redrawListener = null;

  public Dispenser(BlockEntity blockentity) : base(blockentity) {
    _blockBehavior = Block.GetBehavior<BlockBehaviors.Dispenser>();
    _redrawListener = Redraw;
  }

  public override void
  FromTreeAttributes(ITreeAttribute tree,
                     IWorldAccessor worldAccessForResolve) {
    base.FromTreeAttributes(tree, worldAccessForResolve);
    ICoreClientAPI capi = Api as ICoreClientAPI;
    lock (_renewalTimeByPlayer) {
      double myOldRenewal = 0;
      if (capi != null) {
        // GetRenewalHours reacquires the same lock. That's okay, because C#
        // locks are reentrant.
        myOldRenewal = GetRenewalHours(capi.World.Player.PlayerUID);
      }
      _renewalTimeByPlayer.Clear();
      double now = 0;
      if (Api != null) {
        now = Api.World.Calendar.TotalHours;
      }
      foreach (KeyValuePair<string, IAttribute> renewal in tree
                   .GetTreeAttribute("renewalTimeByPlayer")) {
        // Trim expired entries when loading from disk or from the server.
        double entryRenewal = (renewal.Value as DoubleAttribute).value;
        if (entryRenewal <= now) {
          continue;
        }
        _renewalTimeByPlayer[renewal.Key] = entryRenewal;
      }
      if (capi != null) {
        // Check if the redraw schedule needs to be adjusted.
        double myNewRenewal = GetRenewalHours(capi.World.Player.PlayerUID);
        if (myOldRenewal != myNewRenewal) {
          CallbackScheduler scheduler =
              capi.ModLoader.GetModSystem<HavenSystem>().Scheduler;
          scheduler.Cancel(myOldRenewal, _redrawListener);
          if (myNewRenewal != 0) {
            scheduler.Schedule(myNewRenewal, _redrawListener);
          }
        }
      }
    }
  }

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    ITreeAttribute renewalTree =
        tree.GetOrAddTreeAttribute("renewalTimeByPlayer");
    double now = Api.World.Calendar.TotalHours;
    List<string> toRemove = new();
    lock (_renewalTimeByPlayer) {
      foreach (KeyValuePair<string, double> renewal in _renewalTimeByPlayer) {
        if (renewal.Value > now) {
          renewalTree.SetDouble(renewal.Key, renewal.Value);
        } else {
          toRemove.Add(renewal.Key);
        }
      }
      // Trim expired entries from memory when saving to disk.
      foreach (string player in toRemove) {
        _renewalTimeByPlayer.Remove(player);
      }
    }
  }

  public override bool OnTesselation(ITerrainMeshPool mesher,
                                     ITesselatorAPI tessThreadTesselator) {
    if (Api is ICoreClientAPI capi) {
      double renewal = GetRenewalHours(capi.World.Player.PlayerUID);
      if (renewal > capi.World.Calendar.TotalHours && _blockBehavior != null) {
        if (_blockBehavior.RenderHavestedBlock(mesher, tessThreadTesselator,
                                               Pos)) {
          return true;
        }
      }
    }
    return base.OnTesselation(mesher, tessThreadTesselator);
  }

  public override void OnBlockUnloaded() {
    base.OnBlockUnloaded();

    if (Api is ICoreClientAPI capi) {
      double renewal = GetRenewalHours(capi.World.Player.PlayerUID);
      CallbackScheduler scheduler =
          capi.ModLoader.GetModSystem<HavenSystem>().Scheduler;
      scheduler.Cancel(renewal, _redrawListener);
    }
  }

  public double GetRenewalHours(string player) {
    lock (_renewalTimeByPlayer) {
      if (_renewalTimeByPlayer.TryGetValue(player, out double renewal)) {
        return renewal;
      }
    }
    return 0;
  }

  public void SetRenewalHours(string player, double value) {
    lock (_renewalTimeByPlayer) { _renewalTimeByPlayer[player] = value; }
    Blockentity.MarkDirty(true);
  }

  private void Redraw(double gameHours) {
    Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }
}
