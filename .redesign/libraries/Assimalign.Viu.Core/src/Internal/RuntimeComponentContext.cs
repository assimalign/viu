using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class RuntimeComponentContext : ComponentContext
{
    private readonly IReadOnlyDictionary<string, ComponentEventListener> _listeners;
    private readonly Action<Exception, ComponentContext?, string>? _errorHandler;
    private object? _exposedValue;

    internal RuntimeComponentContext(
        ComponentBindings bindings,
        IServiceProvider? services,
        ComponentLifecycle lifecycle,
        IReadOnlyDictionary<string, ComponentEventListener> listeners,
        IReactiveEffectScope scope,
        IReactiveWatchScheduler? watchScheduler,
        ComponentContext? parent,
        Action<Exception, ComponentContext?, string>? errorHandler)
    {
        Bindings = bindings;
        Services = services;
        Lifecycle = lifecycle;
        _listeners = listeners;
        Scope = scope;
        WatchScheduler = watchScheduler;
        Parent = parent;
        _errorHandler = errorHandler;
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
            try
            {
                listener(Array.AsReadOnly(arguments));
            }
            catch (Exception exception)
            {
                RouteError(exception, "component event listener");
            }
        }
    }

    public override void Expose(object? value) => _exposedValue = value;

    public override void Warn(string message)
    {
        // The contract model has no application warning channel; the shipping runtime routes
        // the message through application composition.
        _ = message;
    }

    internal void RouteError(Exception exception, string diagnosticInformation)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrEmpty(diagnosticInformation);

        ComponentContext? source = this;
        for (ComponentContext? ancestor = Parent;
            ancestor is not null;
            ancestor = ancestor.Parent)
        {
            try
            {
                if (!ancestor.Lifecycle.InvokeErrorCaptured(
                    exception,
                    source,
                    diagnosticInformation))
                {
                    return;
                }
            }
            catch (Exception captureException)
            {
                exception = captureException;
                source = ancestor;
            }
        }

        if (_errorHandler is not null)
        {
            _errorHandler(exception, source, diagnosticInformation);
            return;
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    protected override void OnWatchError(Exception exception) =>
        RouteError(exception, "component watch callback");
}
