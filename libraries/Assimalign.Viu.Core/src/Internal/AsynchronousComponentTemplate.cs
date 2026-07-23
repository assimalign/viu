using System;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>Owns the per-mount state of an asynchronous component definition.</summary>
internal sealed class AsynchronousComponentTemplate :
    IComponentTemplate,
    IComponentRootBehaviorForwarder,
    IDisposable
{
    private readonly AsynchronousComponentDefinition _definition;
    private AsynchronousComponentLoadLease? _load;
    private ComponentContext? _context;
    private Reference<bool>? _loaded;
    private Reference<bool>? _delayed;
    private Reference<Exception?>? _error;
    private AsynchronousComponentTarget _target;
    private IDisposable? _delayTimer;
    private IDisposable? _timeoutTimer;
    private bool _hasTarget;
    private bool _isActive;

    internal AsynchronousComponentTemplate(
        AsynchronousComponentDefinition definition)
    {
        _definition = definition;
    }

    public string? Name => "AsynchronousComponentWrapper";

    public ComponentFlags Flags => ComponentFlags.None;

    public ComponentRenderer Setup(IComponentContext context)
    {
        _context = context as ComponentContext
            ?? throw new InvalidOperationException(
                "The asynchronous component wrapper requires Core's mounted component context.");
        _isActive = true;
        _load = _definition.AcquireLoad();
        context.Lifecycle.OnBeforeUnmount(Dispose);

        AsynchronousComponentOptions options = _definition.Options;
        ISuspenseBoundary? boundary = options.Suspensible
            ? _context.SuspenseBoundary
            : null;
        bool suspenseControlled = boundary is not null;
        _loaded = Reactive.Reference(false);
        _delayed = Reactive.Reference(
            !suspenseControlled && options.Delay > 0);
        _error = Reactive.Reference<Exception?>(null);

        if (_load.PendingLoad.IsCompletedSuccessfully)
        {
            _target = _load.PendingLoad.Result;
            _hasTarget = true;
            _loaded.Value = true;
        }
        else
        {
            if (!suspenseControlled)
            {
                SchedulePresentation(options);
            }

            Task trackedLoad = TrackLoadAsync(_load.PendingLoad);
            context.Lifecycle.OnServerPrefetch(() => trackedLoad);
            boundary?.RegisterAsynchronousDependency(
                context,
                _load.PendingLoad);
        }

        return suspenseControlled
            ? RenderSuspenseControlled
            : Render;
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
        Task<AsynchronousComponentTarget> pendingLoad)
    {
        try
        {
            AsynchronousComponentTarget target = await pendingLoad;
            if (!_isActive)
            {
                return;
            }

            _target = target;
            _hasTarget = true;
            _loaded!.Value = true;
        }
        catch (OperationCanceledException) when (!_isActive)
        {
        }
        catch (Exception error)
        {
            if (_isActive)
            {
                HandleError(error);
            }
        }
    }

    private void HandleError(Exception error)
    {
        _error!.Value = error;
        bool hasErrorPresentation = _definition.Options.ErrorComponent is not null;
        if (hasErrorPresentation)
        {
            ComponentErrorHandling.Handle(
                error,
                _context,
                "asynchronous component loader",
                rethrowIfUnhandled: false);
        }
        else
        {
            ComponentErrorHandling.HandleObservedTaskError(
                error,
                _context!,
                "asynchronous component loader");
        }
    }

    private IComponent RenderSuspenseControlled()
    {
        return _loaded!.Value && _hasTarget
            ? CreateResolvedComponent()
            : ComponentTree.Comment();
    }

    private IComponent Render()
    {
        if (_loaded!.Value && _hasTarget)
        {
            return CreateResolvedComponent();
        }

        if (_error!.Value is { } error
            && _definition.Options.ErrorComponent is { } errorComponent)
        {
            return errorComponent(error);
        }

        if (!_delayed!.Value
            && _definition.Options.LoadingComponent is { } loadingComponent)
        {
            return loadingComponent() ?? ComponentTree.Comment();
        }

        return ComponentTree.Comment();
    }

    private IComponent CreateResolvedComponent()
    {
        ITemplateComponent request = _context!.Request;
        return _target.CreateComponent(
            request.Arguments,
            request.Slots,
            request.Key,
            request.Listeners,
            request.Directives,
            request.Reference);
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
        _load?.Dispose();
        _load = null;
    }
}
