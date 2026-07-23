using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// A host-neutral application that owns the configuration for one server-rendered component tree.
/// </summary>
/// <remarks>
/// The application borrows its component factory, service provider, and state registry. A server host
/// should construct a fresh application per request and dispose any request-owned dependencies itself.
/// Server rendering never mounts a live host tree, so <see cref="IsMounted"/> is always false and
/// <see cref="RootContext"/> is always null.
/// </remarks>
public sealed class ServerApplication : IApplication
{
    private readonly HashSet<IApplicationPlugin> _installedPlugins =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<IApplicationPlugin> _pendingPlugins = [];

    /// <summary>Creates a server application over an independently composed application context.</summary>
    /// <param name="context">The application context.</param>
    public ServerApplication(IApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>Creates a server application from independently supplied composition services.</summary>
    /// <param name="rootComponent">The root value in the unified component tree.</param>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The independently supplied service resolver.</param>
    /// <param name="state">The optional application state registry.</param>
    public ServerApplication(
        IComponent rootComponent,
        IComponentFactory components,
        IServiceProvider services,
        IStateStoreRegistry? state = null)
        : this(new ApplicationContext(rootComponent, components, services, state))
    {
    }

    /// <summary>Creates an empty server-application builder.</summary>
    /// <returns>The new builder.</returns>
    public static ServerApplicationBuilder CreateBuilder() => new();

    /// <summary>Creates a builder initialized with the required application composition services.</summary>
    /// <param name="rootComponent">The root value in the unified component tree.</param>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The independently supplied service resolver.</param>
    /// <returns>The initialized builder.</returns>
    public static ServerApplicationBuilder CreateBuilder(
        IComponent rootComponent,
        IComponentFactory components,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(services);

        ServerApplicationBuilder builder = new();
        builder.UseRootComponent(rootComponent);
        builder.UseComponentFactory(components);
        builder.UseServiceProvider(services);
        return builder;
    }

    /// <inheritdoc/>
    public IApplicationContext Context { get; }

    /// <inheritdoc/>
    public bool IsMounted => false;

    /// <inheritdoc/>
    public IComponentContext? RootContext => null;

    /// <summary>Records a plugin for awaited installation before the next server render.</summary>
    /// <param name="plugin">The platform-neutral plugin.</param>
    /// <returns>This application.</returns>
    public ServerApplication Use(IApplicationPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (!_installedPlugins.Add(plugin))
        {
            Context.WarnHandler?.Invoke("Plugin has already been applied to the target application.");
            return this;
        }

        _pendingPlugins.Add(plugin);
        return this;
    }

    /// <inheritdoc/>
    public void Unmount()
    {
    }

    /// <inheritdoc/>
    public ValueTask UnmountAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    internal async ValueTask PrepareAsync(CancellationToken cancellationToken)
    {
        while (_pendingPlugins.Count > 0)
        {
            IApplicationPlugin plugin = _pendingPlugins[0];
            await plugin.InstallAsync(this, cancellationToken).ConfigureAwait(false);
            _pendingPlugins.RemoveAt(0);
        }
    }

    IApplication IApplication.Use(IApplicationPlugin plugin) => Use(plugin);
}
