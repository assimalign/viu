using System;
using System.Collections.Generic;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.Forms.Components;

/// <summary>
/// A live, read-only view of the form model — it is handed the <see cref="RegistrationForm"/> by prop
/// and reads its refs and computeds in its render, so it re-renders whenever a field changes. It uses
/// no <c>v-model</c> and no event payloads, so (unlike the form itself) it mounts in the in-memory test
/// renderer and its reactive updates are asserted DOM-free.
/// </summary>
public sealed class FormPreviewComponent : IComponentDefinition
{
    private static readonly IReadOnlyList<ComponentPropertyDefinition> DeclaredProperties =
        [new ComponentPropertyDefinition("form")];

    /// <inheritdoc/>
    public string? Name => "FormPreview";

    /// <inheritdoc/>
    public IReadOnlyList<ComponentPropertyDefinition>? Properties => DeclaredProperties;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var form = properties.Get<RegistrationForm>("form")!;
        return () => VirtualNodeFactory.Element(
            "aside",
            VirtualNodeFactory.Properties(("class", "preview")),
            VirtualNodeFactory.Element("h2", "Live model"),
            VirtualNodeFactory.Element(
                "p",
                VirtualNodeFactory.Properties(("class", form.IsValid.Value ? "status is-ready" : "status")),
                form.IsValid.Value ? "Ready to submit" : "Incomplete"),
            VirtualNodeFactory.Element(
                "pre",
                VirtualNodeFactory.Properties(("class", "summary")),
                form.Summary.Value));
    }
}
