using System;

namespace Assimalign.Viu.Components;

/// <summary>Provides the instance-local inputs and services used while setting up a template.</summary>
public interface IComponentContext
{
    /// <summary>Gets the arguments supplied by the parent render.</summary>
    IComponentArguments Arguments { get; }

    /// <summary>Gets the combined component activator and dependency resolver.</summary>
    IComponentFactory Components { get; }

    /// <summary>
    /// Gets the standard .NET service resolver. This is the same object as <see cref="Components"/>.
    /// </summary>
    IServiceProvider Services => Components;

    /// <summary>Gets the instance-local lifecycle registrar.</summary>
    IComponentLifecycle Lifecycle { get; }

    /// <summary>Emits a declared component event to the parent.</summary>
    /// <param name="eventName">The declared event name.</param>
    /// <param name="value">The event payload.</param>
    void Emit(string eventName, object? value);
}

