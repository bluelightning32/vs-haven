
using Haven.BlockBehaviors;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven.Blocks;

/// <summary>
/// Delegates additional methods to behaviors on the block.
/// </summary>
public class Delegate : BlockGeneric {
  /// <summary>
  /// Forward GetResistance to any block behaviors that implement
  /// IGettingBrokenBehavior. The behaviors can skip the default
  /// Block.GetResistance by setting handled to PreventSubsequent or
  /// PreventDefault. If a behavior sets handled to Handled (and all subsequent
  /// behaviors leave it as PassThrough), then Block.GetResistance will still
  /// get called, but the remaining resistance will be used from the behavior
  /// instead of the block.
  /// </summary>
  /// <param name="blockAccessor">access to the world</param>
  /// <param name="pos">location of the block</param>
  /// <returns>the remaining resistance</returns>
  public override float GetResistance(IBlockAccessor blockAccessor,
                                      BlockPos pos) {
    EnumHandling handled = EnumHandling.PassThrough;
    float resistance = 0;
    WalkBlockBehaviors(this,
                       (BlockBehavior behavior, ref EnumHandling handled) => {
                         if (behavior is IBlockBreaking forward) {
                           forward.GetResistance(blockAccessor, pos,
                                                 ref resistance, ref handled);
                         }
                       },
                       (Block block, ref EnumHandling handled) => {
                         float blockResistance =
                             base.GetResistance(blockAccessor, pos);
                         // handled is either EnumHandling.PassThrough or
                         // EnumHandling.Handled at this point.
                         if (handled != EnumHandling.Handled) {
                           resistance = blockResistance;
                         }
                       },
                       ref handled);
    return resistance;
  }

  /// <summary>
  /// Forward OnGettingBroken to any block behaviors that implement
  /// IGettingBrokenBehavior. The behaviors can skip the default
  /// Block.OnGettingBroken by setting handled to PreventSubsequent or
  /// PreventDefault. If a behavior sets handled to Handled (and all subsequent
  /// behaviors leave it as PassThrough), then Block.OnGettingBroken will still
  /// get called, but the remaining resistance will be used from the behavior
  /// instead of the block.
  ///
  /// This only runs on the client side.
  /// </summary>
  /// <param name="player">The player that is breaking the block</param>
  /// <param name="blockSel">the block they are breaking</param>
  /// <param name="itemslot">the actively held item by the player</param>
  /// <param name="remainingResistance">the previous remaining time until the
  /// block is broken</param> <param name="dt">how many real life seconds have
  /// passed since this method was last called for this block break</param>
  /// <param name="counter">the number of times this method has been called for
  /// this block break</param> <returns>the new remaining resistance. Return 0
  /// to indicate the block should now break.</returns>
  public override float OnGettingBroken(IPlayer player, BlockSelection blockSel,
                                        ItemSlot itemslot,
                                        float remainingResistance, float dt,
                                        int counter) {
    EnumHandling handled = EnumHandling.PassThrough;
    WalkBlockBehaviors(this,
                       (BlockBehavior behavior, ref EnumHandling handled) => {
                         if (behavior is IBlockBreaking forward) {
                           forward.OnGettingBroken(player, blockSel, itemslot,
                                                   ref remainingResistance, dt,
                                                   counter, ref handled);
                         }
                       },
                       (Block block, ref EnumHandling handled) => {
                         float blockRemainingResistance = base.OnGettingBroken(
                             player, blockSel, itemslot, remainingResistance,
                             dt, counter);
                         // handled is either EnumHandling.PassThrough or
                         // EnumHandling.Handled at this point.
                         if (handled != EnumHandling.Handled) {
                           remainingResistance = blockRemainingResistance;
                         }
                       },
                       ref handled);
    return remainingResistance;
  }

  public delegate void BlockDelegate(Block block, ref EnumHandling handled);
  public delegate void BlockBehaviorDelegate(BlockBehavior behavior,
                                             ref EnumHandling handled);
  /// <summary>
  /// Walk the behaviors on the block, then fallback to calling the default
  /// block action, unless the behaviors indicated that the default block action
  /// should be skipped.
  /// </summary>
  /// <param name="block">the block to traverse</param>
  /// <param name="callBehavior">delegate to run on every behavior on the block
  /// (unless handled says to end the traversal early)</param> <param
  /// name="callBlock">delegate to handle the block's default behavior. The
  /// block behaviors can skip this by setting handled appropriately.</param>
  /// <param name="handled">the final handled value, often from which ever
  /// behavior ran last.</param>
  public static void WalkBlockBehaviors(Block block,
                                        BlockBehaviorDelegate callBehavior,
                                        BlockDelegate callBlock,
                                        ref EnumHandling handled) {
    foreach (BlockBehavior behavior in block.BlockBehaviors) {
      EnumHandling behaviorHandled = EnumHandling.PassThrough;
      callBehavior(behavior, ref behaviorHandled);
      if (behaviorHandled != EnumHandling.PassThrough) {
        handled = behaviorHandled;
      }
      if (handled == EnumHandling.PreventSubsequent) {
        return;
      }
    }
    if (handled != EnumHandling.PreventDefault) {
      callBlock(block, ref handled);
    }
  }
}
