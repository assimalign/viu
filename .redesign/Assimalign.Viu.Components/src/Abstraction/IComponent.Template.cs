using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes a request to mount a user-authored component template. The request is render data;
/// <see cref="IComponentFactory"/> creates the per-mount <see cref="IComponentTemplate"/>.
/// </summary>
public interface ITemplateComponent : IComponent
{
    /// <summary>Gets the explicitly registered template type.</summary>
    Type TemplateType { get; }

    /// <summary>Gets the arguments supplied by the parent render.</summary>
    IComponentArguments Arguments { get; }

    /// <summary>Gets the optional named slots supplied by the parent render.</summary>
    IReadOnlyDictionary<string, ComponentSlot>? Slots { get; }
}

