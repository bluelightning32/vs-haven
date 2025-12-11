using System;

using ProtoBuf;

namespace Haven;

[ProtoContract]
public class Plot {
  [ProtoMember(1)]
  public string OwnerUID = null;
  [ProtoMember(2)]
  public string OwnerName = null;

  public override bool Equals(object obj) {
    if (obj is not Plot other) {
      return false;
    }
    return OwnerUID == other.OwnerUID && OwnerName == other.OwnerName;
  }

  public override int GetHashCode() {
    if (OwnerUID == null) {
      return 0;
    }
    return OwnerUID.GetHashCode();
  }
}

[ProtoContract]
public class PlotRing {
  [ProtoMember(1)]
  public readonly int HoleRadius;
  [ProtoMember(2)]
  public readonly int Width;
  [ProtoMember(3)]
  public readonly double BorderRadians;
  [ProtoMember(4)]
  public readonly double PlotRadians;
  /// <summary>
  /// Array of the owners of each plot. This is an empty string if the plot is
  /// currently unowned.
  /// </summary>
  [ProtoMember(5)]
  public readonly Plot[] Plots = Array.Empty<Plot>();
  [ProtoMember(6)]
  public int UseablePlots;

  public PlotRing(int holeRadius, int width, double borderRadians,
                  double plotRadians) {
    HoleRadius = holeRadius;
    Width = width;
    BorderRadians = borderRadians;
    PlotRadians = plotRadians;
    int plotCount =
        (int)Math.Ceiling(Math.Tau / (BorderRadians + PlotRadians) - 0.001);
    Plots = new Plot[plotCount];
    for (int i = 0; i < plotCount; ++i) {
      Plots[i] = new();
    }
    UseablePlots = plotCount * 3 / 4;
    if (UseablePlots <= 0) {
      UseablePlots = plotCount;
    }
  }

  /// <summary>
  /// For deserialization
  /// </summary>
  private PlotRing() {}

  public static PlotRing Create(int holeRadius, int maxRadius,
                                double borderWidth, int blocksPerPlot) {
    borderWidth /= holeRadius;
    if (borderWidth >= 1) {
      return null;
    }
    double borderRadians = Math.Asin(borderWidth / 2) * 2;

    // ring area = pi * (width + holeRadius)^2 - pi*holeRadius^2
    // ring area = pi * ((width + holeRadius)^2 - holeRadius^2)
    //
    // plot area = ring area * (plotRadians / tau)
    // plotRadians = plot area * tau / ring area
    //
    // plotRadians = tau / numSections - borderRadians
    // tau / numSections = plotRadians + borderRadians
    // numSections = tau / (plotRadians + borderRadians)
    // numSections = tau / (plot area * tau / ring area + borderRadians)
    // numSections = 1 / (plot area / ring area + borderRadians/tau)
    int width = (int)Math.Ceiling(Math.Sqrt(blocksPerPlot));
    width = Math.Min(width, maxRadius - holeRadius);
    double ringArea = Math.PI * ((width + holeRadius) * (width + holeRadius) -
                                 holeRadius * holeRadius);
    int numSections =
        (int)(1 / (blocksPerPlot / ringArea + borderRadians / Math.Tau));
    if (numSections < 1) {
      return null;
    }
    double plotRadians = Math.Tau / numSections - borderRadians;
    return new PlotRing(holeRadius, width, borderRadians, plotRadians);
  }

  /// <summary>
  /// Returns the index of the plot at the given radians (relative to the haven
  /// center)
  /// </summary>
  /// <param name="radians"></param>
  /// <returns>the owner index, or -1 if the location belongs to a
  /// border</returns>
  public int GetOwnerIndex(double radians) {
    if (radians < 0) {
      radians += Math.Tau;
    }
    int index = (int)(radians / (PlotRadians + BorderRadians));
    if (radians - (PlotRadians + BorderRadians) * index <= PlotRadians) {
      return index;
    }
    return -1;
  }

  public bool ShouldStartNewRing() {
    int used = 0;
    foreach (Plot plot in Plots) {
      if (plot.OwnerUID != null) {
        ++used;
      }
    }
    return used >= UseablePlots;
  }

  /// <summary>
  /// Marks the plot as owned if possible
  /// </summary>
  /// <param name="radians"></param>
  /// <param name="ownerUID"></param>
  /// <param name="langCode"></param>
  /// <returns>null if the ownership request succeeded, or otherwise an error
  /// message</returns>
  public string ClaimPlot(double radians, string ownerUID, string ownerName) {
    int ownerIndex = GetOwnerIndex(radians);
    if (ownerIndex < 0) {
      return "haven:cannot-claim-border";
    }
    Plot plot = Plots[ownerIndex];
    if (plot.OwnerUID != null) {
      if (plot.OwnerUID == ownerUID) {
        return "haven:plot-already-owned-by-same-owner";
      }
      return "haven:plot-already-owned";
    }
    plot.OwnerUID = ownerUID;
    plot.OwnerName = ownerName;
    return null;
  }

  public string UnclaimPlot(double radians, string playerUID) {
    int ownerIndex = GetOwnerIndex(radians);
    if (ownerIndex < 0) {
      return "haven:cannot-unclaim-border";
    }
    Plot plot = Plots[ownerIndex];
    if (plot.OwnerUID != playerUID) {
      return "haven:cannot-unclaim-not-owned";
    }
    plot.OwnerUID = null;
    return null;
  }
}
