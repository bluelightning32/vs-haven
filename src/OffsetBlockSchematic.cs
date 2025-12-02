using System.Collections.Generic;

using Newtonsoft.Json;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

public class OffsetBlockSchematic : BlockSchematic {
  [JsonProperty]
  public int OffsetY = 0;
  [JsonProperty]
  public TerrainProbe[] Probes = null;

  public AABBList Outline { get; private set; }

  public void UpdateOutline() {
    Outline = new AABBList(GetJustPositions(new BlockPos(0, 0, 0)));
  }

  /// <summary>
  /// Auto configured probes are spaced apart this far along the perimeter. The
  /// extreme points of the perimeter are also included.
  /// </summary>
  public const int AutoProbeSpacing = 10;

  /// <summary>
  /// Get the block positions that are considered solid enough that the count
  /// towards the column of a probe position.
  /// </summary>
  /// <returns>all relatively solid positions</returns>
  private HashSet<BlockPos> GetProbeSolidPositions() {
    HashSet<BlockPos> positions = [];
    for (int i = 0; i < Indices.Count; i++) {
      int blockId = BlockIds[i];
      if (blockId == FillerBlockId) {
        continue;
      }
      if (blockId == PathwayBlockId) {
        continue;
      }
      AssetLocation block = BlockCodes.GetValueOrDefault(blockId);
      if (block == null) {
        continue;
      }

      uint index = Indices[i];
      int x = (int)(index & PosBitMask);
      int y = (int)((index >> 20) & PosBitMask);
      int z = (int)((index >> 10) & PosBitMask);
      positions.Add(new(x, y, z));
    }
    return positions;
  }

  private int GetProbeYMax(HashSet<BlockPos> probeSolid, int x, int z) {
    int y = -OffsetY;
    while (probeSolid.Contains(new(x, y, z))) {
      ++y;
    }
    return y + OffsetY;
  }

