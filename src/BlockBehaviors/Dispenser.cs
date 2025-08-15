using System;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Haven.BlockBehaviors;

/// <summary>
/// Dispenses out resources to each player that harvests the block, by right
/// clicking or breaking the block. The block type should use Delegate class in
/// order for the right click and block breaking times to match. However, the
/// break to harvest functionality will still work (with a duration depending on
/// the block resistance) without the Delegate block class.
/// </summary>
public class Dispenser : BlockBehaviorHarvestable, IBlockBreaking {
  // Dispenser's own copy of harvestTime, because
  // BlockBehaviorHarvestable.harvestTime is private.
  private float _harvestTime;
  private BlockSelection _lastSelection = null;
  private ICoreAPI _api;

  public Dispenser(Block block) : base(block) {}

  public override void Initialize(JsonObject properties) {
    if (properties["harvestedBlockCode"].AsString() != null) {
      throw new ArgumentException("The Dispenser behavior should not have " +
                                  "harvestedBlockCode set. Found set on " +
                                  block.Code + ".");
    }
    base.Initialize(properties);
    _harvestTime = properties["harvestTime"].AsFloat(0);
    if (harvestedStacks == null) {
      throw new ArgumentNullException(
          $"harvestedStacks is null for block {block.Code}");
    }
  }

  public override void OnLoaded(ICoreAPI api) {
    // BlockBehaviorHarvestable.OnLoaded would log a warning that
    // harvestedBlockCode could not be resolved (because it is null). So skip
    // calling it.
    //
    // BlockBehaviorHarvestable.OnLoaded would also call its base's
    // implementation of OnLoaded, which is CollectibleBehavior.OnLoaded. C#
    // doesn't have any clean way of skipping the parent's implementation and
    // calling the grandparent's method. Fortunately,
    // CollectibleBehavior.OnLoaded doesn't do anything. So it is safe to skip
    // calling it.
    //
    // Finally, BlockBehaviorHarvestable.OnLoaded resolves harvestedStacks,
    // which still needs to be done.
    harvestedStacks.Foreach(
        harvestedStack => harvestedStack?.Resolve(
            api.World, "harvestedStack of block ", block.Code));
    _api = api;
  }

  public override bool
  OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
                      BlockSelection blockSel, ref EnumHandling handled) {
    // The base method plays a sound and spawns particles.
    base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel,
                             ref handled);
    // However, the base method won't return false on the client. Returning stop
    // after the harvest time is necessary to end the block use animation.
    // Otherwise, it keeps running until the player releases the mouse button.
    return secondsUsed < _harvestTime;
  }

  public override void
  OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
                      BlockSelection blockSel, ref EnumHandling handled) {
    base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel,
                             ref handled);

    // Unfortunately BlockBehaviorHarvestable does not have any hooks to run
    // code when the block is harvested. So the harvest condition has to be
    // duplicated here.
    if (secondsUsed > _harvestTime - 0.05f &&
        world.Side == EnumAppSide.Server) {
    }
  }

  public void GetResistance(IBlockAccessor blockAccessor, BlockPos pos,
                            ref float resistance, ref EnumHandling handled) {
    resistance = _harvestTime;
    handled = EnumHandling.PreventSubsequent;
  }

  public void OnGettingBroken(IPlayer player, BlockSelection blockSel,
                              ItemSlot itemslot, ref float remainingResistance,
                              float dt, int counter, ref EnumHandling handled) {
    if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) {
      // Fall though to the default block behavior to let the player actually
      // break the block instead of harvesting it.
      handled = EnumHandling.PassThrough;
      return;
    }
    remainingResistance -= dt;
    _lastSelection = blockSel;
    handled = EnumHandling.PreventSubsequent;
  }

  public override void OnBlockBroken(IWorldAccessor world, BlockPos pos,
                                     IPlayer player, ref EnumHandling handled) {
    if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) {
      // Fall though to the default block behavior to let the player actually
      // break the block instead of harvesting it.
      handled = EnumHandling.PassThrough;
      return;
    }
    // Treat the block break as a right click harvest instead.
    BlockSelection blockSelection;
    if (_lastSelection?.Block == block && _lastSelection.Position == pos) {
      blockSelection = _lastSelection;
    } else {
      blockSelection = new BlockSelection(pos, BlockFacing.UP, block);
    }
    OnBlockInteractStop(_harvestTime, world, player, blockSelection,
                        ref handled);
  }
}
