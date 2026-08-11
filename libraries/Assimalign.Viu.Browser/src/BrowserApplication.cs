using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>A persistent Viu application hosted by a browser DOM.</summary>
/// <remarks>
/// Browser nodes are opaque integer handles. This type owns Browser-specific mount operations and
/// implements the platform-neutral lifetime directly. The application borrows every resolver in
/// its context and never disposes one. Its asynchronous pipeline is single-use and not thread-safe.
/// Specified by <c>[APP-1]</c> through <c>[APP-7]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserApplication : IApplication
{
    internal const string DefaultMountTargetSelector = "#app";

    private readonly List<ApplicationMiddleware> _middleware = [];
    private readonly TaskCompletionSource<object?> _startupCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Renderer<int> _renderer;
    private readonly ApplicationContext _context;
    private readonly ApplicationLifetime _lifetime;
    private readonly BufferedBrowserNodeOperations? _bufferedOperations;
    private readonly bool _hydrate;
    private readonly Func<CancellationToken, Task> _initialize;
    private readonly Action<int> _clearContainer;
    private readonly Func<string, int> _resolveContainer;
    private readonly string _mountTargetSelector;
    private CancellationTokenRegistration _startCancellationRegistration;
    private IDisposable? _bufferedOperationsActivation;
    private Task? _pipelineExecution;
    private Task? _stopExecution;
    private Task? _initialization;
    private int _container;
    private bool _isDirectMount;
    private bool _isDisposed;

    internal BrowserApplication(
        Renderer<int> renderer,
        ApplicationContext context,
        BufferedBrowserNodeOperations? bufferedOperations = null,
        bool hydrate = false,
        Func<CancellationToken, Task>? initialize = null,
        Action<int>? clearContainer = null,
        Func<string, int>? resolveContainer = null,
        string mountTargetSelector = DefaultMountTargetSelector)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(mountTargetSelector);
        _renderer = renderer;
        _context = context;
        _lifetime = new ApplicationLifetime(context);
        _bufferedOperations = bufferedOperations;
        _hydrate = hydrate;
        _initialize = initialize ?? BrowserRuntime.EnsureBridgeAsync;
        _clearContainer = clearContainer ?? BrowserRuntime.ClearContainer;
        _resolveContainer = resolveContainer ?? BrowserRuntime.QuerySelector;
        _mountTargetSelector = mountTargetSelector;
        void HandleEventError(Exception exception)
        {
            Action<Exception, ComponentContext?, string>? handler =
                Context.ErrorHandler;
            if (handler is not null)
            {
                handler(exception, null, "browser event handler");
                return;
            }

            Debug.WriteLine(
                $"[Viu warn] Unhandled error in browser event handler: {exception}");
        }

        BrowserNodeOperations.ErrorSink = HandleEventError;
        if (bufferedOperations is not null)
        {
            bufferedOperations.ErrorSink = HandleEventError;
        }
    }

    /// <inheritdoc/>
    public IApplicationContext Context => _context;

    /// <summary>Gets the mounted root component context, or null while unmounted.</summary>
    public ComponentContext? RootContext { get; private set; }

    /// <summary>Gets whether this application hydrates server-rendered markup.</summary>
    public bool IsHydrating => _hydrate;

    /// <summary>Gets the CSS selector resolved by top-level startup.</summary>
    public string MountTargetSelector => _mountTargetSelector;

    internal bool HasFailed => _lifetime.HasFailed;

    /// <inheritdoc/>
    public IApplication Use(ApplicationMiddleware middleware)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(middleware);

        if (_lifetime.State != ApplicationState.Created)
        {
            throw new InvalidOperationException(
                "Application middleware must be registered before execution begins.");
        }

        _middleware.Add(middleware);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        BeginExecution(isDirectMount: false);
        _startCancellationRegistration = RegisterStopping(cancellationToken);
        _pipelineExecution = ExecutePipelineAsync();
        return new ValueTask(_startupCompletion.Task);
    }

    /// <summary>
    /// Synchronously mounts the root tree into a Browser node handle without invoking top-level
    /// lifetime middleware.
    /// </summary>
    /// <param name="container">The Browser DOM container handle.</param>
    /// <returns>The mounted root component context, when the root is a template.</returns>
    /// <remarks>
    /// This lower-level embedding and testing API bypasses the middleware pipeline. Call
    /// <see cref="IApplication.StopAsync(CancellationToken)"/> for teardown. Specified by
    /// <c>[APP-7]</c>.
    /// </remarks>
    public ComponentContext? Mount(int container)
    {
        BeginExecution(isDirectMount: true);

        bool mountAttempted = false;
        try
        {
            ValueTask initialization = OnInitializeAsync(CancellationToken.None);
            if (!initialization.IsCompleted)
            {
                throw new InvalidOperationException(
                    "The Browser host requires asynchronous initialization. Use MountAsync instead.");
            }

            initialization.GetAwaiter().GetResult();
            mountAttempted = true;
            RootContext = MountCore(container);
            TransitionToRunning();
            _pipelineExecution = Task.CompletedTask;
            return RootContext;
        }
        catch (Exception exception)
        {
            try
            {
                if (mountAttempted)
                {
                    UnmountCore();
                }
            }
            finally
            {
                RootContext = null;
                Fail(exception);
            }

            throw;
        }
    }

    /// <summary>
    /// Mounts the root tree into a Browser node handle without invoking top-level lifetime
    /// middleware.
    /// </summary>
    /// <param name="container">The Browser DOM container handle.</param>
    /// <param name="cancellationToken">Cancels host initialization before the first render.</param>
    /// <returns>The mounted root component context, when the root is a template.</returns>
    /// <remarks>
    /// This lower-level embedding and testing API bypasses the middleware pipeline. Call
    /// <see cref="IApplication.StopAsync(CancellationToken)"/> for teardown. Specified by
    /// <c>[APP-7]</c>.
    /// </remarks>
    public ValueTask<ComponentContext?> MountAsync(
        int container,
        CancellationToken cancellationToken = default)
    {
        return MountResolvedAsync(
            _ => ValueTask.FromResult(container),
            cancellationToken);
    }

    /// <summary>
    /// Initializes the Browser bridge, resolves a CSS selector, and mounts the root tree without
    /// invoking top-level lifetime middleware.
    /// </summary>
    /// <param name="selector">The CSS selector for the mount container.</param>
    /// <param name="cancellationToken">Cancels bridge initialization before the first render.</param>
    /// <returns>The mounted root component context, when the root is a template.</returns>
    /// <remarks>
    /// This lower-level embedding and testing API bypasses the middleware pipeline. Call
    /// <see cref="IApplication.StopAsync(CancellationToken)"/> for teardown. Specified by
    /// <c>[APP-7]</c>.
    /// </remarks>
    public ValueTask<ComponentContext?> MountAsync(
        string selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(selector);
        return MountResolvedAsync(
            stopping =>
            {
                stopping.ThrowIfCancellationRequested();
                return ValueTask.FromResult(_resolveContainer(selector));
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Task stopExecution = GetOrCreateStopExecution();
        return cancellationToken.CanBeCanceled
            ? new ValueTask(stopExecution.WaitAsync(cancellationToken))
            : new ValueTask(stopExecution);
    }

    /// <summary>
    /// Stops and releases this application without disposing its borrowed composition dependencies.
    /// </summary>
    /// <returns>A task that completes after application cleanup.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await GetOrCreateStopExecution().ConfigureAwait(false);
        }
        finally
        {
            _startCancellationRegistration.Dispose();
            _lifetime.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal static BrowserApplication Create(
        ApplicationContext context,
        bool hydrate,
        string mountTargetSelector)
    {
        ArgumentNullException.ThrowIfNull(context);

        BufferedBrowserNodeOperations operations =
            BufferedBrowserNodeOperations.CreateProduction();
        Renderer<int> renderer = RendererFactory.CreateRenderer(operations.Create());
        return new BrowserApplication(
            renderer,
            context,
            operations,
            hydrate,
            initialize: hydrate && context.State is { } state
                ? cancellationToken => BrowserStateHydration.InitializeAsync(
                    state,
                    cancellationToken)
                : null,
            mountTargetSelector: mountTargetSelector);
    }

    /// <summary>
    /// Creates a lower-level Browser application over an explicitly supplied integer-handle
    /// renderer and host callbacks.
    /// </summary>
    /// <param name="renderer">The Browser-compatible renderer that owns the mounted tree.</param>
    /// <param name="context">The frozen application composition.</param>
    /// <param name="hydrate">Whether the first mount adopts server-rendered nodes.</param>
    /// <param name="initialize">The optional asynchronous host initializer.</param>
    /// <param name="clearContainer">The optional non-hydrating container reset.</param>
    /// <param name="resolveContainer">The optional selector resolver.</param>
    /// <param name="mountTargetSelector">The top-level pipeline mount selector.</param>
    /// <returns>A single-use persistent Browser application.</returns>
    /// <remarks>
    /// This embedding and test seam preserves the ordinary D5a lifetime and middleware machine;
    /// ownership of the renderer and callbacks remains with the caller. Specified by
    /// <c>[APP-6]</c> and <c>[APP-7]</c>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static BrowserApplication CreateEmbedded(
        Renderer<int> renderer,
        ApplicationContext context,
        bool hydrate = false,
        Func<CancellationToken, Task>? initialize = null,
        Action<int>? clearContainer = null,
        Func<string, int>? resolveContainer = null,
        string mountTargetSelector = DefaultMountTargetSelector)
    {
        return new BrowserApplication(
            renderer,
            context,
            bufferedOperations: null,
            hydrate,
            initialize,
            clearContainer,
            resolveContainer,
            mountTargetSelector);
    }

    /// <summary>
    /// Creates a lower-level Browser application over a DOM-less command-frame host.
    /// </summary>
    /// <param name="host">The host that owns the renderer operations and command boundary.</param>
    /// <param name="context">The frozen application composition.</param>
    /// <param name="hydrate">Whether the first mount adopts server-rendered nodes.</param>
    /// <param name="initialize">The optional asynchronous host initializer.</param>
    /// <param name="resolveContainer">The optional selector resolver.</param>
    /// <param name="mountTargetSelector">The top-level pipeline mount selector.</param>
    /// <returns>A single-use persistent Browser application owning the host activation lease.</returns>
    /// <remarks>
    /// This embedding and command-capture seam preserves the ordinary lifetime and exclusive
    /// Browser operation routing. Specified by <c>[APP-6]</c>, <c>[APP-7]</c>, and
    /// <c>[RND-IO-1]</c>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static BrowserApplication CreateEmbedded(
        BrowserRendererHost host,
        ApplicationContext context,
        bool hydrate = false,
        Func<CancellationToken, Task>? initialize = null,
        Func<string, int>? resolveContainer = null,
        string mountTargetSelector = DefaultMountTargetSelector)
    {
        ArgumentNullException.ThrowIfNull(host);
        Renderer<int> renderer = RendererFactory.CreateRenderer(host.Options);
        return new BrowserApplication(
            renderer,
            context,
            host.Operations,
            hydrate,
            initialize,
            clearContainer: null,
            resolveContainer,
            mountTargetSelector);
    }

    private ValueTask<ComponentContext?> MountResolvedAsync(
        Func<CancellationToken, ValueTask<int>> resolveMountTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolveMountTarget);
        BeginExecution(isDirectMount: true);
        Task<ComponentContext?> execution =
            MountDirectAsync(resolveMountTarget, cancellationToken);
        _pipelineExecution = execution;
        return new ValueTask<ComponentContext?>(execution);
    }

    private async Task ExecutePipelineAsync()
    {
        await Task.Yield();
        ApplicationDelegate pipeline = ExecuteTerminalAsync;
        for (int index = _middleware.Count - 1; index >= 0; index--)
        {
            ApplicationMiddleware middleware = _middleware[index];
            ApplicationDelegate next = pipeline;
            pipeline = context => middleware(context, next);
        }

        try
        {
            await pipeline(Context).ConfigureAwait(false);
            CompleteStopping();
            _startupCompletion.TrySetResult(null);
        }
        catch (OperationCanceledException exception)
            when (IsStoppingCancellation(exception))
        {
            CompleteStopping();
            _startupCompletion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            Fail(exception);
            _startupCompletion.TrySetException(exception);
            throw;
        }
        finally
        {
            _startCancellationRegistration.Dispose();
        }
    }

    private async ValueTask ExecuteTerminalAsync(IApplicationContext context)
    {
        bool mountAttempted = false;
        try
        {
            context.Stopping.ThrowIfCancellationRequested();
            await OnInitializeAsync(context.Stopping).ConfigureAwait(false);
            context.Stopping.ThrowIfCancellationRequested();
            int container = await ResolveMountTargetAsync(context.Stopping)
                .ConfigureAwait(false);
            context.Stopping.ThrowIfCancellationRequested();
            mountAttempted = true;
            RootContext = await MountCoreAsync(container, context.Stopping)
                .ConfigureAwait(false);
            context.Stopping.ThrowIfCancellationRequested();
            TransitionToRunning();
            _startupCompletion.TrySetResult(null);
            await WaitForStoppingAsync(context.Stopping).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (mountAttempted)
                {
                    await UnmountCoreAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                RootContext = null;
            }
        }
    }

    private async Task<ComponentContext?> MountDirectAsync(
        Func<CancellationToken, ValueTask<int>> resolveMountTarget,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        using CancellationTokenRegistration cancellationRegistration =
            RegisterStopping(cancellationToken);
        bool mountAttempted = false;
        try
        {
            _lifetime.Stopping.ThrowIfCancellationRequested();
            await OnInitializeAsync(_lifetime.Stopping).ConfigureAwait(false);
            _lifetime.Stopping.ThrowIfCancellationRequested();
            int container = await resolveMountTarget(_lifetime.Stopping)
                .ConfigureAwait(false);
            _lifetime.Stopping.ThrowIfCancellationRequested();
            mountAttempted = true;
            RootContext = await MountCoreAsync(container, _lifetime.Stopping)
                .ConfigureAwait(false);
            _lifetime.Stopping.ThrowIfCancellationRequested();
            TransitionToRunning();
            return RootContext;
        }
        catch (OperationCanceledException exception)
            when (IsStoppingCancellation(exception))
        {
            try
            {
                await CleanupFailedMountAsync(mountAttempted).ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                Fail(cleanupException);
                throw;
            }

            CompleteStopping();
            throw;
        }
        catch (Exception exception)
        {
            try
            {
                await CleanupFailedMountAsync(mountAttempted).ConfigureAwait(false);
            }
            finally
            {
                Fail(exception);
            }

            throw;
        }
    }

    private ValueTask OnInitializeAsync(CancellationToken cancellationToken)
    {
        return new ValueTask(
            _initialization ??= _initialize(cancellationToken));
    }

    private ValueTask<int> ResolveMountTargetAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            _resolveContainer(_mountTargetSelector));
    }

    private ComponentContext? MountCore(int container)
    {
        if (container == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(container),
                "A Browser mount container must use a nonzero host handle.");
        }

        IDisposable? bufferedOperationsActivation =
            _bufferedOperations?.Activate();
        _bufferedOperationsActivation = bufferedOperationsActivation;
        _container = container;
        _bufferedOperations?.ObserveForeignHandle(container);
        ComponentContext? rootContext;
        if (_hydrate)
        {
            rootContext =
                _renderer.Hydrate(Context.RootComponent, container, Context);
        }
        else
        {
            if (_bufferedOperations is null)
            {
                _clearContainer(container);
            }
            else
            {
                _bufferedOperations.ClearElement(container);
            }

            rootContext =
                _renderer.Render(Context.RootComponent, container, Context);
        }

        _bufferedOperations?.ApplyPending();
        return rootContext;
    }

    private ValueTask<ComponentContext?> MountCoreAsync(
        int container,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(MountCore(container));
    }

    private void UnmountCore()
    {
        if (_container == default)
        {
            return;
        }

        try
        {
            _renderer.Render(null, _container, Context);
            _bufferedOperations?.ApplyPending();
        }
        finally
        {
            _bufferedOperationsActivation?.Dispose();
            _bufferedOperationsActivation = null;
            _container = default;
        }
    }

    private ValueTask UnmountCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UnmountCore();
        return ValueTask.CompletedTask;
    }

    private async Task CleanupFailedMountAsync(bool mountAttempted)
    {
        try
        {
            if (mountAttempted)
            {
                await UnmountCoreAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            RootContext = null;
        }
    }

    private Task GetOrCreateStopExecution()
    {
        if (_stopExecution is not null)
        {
            return _stopExecution;
        }

        if (_lifetime.State is ApplicationState.Starting or ApplicationState.Running)
        {
            RequestStopping();
        }

        if (_isDirectMount && _lifetime.State == ApplicationState.Stopping)
        {
            _stopExecution = StopDirectMountAsync();
            return _stopExecution;
        }

        if (_pipelineExecution is null)
        {
            return Task.CompletedTask;
        }

        _stopExecution = _pipelineExecution;
        return _stopExecution;
    }

    private async Task StopDirectMountAsync()
    {
        if (_pipelineExecution is not null)
        {
            try
            {
                await _pipelineExecution.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (IsStoppingCancellation(exception))
            {
            }
        }

        if (_lifetime.State != ApplicationState.Stopping)
        {
            return;
        }

        try
        {
            await UnmountCoreAsync(CancellationToken.None).ConfigureAwait(false);
            RootContext = null;
            CompleteStopping();
        }
        catch (Exception exception)
        {
            RootContext = null;
            Fail(exception);
            throw;
        }
    }

    private static async Task WaitForStoppingAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private CancellationTokenRegistration RegisterStopping(
        CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
            ? cancellationToken.Register(
                static state => ((BrowserApplication)state!).RequestStopping(),
                this)
            : default;
    }

    private bool IsStoppingCancellation(OperationCanceledException exception) =>
        _lifetime.IsStoppingCancellation(exception);

    private void RequestStopping() => _lifetime.RequestStopping();

    private void BeginExecution(bool isDirectMount)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        _isDirectMount = isDirectMount;
        _lifetime.StartExecution();
    }

    private void TransitionToRunning() => _lifetime.SignalRunning();

    private void CompleteStopping()
    {
        _lifetime.CompleteStopping();
    }

    private void Fail(Exception exception)
    {
        _lifetime.Fail(exception, RootContext);
    }
}
