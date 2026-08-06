using System;
using System.Collections.Generic;
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
    private readonly CancellationTokenSource _stoppingSource = new();
    private readonly TaskCompletionSource<object?> _startupCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Renderer<int> _renderer;
    private readonly ApplicationContext _context;
    private readonly BufferedBrowserNodeOperations? _bufferedOperations;
    private readonly bool _hydrate;
    private readonly Func<CancellationToken, Task> _initialize;
    private readonly Action<int> _clearContainer;
    private readonly Func<string, int> _resolveContainer;
    private readonly string _mountTargetSelector;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task? _pipelineExecution;
    private Task? _stopExecution;
    private Task? _initialization;
    private ApplicationState _state;
    private int _container;
    private bool _isDirectMount;
    private bool _isDisposed;
    private bool _isFailureReported;

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
        _bufferedOperations = bufferedOperations;
        _hydrate = hydrate;
        _initialize = initialize ?? BrowserRuntime.EnsureBridgeAsync;
        _clearContainer = clearContainer ?? BrowserRuntime.ClearContainer;
        _resolveContainer = resolveContainer ?? BrowserRuntime.QuerySelector;
        _mountTargetSelector = mountTargetSelector;
        _context.InitializeRuntime(_stoppingSource.Token);

        void HandleEventError(Exception exception)
        {
            Action<Exception, IComponentContext?, string>? handler =
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
    public IComponentContext? RootContext { get; private set; }

    /// <summary>Gets whether this application hydrates server-rendered markup.</summary>
    public bool IsHydrating => _hydrate;

    internal bool HasFailed => _state == ApplicationState.Failed;

    /// <inheritdoc/>
    public IApplication Use(ApplicationMiddleware middleware)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(middleware);

        if (_state != ApplicationState.Created)
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
    public IComponentContext? Mount(int container)
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
    public ValueTask<IComponentContext?> MountAsync(
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
    public ValueTask<IComponentContext?> MountAsync(
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
            _stoppingSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    internal static BrowserApplication Create(
        ApplicationContext context,
        bool useCommandBuffer,
        bool hydrate,
        string mountTargetSelector)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (useCommandBuffer)
        {
            BufferedBrowserNodeOperations operations =
                BufferedBrowserNodeOperations.CreateProduction();
            Renderer<int> renderer =
                RendererFactory.CreateRenderer(operations.Create());
            return new BrowserApplication(
                renderer,
                context,
                operations,
                hydrate,
                mountTargetSelector: mountTargetSelector);
        }

        return new BrowserApplication(
            RendererFactory.CreateRenderer(BrowserNodeOperations.Create()),
            context,
            bufferedOperations: null,
            hydrate,
            mountTargetSelector: mountTargetSelector);
    }

    private ValueTask<IComponentContext?> MountResolvedAsync(
        Func<CancellationToken, ValueTask<int>> resolveMountTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolveMountTarget);
        BeginExecution(isDirectMount: true);
        Task<IComponentContext?> execution =
            MountDirectAsync(resolveMountTarget, cancellationToken);
        _pipelineExecution = execution;
        return new ValueTask<IComponentContext?>(execution);
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
                _context.SetIsRunning(false);
            }
        }
    }

    private async Task<IComponentContext?> MountDirectAsync(
        Func<CancellationToken, ValueTask<int>> resolveMountTarget,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        using CancellationTokenRegistration cancellationRegistration =
            RegisterStopping(cancellationToken);
        bool mountAttempted = false;
        try
        {
            _stoppingSource.Token.ThrowIfCancellationRequested();
            await OnInitializeAsync(_stoppingSource.Token).ConfigureAwait(false);
            _stoppingSource.Token.ThrowIfCancellationRequested();
            int container = await resolveMountTarget(_stoppingSource.Token)
                .ConfigureAwait(false);
            _stoppingSource.Token.ThrowIfCancellationRequested();
            mountAttempted = true;
            RootContext = await MountCoreAsync(container, _stoppingSource.Token)
                .ConfigureAwait(false);
            _stoppingSource.Token.ThrowIfCancellationRequested();
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

    private IComponentContext? MountCore(int container)
    {
        _container = container;
        _bufferedOperations?.ObserveForeignHandle(container);
        IComponentContext? rootContext;
        if (_hydrate)
        {
            rootContext =
                _renderer.Hydrate(Context.RootComponent, container, Context);
        }
        else
        {
            _clearContainer(container);
            rootContext =
                _renderer.Render(Context.RootComponent, container, Context);
        }

        _bufferedOperations?.ApplyPending();
        return rootContext;
    }

    private ValueTask<IComponentContext?> MountCoreAsync(
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

        _renderer.Render(null, _container, Context);
        _bufferedOperations?.ApplyPending();
        _bufferedOperations?.Deactivate();
        _container = default;
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
            _context.SetIsRunning(false);
        }
    }

    private Task GetOrCreateStopExecution()
    {
        if (_stopExecution is not null)
        {
            return _stopExecution;
        }

        if (_state is ApplicationState.Starting or ApplicationState.Running)
        {
            RequestStopping();
        }

        if (_isDirectMount && _state == ApplicationState.Stopping)
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

        if (_state != ApplicationState.Stopping)
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

    private bool IsStoppingCancellation(OperationCanceledException exception)
    {
        return _stoppingSource.IsCancellationRequested &&
            exception.CancellationToken == _stoppingSource.Token;
    }

    private void RequestStopping()
    {
        if (_state is ApplicationState.Starting or ApplicationState.Running)
        {
            _state = ApplicationState.Stopping;
        }

        _context.SetIsRunning(false);
        if (!_stoppingSource.IsCancellationRequested)
        {
            _stoppingSource.Cancel();
        }
    }

    private void BeginExecution(bool isDirectMount)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_state != ApplicationState.Created)
        {
            throw new InvalidOperationException(
                "An application instance can start only once.");
        }

        _isDirectMount = isDirectMount;
        _state = ApplicationState.Starting;
    }

    private void TransitionToRunning()
    {
        _stoppingSource.Token.ThrowIfCancellationRequested();
        if (_state != ApplicationState.Starting)
        {
            throw new InvalidOperationException(
                "The application cannot enter Running from its current state.");
        }

        _state = ApplicationState.Running;
        _context.SetIsRunning(true);
    }

    private void CompleteStopping()
    {
        if (_state is ApplicationState.Starting or ApplicationState.Running)
        {
            _state = ApplicationState.Stopping;
        }

        _context.SetIsRunning(false);
        if (!_stoppingSource.IsCancellationRequested)
        {
            _stoppingSource.Cancel();
        }

        if (_state == ApplicationState.Stopping)
        {
            _state = ApplicationState.Stopped;
        }
    }

    private void Fail(Exception exception)
    {
        _state = ApplicationState.Failed;
        _context.SetIsRunning(false);
        if (!_stoppingSource.IsCancellationRequested)
        {
            _stoppingSource.Cancel();
        }

        if (_isFailureReported)
        {
            return;
        }

        _isFailureReported = true;
        try
        {
            Context.ErrorHandler?.Invoke(
                exception,
                RootContext,
                "application lifetime");
        }
        catch (Exception handlerException)
        {
            Debug.WriteLine(
                $"[Viu warn] Application error handler failed: {handlerException}");
        }
    }
}
