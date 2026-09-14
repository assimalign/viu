using System;

namespace Assimalign.Viu.DevTools;

/// <summary>Controls bounded telemetry buffering and safe snapshot expansion for a diagnostics session.</summary>
public sealed class DevToolsSessionOptions
{
    private int _bufferCapacity = 256;
    private int _maximumSnapshotDepth = 8;
    private int _maximumCollectionEntries = 64;
    private int _timelineCapacity = 1024;
    private int _timelineSamplingInterval = 1;
    private int _maximumTimelineEventsPerSecond = 10_000;

    /// <summary>
    /// Gets or sets the fixed timeline ring capacity, captured when a session is constructed.
    /// Overflow evicts the oldest event and reports its loss. Specified by <c>[DVT-11]</c>.
    /// </summary>
    public int TimelineCapacity
    {
        get => _timelineCapacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _timelineCapacity = value;
        }
    }

    /// <summary>
    /// Gets or sets deterministic sampling: one captures every candidate; N captures the first
    /// and every Nth candidate thereafter. Captured at construction. Specified by <c>[DVT-11]</c>.
    /// </summary>
    public int TimelineSamplingInterval
    {
        get => _timelineSamplingInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _timelineSamplingInterval = value;
        }
    }

    /// <summary>
    /// Gets or sets the admitted timeline events per elapsed session second after sampling.
    /// Excess events count toward timeline loss; captured at construction. Specified by <c>[DVT-11]</c>.
    /// </summary>
    public int MaximumTimelineEventsPerSecond
    {
        get => _maximumTimelineEventsPerSecond;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumTimelineEventsPerSecond = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum queued renderer-telemetry messages before oldest-first dropping.
    /// Reliable control messages do not consume this capacity.
    /// </summary>
    public int BufferCapacity
    {
        get => _bufferCapacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _bufferCapacity = value;
        }
    }

    /// <summary>Gets or sets the largest client-requested snapshot expansion depth.</summary>
    public int MaximumSnapshotDepth
    {
        get => _maximumSnapshotDepth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maximumSnapshotDepth = value;
        }
    }

    /// <summary>Gets or sets the maximum dictionary or sequence entries encoded at one level.</summary>
    public int MaximumCollectionEntries
    {
        get => _maximumCollectionEntries;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumCollectionEntries = value;
        }
    }
}
