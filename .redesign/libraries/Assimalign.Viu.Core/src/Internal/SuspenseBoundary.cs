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

    internal void Register(
        Task dependency,
        RuntimeComponentContext context,
        bool rethrowIfUnhandled)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(context);
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (!_dependencies.Add(dependency))
        {
            return;
        }

        _ = ObserveAsync(dependency, context, rethrowIfUnhandled);
    }

    public void Dispose()
    {
        _isDisposed = true;
        Resolved = null;
        _dependencies.Clear();
    }

    private async Task ObserveAsync(
        Task dependency,
        RuntimeComponentContext context,
        bool rethrowIfUnhandled)
    {
        try
        {
            await dependency.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (context.Lifecycle.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            context.RouteError(
                exception,
                "suspense asynchronous dependency",
                rethrowIfUnhandled);
        }
        finally
        {
            if (!_isDisposed
                && _dependencies.Remove(dependency)
                && _dependencies.Count == 0)
            {
                Resolved?.Invoke();
            }
        }
    }
}