  /// <summary>
  /// Set Probes to an automatically selected values based on the OffsetY and
  /// Outline. See the auto configuration description at SchematicData.Probes.
  /// </summary>
  public void AutoConfigureProbes() {
    HashSet<BlockPos> probeSolid = GetProbeSolidPositions();
    int surface = int.Max(1, 1 - OffsetY);
    Cuboidi surfaceBox = Outline.GetBoundingBoxForIntersection(
        new Cuboidi(0, 0, 0, int.MaxValue, surface, int.MaxValue));
    HashSet<TerrainProbe> probes = [];
    int x = surfaceBox.X1;
    for (; x < surfaceBox.X2; x += AutoProbeSpacing) {
      Cuboidi rowBox = Outline.GetBoundingBoxForIntersection(
          new Cuboidi(x, 0, 0, x + 1, surface, int.MaxValue));
      if (rowBox.Z1 < rowBox.Z2) {
        TerrainProbe north =
            new() { X = x, Z = rowBox.Z1, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, x, rowBox.Z1) };
        probes.Add(north);
        TerrainProbe south =
            new() { X = x, Z = rowBox.Z2 - 1, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, x, rowBox.Z2 - 1) };
        probes.Add(south);
      }
    }
    if (surfaceBox.X1 + 1 < surfaceBox.X2 &&
        x - AutoProbeSpacing < surfaceBox.X2) {
      x = surfaceBox.X2 - 1;
      Cuboidi rowBox = Outline.GetBoundingBoxForIntersection(
          new Cuboidi(x, 0, 0, x + 1, surface, int.MaxValue));
      if (rowBox.Z1 < rowBox.Z2) {
        TerrainProbe north =
            new() { X = x, Z = rowBox.Z1, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, x, rowBox.Z1) };
        probes.Add(north);
        TerrainProbe south =
            new() { X = x, Z = rowBox.Z2 - 1, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, x, rowBox.Z2 - 1) };
        probes.Add(south);
      }
    }
    int z = surfaceBox.Z1;
    for (; z < surfaceBox.Z2; z += AutoProbeSpacing) {
      Cuboidi rowBox = Outline.GetBoundingBoxForIntersection(
          new Cuboidi(0, 0, z, int.MaxValue, surface, z + 1));
      if (rowBox.X1 < rowBox.X2) {
        TerrainProbe west =
            new() { X = rowBox.X1, Z = z, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, rowBox.X1, z) };
        probes.Add(west);
        TerrainProbe east =
            new() { X = rowBox.X2 - 1, Z = z, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, rowBox.X2 - 1, z) };
        probes.Add(east);
      }
    }
    if (surfaceBox.Z1 + 1 < surfaceBox.Z2 &&
        z - AutoProbeSpacing < surfaceBox.Z2) {
      z = surfaceBox.Z2 - 1;
      Cuboidi rowBox = Outline.GetBoundingBoxForIntersection(
          new Cuboidi(0, 0, z, int.MaxValue, surface, z + 1));
      if (rowBox.X1 < rowBox.X2) {
        TerrainProbe west =
            new() { X = rowBox.X1, Z = z, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, rowBox.X1, z) };
        probes.Add(west);
        TerrainProbe east =
            new() { X = rowBox.X2 - 1, Z = z, YMin = int.Min(-1, OffsetY - 1),
                    YMax = GetProbeYMax(probeSolid, rowBox.X2 - 1, z) };
        probes.Add(east);
      }
    }
    Probes = [..probes];
  }

  public Cuboidi GetOffsetBoundingBox(BlockPos startPos) {
    Cuboidi result =
        new Cuboidi(startPos, startPos.AddCopy(SizeX, SizeY, SizeZ));
    result.Y1 += OffsetY;
    result.Y2 += OffsetY;
    return result;
  }

  /// <summary>
  /// Determines if there is an intersection between two schematics.
  /// </summary>
  /// <param name="startPos">the start location of this schematic</param>
  /// <param name="with">the other schematic to perform the intersection test
  /// with</param>
  /// <param name="withStartPos">the start location of the other
  /// schematic</param>
  /// <returns>a cuboid from the other schematic that intersects with this one,
  /// or null.</returns>
  public Cuboidi Intersects(BlockPos startPos, OffsetBlockSchematic with,
                            BlockPos withStartPos) {
    Vec3i withOffset = withStartPos.SubCopy(startPos).AsVec3i;
    withOffset.Y += with.OffsetY - OffsetY;
    if (!GetOffsetBoundingBox(startPos).Intersects(
            with.GetOffsetBoundingBox(withStartPos))) {
      return null;
    }
    Cuboidi result = Outline.Intersects(with.Outline, withOffset);
    if (result == null) {
      return result;
    }
    return result.OffsetCopy(0, with.OffsetY, 0);
  }

  /// <summary>
  /// Moves startPos in direction to avoid an intersection with another
  /// schematic
  /// </summary>
  /// <param name="startPos"></param>
  /// <param name="with"></param>
  /// <param name="withStartPos"></param>
  /// <param name="direction">direction to move the schematic to avoid an
  /// intersection. This should be a unit vector</param>
  /// <returns>true if there was no intersection, or false if startPos was
  /// updated to avoid an intersection</returns>
  public bool AvoidIntersection(BlockPos startPos, OffsetBlockSchematic with,
                                BlockPos withStartPos, Vec3d direction) {
    Vec3i withOffset = withStartPos.SubCopy(startPos).AsVec3i;
    withOffset.Y += with.OffsetY - OffsetY;
    if (!GetOffsetBoundingBox(startPos).Intersects(
            with.GetOffsetBoundingBox(withStartPos))) {
      return true;
    }
    if (Outline.AvoidIntersection(with.Outline, withOffset, direction * -1)) {
      return true;
    }
    withOffset.Y -= with.OffsetY - OffsetY;
    withOffset.X -= withStartPos.X;
    withOffset.Y -= withStartPos.Y;
    withOffset.Z -= withStartPos.Z;
    startPos.X = -withOffset.X;
    startPos.Y = -withOffset.Y;
    startPos.Z = -withOffset.Z;
    return false;
  }

  /// <summary>
  /// Determines whether the terrain is sufficiently flat at the given location.
  /// </summary>
  /// <param name="startPos">
  /// where to put the north-west corner of the schematic
  /// </param>
  /// <returns>
  /// the preferred height if the probes pass, -1 if the probe were incomplete
  /// due to unloaded chunks, or -2 if the probes failed
  /// </returns>
  public int ProbeTerrain(ITerrainHeightReader reader, IBlockAccessor accessor,
                          Vec2i startPos) {
    // This is the acceptable y range so far.
    int yMin = int.MinValue;
    int yMax = int.MaxValue;
    // This is the sum of the y heights at each probe location.
    int yTerrainSum = 0;
    Vec2i pos = new();
    bool incomplete = false;
    int y;
    foreach (TerrainProbe probe in Probes) {
      pos.X = probe.X + startPos.X;
      pos.Y = probe.Z + startPos.Y;
      y = reader.GetHeight(accessor, pos);
      if (y == -1) {
        // Keep probing the rest of the locations. This will queue up any
        // remaining chunk load requests. Also, one of the other probes may
        // fail.
        incomplete = true;
        continue;
      }
      yTerrainSum += y;
      yMin = int.Max(yMin, probe.YMin + y);
      yMax = int.Min(yMax, probe.YMax + y);
      if (yMin >= yMax) {
        // The probe failed.
        return -2;
      }
    }
    if (incomplete) {
      return -1;
    }
    // If the range allows it, place the structure at the average terrain
    // height.
    y = yTerrainSum / Probes.Length;
    // Adjust the position so that it is within the allowed range.
    y = int.Max(y, yMin);
    y = int.Min(y, yMax);
    return y;
  }
}
