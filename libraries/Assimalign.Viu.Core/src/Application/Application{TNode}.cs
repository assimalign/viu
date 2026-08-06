using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Provides the host-generic application lifetime used by Browser and future platform packages.
/// </summary>
/// <typeparam name="TNode">The host renderer's node handle type.</typeparam>
/// <remarks>
/// Each instance executes at most once. Runtime middleware surrounds initialization, target
/// resolution, mounting, the live wait, and unmounting. The application borrows every composition
/// dependency in <see cref="Context"/> and never disposes one. Not thread-safe. Specified by
/// <c>[APP-1]</c> through <c>[APP-7]</c>.
/// </remarks>
public abstract class Application<TNode> : IApplication<TNode>
    where TNode : notnull
{
    private readonly List<ApplicationMiddleware> _middleware = [];
    private readonly CancellationTokenSource _stoppingSource = new();
    private Task? _execution;
    private Task? _stopExecution;
    private ApplicationState _state;
    private bool _isDirectMount;
    private bool _isDisposed;

    /// <summary>Initializes an application over an independently composed context.</summary>
    /// <param name="context">The immutable application composition context.</param>
    protected Application(IApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <inheritdoc/>
    public IApplicationContext Context { get; }

    /// <inheritdoc/>
    public bool IsRunning => _state == ApplicationState.Running;

    /// <inheritdoc/>
    public IComponentContext? RootContext { get; private set; }

    internal ApplicationState State => _state;

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
    public ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        BeginExecution(isDirectMount: false);
        Task execution = ExecuteAsync(cancellationToken);
        _execution = execution;
        return new ValueTask(execution);
    }

    /// <inheritdoc/>
    public IComponentContext? Mount(TNode container)
    {
        ArgumentNullException.ThrowIfNull(container);
        BeginExecution(isDirectMount: true);

        bool mountAttempted = false;
        try
        {
            ValueTask initialization = OnInitializeAsync(CancellationToken.None);
            if (!initialization.IsCompleted)
            {
                throw new InvalidOperationException(
                    "This host requires asynchronous initialization. Use MountAsync instead.");
            }

            initialization.GetAwaiter().GetResult();
            mountAttempted = true;
            RootContext = MountCore(container);
            _execution = Task.CompletedTask;
            return RootContext;
        }
        catch
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
                _state = ApplicationState.Failed;
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<IComponentContext?> MountAsync(
        TNode container,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);
        return MountResolvedAsync(
            _ => ValueTask.FromResult(container),
            cancellationToken);
    }

    internal ValueTask<IComponentContext?> MountResolvedAsync(
        Func<CancellationToken, ValueTask<TNode>> resolveMountTarget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolveMountTarget);
        BeginExecution(isDirectMount: true);

        Task<IComponentContext?> execution =
            MountDirectAsync(resolveMountTarget, cancellationToken);
        _execution = execution;
        return new ValueTask<IComponentContext?>(execution);
    }

    /// <inheritdoc/>
    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Task stopExecution = GetOrCreateStopExecution();
        return cancellationToken.CanBeCanceled
            ? new ValueTask(stopExecution.WaitAsync(cancellationToken))
            : new ValueTask(stopExecution);
    }

    /// <summary>Asynchronously releases the running application without disposing borrowed dependencies.</summary>
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
            _stoppingSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Resolves the host mount target used by top-level application execution.</summary>
    /// <param name="cancellationToken">Signals graceful shutdown during target resolution.</param>
    /// <returns>The host mount target.</returns>
    /// <remarks>
    /// Lower-level <see cref="Mount(TNode)"/> calls supply a target directly and do not invoke this
    /// method. Specified by <c>[APP-4]</c> and <c>[APP-7]</c>.
    /// </remarks>
    protected abstract ValueTask<TNode> ResolveMountTargetAsync(
        CancellationToken cancellationToken);

    /// <summary>Performs host initialization before target resolution and the first render.</summary>
    /// <param name="cancellationToken">Signals graceful shutdown during host initialization.</param>
    /// <returns>A task that completes when the host is ready.</returns>
    protected virtual ValueTask OnInitializeAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>Synchronously mounts the root tree into the supplied host container.</summary>
    /// <param name="container">The host container.</param>
    /// <returns>The root component context, when one exists.</returns>
    protected abstract IComponentContext? MountCore(TNode container);

    /// <summary>Asynchronously mounts the root tree into the supplied host container.</summary>
    /// <param name="container">The host container.</param>
    /// <param name="cancellationToken">Signals graceful shutdown during host work.</param>
    /// <returns>The root component context, when one exists.</returns>
    protected virtual ValueTask<IComponentContext?> MountCoreAsync(
        TNode container,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(MountCore(container));
    }

    /// <summary>Synchronously removes the mounted tree from the host.</summary>
    protected abstract void UnmountCore();

    /// <summary>Asynchronously removes the mounted tree from the host.</summary>
    /// <param name="cancellationToken">Cancels host-specific asynchronous teardown.</param>
    /// <returns>A task that completes after host teardown.</returns>
    /// <remarks>
    /// Top-level shutdown passes a non-cancelled token so a stop request cannot skip cleanup.
    /// </remarks>
    protected virtual ValueTask UnmountCoreAsync(CancellationToken cancellationToken)
    {
        UnmountCore();
        return ValueTask.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        using CancellationTokenRegistration cancellationRegistration =
            RegisterStopping(cancellationToken);
        ApplicationExecutionContext context =
            new(this, _stoppingSource.Token);
        ApplicationDelegate pipeline = ExecuteTerminalAsync;
        for (int index = _middleware.Count - 1; index >= 0; index--)
        {
            ApplicationMiddleware middleware = _middleware[index];
            ApplicationDelegate next = pipeline;
            pipeline = execution => middleware(execution, next);
        }

        try
        {
            await pipeline(context).ConfigureAwait(false);
            CompleteStopping();
        }
        catch (OperationCanceledException exception)
            when (IsStoppingCancellation(exception))
        {
            CompleteStopping();
        }
        catch
        {
            _state = ApplicationState.Failed;
            throw;
        }
    }

    private async ValueTask ExecuteTerminalAsync(
        ApplicationExecutionContext context)
    {
        bool mountAttempted = false;
        try
        {
            context.Stopping.ThrowIfCancellationRequested();
            await OnInitializeAsync(context.Stopping).ConfigureAwait(false);
            context.Stopping.ThrowIfCancellationRequested();
            TNode container = await ResolveMountTargetAsync(context.Stopping)
                .ConfigureAwait(false);
            context.Stopping.ThrowIfCancellationRequested();
            mountAttempted = true;
            RootContext = await MountCoreAsync(container, context.Stopping)
                .ConfigureAwait(false);
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

    private async Task<IComponentContext?> MountDirectAsync(
        Func<CancellationToken, ValueTask<TNode>> resolveMountTarget,
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
            TNode container = await resolveMountTarget(_stoppingSource.Token)
                .ConfigureAwait(false);
            _stoppingSource.Token.ThrowIfCancellationRequested();
            mountAttempted = true;
            RootContext = await MountCoreAsync(container, _stoppingSource.Token)
                .ConfigureAwait(false);
            return RootContext;
        }
        catch (OperationCanceledException exception)
            when (IsStoppingCancellation(exception))
        {
            try
            {
                await CleanupFailedMountAsync(mountAttempted).ConfigureAwait(false);
            }
            catch
            {
                _state = ApplicationState.Failed;
                throw;
            }

            CompleteStopping();
            throw;
        }
        catch
        {
            try
            {
                await CleanupFailedMountAsync(mountAttempted).ConfigureAwait(false);
            }
            finally
            {
                _state = ApplicationState.Failed;
            }

            throw;
        }
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

        if (_state == ApplicationState.Running)
        {
            RequestStopping();
        }

        if (_state != ApplicationState.Stopping)
        {
            return Task.CompletedTask;
        }

        _stopExecution = _isDirectMount
            ? StopDirectMountAsync()
            : _execution ?? Task.CompletedTask;
        return _stopExecution;
    }

    private async Task StopDirectMountAsync()
    {
        if (_execution is not null)
        {
            try
            {
                await _execution.ConfigureAwait(false);
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
        catch
        {
            RootContext = null;
            _state = ApplicationState.Failed;
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
                static state => ((Application<TNode>)state!).RequestStopping(),
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
        if (_state == ApplicationState.Running)
        {
            _state = ApplicationState.Stopping;
        }

        _stoppingSource.Cancel();
    }

    private void BeginExecution(bool isDirectMount)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_state != ApplicationState.Created)
        {
            throw new InvalidOperationException(
                "An application instance can execute only once.");
        }

        _isDirectMount = isDirectMount;
        _state = ApplicationState.Running;
    }

    private void CompleteStopping()
    {
        if (_state == ApplicationState.Running)
        {
            _state = ApplicationState.Stopping;
        }

        if (_state == ApplicationState.Stopping)
        {
            _state = ApplicationState.Stopped;
        }
    }
}
