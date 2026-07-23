using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Provides the instance-local inputs and application context used to set up a template.</summary>
public interface IComponentContext
{
    /// <summary>Gets the arguments supplied by the parent render.</summary>
    IComponentArguments Arguments { get; }

    /// <summary>Gets the current named slots supplied by the parent render.</summary>
    IReadOnlyDictionary<string, ComponentSlot> Slots { get; }

    /// <summary>
    /// Gets the current undeclared attributes eligible to fall through to the rendered root.
    /// </summary>
    IComponentAttributeCollection Attributes { get; }

    /// <summary>Gets the application-selected component resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>Gets the independently supplied application service resolver.</summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets the instance-local lifecycle registrar and component-lifetime cancellation token.
    /// </summary>
    IComponentLifecycle Lifecycle { get; }

    /// <summary>
    /// Emits a declared component event to the parent with zero or more arguments.
    /// </summary>
    /// <param name="eventName">The declared event name.</param>
    /// <param name="arguments">The event arguments supplied to the parent listener.</param>
    void Emit(string eventName, params object?[] arguments);

    /// <summary>
    /// Selects the public surface assigned to a parent template reference. When this method is not
    /// called, Core assigns the mounted component context.
    /// </summary>
    /// <param name="value">The public component surface.</param>
    void Expose(object? value)
    {
    }
}
