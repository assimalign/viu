using System;
using System.Collections.Generic;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateContext : IStateContext
{
    private List<Action>? _initializationNotifications;

    internal StateContext(
        IReactiveEffectScope scope,
        IServiceProvider? services,
        IReactiveWatchScheduler? watchScheduler)
    {
        Scope = scope;
        Services = services;
        WatchScheduler = watchScheduler;
    }

    public IReactiveEffectScope Scope { get; }

    public IServiceProvider? Services { get; }

    public IReactiveWatchScheduler? WatchScheduler { get; }

    internal bool IsInitializing { get; set; }

    internal void DeferNotification(Action notification)
    {
        _initializationNotifications ??= new List<Action>();
        if (!_initializationNotifications.Contains(notification))
        {
            _initializationNotifications.Add(notification);
        }
    }

    internal void CompleteInitialization()
    {
        IsInitializing = false;
        List<Action>? notifications = _initializationNotifications;
        _initializationNotifications = null;
        if (notifications is null)
        {
            return;
        }

        foreach (Action notification in notifications)
        {
            notification();
        }
    }
}
