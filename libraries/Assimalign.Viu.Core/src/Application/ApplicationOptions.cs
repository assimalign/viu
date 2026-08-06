using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>Configures composition and diagnostics frozen into an application context at build time.</summary>
/// <remarks>
/// A builder snapshots these values for each built application; later option mutations do not alter
/// an existing context. Not thread-safe. Specified by <c>[APP-2]</c>.
/// </remarks>
public sealed class ApplicationOptions
{
    /// <summary>Gets or sets the required root value in the unified component tree.</summary>
    public IComponent? RootComponent { get; set; }

    /// <summary>
    /// Gets or sets the application-selected component resolver, defaulting to an empty resolver.
    /// </summary>
    public IComponentFactory Components { get; set; } = EmptyComponentFactory.Instance;

    /// <summary>
    /// Gets or sets the borrowed application service resolver, defaulting to an empty resolver.
    /// </summary>
    public IServiceProvider Services { get; set; } = EmptyServiceProvider.Instance;

    /// <summary>Gets or sets the optional borrowed directive resolver.</summary>
    public IDirectiveResolver? Directives { get; set; }

    /// <summary>Gets or sets the optional borrowed application state registry.</summary>
    public IStateStoreRegistry? State { get; set; }

    /// <summary>
    /// Gets or sets the terminal handler for render, lifecycle, watcher, and event errors that no
    /// component error-capture hook stopped.
    /// </summary>
    public Action<Exception, IComponentContext?, string>? ErrorHandler { get; set; }

    /// <summary>Gets or sets the application warning handler.</summary>
    public Action<string>? WarnHandler { get; set; }

    internal Action<IComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; set; }
}
