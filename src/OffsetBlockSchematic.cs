using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haven;

[ProtoContract(Surrogate = typeof(OffsetBlockSchematicSurrogate))]
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
  /// <param name="worldForResolve"></param>
  /// <returns>all relatively solid positions</returns>
  private HashSet<BlockPos>
  GetProbeSolidPositions(IWorldAccessor worldForResolve) {
    HashSet<int> solidBlockIds = [];
    foreach ((int blockId, AssetLocation name) in BlockCodes) {
      if (blockId == FillerBlockId) {
        continue;
      }
      if (blockId == PathwayBlockId) {
        continue;
      }
      if (blockId == 0) {
        // This is air
        continue;
      }
      Block block = worldForResolve.GetBlock(blockId);
      if (block.ForFluidsLayer) {
        continue;
      }
      solidBlockIds.Add(blockId);
    }
    HashSet<BlockPos> positions = [];
    for (int i = 0; i < Indices.Count; i++) {
      int blockId = BlockIds[i];
      if (!solidBlockIds.Contains(blockId)) {
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

  private int GetProbeYMax(HashSet<BlockPos> probeSolid, int x, int yStart,
                           int z) {
    yStart -= OffsetY;
    while (probeSolid.Contains(new(x, yStart, z))) {
      ++yStart;
    }
    return yStart + OffsetY;
  }

  private TerrainProbe CreateProbeAtXZ(HashSet<BlockPos> probeSolid, int x,
                                       int yEnd, int z, ref bool aboveSurface) {
    if (yEnd == int.MaxValue) {
      Cuboidi columnBox = Outline.GetBoundingBoxForIntersection(
          new Cuboidi(x, 0, z, x + 1, int.MaxValue, z + 1));
      if (columnBox.Y1 > int.Abs(OffsetY)) {
        aboveSurface = true;
        return new() { X = x, Z = z, YMin = int.MinValue,
                       YEnd = GetProbeYMax(probeSolid, x, columnBox.Y1, z) };
      }
    }
    return new() { X = x, Z = z, YMin = int.Min(-1, OffsetY - 1),
                   YEnd = GetProbeYMax(probeSolid, x, 0, z) };
  }

  private void AddNorthSouthProbes(HashSet<BlockPos> probeSolid, int x,
                                   int yEnd, HashSet<TerrainProbe> probes,
                                   ref bool aboveSurface) {
    Cuboidi rowBox = Outline.GetBoundingBoxForIntersection(
        new Cuboidi(x, 0, 0, x + 1, yEnd, int.MaxValue));
    if (rowBox.Z1 >= rowBox.Z2) {
      return;
    }
    TerrainProbe north =
        CreateProbeAtXZ(probeSolid, x, yEnd, rowBox.Z1, ref aboveSurface);
    probes.Add(north);
    TerrainProbe south =
        CreateProbeAtXZ(probeSolid, x, yEnd, rowBox.Z2 - 1, ref aboveSurface);
    probes.Add(south);
  }

  private void AddWestEastProbes(HashSet<BlockPos> probeSolid, int z, int yEnd,
                                 HashSet<TerrainProbe> probes,
                                 ref bool aboveSurface) {
    Cuboidi rowBox = Outline.GetBoundingBoxForIntersection(
        new Cuboidi(0, 0, z, int.MaxValue, yEnd, z + 1));
    if (rowBox.X1 >= rowBox.X2) {
      return;
    }
    TerrainProbe west =
        CreateProbeAtXZ(probeSolid, rowBox.X1, yEnd, z, ref aboveSurface);
    probes.Add(west);
    TerrainProbe east =
        CreateProbeAtXZ(probeSolid, rowBox.X2 - 1, yEnd, z, ref aboveSurface);
    probes.Add(east);
  }

  /// <summary>
  /// Set Probes to an automatically selected values based on the OffsetY and
  /// Outline. See the auto configuration description at SchematicData.Probes.
  /// </summary>
  /// <param name="worldForResolve"></param>
  public void AutoConfigureProbes(IWorldAccessor worldForResolve) {
    HashSet<BlockPos> probeSolid = GetProbeSolidPositions(worldForResolve);
    int yEnd = int.MaxValue;
    Cuboidi surfaceBox = Outline.GetBoundingBoxForIntersection(
        new Cuboidi(0, 0, 0, int.MaxValue, yEnd, int.MaxValue));
    HashSet<TerrainProbe> probes = [];
    bool aboveSurface = false;
    int x = surfaceBox.X1;
    for (; x < surfaceBox.X2; x += AutoProbeSpacing) {
      AddNorthSouthProbes(probeSolid, x, yEnd, probes, ref aboveSurface);
    }
    if (surfaceBox.X1 + 1 < surfaceBox.X2 &&
        x - AutoProbeSpacing < surfaceBox.X2) {
      x = surfaceBox.X2 - 1;
      AddNorthSouthProbes(probeSolid, x, yEnd, probes, ref aboveSurface);
    }
    int z = surfaceBox.Z1;
    for (; z < surfaceBox.Z2; z += AutoProbeSpacing) {
      AddWestEastProbes(probeSolid, z, yEnd, probes, ref aboveSurface);
    }
    if (surfaceBox.Z1 + 1 < surfaceBox.Z2 &&
        z - AutoProbeSpacing < surfaceBox.Z2) {
      z = surfaceBox.Z2 - 1;
      AddWestEastProbes(probeSolid, z, yEnd, probes, ref aboveSurface);
    }
    if (aboveSurface) {
      yEnd = int.Max(1, 1 - OffsetY);
      surfaceBox = Outline.GetBoundingBoxForIntersection(
          new Cuboidi(0, 0, 0, int.MaxValue, yEnd, int.MaxValue));
      x = surfaceBox.X1;
      for (; x < surfaceBox.X2; x += AutoProbeSpacing) {
        AddNorthSouthProbes(probeSolid, x, yEnd, probes, ref aboveSurface);
      }
      if (surfaceBox.X1 + 1 < surfaceBox.X2 &&
          x - AutoProbeSpacing < surfaceBox.X2) {
        x = surfaceBox.X2 - 1;
        AddNorthSouthProbes(probeSolid, x, yEnd, probes, ref aboveSurface);
      }
      z = surfaceBox.Z1;
      for (; z < surfaceBox.Z2; z += AutoProbeSpacing) {
        AddWestEastProbes(probeSolid, z, yEnd, probes, ref aboveSurface);
      }
      if (surfaceBox.Z1 + 1 < surfaceBox.Z2 &&
          z - AutoProbeSpacing < surfaceBox.Z2) {
        z = surfaceBox.Z2 - 1;
        AddWestEastProbes(probeSolid, z, yEnd, probes, ref aboveSurface);
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
  public int ProbeTerrain(TerrainSurvey terrain, IBlockAccessor accessor,
                          Vec2i startPos) {
    // This is the acceptable y range so far. Note that the probe YMin and YEnd
    // are in terms of the difference between the structure and surface.
    // However, yMin and yMax are the y range for the structure in absolute
    // position. The structure must be placed above yMin and at or below yMax.
    int yMin = int.MinValue;
    int yMax = int.MaxValue;
    // This is the sum of the y heights at each probe location.
    int yTerrainSum = 0;
    bool incomplete = false;
    int y;
    foreach (TerrainProbe probe in Probes) {
      y = terrain.GetHeight(accessor, probe.X + startPos.X,
                            probe.Z + startPos.Y);
      if (y == -1) {
        // Keep probing the rest of the locations. This will queue up any
        // remaining chunk load requests. Also, one of the other probes may
        // fail.
        incomplete = true;
        continue;
      }
      int solid =
          terrain.IsSolid(accessor, probe.X + startPos.X, probe.Z + startPos.Y);
      if (solid == -1) {
        incomplete = true;
        continue;
      }
      if (solid == 0) {
        // The probe failed.
        return -2;
      }
      yTerrainSum += y;
      if (probe.YMin > int.MinValue) {
        // Avoid running this update when YMin = int.MinValue, because that
        // would cause overflow errors.
        yMax = int.Min(yMax, y - probe.YMin);
      }
      yMin = int.Max(yMin, y - probe.YEnd);
      if (yMin >= yMax) {
        // The probe failed.
        return -2;
      }
    }
    if (incomplete) {
      return -1;
    }
    // If the range allows it, place the structure on top of the average terrain
    // surface block y coordinate.
    y = yTerrainSum / Probes.Length + 1;
    // Adjust the position so that it is within the allowed range.
    y = int.Max(y, yMin + 1);
    y = int.Min(y, yMax);
    return y;
  }

  public override int Place(IBlockAccessor blockAccessor,
                            IWorldAccessor worldForCollectibleResolve,
                            BlockPos startPos, bool replaceMetaBlocks = true) {
    BlockPos pos = startPos.Copy();
    pos.Y += OffsetY;
    return base.Place(blockAccessor, worldForCollectibleResolve, pos,
                      replaceMetaBlocks);
  }

  public Cuboidi GetBoundingBox(BlockPos startPos) {
    return new Cuboidi(startPos.X, startPos.Y + OffsetY, startPos.Z,
                       startPos.X + SizeX, startPos.Y + OffsetY + SizeY,
                       startPos.Z + SizeZ);
  }

  public void PlaceEntitiesAndBlockEntities(IBlockAccessor accessor,
                                            IWorldAccessor worldForResolve,
                                            BlockPos startPos,
                                            bool replaceMetaBlocks = true) {
    BlockPos pos = startPos.Copy();
    pos.Y += OffsetY;
    PlaceEntitiesAndBlockEntities(accessor, worldForResolve, pos, BlockCodes,
                                  ItemCodes, replaceBlockEntities: false, null,
                                  0, null, replaceMetaBlocks);
  }

  public bool UpdateTerrain(BlockPos offset) {
    throw new NotImplementedException();
  }
}

[ProtoContract]
internal class OffsetBlockSchematicSurrogate {
  [ProtoMember(1)]
  public string Json = null;

  public static implicit
  operator OffsetBlockSchematic(OffsetBlockSchematicSurrogate surrogate) {
    if (surrogate == null) {
      return null;
    }
    return JsonUtil.FromString<OffsetBlockSchematic>(surrogate.Json);
  }

  public static implicit
  operator OffsetBlockSchematicSurrogate(OffsetBlockSchematic source) {
    if (source == null) {
      return null;
    }
    return new OffsetBlockSchematicSurrogate { Json =
                                                   JsonUtil.ToString(source) };
  }
}
