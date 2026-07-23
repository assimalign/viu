using System;
using System.Collections.Generic;

namespace Assimalign.Viu.State;

/// <summary>
/// Describes one observed state-store action invocation and collects its completion hooks.
/// </summary>
/// <remarks>
/// Viu cannot intercept arbitrary methods without a JavaScript-style proxy, so actions opt into
/// observation through <see cref="StateStore{TState}.RunAction(string,Action)"/> and its overloads.
/// </remarks>
public sealed class StateStoreActionContext
{
    private List<Action<object?>>? _afterCallbacks;
    private List<Action<Exception>>? _errorCallbacks;

    internal StateStoreActionContext(string name, object stateStore)
    {
        Name = name;
        StateStore = stateStore;
    }

    /// <summary>Gets the action name.</summary>
    public string Name { get; }

    /// <summary>Gets the concrete state-store instance that owns the action.</summary>
    public object StateStore { get; }

    /// <summary>Registers a hook invoked with the action's resolved result.</summary>
    /// <param name="callback">The completion callback.</param>
    public void After(Action<object?> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        (_afterCallbacks ??= new List<Action<object?>>()).Add(callback);
    }

    /// <summary>Registers a hook invoked when the action throws or its task faults.</summary>
    /// <param name="callback">The error callback.</param>
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

        foreach (Action<object?> callback in _afterCallbacks)
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

        foreach (Action<Exception> callback in _errorCallbacks)
        {
            callback(exception);
        }
    }
}
