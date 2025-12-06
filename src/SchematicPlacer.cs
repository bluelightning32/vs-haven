using System;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

using static Haven.ISchematicPlacerSupervisor;

namespace Haven;

public interface ISchematicPlacerSupervisor {
  enum LocationResult {
    /// <summary>
    /// This location was rejected. The placer should try a different location
    /// next time.
    /// </summary>
    Rejected,
    /// <summary>
    /// This location was accepted. The placer may start placing the blocks.
    /// </summary>
    Accepted,
    /// <summary>
    /// The supervisor cannot currently tell if this location is acceptable due
    /// to chunks being unavailable. The placer should retry this same location
    /// on the next Generate cycle.
    /// </summary>
    TryAgain
  }
  /// <summary>
  /// Called when the placer is done surveying the terrain and tries to pick its
  /// final location. placer.Offset will only be updated after this returns.
  /// </summary>
  /// <param name="placer"></param>
  /// <param name="offset"></param>
  /// <returns>true if the location was accepted, or false if it was rejected
  /// due to an intersection with another placer</returns>
  LocationResult TryFinalizeLocation(IBlockAccessor accessor,
                                     SchematicPlacer placer, BlockPos offset);
  public TerrainSurvey Terrain { get; }
  public IChunkLoader Loader { get; }

  public IWorldAccessor WorldForResolve { get; }

  public ILogger Logger { get; }
}

[ProtoContract]
public class SchematicPlacer : IWorldGenerator {
  [ProtoMember(1)]
  public readonly OffsetBlockSchematic Schematic;
  /// <summary>
  /// Initially this is the proposed offset. It is the location to place the
  /// northwest bottom corner of the schematic. This does not include the effect
  /// of OffsetY. For example, if OffsetY=-1, then the schematic may place a
  /// block below the location at Offset.
  ///
  /// The placer searches for a good location near this. After a good location
  /// is found, this is updated to the final location.
  /// </summary>
  [ProtoMember(2)]
  private BlockPos _offset;

  public BlockPos Offset {
    get { return _offset; }
    set {
      if (IsOffsetFinal) {
        throw new InvalidOperationException(
            "The offset cannot be changed after it is finalized.");
      }
      _offset = value;
    }
  }
  [ProtoMember(3)]
  private SquareSpiral _locationSearch = new();

  public bool IsOffsetFinal {
    get { return _locationSearch == null; }
  }

  enum PlacementState { None, BlocksPlaced, EntitiesPlaced }
  [ProtoMember(4)]
  PlacementState _placementState = PlacementState.None;

  private ISchematicPlacerSupervisor _supervisor;

  public SchematicPlacer(OffsetBlockSchematic schematic, BlockPos offset,
                         ISchematicPlacerSupervisor supervisor) {
    Schematic = schematic;
    Offset = offset;
    _supervisor = supervisor;
  }

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private SchematicPlacer() {}

  private bool FinalizeLocation(IBlockAccessor accessor) {
    if (_locationSearch == null) {
      return true;
    }
    while (true) {
      Vec2i testPos = _locationSearch.SquareOffset;
      testPos.X += Offset.X;
      testPos.Y += Offset.Z;
      int y = Schematic.ProbeTerrain(_supervisor.Terrain, accessor, testPos);
      if (y == -1) {
        // The probes were incomplete due to missing chunks.
        return false;
      }
      if (y >= 0) {
        BlockPos updatedOffset = new(testPos.X, y, testPos.Y);
        LocationResult result =
            _supervisor.TryFinalizeLocation(accessor, this, updatedOffset);
        switch (result) {
        case LocationResult.Rejected:
          break;
        case LocationResult.Accepted:
          Offset = updatedOffset;
          _locationSearch = null;
          return true;
        case LocationResult.TryAgain:
          return false;
        }
      }
      _locationSearch.Next();
      if (_locationSearch.Index % 20 == 0) {
        _supervisor.Logger.Warning(
            $"Schematic placement attempted {_locationSearch.Index} times.");
      }
    }
  }

  /// <summary>
  /// Load all chunks that overlap with the schematic
  /// </summary>
  /// <param name="accessor"></param>
  /// <returns>true if the chunks are loaded, or false if chunk loads were
  /// scheduled</returns>
  private bool LoadAllChunks(IBlockAccessor accessor) {
    bool complete = true;
    Cuboidi boundingBox = Schematic.GetBoundingBox(Offset);
    for (int z = boundingBox.Z1 / GlobalConstants.ChunkSize;
         z < boundingBox.Z2 / GlobalConstants.ChunkSize; ++z) {
      for (int y = boundingBox.Y1 / GlobalConstants.ChunkSize;
           y < boundingBox.Y2 / GlobalConstants.ChunkSize; ++y) {
        for (int x = boundingBox.X1 / GlobalConstants.ChunkSize;
             x < boundingBox.X2 / GlobalConstants.ChunkSize; ++x) {
          if (accessor.GetChunk(x, y, z) == null) {
            complete = false;
            _supervisor.Loader.LoadChunkColumn(x, z);
          }
        }
      }
    }
    return complete;
  }

  public bool Generate(IBlockAccessor accessor) {
    if (!FinalizeLocation(accessor)) {
      return false;
    }
    if (_placementState >= PlacementState.BlocksPlaced) {
      return true;
    }
    // OffsetBlockSchematic.Place does not gracefully handle unloaded chunks. It
    // will try to place all of its blocks regardless of whether the chunk is
    // loaded. The world gen block accessor will internally log an error when it
    // tries to place the block in an unloaded chunk, then drop the request. So
    // to avoid that, this first verifies that all of the chunks are loaded.
    if (!LoadAllChunks(accessor)) {
      return false;
    }
    Schematic.Place(accessor, _supervisor.WorldForResolve, Offset);
    if (accessor is IBlockAccessorRevertable) {
      // SchematicPlacer.Place skipped placing the block entities and entities
      // if the accessor was a IBlockAccessorRevertable.
      _placementState = PlacementState.BlocksPlaced;
    } else {
      _placementState = PlacementState.EntitiesPlaced;
    }
    return true;
  }

  /// <summary>
  /// Call this to initialize the remaining fields after the object has been
  /// deserialized.
  /// </summary>
  /// <param name="supervisor"></param>
  public void Restore(ISchematicPlacerSupervisor supervisor) {
    _supervisor = supervisor;
  }

  public bool Commit(IBlockAccessor accessor) {
    if (_placementState >= PlacementState.EntitiesPlaced) {
      return true;
    }
    if (!LoadAllChunks(accessor)) {
      return false;
    }
    Schematic.PlaceEntitiesAndBlockEntities(
        accessor, _supervisor.WorldForResolve, Offset);
    _placementState = PlacementState.EntitiesPlaced;
    return true;
  }
}
