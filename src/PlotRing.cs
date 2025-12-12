using System;
using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Haven;

[ProtoContract]
public class Plot {
  [ProtoMember(1)]
  public string OwnerUID = null;
  [ProtoMember(2)]
  public string OwnerName = null;
  [ProtoMember(3)]
  public string FormerOwnerUID = null;
  [ProtoMember(4)]
  public BlockPos ChestPos = null;

  public override bool Equals(object obj) {
    if (obj is not Plot other) {
      return false;
    }
    return OwnerUID == other.OwnerUID && OwnerName == other.OwnerName && FormerOwnerUID == other.FormerOwnerUID && ChestPos == other.ChestPos;
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
  /// <param name="index">the plot index, or -1 if the caller accidentally tried
  /// to claim a border</param> <param name="ownerUID"></param> <param
  /// name="langCode"></param> <returns>null if the ownership request succeeded,
  /// or otherwise an error message</returns>
  public string ClaimPlot(int index, string ownerUID, string ownerName) {
    if (index < 0) {
      return "haven:cannot-claim-border";
    }
    Plot plot = Plots[index];
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

  public string AdminUnclaimPlot(IBlockAccessor accessor, int index) {
    if (index < 0) {
      return "haven:cannot-unclaim-border";
    }
    Plot plot = Plots[index];
    if (plot.OwnerUID == null) {
      return "haven:already-unclaimed";
    }
    plot.FormerOwnerUID = plot.OwnerUID;
    plot.OwnerUID = null;
    if (plot.ChestPos != null) {
      // Do not call accessor.BreakBlock, because that drops the chest itself along with the chest contents.
      BlockEntity entity = accessor.GetBlockEntity(plot.ChestPos);
      if (entity is IBlockEntityContainer container) {
        container.DropContents(new Vec3d(plot.ChestPos.X, plot.ChestPos.Y + 1, plot.ChestPos.Z));
      } else {
        HavenSystem.Logger.Error("Plot chest is missing IBlockEntityContainer {0} {1}.", plot.ChestPos);
      }
      accessor.SetBlock(0, plot.ChestPos);
      plot.ChestPos = null;
    }
    return null;
  }

  public string UnclaimPlot(IBlockAccessor accessor, int index, string playerUID) {
    if (index < 0) {
      return "haven:cannot-unclaim-border";
    }
    Plot plot = Plots[index];
    if (plot.OwnerUID == null) {
      return "haven:already-unclaimed";
    }
    if (plot.OwnerUID != playerUID) {
      return "haven:cannot-unclaim-not-owned";
    }
    return AdminUnclaimPlot(accessor, index);
  }

  /// <summary>
  /// Get a bounding box around the X,Z coordinates of blocks within the plot.
  /// All of the plot's blocks are included in the bounding box, but the
  /// bounding box is not the tightest possible bounding box.
  /// </summary>
  /// <param name="centerX"></param>
  /// <param name="centerZ"></param>
  /// <param name="plot"></param>
  /// <returns></returns>
  public Rectanglei GetPlotBoundingBox(int centerX, int centerZ, int plot) {
    double startRadians = plot * (PlotRadians + BorderRadians);
    double endRadians = startRadians + PlotRadians;

    int x1 = (int)(HoleRadius * Math.Cos(startRadians));
    int z1 = (int)(HoleRadius * Math.Sin(startRadians));
    int x2 = x1;
    int z2 = z1;

    // Experimentally, 0.204 was sufficient to get the unit tests to pass. So
    // for safety, it is set to 0.5.
    double outerRadius = HoleRadius + Width + 0.5;

    ExpandRect(ref x1, ref z1, ref x2, ref z2,
               (int)(HoleRadius * Math.Cos(endRadians)),
               (int)(HoleRadius * Math.Sin(endRadians)));
    ExpandRect(ref x1, ref z1, ref x2, ref z2,
               (int)(outerRadius * Math.Cos(startRadians)),
               (int)(outerRadius * Math.Sin(startRadians)));
    ExpandRect(ref x1, ref z1, ref x2, ref z2,
               (int)(outerRadius * Math.Cos(endRadians)),
               (int)(outerRadius * Math.Sin(endRadians)));

    if (endRadians > Math.Tau) {
      // The section crosses the positive x axis.
      x2 = int.Max(x2, (int)outerRadius);
    }
    if (startRadians < Math.Tau / 4 && endRadians > Math.Tau / 4) {
      // The section crosses the positive z axis.
      z2 = int.Max(z2, (int)outerRadius);
    }
    if (startRadians < Math.PI && endRadians > Math.PI) {
      // The section crosses the negative x axis.
      x1 = int.Min(x1, (int)outerRadius);
    }
    if (startRadians < Math.Tau * 3 / 4 && endRadians > Math.Tau * 3 / 4) {
      // The section crosses the negative z axis.
      z1 = int.Min(z1, (int)outerRadius);
    }

    return new Rectanglei(x1 + centerX, z1 + centerZ, x2 - x1, z2 - z1);
  }

  public void TraversePlotMapChunks(int centerX, int centerZ, int plot,
                                    Action<int, int, Rectanglei> onChunk) {
    Rectanglei rect = GetPlotBoundingBox(centerX, centerZ, plot);
    for (int z = rect.Y1 / GlobalConstants.ChunkSize;
         z <= rect.Y2 / GlobalConstants.ChunkSize; ++z) {
      for (int x = rect.X1 / GlobalConstants.ChunkSize;
           x <= rect.X2 / GlobalConstants.ChunkSize; ++x) {
        onChunk(x, z, rect);
      }
    }
  }

  /// <summary>
  /// Expands the rectangle to include another point
  /// </summary>
  /// <param name="x1">lower x bound of the rectangle</param>
  /// <param name="z1">lower z bound of the rectangle</param>
  /// <param name="x2">upper x bound of the rectangle (this is included)</param>
  /// <param name="z2">upper z bound of the rectangle (this is included)</param>
  /// <param name="x">new x value to add to the rectangle</param>
  /// <param name="z">new z value to add to the rectangle</param>
  private static void ExpandRect(ref int x1, ref int z1, ref int x2, ref int z2,
                                 int x, int z) {
    x1 = Math.Min(x1, x);
    z1 = Math.Min(z1, z);
    x2 = Math.Max(x2, x);
    z2 = Math.Max(z2, z);
  }

  public bool IsInPlot(int centerX, int centerZ, int plot, int plotX,
                       int plotZ) {
    int dx = plotX - centerX;
    int dz = plotZ - centerZ;
    int distSq = dx * dx + dz * dz;
    if (distSq <= HoleRadius * HoleRadius) {
      return false;
    }
    if (distSq > (HoleRadius + Width) * (HoleRadius + Width)) {
      return false;
    }
    double radians = Math.Atan2(dz, dx);
    return GetOwnerIndex(radians) == plot;
  }

  public void RaisePlot(IWorldAccessor world, IChunkLoader loader,
                        PrunedTerrainHeightReader terrain, int centerX,
                        int centerZ, int dimension, int plot) {
    Dictionary<BlockPos, TreeAttribute> queuedBlockEntities = [];
    void ProcessChunk(int chunkX, int chunkZ, Rectanglei boundingBox) {
      int startX = chunkX * GlobalConstants.ChunkSize;
      int startZ = chunkZ * GlobalConstants.ChunkSize;
      int endX = startX + GlobalConstants.ChunkSize;
      int endZ = startZ + GlobalConstants.ChunkSize;
      int offset = 0;
      ushort[] sourceHeights =
          terrain.Source.GetHeights(world.BlockAccessor, chunkX, chunkZ);
      BlockPos pos = new(dimension);
      for (pos.Z = startZ; pos.Z < endZ; ++pos.Z) {
        for (pos.X = startX; pos.X < endX; ++pos.X, ++offset) {
          if (!boundingBox.PointInside(pos.X, pos.Z)) {
            continue;
          }
          if (!IsInPlot(centerX, centerZ, plot, pos.X, pos.Z)) {
            continue;
          }
          terrain.ApplyColumnChanges(world.BlockAccessor, sourceHeights, pos,
                                     offset, queuedBlockEntities);
          foreach ((BlockPos bePos, TreeAttribute tree)
                       in queuedBlockEntities) {
            PrunedTerrainHeightReader.CommitBlockEntity(
                world, loader, world.BlockAccessor, bePos, tree);
          }
          queuedBlockEntities.Clear();
        }
      }
    }
    TraversePlotMapChunks(centerX, centerZ, plot, ProcessChunk);
  }

  public bool CreateChest(ITerrainHeightReader reader, IWorldAccessor world, BlockPos havenCenter, int plotIndex) {
    Plot plot = Plots[plotIndex];
    if (plot.OwnerUID == null) {
      return false;
    }
    if (plot.ChestPos != null) {
      return false;
    }
    // The first plot starts at 0 radians and extends to PlotRadians, followed by BorderRadians before the next plot.
    double startRadians = plotIndex * (BorderRadians + PlotRadians);
    double midRadians = startRadians + PlotRadians / 2;
    // Place the chest right next to the inner ring.
    double radius = HoleRadius + 2;
    int x = havenCenter.X + (int)(Math.Cos(midRadians) * radius);
    int z = havenCenter.Z + (int)(Math.Sin(midRadians) * radius);
    // GetHeight returns the coordinate of the surface block. The chest is placed above the surface block.
    int y = reader.GetHeight(world.BlockAccessor, new Vec2i(x, z)) + 1;
    Block labeledChest = world.GetBlock(AssetLocation.Create("labeledchest-east"));
    BlockPos chestPos = new BlockPos(x, y, z, havenCenter.dimension);
    world.BlockAccessor.SetBlock(labeledChest.Id, chestPos);
    BlockEntity entity = world.BlockAccessor.GetBlockEntity(chestPos);
    TreeAttribute tree = new();
    entity.ToTreeAttributes(tree);
    string oldText = tree.GetString("text");
    if (oldText != "") {
      HavenSystem.Logger.Error("Expected newly created plot chest to have empty text, but instead it has {0}", oldText);
      return false;
    }
    tree.SetInt("color", ColorUtil.ToRgba(255, 50, 0, 100));
    tree.SetString("text", Plots[plotIndex].OwnerName);
    entity.FromTreeAttributes(tree, world);
    entity.MarkDirty(true);
    plot.ChestPos = chestPos;
    return true;
  }
}
