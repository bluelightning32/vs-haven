using System;
using System.Collections.Generic;

using Vintagestory.API.Common;

namespace Haven;

/// <summary>
/// Keeps a schedule of which callbacks to run when, then runs them when their
/// scheduled time is reached. The time unit for the schedule is in game hours.
/// </summary>
public class CallbackScheduler : IDisposable {
  private readonly SortedSet<Tuple<double, Action<double>>> _schedule = new();
  ICoreAPI _api = null;
  private long _tickListener = -1;

  public void Start(ICoreAPI api) { _api = api; }

  public void Dispose() {
    GC.SuppressFinalize(this);
    _schedule.Clear();
    MaybeCancelTickListener();
  }

  public void Schedule(double when, Action<double> action) {
    _schedule.Add(new(when, action));
    MaybeStartTickListener();
  }

  public bool Cancel(double when, Action<double> action) {
    if (!_schedule.Remove(new(when, action))) {
      return false;
    }
    MaybeCancelTickListener();
    return true;
  }

  private void OnTick(float dt) {
    double now = _api.World.Calendar.TotalHours;
    while (true) {
      if (_schedule.Count == 0) {
        MaybeCancelTickListener();
        return;
      }
      Tuple<double, Action<double>> first = _schedule.Min;
      if (first.Item1 <= now) {
        _schedule.Remove(first);
        first.Item2(now);
      } else {
        return;
      }
    }
  }

  private void MaybeStartTickListener() {
    if (_tickListener == -1 && _schedule.Count > 0) {
      _api.Logger.Debug("Registering tick listener for CallbackScheduler");
      // Register the callback at a prime number close to 1000. This is to
      // reduce the chances of too many tick callbacks getting scheduled at the
      // same time.
      _tickListener = _api.World.RegisterGameTickListener(OnTick, 991, 0);
    }
  }

  private void MaybeCancelTickListener() {
    if (_tickListener != -1 && _schedule.Count == 0) {
      _api.Logger.Debug("Unregistering tick listener for CallbackScheduler");
      _api.World.UnregisterGameTickListener(_tickListener);
      _tickListener = -1;
    }
  }
}
