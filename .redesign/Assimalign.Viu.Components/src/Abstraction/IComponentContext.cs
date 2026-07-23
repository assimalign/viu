using System;

namespace Assimalign.Viu.Components;

/// <summary>Provides the instance-local inputs and application context used to set up a template.</summary>
public interface IComponentContext
{
    /// <summary>Gets the arguments supplied by the parent render.</summary>
    IComponentArguments Arguments { get; }

    /// <summary>Gets the application-selected component resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the independently supplied application service resolver.</summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the instance-local lifecycle registrar and component-lifetime cancellation token.
    /// </summary>
    IComponentLifecycle Lifecycle { get; }

    /// <summary>Emits a declared component event to the parent.</summary>
    /// <param name="eventName">The declared event name.</param>
    /// <param name="value">The event payload.</param>
    void Emit(string eventName, object? value);
}
