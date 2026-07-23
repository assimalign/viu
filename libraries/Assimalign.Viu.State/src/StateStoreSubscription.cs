using System;

namespace Assimalign.Viu.State;

/// <summary>
/// A removable state or action subscription. Stopping and disposing are idempotent.
/// </summary>
/// <remarks>Not thread-safe; designed for Viu's single-threaded event-loop model.</remarks>
public sealed class StateStoreSubscription : IDisposable
{
    private Action? _remove;

    internal StateStoreSubscription(Action remove)
    {
        _remove = remove;
    }

    /// <summary>Gets whether the subscription is still registered.</summary>
    public bool IsActive => _remove is not null;

    /// <summary>Removes the subscription. Repeated calls do nothing.</summary>
    public void Stop()
    {
        Action? remove = _remove;
        if (remove is null)
        {
            return;
        }

        _remove = null;
        remove();
    }

    /// <summary>Removes the subscription.</summary>
    public void Dispose() => Stop();
}
