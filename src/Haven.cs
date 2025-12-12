using System;
using System.Collections.Generic;
using System.Text;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

[ProtoContract]
public class Haven {
  [ProtoMember(1)]
  private readonly BlockPos _center;
  [ProtoMember(2)]
  private readonly int _resourceZoneRadius;
  [ProtoMember(3)]
  private readonly int _radius;
  [ProtoMember(4)]
  private readonly List<PlotRing> _plotRings = [];

  public Haven(BlockPos center, int resourceZoneRadius, int radius,
               int borderWidth, int blocksPerPlot) {
    _center = center;
    _resourceZoneRadius = resourceZoneRadius;
    _radius = radius;
    TryExpand(borderWidth, blocksPerPlot);
  }

  public Haven(HavenRegionIntersection intersection, int borderWidth,
               int blocksPerPlot)
      : this(intersection.Center, intersection.ResourceZoneRadius,
             intersection.Radius, borderWidth, blocksPerPlot) {}

  /// <summary>
  /// Serialization constructor
  /// </summary>
  private Haven() {}

  public string ToString(int indentSpaces) {
    StringBuilder sb = new();
    sb.AppendLine(
        $"center: {_center}, resourceZoneRadius: {_resourceZoneRadius}, radius: {_radius}");
    sb.Append(' ', indentSpaces);
    sb.AppendLine("plot rings:");
    foreach (PlotRing ring in _plotRings) {
      sb.Append(' ', indentSpaces + 2);
      sb.AppendLine(
          $"width: {ring.Width}, borderRadians: {ring.BorderRadians}, plotRadians: {ring.PlotRadians}, numPlots: {ring.Plots.Length}");
    }
    sb.Append(' ', indentSpaces);
    sb.Append("plots:");
    foreach (PlotRing ring in _plotRings) {
      foreach (Plot plot in ring.Plots) {
        sb.Append('\n');
        sb.Append(' ', indentSpaces + 2);
        if (plot.OwnerUID == null) {
          sb.Append("none");
        } else {
          sb.Append(plot.OwnerName);
        }
      }
    }
    return sb.ToString();
  }

  public int SafeZoneRadius {
    get {
      if (_plotRings.Count > 0) {
        PlotRing last = _plotRings[^1];
        return last.HoleRadius + last.Width;
      }
      return _resourceZoneRadius;
    }
  }

  /// <summary>
  /// Attempt to add a new ring of plots
  /// </summary>
  /// <param name="borderWidth">the border size in blocks between plots, where
  /// no one can build</param> <param name="blocksPerPlot">the minimum number of
  /// blocks in each plot</param> <returns>a new HavenRegionIntersection if a
  /// new ring was added, and the haven needs to be serialized to the save game
  /// again</returns>
  public bool TryExpand(int borderWidth, int blocksPerPlot) {
    int holeRadius;
    if (_plotRings.Count > 0) {
      PlotRing last = _plotRings[^1];
      if (!last.ShouldStartNewRing()) {
        return false;
      }
      holeRadius = last.HoleRadius + last.Width + borderWidth;
    } else {
      holeRadius = _resourceZoneRadius;
    }
    PlotRing newRing =
        PlotRing.Create(holeRadius, _radius, borderWidth, blocksPerPlot);
    if (newRing == null) {
      return false;
    }
    _plotRings.Add(newRing);
    return true;
  }

  public HavenRegionIntersection GetIntersection() {
    return new() { Center = _center, ResourceZoneRadius = _resourceZoneRadius,
                   Radius = _radius, SafeZoneRadius = SafeZoneRadius };
  }

  /// <summary>
  /// Gets the plot ring at the specified location along with the radians within
  /// the ring
  /// </summary>
  /// <param name="pos"></param>
  /// <param name="havenBelowHeight"></param>
  /// <param name="havenAboveHeight"></param>
  /// <returns></returns>
  private (PlotRing, double)
      GetPlotRing(BlockPos pos, int havenBelowHeight, int havenAboveHeight) {
    if (pos.Y < _center.Y - havenBelowHeight) {
      return (null, 0);
    }
    if (pos.Y >= _center.Y + havenAboveHeight) {
      return (null, 0);
    }
    int dx = pos.X - _center.X;
    int dz = pos.Z - _center.Z;
    int distSq = dx * dx + dz * dz;
    foreach (PlotRing ring in _plotRings) {
      if (distSq <= ring.HoleRadius * ring.HoleRadius) {
        return (null, 0);
      }
      if (distSq <=
          (ring.HoleRadius + ring.Width) * (ring.HoleRadius + ring.Width)) {
        return (ring, Math.Atan2(dz, dx));
      }
    }
    return (null, 0);
  }

  /// <summary>
  /// Gets the plot ring at the specified location along with the index of the
  /// plot in it.
  /// </summary>
  /// <param name="pos"></param>
  /// <param name="havenBelowHeight"></param>
  /// <param name="havenAboveHeight"></param>
  /// <returns>the plot (or null if the position is not in a plot) and the plot
  /// index within it (or -1 if the position is part of the border)</returns>
  public (PlotRing, int)
      GetPlot(BlockPos pos, int havenBelowHeight, int havenAboveHeight) {
    (PlotRing ring, double radians) =
        GetPlotRing(pos, havenBelowHeight, havenAboveHeight);
    if (ring == null) {
      return (null, -1);
    }
    return (ring, ring.GetOwnerIndex(radians));
  }

  /// <summary>
  /// Gets the number of plots owned by a player
  /// </summary>
  /// <param name="playerUID"></param>
  /// <returns></returns>
  public int GetOwnedPlots(string playerUID) {
    int count = 0;
    foreach (PlotRing ring in _plotRings) {
      foreach (Plot plot in ring.Plots) {
        if (plot.OwnerUID == playerUID) {
          ++count;
        }
      }
    }
    return count;
  }

  /// <summary>
  /// Unclaim all plots in this haven owned by the given player
  /// </summary>
  /// <param name="accessor"></param>
  /// <param name="playerUID"></param>
  /// <returns></returns>
  public int UnclaimAllPlots(IBlockAccessor accessor, string playerUID) {
    int unclaimed = 0;
    foreach (PlotRing ring in _plotRings) {
      for (int i = 0; i < ring.Plots.Length; ++i) {
        if (ring.Plots[i].OwnerUID == playerUID) {
          ring.UnclaimPlot(accessor, i, playerUID);
          ++unclaimed;
        }
      }
    }
    return unclaimed;
  }
}
