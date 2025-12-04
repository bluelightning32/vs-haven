using System;

using ProtoBuf;

using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Haven;

interface IWorldGenerator {
  /// <summary>
  /// Generate as much of the world as possible. If the method returns false,
  /// then call it again when more chunks are loaded (especially if the
  /// generator asks to load more chunks through other interfaces). Any extra
  /// calls after the generator is complete are no-ops.
  ///
  /// This method is thread safe. It may be called from the main thread with the
  /// default accessor and from the world generation thread with the world
  /// generation accessor.
  /// </summary>
  /// <param name="accessor"></param>
  /// <returns>
  /// true if the generation is complete, or false if Generate should be called
  /// again
  /// </returns>
  public bool Generate(IBlockAccessor accessor);

  /// <summary>
  /// If a IBlockAccessorRevertable was used, then call this after the blocks
  /// are committed.
  /// </summary>
  /// <param name="accessor"></param>
  /// <returns>
  /// true if all the block entites were committed, or false if Commit should be
  /// called again.
  /// </returns>
  public bool Commit(IBlockAccessor accessor);
}

/// <summary>
/// Searches for the center for a disk, such that the that the disk is
/// sufficiently flat. The search is done in a spiral pattern from the given
/// start location.
/// </summary>
[ProtoContract]
public class FlatDiskLocator : IWorldGenerator {
  [ProtoMember(1)]
  private readonly Vec2i _start;

  [ProtoMember(2)]
  private readonly int _radius;

  [ProtoMember(3)]
  private readonly SquareSpiral _searchOffset = new();

  /// <summary>
  /// Reject locations which are more than _maxRoughness
  /// </summary>
  [ProtoMember(4)]
  private readonly int _maxRoughness;
  /// <summary>
  /// Reject locations that have a lower solid block at the surface level ratio
  /// than this
  /// </summary>
  [ProtoMember(5)]
  private readonly double _minSolid;

  [ProtoMember(6)]
  private bool _done = false;
  [ProtoMember(7)]
  private int _y = 0;

  private ILogger _logger;
  private TerrainSurvey _terrain;

  public const int MaxAttempts = 200;

  /// <summary>
  /// Survey the area for a suitable location for a haven. Note that the
  /// roughness parameters are combined together into a total allowed roughness.
  /// They are passed as separate parameters because the radius affects them
  /// differently.
  /// </summary>
  /// <param name="terrain"></param>
  /// <param name="start"></param>
  /// <param name="radius"></param>
  /// <param name="maxRoughnessPerimeter">
  /// the roughness allowed per block of the perimeter</param>
  /// <param name="maxRoughnessArea">
  /// the roughness allowed per block of the center
  /// </param>
  /// <param name="minSolid">minimum land (as opposed to water) ratio</param>
  public FlatDiskLocator(ILogger logger, TerrainSurvey terrain, Vec2i start,
                         int radius, double maxRoughnessPerimeter,
                         double maxRoughnessArea, double minSolid) {
    _logger = logger;
    _terrain = terrain;
    _start = start.Copy();
    _radius = radius;
    double perimeter = Math.Tau * radius;
    int estimatedArea = (int)(Math.PI * radius * radius);
    _maxRoughness = (int)(maxRoughnessPerimeter * perimeter +
                          maxRoughnessArea * estimatedArea);
    _minSolid = minSolid;
  }

  /// <summary>
  /// Constructor for deserialization
  /// </summary>
  private FlatDiskLocator() {}

  /// <summary>
  /// Call this to initialize the remaining fields after the object has been
  /// deserialized.
  /// </summary>
  /// <param name="terrain"></param>
  public void Restore(ILogger logger, TerrainSurvey terrain) {
    _logger = logger;
    _terrain = terrain;
  }

  public Vec2i Center2D {
    get { return _start + _searchOffset.SquareOffset * (_radius / 4); }
  }

  public BlockPos Center {
    get {
      Vec2i center = Center2D;
      return new BlockPos(center.X, _y, center.Y, Dimensions.NormalWorld);
    }
  }

  public bool Done {
    get { return _done; }
  }

  public bool Generate(IBlockAccessor accessor) {
    if (_done) {
      return true;
    }
    while (!_done) {
      if (IsGoodLocation(accessor, out bool incomplete)) {
        _done = true;
        break;
      }
      if (incomplete) {
        return false;
      }
      _searchOffset.Next();
      if (Failed) {
        _done = true;
        break;
      }
    }
    return true;
  }

  private bool IsGoodLocation(IBlockAccessor accessor, out bool incomplete) {
    Vec2i center = Center2D;
    incomplete = false;
    TerrainStats stats = _terrain.GetDiskStats(accessor, center, _radius,
                                               out int area, ref incomplete);
    // The roughness check may exclude the location even before all of the
    // chunks are surveyed.
    if (stats.Roughness > _maxRoughness) {
      return false;
    }
    if (incomplete) {
      return false;
    }
    if (stats.SolidCount < _minSolid * area) {
      return false;
    }
    _y = stats.SumHeight / area;
    _logger.Build(
        $"Located flat disk at ({Center}) with radius {_radius} and " +
        $"roughness {stats.Roughness / (double)area} and " +
        $"{stats.SolidCount / (double)area} solid ratio.");
    return true;
  }

  public bool Commit(IBlockAccessor accessor) { return true; }

  public bool Failed {
    get { return _searchOffset.Index >= MaxAttempts; }
  }
}
