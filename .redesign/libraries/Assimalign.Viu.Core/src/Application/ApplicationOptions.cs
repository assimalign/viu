using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>Configures composition and diagnostics captured by an application build.</summary>
/// <remarks>
/// Values are borrowed; neither a built context nor its lifetime disposes them. The options object
/// is mutable and single-threaded, while each <see cref="ApplicationContext"/> snapshots its current
/// values. Specified by <c>[APP-2]</c> and <c>[APP-6]</c>.
/// </remarks>
public sealed class ApplicationOptions
{
    /// <summary>Gets or sets the required immutable root render description.</summary>
    public VirtualNode? RootComponent { get; set; }

    /// <summary>Gets or sets the borrowed component resolver.</summary>
    public IComponentFactory Components { get; set; } = new ComponentFactory();

    /// <summary>Gets or sets the optional borrowed application service provider.</summary>
    public IServiceProvider? Services { get; set; }

    /// <summary>Gets or sets the optional borrowed application state registry.</summary>
    public IStateStoreRegistry? State { get; set; }

    /// <summary>Gets or sets the optional borrowed reflection-free directive resolver.</summary>
    public IDirectiveResolver? Directives { get; set; }

    /// <summary>
    /// Gets or sets the terminal handler for component and lifetime errors not stopped by an
    /// ancestor capture hook.
    /// </summary>
    public Action<Exception, ComponentContext?, string>? ErrorHandler { get; set; }

    /// <summary>Gets or sets the application warning handler.</summary>
    public Action<string>? WarnHandler { get; set; }

    /// <summary>
    /// Gets or sets the optional observer notified after component event dispatch. This public
    /// deterministic seam lets a test host observe events without mounted-engine access.
    /// </summary>
    /// <remarks>Specified by seam S3 in the component-model plan.</remarks>
    public Action<ComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; set; }
}
