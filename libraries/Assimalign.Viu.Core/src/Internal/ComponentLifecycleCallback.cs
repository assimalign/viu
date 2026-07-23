using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Stores one typed lifecycle callback without reflection-based invocation.</summary>
internal readonly struct ComponentLifecycleCallback
{
    private readonly Action? _synchronousCallback;
    private readonly Func<Task>? _asynchronousCallback;
    private readonly Func<CancellationToken, Task>? _cancellableCallback;

    internal ComponentLifecycleCallback(Action callback)
    {
        _synchronousCallback = callback;
        _asynchronousCallback = null;
        _cancellableCallback = null;
    }

    internal ComponentLifecycleCallback(Func<Task> callback)
    {
        _synchronousCallback = null;
        _asynchronousCallback = callback;
        _cancellableCallback = null;
    }

    internal ComponentLifecycleCallback(Func<CancellationToken, Task> callback)
    {
        _synchronousCallback = null;
        _asynchronousCallback = null;
        _cancellableCallback = callback;
    }

    internal void Invoke(ComponentContext context, string diagnosticInformation)
    {
        try
        {
            if (_synchronousCallback is not null)
            {
                context.Run(_synchronousCallback);
                return;
            }

            Func<Task>? asynchronousCallback = _asynchronousCallback;
            Func<CancellationToken, Task>? cancellableCallback = _cancellableCallback;
            Task task = context.Run(
                () => asynchronousCallback is not null
                    ? asynchronousCallback()
                    : cancellableCallback!(context.Lifecycle.CancellationToken));
            context.ObserveTask(task, diagnosticInformation);
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(exception, context, diagnosticInformation);
        }
    }

    internal async Task InvokeAndAwaitAsync(
        ComponentContext context,
        string diagnosticInformation)
    {
        try
        {
            if (_synchronousCallback is not null)
            {
                context.Run(_synchronousCallback);
                return;
            }

            Func<Task>? asynchronousCallback = _asynchronousCallback;
            Func<CancellationToken, Task>? cancellableCallback = _cancellableCallback;
            Task task = context.Run(
                () => asynchronousCallback is not null
                    ? asynchronousCallback()
                    : cancellableCallback!(context.Lifecycle.CancellationToken));
            if (task is null)
            {
                throw new InvalidOperationException(
                    "An asynchronous lifecycle callback returned a null task.");
            }

            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (context.Lifecycle.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(exception, context, diagnosticInformation);
        }
    }
}
