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
}

/// <summary>
/// Searches for the center for a circle, such that the that the circle is
/// sufficiently flat. The search is done in a spiral pattern from the given
/// start location.
/// </summary>
public class FlatCircleLocator : IWorldGenerator {
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
  /// Reject locations that have fewer blocks above seaLevel than this
  /// </summary>
  [ProtoMember(5)]
  private readonly int _minAboveSea;

  [ProtoMember(6)]
  private bool _done = false;

  private int _circleArea;

  private TerrainSurvey _terrain;

  public const int MaxAttempts = 100;

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
  /// <param name="minAboveSea">minimum land (as opposed to water) ratio</param>
  public FlatCircleLocator(TerrainSurvey terrain, Vec2i start, int radius,
                           double maxRoughnessPerimeter,
                           double maxRoughnessArea, double minAboveSea) {
    _terrain = terrain;
    _start = start.Copy();
    _radius = radius;
    double perimeter = Math.Tau * radius;
    _circleArea = (int)(Math.PI * radius * radius);
    _maxRoughness = (int)(maxRoughnessPerimeter * perimeter +
                          maxRoughnessArea * _circleArea);
    _minAboveSea = (int)(minAboveSea * _circleArea);
  }

  /// <summary>
  /// Call this to initialize the remaining fields after the object has been
  /// deserialized.
  /// </summary>
  /// <param name="terrain"></param>
  public void Restore(TerrainSurvey terrain) {
    _terrain = terrain;
    _circleArea = (int)(Math.PI * _radius * _radius);
  }

  public Vec2i Center {
    get { return _start + _searchOffset.SquareOffset * (_radius / 4); }
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
    Vec2i center = Center;
    incomplete = false;
    TerrainStats stats = _terrain.GetRoughCircleStats(
        accessor, center, _radius, out int chunkCount, ref incomplete);
    int surveyedArea =
        chunkCount * GlobalConstants.ChunkSize * GlobalConstants.ChunkSize;
    // The roughness check may exclude the location even before all of the
    // chunks are surveyed.
    if (stats.Roughness * _circleArea / surveyedArea > _maxRoughness) {
      return false;
    }
    if (incomplete) {
      return false;
    }
    if (stats.AboveSea * _circleArea / surveyedArea < _minAboveSea) {
      return false;
    }
    return true;
  }

  public bool Failed {
    get { return _searchOffset.Index >= MaxAttempts; }
  }
}
