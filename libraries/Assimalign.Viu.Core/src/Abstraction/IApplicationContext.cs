using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Provides the root component, independently selected component and service resolvers, and optional
/// state registry shared by one application.
/// </summary>
public interface IApplicationContext
{
    /// <summary>Gets the root value in the unified component tree.</summary>
    IComponent RootComponent { get; }

    /// <summary>Gets the application-selected component resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the independently supplied application service resolver.</summary>
    IServiceProvider Services { get; }

    /// <summary>Gets the optional application state registry.</summary>
    IStateStoreRegistry? State { get; }

    /// <summary>Gets the optional application directive resolver.</summary>
    IDirectiveResolver? Directives { get; }

    /// <summary>
    /// Gets or sets the terminal handler for render, lifecycle, watcher, and event errors that no
    /// component error-capture hook stopped.
    /// </summary>
    Action<Exception, IComponentContext?, string>? ErrorHandler { get; set; }

    /// <summary>Gets or sets the application warning handler.</summary>
    Action<string>? WarnHandler { get; set; }

    /// <summary>Gets or sets whether host-neutral performance instrumentation is enabled.</summary>
    bool Performance { get; set; }
}
