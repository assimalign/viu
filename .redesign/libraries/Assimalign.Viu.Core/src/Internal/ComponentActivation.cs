using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Owns the platform-neutral portion of one persistent component mount. The renderer owns the
/// reactive render effect, mounted subtree, and client lifecycle choreography layered over it.
/// </summary>
internal sealed class ComponentActivation
{
    private const int DefaultRenderCacheSize = 64;

    private Task? _releaseTask;
    private ComponentRenderer? _renderer;

    private ComponentActivation(
        ComponentNode request,
        ComponentRegistration registration,
        IComponent instance,
        RuntimeComponentContext context,
        EffectScope scope,
        ComponentLifecycle lifecycle,
        ComponentRenderFrame frame)
    {
        Request = request;
        Registration = registration;
        Instance = instance;
        Context = context;
        Scope = scope;
        Lifecycle = lifecycle;
        Frame = frame;
    }

    internal ComponentNode Request { get; private set; }

    internal ComponentRegistration Registration { get; }

    internal IComponent Instance { get; }

    internal ComponentRenderer Renderer => _renderer
        ?? throw new InvalidOperationException("The component activation has not completed setup.");

    internal RuntimeComponentContext Context { get; }

    internal EffectScope Scope { get; }

    internal ComponentLifecycle Lifecycle { get; }

    internal ComponentRenderFrame Frame { get; }

    internal bool IsReleased { get; private set; }

    internal static ComponentActivation Activate(
        ComponentNode request,
        ComponentRuntimeOptions options,
        ComponentContext? parent = null,
        IReactiveWatchScheduler? watchScheduler = null,
        int renderCacheSize = DefaultRenderCacheSize,
        SuspenseBoundary? suspenseBoundary = null)
    {
        ComponentActivation activation = Create(
            request,
            options,
            parent,
            watchScheduler,
            renderCacheSize,
            suspenseBoundary);
        try
        {
            activation.Initialize();
            return activation;
        }
        catch (Exception exception)
        {
            ExceptionDispatchInfo failure = ExceptionDispatchInfo.Capture(exception);
            try
            {
                activation.Release();
            }
            catch
            {
                // The activation failure is the primary error; synchronous cleanup still ran as
                // far as it could, and Release observes asynchronous cleanup independently.
            }

            failure.Throw();
            throw;
        }
    }

    internal static async ValueTask<ComponentActivation> ActivateAsync(
        ComponentNode request,
        ComponentRuntimeOptions options,
        ComponentContext? parent = null,
        IReactiveWatchScheduler? watchScheduler = null,
        int renderCacheSize = DefaultRenderCacheSize,
        SuspenseBoundary? suspenseBoundary = null)
    {
        ComponentActivation activation = Create(
            request,
            options,
            parent,
            watchScheduler,
            renderCacheSize,
            suspenseBoundary);
        try
        {
            activation.Initialize();
            return activation;
        }
        catch (Exception exception)
        {
            ExceptionDispatchInfo failure = ExceptionDispatchInfo.Capture(exception);
            try
            {
                await activation.ReleaseAsync().ConfigureAwait(false);
            }
            catch
            {
                // Preserve the activation/setup failure after completing best-effort teardown.
            }

            failure.Throw();
            throw;
        }
    }

    internal VirtualNode? Render()
    {
        ObjectDisposedException.ThrowIf(IsReleased, this);
        try
        {
            return Scope.Run(() => Context.Run(() => Renderer(Frame)));
        }
        catch (Exception exception)
        {
            Frame.ResetBlockTracking();
            Context.RouteError(exception, "component render");
            return null;
        }
    }

    internal void Update(ComponentNode request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(IsReleased, this);
        if (request.Component != Registration.Reference)
        {
            throw new InvalidOperationException(
                "A component activation can update only from the registration that created it.");
        }

        Context.Update(request.Invocation);
        Context.UpdateMountReference(request.MountReference);
        Request = request;
    }

    internal void Release()
    {
        ValueTask release = ReleaseAsync();
        if (release.IsCompleted)
        {
            release.GetAwaiter().GetResult();
            return;
        }

        _ = ObserveReleaseAsync(release.AsTask());
    }

