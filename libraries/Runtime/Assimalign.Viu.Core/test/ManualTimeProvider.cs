using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Core.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    internal void Advance(TimeSpan duration)
    {
        _ticks += duration.Ticks;
        foreach (ManualTimer timer in _timers.ToArray())
        {
            timer.FireIfDue();
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _clock;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private long _due = long.MaxValue;
        private long _period;
        private bool _disposed;

        internal ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state)
        {
            _clock = clock;
            _callback = callback;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : _clock._ticks + dueTime.Ticks;
            _period = period.Ticks;
            return true;
        }

        internal void FireIfDue()
        {
            if (_disposed || _clock._ticks < _due)
            {
                return;
            }

            _due = _period > 0 ? _clock._ticks + _period : long.MaxValue;
            _callback(_state);
        }

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
