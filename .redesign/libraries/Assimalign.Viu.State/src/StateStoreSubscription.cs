using System;

namespace Assimalign.Viu.State;

/// <summary>
/// Controls one state or action subscription. Stopping and disposing are idempotent. Specified by
/// <c>[STA-5]</c> and <c>[STA-8]</c>.
/// </summary>
/// <remarks>This type is not thread-safe and targets Viu's single-threaded event-loop model.</remarks>
public sealed class StateStoreSubscription : IDisposable
{
    private Action? _remove;

    internal StateStoreSubscription(Action remove)
    {
        _remove = remove;
    }

    /// <summary>Gets whether the callback remains registered.</summary>
    public bool IsActive => _remove is not null;

    /// <summary>Removes the callback. Repeated calls have no effect.</summary>
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

    /// <summary>Removes the callback; equivalent to <see cref="Stop"/>.</summary>
    public void Dispose() => Stop();
}
