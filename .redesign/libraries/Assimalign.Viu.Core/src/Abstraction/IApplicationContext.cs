using System;
using System.Collections.Generic;
using System.Threading;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>Provides one application's frozen composition and observable runtime state.</summary>
/// <remarks>
/// Composition members are borrowed and never disposed by Core. <see cref="IsRunning"/> and
/// <see cref="Stopping"/> are controlled by the context's single attached
/// <see cref="ApplicationLifetime"/>. Specified by <c>[APP-1]</c>, <c>[APP-2]</c>, and
/// <c>[APP-6]</c>.
/// </remarks>
public interface IApplicationContext
{
    /// <summary>Gets whether the host has signalled Running and has not begun stopping.</summary>
    bool IsRunning { get; }

    /// <summary>Gets the shared graceful-shutdown signal.</summary>
    CancellationToken Stopping { get; }

    /// <summary>Gets the immutable root render description.</summary>
    VirtualNode RootComponent { get; }

    /// <summary>Gets the borrowed component registration resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>
    /// Gets the optional application service provider. When a state registry is configured, this
    /// provider resolves it before delegating other service requests.
    /// </summary>
    IServiceProvider? Services { get; }

    /// <summary>Gets the optional borrowed state registry composed for the application.</summary>
    IStateStoreRegistry? State { get; }

    /// <summary>Gets the optional borrowed reflection-free directive resolver.</summary>
    IDirectiveResolver? Directives { get; }

    /// <summary>Gets the terminal component and application error handler.</summary>
    Action<Exception, ComponentContext?, string>? ErrorHandler { get; }

    /// <summary>Gets the application warning handler.</summary>
    Action<string>? WarnHandler { get; }

    /// <summary>Gets the optional observer invoked after a component event listener runs.</summary>
    Action<ComponentContext, string, IReadOnlyList<object?>>? EventObserver { get; }
}
