using System;

using ProtoBuf;

using Vintagestory.API.MathTools;

namespace Haven;

/// <summary>
/// Generates offsets in a square spiral pattern.
/// <code>
/// 15 14 13 12  11
/// 16  4  3  2  10
/// 17  5  0  1  9
/// 18  6  7  8  24
/// 19 20 21 22  23
/// </code>
/// </summary>
[ProtoContract]
public class SquareSpiral {
  [ProtoMember(1)]
  public int Index { get; private set; } = 0;

  /// <summary>
  /// Returns the number of blocks in a round, for a round > 0
  /// </summary>
  /// <param name="round"></param>
  /// <returns></returns>
  static private int GetRoundSize(int round) {
    // Every round except the 0th has 8 more blocks than the previous one.
    // 0 - 1  = 1
    // 1 - 8  = 2*4
    // 2 - 16 = 4*4
    // 3 - 24 = 6*4
    // 4 - 32 = 8*4
    // 5 - 40 = 10*4
    return round * 8;
  }

  /// <summary>
  /// Returns the start index of the round, for a round > 0
  /// </summary>
  /// <param name="round"></param>
  /// <returns></returns>
  static private int GetRoundStart(int round) {
    // Adding all of the blocks from the previous rounds produces:
    // 0 - [0, 1)
    // 1 - [1, 9)
    // 2 - [9, 25)
    // 3 - [25, 49)
    // 4 - [49, 81)
    // 5 - [81, 121)

    // The start index for a round is an arithmetic series. The formula for the
    // start index of round n is: S_n = (n - 1) * (8 + (n-1)*8)/2 + 1
    //     = (n - 1) * 4 * (1 + (n - 1)) + 1
    //     = (n - 1) * 4 * n + 1
    //     = 4 * n^2 - 4 * n + 1
    return (((round - 1) * round) << 2) | 1;
  }

  private int Round {
    get {
      // Solving the round equation for n gives the round for a given index:
      // S_n = 4n^2 - 4n + 1 = ((2 * n) - 1)^2
      // (2 * n) - 1 = sqrt(S_n)
      // n = (sqrt(S_n) + 1) / 2
      return ((int)Math.Sqrt(Index) + 1) / 2;
    }
  }

  public Vec2i Offset {
    get {
      if (Index == 0) {
        return Vec2i.Zero;
      }
      int round = Round;
      int roundStart = GetRoundStart(round);
      int indexOffset = Index - roundStart + round;
      // dirIndex is in the range [0, 4].
      int dirIndex = indexOffset / (round << 1);
      int sideOffset = indexOffset - dirIndex * (round << 1) - round;
      return dirIndex switch {
        1 => new(-sideOffset, round),
        2 => new(-round, -sideOffset),
        3 => new(sideOffset, -round),
        _ => new(round, sideOffset),
      };
    }
  }

  public Vec2i SquareOffset {
    get {
      Vec2i offset = Offset;
      offset.X *= offset.X;
      offset.Y *= offset.Y;
      return offset;
    }
  }

  public void Next() { ++Index; }

  public SquareSpiral() {}
}
