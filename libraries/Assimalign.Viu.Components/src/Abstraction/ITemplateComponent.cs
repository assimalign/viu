using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a request to mount a user-authored component template. The request is render data;
/// <see cref="IComponentFactory"/> creates the per-mount <see cref="IComponentTemplate"/>.
/// </summary>
public interface ITemplateComponent : IComponent
{
    /// <summary>
    /// Gets the explicitly registered template type, or null when this request uses
    /// <see cref="TemplateName"/>.
    /// </summary>
    Type? TemplateType { get; }

    /// <summary>
    /// Gets the registered template name, or null when this request uses
    /// <see cref="TemplateType"/>.
    /// </summary>
    string? TemplateName { get; }

    /// <summary>Gets the arguments supplied by the parent render.</summary>
    IComponentArguments Arguments { get; }

    /// <summary>Gets the optional named slots supplied by the parent render.</summary>
    IReadOnlyDictionary<string, ComponentSlot>? Slots { get; }

    /// <summary>
    /// Gets the optional parent listeners for component-emitted events. Listener keys use event
    /// names such as <c>saved</c>; the <c>savedOnce</c> key carries Vue's <c>onSavedOnce</c>
    /// convention.
    /// </summary>
    IReadOnlyDictionary<string, ComponentEventListener>? Listeners { get; }

    /// <summary>Gets the immutable directives applied to the template's rendered root.</summary>
    IReadOnlyList<IComponentDirectiveBinding> Directives { get; }
}
