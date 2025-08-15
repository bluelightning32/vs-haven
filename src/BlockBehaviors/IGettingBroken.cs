using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven.BlockBehaviors;

/// <summary>
/// Called by Blocks.Delegate.
/// </summary>
public interface IBlockBreaking {
  public void GetResistance(IBlockAccessor blockAccessor, BlockPos pos,
                            ref float resistance, ref EnumHandling handled);

  public void OnGettingBroken(IPlayer player, BlockSelection blockSel,
                              ItemSlot itemslot, ref float remainingResistance,
                              float dt, int counter, ref EnumHandling handled);
}
