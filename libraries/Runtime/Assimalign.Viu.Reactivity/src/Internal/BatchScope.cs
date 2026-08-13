using System;

namespace Assimalign.Viu.Reactivity;

/// <summary>Owns exactly one nesting level in the ambient reactive batch.</summary>
internal sealed class BatchScope : IDisposable
{
    private bool _isDisposed;

    /// <summary>Opens the batch level owned by this scope.</summary>
    internal BatchScope() => ReactivityState.StartBatch();

    /// <summary>Closes this scope's batch level once.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ReactivityState.EndBatch();
    }
}
