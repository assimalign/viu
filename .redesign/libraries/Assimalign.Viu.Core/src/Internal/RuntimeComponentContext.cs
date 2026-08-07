using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class RuntimeComponentContext : ComponentContext
{
    private readonly IReadOnlyDictionary<string, ComponentEventListener> _listeners;
    private object? _exposedValue;

    internal RuntimeComponentContext(
        ComponentBindings bindings,
        IServiceProvider? services,
        ComponentLifecycle lifecycle,
        IReadOnlyDictionary<string, ComponentEventListener> listeners,
        IReactiveEffectScope scope,
        IReactiveWatchScheduler? watchScheduler,
        ComponentContext? parent)
    {
        Bindings = bindings;
        Services = services;
        Lifecycle = lifecycle;
        _listeners = listeners;
        Scope = scope;
        WatchScheduler = watchScheduler;
        Parent = parent;
    }

    public override ComponentBindings Bindings { get; }

    public override IServiceProvider? Services { get; }

    public override ComponentLifecycle Lifecycle { get; }

    public override IReactiveEffectScope Scope { get; }

    public override IReactiveWatchScheduler? WatchScheduler { get; }

    public override ComponentContext? Parent { get; }

    internal object? ExposedValue => _exposedValue;

    public override void Emit(string name, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(arguments);
        if (_listeners.TryGetValue(name, out var listener))
        {
            listener(Array.AsReadOnly(arguments));
        }
    }

    public override void Expose(object? value) => _exposedValue = value;

    public override void Warn(string message)
    {
        // The contract model has no application warning channel; the shipping runtime routes
        // the message through application composition.
        _ = message;
    }

    protected override void OnWatchError(Exception exception)
    {
        // The shipping runtime routes watch failures to OnErrorCaptured; the contract model
        // only fixes the seam.
        _ = exception;
    }
}
