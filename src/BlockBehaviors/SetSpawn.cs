using System.Collections.Generic;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Haven.BlockBehaviors;

public class SetSpawn : BlockBehavior {
  /// <summary>
  /// Every player that is using one of these blocks anywhere gets a fake gear.
  /// The fake gear is responsible for the particles and setting spawn behavior.
  /// If the player uses the block long enough to set their spawn, the gear
  /// inside the item slot is destroyed. An empty item slot prevents the player
  /// from using the block again. The state is cleared (item slot is destroyed)
  /// when the player cancels the interaction by releasing the mouse.
  /// </summary>
  private readonly Dictionary<IPlayer, DummySlot> _active = [];
  ICoreAPI _api = null;

  public SetSpawn(Block block) : base(block) {}

  public override void OnLoaded(ICoreAPI api) { _api = api; }

  public override bool OnBlockInteractStart(IWorldAccessor world,
                                            IPlayer byPlayer,
                                            BlockSelection blockSel,
                                            ref EnumHandling handling) {
    if (world.Api.Side == EnumAppSide.Server) {
      HavenSystem.Logger.Audit("SetSpawn block interaction start at {0} by {1}",
                               blockSel?.Position, byPlayer?.PlayerName);
    }
    DummySlot gear = GetOrCreateGear(byPlayer);
    if (gear.Empty) {
      // The gear should not get in this state, because the previous interaction
      // should have continued (or been cancelled) instead of starting a new
      // one. Reset the block just in case.
      MaybeDestroy(byPlayer, gear);
      gear = GetOrCreateGear(byPlayer);
    }
    EnumHandHandling handHandling = EnumHandHandling.NotHandled;
    gear.Itemstack.Item.OnHeldInteractStart(gear, byPlayer.Entity, blockSel,
                                            null, true, ref handHandling);
    MaybeDestroy(byPlayer, gear);
    if (handHandling != EnumHandHandling.NotHandled) {
      handling = EnumHandling.PreventSubsequent;
      return true;
    }
    return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
  }

  public override bool
  OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
                      BlockSelection blockSel, ref EnumHandling handling) {
    DummySlot gear = GetGear(byPlayer);
    if (gear == null) {
      return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel,
                                      ref handling);
    }
    if (gear.Empty) {
      // This means the gear is complete and the spawn point is set. However,
      // this block continues the interaction until the player cancels it. If
      // the iteraction was stopped at this point, then a new interaction would
      // be immeidately started (because the player is still holding the mouse
      // button). The player would not know when the action was complete,
      // because the camera would continue shaking.
      handling = EnumHandling.PreventSubsequent;
      return true;
    }
    bool result = gear.Itemstack.Item.OnHeldInteractStep(
        secondsUsed, gear, byPlayer.Entity, blockSel, null);
    MaybeDestroy(byPlayer, gear);
    handling = EnumHandling.PreventSubsequent;
    if (!result && !gear.Empty) {
      gear.Itemstack.Item.OnHeldInteractStop(secondsUsed, gear, byPlayer.Entity,
                                             blockSel, null);
      if (world.Api.Side == EnumAppSide.Server) {
        HavenSystem.Logger.Audit("SetSpawn done at {0} for {1}",
                                 blockSel?.Position, byPlayer?.PlayerName);
      }
      // Continue the interaction until the player releases the mouse button.
      return true;
    }
    return result;
  }

  public override void
  OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
                      BlockSelection blockSel, ref EnumHandling handling) {
    DummySlot gear = GetGear(byPlayer);
    if (gear == null || gear.Empty) {
      base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel,
                               ref handling);
      return;
    }
    gear.Itemstack.Item.OnHeldInteractStop(secondsUsed, gear, byPlayer.Entity,
                                           blockSel, null);
    // Do not destroy the gear until the cancel step.
    handling = EnumHandling.PreventDefault;
  }

  public override bool OnBlockInteractCancel(float secondsUsed,
                                             IWorldAccessor world,
                                             IPlayer byPlayer,
                                             BlockSelection blockSel,
                                             ref EnumHandling handling) {
    DummySlot gear = GetGear(byPlayer);
    if (gear == null) {
      return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel,
                                        ref handling);
    }
    if (gear.Empty) {
      // The interaction already completed. The player has finally released the
      // mouse button. So reset the state so that the player can use the block
      // again.
      MaybeDestroy(byPlayer, gear);
      return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel,
                                        ref handling);
    }
    bool result = gear.Itemstack.Item.OnHeldInteractCancel(
        secondsUsed, gear, byPlayer.Entity, blockSel, null,
        EnumItemUseCancelReason.ReleasedMouse);
    // Possibly reset the gear so that the player can use it again.
    MaybeDestroy(byPlayer, gear);
    handling = EnumHandling.PreventDefault;
    return result;
  }

  private DummySlot GetGear(IPlayer player) {
    return _active.TryGetValue(player, out DummySlot gear) ? gear : null;
  }

  private DummySlot GetOrCreateGear(IPlayer player) {
    DummySlot slot = GetGear(player);
    if (slot != null) {
      return slot;
    }
    // Create a gear and initialize it enough that it can be used without
    // generating null exceptions.
    ItemTemporalGear gear =
        new() { FpHandTransform = ModelTransform.ItemDefaultFp(),
                TpHandTransform = ModelTransform.ItemDefaultTp() };
    gear.OnLoadedNative(_api);

    slot = new DummySlot(new ItemStack(gear, 1));
    _active.Add(player, slot);
    return slot;
  }

  private void MaybeDestroy(IPlayer player, DummySlot gear) {
    if (gear.Empty) {
      _active.Remove(player);
    }
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
      IWorldAccessor world, BlockSelection selection, IPlayer forPlayer,
      ref EnumHandling handling) {
    handling = EnumHandling.PreventSubsequent;
    return [new WorldInteraction { ActionLangCode = "haven:blockhelp-set-spawn",
                                   MouseButton = EnumMouseButton.Right }];
  }
}
