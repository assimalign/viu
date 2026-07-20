using System;

namespace Assimalign.Viu.Store;

/// <summary>
/// The removable handle returned by <see cref="Store{TState}.Subscribe"/> and
/// <see cref="Store{TState}.OnAction"/> — the C# port of the stop function Pinia's <c>$subscribe</c>
/// and <c>$onAction</c> return (<c>packages/pinia/src/subscriptions.ts</c>). Calling <see cref="Stop"/>
/// (or disposing) removes the subscription. A subscription created inside an active
/// <see cref="Assimalign.Viu.Reactivity.EffectScope"/> (for example a component's) is also removed
/// automatically when that scope stops, unless it was created <c>detached</c>. Stopping is idempotent.
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class StoreSubscription : IDisposable
{
    private Action? _remove;

    internal StoreSubscription(Action remove) => _remove = remove;

    /// <summary>Whether the subscription is still registered.</summary>
    public bool IsActive => _remove is not null;

    /// <summary>
    /// Removes the subscription so its callback no longer fires — the C# port of calling the stop
    /// function Pinia returns. Idempotent: a second call does nothing.
    /// </summary>
    public void Stop()
    {
        var remove = _remove;
        if (remove is null)
        {
            return;
        }
        _remove = null;
        remove();
    }

    /// <summary>Removes the subscription; equivalent to <see cref="Stop"/> for <c>using</c> support.</summary>
    public void Dispose() => Stop();
}
