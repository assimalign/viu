using System;

namespace Assimalign.Viu.DevTools;

/// <summary>Controls bounded telemetry buffering and safe snapshot expansion for a diagnostics session.</summary>
public sealed class DevToolsSessionOptions
{
    private int _bufferCapacity = 256;
    private int _maximumSnapshotDepth = 8;
    private int _maximumCollectionEntries = 64;

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
