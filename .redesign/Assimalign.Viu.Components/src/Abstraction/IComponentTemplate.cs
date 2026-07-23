using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// A freshly activated, user-authored component template. One instance belongs to one mounted
/// <see cref="ITemplateComponent"/>.
/// </summary>
public interface IComponentTemplate
{
    /// <summary>Gets the optional display name used for diagnostics.</summary>
    string? Name => null;

    /// <summary>Gets the component behavior flags.</summary>
    ComponentFlags Flags => ComponentFlags.InheritAttributes;

    /// <summary>Gets the declared input parameters, or null when none are declared.</summary>
    IReadOnlyList<IComponentParameter>? Parameters => null;

    /// <summary>Gets the declared output events, or null when none are declared.</summary>
    IReadOnlyList<IComponentEvent>? Events => null;

    /// <summary>Creates the renderer and registers instance-local behavior.</summary>
    /// <param name="context">The mounted component's setup context.</param>
    /// <returns>The renderer invoked by Core's reactive render effect.</returns>
    ComponentRenderer Setup(IComponentContext context);
}

