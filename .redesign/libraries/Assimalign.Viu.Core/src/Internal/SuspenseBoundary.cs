using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Assimalign.Viu;

internal sealed class SuspenseBoundary : IDisposable
{
    private readonly HashSet<Task> _dependencies = [];
    private bool _isDisposed;

    internal event Action? Resolved;

    internal int PendingCount => _dependencies.Count;

    internal void Register(Task dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_dependencies.Add(dependency))
        {
            return;
        }
    }

    internal void Settle(Task dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        if (!_isDisposed
            && _dependencies.Remove(dependency)
            && _dependencies.Count == 0)
        {
            Resolved?.Invoke();
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        Resolved = null;
        _dependencies.Clear();
    }
}