    internal void ReleaseClient(Action unmountSubtree)
    {
        ArgumentNullException.ThrowIfNull(unmountSubtree);
        if (IsReleased)
        {
            return;
        }

        IsReleased = true;
        ExceptionDispatchInfo? failure = null;

        RunSynchronousTeardown(() => Context.Run(Lifecycle.InvokeBeforeUnmount));
        RunSynchronousTeardown(() => Context.Run(Lifecycle.Cancel));
        RunSynchronousTeardown(() => Context.Run(Scope.Dispose));
        RunSynchronousTeardown(unmountSubtree);
        RunSynchronousTeardown(() => Context.Run(Lifecycle.InvokeUnmounted));
        RunSynchronousTeardown(DisposeClientInstance);

        _releaseTask = CompleteClientReleaseAsync();
        if (_releaseTask.IsCompleted)
        {
            try
            {
                _releaseTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
        else
        {
            _ = ObserveReleaseAsync(_releaseTask);
        }

        failure?.Throw();

        void RunSynchronousTeardown(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    internal ValueTask ReleaseAsync()
    {
        _releaseTask ??= ReleaseCoreAsync();
        return new ValueTask(_releaseTask);
    }

    private static ComponentActivation Create(
        ComponentNode request,
        ComponentRuntimeOptions options,
        ComponentContext? parent,
        IReactiveWatchScheduler? watchScheduler,
        int renderCacheSize,
        SuspenseBoundary? suspenseBoundary)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(renderCacheSize);

        ComponentRegistration registration = options.Components.Resolve(request.Component);
        IComponent instance = registration.Activator(options.Services)
            ?? throw new InvalidOperationException("A component activator returned null.");
        EffectScope scope = parent is null
            ? new EffectScope(detached: true)
            : parent.Scope.Run(() => new EffectScope());
        ComponentLifecycle lifecycle = new();
        RuntimeComponentContext context = new(
            registration.Contract,
            request.Invocation,
            request.MountReference,
            options.Services,
            lifecycle,
            scope,
            watchScheduler ?? options.WatchScheduler,
            parent,
            options.ErrorHandler,
            options.WarnHandler,
            options.EventObserver);
        context.SuspenseBoundary = suspenseBoundary ?? context.SuspenseBoundary;
        lifecycle.SetObservedTaskFaultHandler(
            (exception, diagnosticInformation) =>
                context.RouteError(exception, diagnosticInformation));
        ComponentRenderFrame frame = new(renderCacheSize);
        return new ComponentActivation(
            request,
            registration,
            instance,
            context,
            scope,
            lifecycle,
            frame);
    }

    private void Initialize()
    {
        Context.Update(Request.Invocation, isInitial: true);
        _renderer = Scope.Run(
            () => Context.Run(() => Instance.Setup(Context)))
            ?? throw new InvalidOperationException("Component setup returned a null renderer.");
    }

    private async Task ReleaseCoreAsync()
    {
        if (IsReleased)
        {
            return;
        }

        IsReleased = true;
        ExceptionDispatchInfo? failure = null;
        try
        {
            Context.Run(Lifecycle.Cancel);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            Context.Run(Scope.Dispose);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            switch (Instance)
            {
                case IAsyncDisposable asynchronousDisposable:
                    ValueTask disposal = Context.Run(asynchronousDisposable.DisposeAsync);
                    await disposal.ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    Context.Run(disposable.Dispose);
                    break;
            }
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            await Lifecycle.DrainAsync().ConfigureAwait(false);
            await Context.DrainObservedTasksAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            Context.Run(Lifecycle.Dispose);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        failure?.Throw();
    }

    private void DisposeClientInstance()
    {
        switch (Instance)
        {
            case IAsyncDisposable asynchronousDisposable:
                ValueTask disposal = Context.Run(asynchronousDisposable.DisposeAsync);
                Context.ObserveTask(
                    disposal.AsTask(),
                    "asynchronous component instance disposal");
                break;
            case IDisposable disposable:
                Context.Run(disposable.Dispose);
                break;
        }
    }

    private async Task CompleteClientReleaseAsync()
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            await Lifecycle.DrainAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            await Context.DrainObservedTasksAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            Context.Run(Lifecycle.Dispose);
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }

        failure?.Throw();
    }

    private async Task ObserveReleaseAsync(Task release)
    {
        try
        {
            await release.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Context.RouteError(exception, "component teardown");
        }
    }
}
