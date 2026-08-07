using System;
using System.Runtime.ExceptionServices;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State;

internal sealed class StateStoreEntry : IDisposable
{
    private bool _isDisposed;

    internal StateStoreEntry(
        object definition,
        object instance,
        IReactiveEffectScope scope)
    {
        Definition = definition;
        Instance = instance;
        Scope = scope;
    }

    internal object Definition { get; }

    internal object Instance { get; }

    internal IReactiveEffectScope Scope { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ExceptionDispatchInfo? error = null;
        try
        {
            Scope.Stop();
        }
        catch (Exception exception)
        {
            error = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception exception)
        {
            error ??= ExceptionDispatchInfo.Capture(exception);
        }

        error?.Throw();
    }
}
