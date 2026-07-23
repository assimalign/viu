using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Store;

/// <summary>
/// The per-invocation context passed to every <see cref="Delegates.StoreActionCallback"/> registered
/// with <see cref="Store{TState}.OnAction"/> — the C# port of the object Pinia's <c>$onAction</c>
/// callback receives (<c>{ name, store, after, onError }</c>;
/// https://pinia.vuejs.org/core-concepts/actions.html#Subscribing-to-actions,
/// <c>packages/pinia/src/store.ts</c>). A fresh context is created for each action run and shared
/// across all action subscribers; each may register an <see cref="After"/> hook (invoked with the
/// action's resolved return value, including an awaited <see cref="System.Threading.Tasks.Task"/>/
/// <see cref="System.Threading.Tasks.ValueTask"/> result) and an <see cref="OnError"/> hook (invoked
/// with a thrown exception). Not thread-safe (single-threaded JS event-loop model).
/// <para>
/// Divergence from Pinia: there is no JS <c>Proxy</c>, so actions opt into observation by routing
/// their body through <see cref="Store{TState}.RunAction(string, System.Action)"/> (and overloads);
/// <see cref="Arguments"/> is therefore not captured (upstream reads them from the proxied call), and
/// <see cref="Store"/> is typed as <see cref="object"/> — cast it to your concrete store type.
/// </para>
/// </summary>
public sealed class StoreActionContext
{
    private List<Action<object?>>? _afterCallbacks;
    private List<Action<Exception>>? _errorCallbacks;

    internal StoreActionContext(string name, object store)
    {
        Name = name;
        Store = store;
    }

    /// <summary>The name of the action being invoked (upstream: <c>context.name</c>).</summary>
    public string Name { get; }

    /// <summary>
    /// The store instance the action belongs to (upstream: <c>context.store</c>). Typed as
    /// <see cref="object"/> because <see cref="Store{TState}"/> does not know the concrete store type;
    /// cast it to your store class.
    /// </summary>
    public object Store { get; }

    /// <summary>
    /// Registers a hook invoked after the action resolves successfully, with its return value — the
    /// C# port of upstream <c>after(callback)</c>. For an <c>async</c> action routed through
    /// <see cref="Store{TState}.RunAction(string, System.Func{System.Threading.Tasks.Task})"/> the
    /// hook receives the <em>awaited</em> result; for a <c>void</c> action it receives
    /// <see langword="null"/>. Value-type results are boxed. Hooks fire in registration order.
    /// </summary>
    /// <param name="callback">The after-hook; receives the (boxed) resolved return value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public void After(Action<object?> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        (_afterCallbacks ??= new List<Action<object?>>()).Add(callback);
    }

    /// <summary>
    /// Registers a hook invoked when the action throws (or its returned task faults), with the
    /// exception — the C# port of upstream <c>onError(callback)</c>. The exception still propagates to
    /// the caller after the hooks run; hooks fire in registration order.
    /// </summary>
    /// <param name="callback">The error-hook; receives the thrown exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is null.</exception>
    public void OnError(Action<Exception> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        (_errorCallbacks ??= new List<Action<Exception>>()).Add(callback);
    }

    internal void RunAfter(object? result)
    {
        if (_afterCallbacks is null)
        {
            return;
        }
        foreach (var callback in _afterCallbacks)
        {
            callback(result);
        }
    }

    internal void RunError(Exception exception)
    {
        if (_errorCallbacks is null)
        {
            return;
        }
        foreach (var callback in _errorCallbacks)
        {
            callback(exception);
        }
    }
}
