using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// Owns reactive effects and watch handles that must stop as one lifetime.
/// </summary>
/// <remarks>This model is single-threaded and intended for the host event loop.</remarks>
public sealed class EffectScope : IReactiveEffectScope
{
    private readonly List<IDisposable> _resources = new();

    /// <summary>Gets whether the scope has released all owned resources.</summary>
    public bool IsStopped { get; private set; }

    /// <inheritdoc />
    public bool IsActive => !IsStopped;

    /// <summary>Adds a resource to this scope's reverse-order teardown.</summary>
    /// <param name="resource">The resource owned by the scope.</param>
    public void Own(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ObjectDisposedException.ThrowIf(IsStopped, this);
        _resources.Add(resource);
    }

    /// <inheritdoc />
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(IsStopped, this);
        action();
    }

    /// <inheritdoc />
    public TResult Run<TResult>(Func<TResult> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(IsStopped, this);
        return function();
    }

    /// <summary>Stops the scope and releases every owned resource in reverse registration order.</summary>
    public void Stop()
    {
        if (IsStopped)
        {
            return;
        }

        IsStopped = true;
        for (var index = _resources.Count - 1; index >= 0; index--)
        {
            _resources[index].Dispose();
        }

        _resources.Clear();
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
