using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

internal sealed class AsynchronousComponentWrapper : IComponent, IDisposable
{
    private readonly AsynchronousComponentDefinition _definition;
    private AsynchronousComponentLoadLease? _load;
    private IAsynchronousComponentRuntime? _runtime;
    private ComponentInvocation _fallbackInvocation = ComponentInvocation.Empty;
    private Reference<bool>? _loaded;
    private Reference<bool>? _delayed;
    private Reference<Exception?>? _error;
    private AsynchronousComponentTarget _target;
    private IDisposable? _delayTimer;
    private IDisposable? _timeoutTimer;
    private bool _hasTarget;
    private bool _isActive;
    private bool _suspenseControlled;

    internal AsynchronousComponentWrapper(AsynchronousComponentDefinition definition)
    {
        _definition = definition;
    }

    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _isActive = true;
        _load = _definition.AcquireLoad();
        context.Lifecycle.OnBeforeUnmount(Dispose);

        IAsynchronousComponentRuntime? runtime = context as IAsynchronousComponentRuntime;
        _runtime = runtime;
        if (runtime is null)
        {
            _fallbackInvocation = CreateFallbackInvocation(context);
        }

        AsynchronousComponentOptions options = _definition.Options;
        _loaded = Reactive.Reference(false);
        _delayed = Reactive.Reference(options.Delay > 0);
        _error = Reactive.Reference<Exception?>(null);

        if (_load.PendingLoad.IsCompletedSuccessfully)
        {
            _target = _load.PendingLoad.Result;
            _hasTarget = true;
            _loaded.Value = true;
            _delayed.Value = false;
        }
        else
        {
            Task<AsynchronousComponentTarget> pendingLoad = _load.PendingLoad;
            if (options.Suspensible && runtime is not null)
            {
                _suspenseControlled = runtime.RegisterAsynchronousDependency(
                    pendingLoad);
            }

            if (_suspenseControlled)
            {
                _delayed.Value = false;
            }
            else
            {
                SchedulePresentation(options);
            }

            Task trackedLoad = TrackLoadAsync(pendingLoad, runtime);
            context.Lifecycle.OnServerPrefetch(() => trackedLoad);
        }

        return Render;
    }

    private static ComponentInvocation CreateFallbackInvocation(ComponentContext context)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> parameter in context.Bindings.Parameters)
        {
            arguments[parameter.Key] = parameter.Value;
        }

        foreach (KeyValuePair<string, object?> binding in context.Bindings.FallthroughBindings)
        {
            arguments[binding.Key] = binding.Value;
        }

        return new ComponentInvocation(arguments, context.Bindings.Slots);
    }

    private void SchedulePresentation(AsynchronousComponentOptions options)
    {
        if (options.Delay > 0)
        {
            _delayTimer = AsynchronousComponentDelay.Schedule(
                options.Delay,
                () =>
                {
                    if (_isActive && !_loaded!.Value && _error!.Value is null)
                    {
                        _delayed!.Value = false;
                    }
                });
        }
        else
        {
            _delayed!.Value = false;
        }

        if (options.Timeout is not { } timeout)
        {
            return;
        }

        _timeoutTimer = AsynchronousComponentDelay.Schedule(
            timeout,
            () =>
            {
                if (_isActive && !_loaded!.Value && _error!.Value is null)
                {
                    HandleError(
                        new TimeoutException(
                            $"Asynchronous component timed out after {timeout}ms."));
                }
            });
    }

    private async Task TrackLoadAsync(
        Task<AsynchronousComponentTarget> pendingLoad,
        IAsynchronousComponentRuntime? runtime)
    {
        try
        {
            AsynchronousComponentTarget target = await pendingLoad.ConfigureAwait(false);
            if (!_isActive)
            {
                return;
            }

            _target = target;
            _hasTarget = true;
            runtime?.SettleAsynchronousDependency(pendingLoad);
            runtime = null;
            _loaded!.Value = true;
        }
        catch (OperationCanceledException) when (!_isActive)
        {
        }
        catch (Exception error)
        {
            if (_isActive)
            {
                runtime?.SettleAsynchronousDependency(pendingLoad);
                runtime = null;
                HandleError(error);
            }
        }
        finally
        {
            runtime?.SettleAsynchronousDependency(pendingLoad);
        }
    }

    private void HandleError(Exception error)
    {
        if (_error!.Value is not null)
        {
            return;
        }

        _error!.Value = error;
        _runtime?.RouteAsynchronousError(
            error,
            rethrowIfUnhandled: _definition.Options.ErrorComponent is null);
    }

    private VirtualNode? Render(ComponentRenderFrame frame)
    {
        if (_loaded!.Value && _hasTarget)
        {
            ComponentInvocation invocation = _runtime?.Invocation ?? _fallbackInvocation;
            return _target.CreateComponent(invocation, _runtime?.MountReference);
        }

        if (_error!.Value is { } error)
        {
            if (_definition.Options.ErrorComponent is { } errorComponent)
            {
                return errorComponent(error);
            }

            return new CommentNode(string.Empty);
        }

        if (!_suspenseControlled
            && !_delayed!.Value
            && _definition.Options.LoadingComponent is { } loadingComponent)
        {
            return loadingComponent(frame);
        }

        return new CommentNode(string.Empty);
    }

    public void Dispose()
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _delayTimer?.Dispose();
        _delayTimer = null;
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
        _runtime = null;
        _load?.Dispose();
        _load = null;
    }
}
