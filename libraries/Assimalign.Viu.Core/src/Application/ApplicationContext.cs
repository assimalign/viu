using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>The immutable default application composition context.</summary>
public sealed class ApplicationContext : IApplicationContext
{
    /// <summary>Creates an application context.</summary>
    /// <param name="rootComponent">The root value in the component tree.</param>
    /// <param name="components">The application-selected component resolver.</param>
    /// <param name="services">The independently supplied application service resolver.</param>
    /// <param name="state">The optional application state registry.</param>
    /// <param name="directives">The optional application directive resolver.</param>
    /// <param name="options">The optional diagnostics to snapshot into the context.</param>
    public ApplicationContext(
        IComponent rootComponent,
        IComponentFactory components,
        IServiceProvider services,
        IStateStoreRegistry? state = null,
        IDirectiveResolver? directives = null,
        ApplicationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(services);
        RootComponent = rootComponent;
        Components = components;
        Services = services;
        State = state;
        Directives = directives;
        ErrorHandler = options?.ErrorHandler;
        WarnHandler = options?.WarnHandler;
        EventObserver = options?.EventObserver;
    }

    /// <inheritdoc/>
    public IComponent RootComponent { get; }

    /// <inheritdoc/>
    public IComponentFactory Components { get; }

    /// <inheritdoc/>
    public IServiceProvider Services { get; }

    /// <inheritdoc/>
    public IStateStoreRegistry? State { get; }

    /// <inheritdoc/>
    public IDirectiveResolver? Directives { get; }

    /// <inheritdoc/>
    public Action<Exception, IComponentContext?, string>? ErrorHandler { get; }

    /// <inheritdoc/>
    public Action<string>? WarnHandler { get; }

    internal Action<IComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; }
}
