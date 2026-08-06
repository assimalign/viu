using System;
using System.Threading;

using Assimalign.Viu.Components;
using Assimalign.Viu.State;

namespace Assimalign.Viu;

/// <summary>
/// Provides the frozen composition and observable runtime state shared by one application.
/// </summary>
/// <remarks>
/// Composition members never change after the application is built. <see cref="IsRunning"/> and
/// <see cref="Stopping"/> expose the host-owned lifetime without making middleware depend on a
/// separate execution object. Specified by <c>[APP-1]</c>, <c>[APP-2]</c>, and <c>[APP-5]</c>.
/// </remarks>
public interface IApplicationContext
{
    /// <summary>Gets whether the host terminal is mounted and has not begun stopping.</summary>
    bool IsRunning { get; }

    /// <summary>Gets the token that signals graceful application shutdown.</summary>
    CancellationToken Stopping { get; }

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
    /// Gets the terminal handler for render, lifecycle, watcher, and event errors that no
    /// component error-capture hook stopped.
    /// </summary>
    Action<Exception, IComponentContext?, string>? ErrorHandler { get; }

    /// <summary>Gets the application warning handler.</summary>
    Action<string>? WarnHandler { get; }
}
