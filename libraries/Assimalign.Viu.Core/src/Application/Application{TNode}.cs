using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Provides the host-generic application lifecycle used by Browser and future platform packages.
/// </summary>
/// <typeparam name="TNode">The host renderer's node handle type.</typeparam>
/// <remarks>
/// The application borrows the component factory, service provider, and state registry contained
/// in <see cref="Context"/>. Their composition root retains ownership. Not thread-safe.
/// </remarks>
public abstract class Application<TNode> :
    IApplication<TNode>,
    IDisposable,
    IAsyncDisposable
    where TNode : notnull
{
    private readonly HashSet<IApplicationPlugin> _installedPlugins =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<IApplicationPlugin> _pendingPlugins = [];
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
    public bool IsMounted { get; private set; }

    /// <inheritdoc/>
    public IComponentContext? RootContext { get; private set; }

    /// <inheritdoc/>
    public IApplication Use(IApplicationPlugin plugin)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        if (!_installedPlugins.Add(plugin))
        {
            Warn("Plugin has already been applied to the target application.");
            return this;
        }

        if (IsMounted)
        {
            Warn("Plugins registered after mount cannot affect the existing root tree.");
        }

        _pendingPlugins.Add(plugin);
        return this;
    }

    /// <inheritdoc/>
    public IComponentContext? Mount(TNode container)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(container);

        if (IsMounted)
        {
            Warn("Application is already mounted.");
            return RootContext;
        }

        InstallPendingPluginsSynchronously();
        ValueTask initialization = OnInitializeAsync(CancellationToken.None);
        if (!initialization.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException(
                "This host requires asynchronous initialization. Use MountAsync instead.");
        }

        initialization.GetAwaiter().GetResult();
        RootContext = MountCore(container);
        IsMounted = true;
        return RootContext;
    }

    /// <inheritdoc/>
    public async ValueTask<IComponentContext?> MountAsync(
        TNode container,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(container);

        if (IsMounted)
        {
            Warn("Application is already mounted.");
            return RootContext;
        }

        await InstallPendingPluginsAsync(cancellationToken).ConfigureAwait(false);
        await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
        RootContext = await MountCoreAsync(container, cancellationToken).ConfigureAwait(false);
        IsMounted = true;
        return RootContext;
    }

    /// <inheritdoc/>
    public void Unmount()
    {
        if (!IsMounted)
        {
            return;
        }

        UnmountCore();
        RootContext = null;
        IsMounted = false;
    }

    /// <inheritdoc/>
    public async ValueTask UnmountAsync(CancellationToken cancellationToken = default)
    {
        if (!IsMounted)
        {
            return;
        }

        await UnmountCoreAsync(cancellationToken).ConfigureAwait(false);
        RootContext = null;
        IsMounted = false;
    }

    /// <summary>Releases application-owned mounted runtime state.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Unmount();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>Asynchronously releases application-owned mounted runtime state.</summary>
    /// <returns>A task that completes after host teardown.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await UnmountAsync().ConfigureAwait(false);
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs host initialization after plugins install and before the first render.
    /// </summary>
    /// <param name="cancellationToken">Cancels host initialization.</param>
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
    /// <param name="cancellationToken">Cancels asynchronous host work.</param>
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
    /// <param name="cancellationToken">Cancels asynchronous host teardown.</param>
    /// <returns>A task that completes after host teardown.</returns>
    protected virtual ValueTask UnmountCoreAsync(CancellationToken cancellationToken)
    {
        UnmountCore();
        return ValueTask.CompletedTask;
    }

    private void InstallPendingPluginsSynchronously()
    {
        while (_pendingPlugins.Count > 0)
        {
            IApplicationPlugin plugin = _pendingPlugins[0];
            ValueTask installation = plugin.InstallAsync(this, CancellationToken.None);
            if (!installation.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException(
                    "A plugin requires asynchronous installation. Use MountAsync instead.");
            }

            installation.GetAwaiter().GetResult();
            _pendingPlugins.RemoveAt(0);
        }
    }

    private async ValueTask InstallPendingPluginsAsync(CancellationToken cancellationToken)
    {
        while (_pendingPlugins.Count > 0)
        {
            IApplicationPlugin plugin = _pendingPlugins[0];
            await plugin.InstallAsync(this, cancellationToken).ConfigureAwait(false);
            _pendingPlugins.RemoveAt(0);
        }
    }

    private void Warn(string message)
    {
        Context.WarnHandler?.Invoke(message);
    }
}
