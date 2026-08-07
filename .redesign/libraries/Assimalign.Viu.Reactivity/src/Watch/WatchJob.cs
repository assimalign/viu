using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Provides stable identity and ordering information for one scheduled watch callback.
/// </summary>
public sealed class WatchJob
{
    private readonly Action _callback;

    /// <summary>Initializes a stable watch job.</summary>
    /// <param name="sequence">The host-relative ordering sequence.</param>
    /// <param name="callback">The callback invoked for each accepted schedule.</param>
    public WatchJob(long sequence, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Sequence = sequence;
        _callback = callback;
    }

    /// <summary>Gets the host-relative ordering sequence.</summary>
    public long Sequence { get; }

    /// <summary>Runs the callback once.</summary>
    public void Run() => _callback();
}
