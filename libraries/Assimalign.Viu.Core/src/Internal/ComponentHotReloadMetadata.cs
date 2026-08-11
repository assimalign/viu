using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class ComponentHotReloadMetadata
{
    internal ComponentHotReloadMetadata(
        Type componentType,
        Type templateMarker,
        Type scriptMarker,
        Type styleMarker)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(templateMarker);
        ArgumentNullException.ThrowIfNull(scriptMarker);
        ArgumentNullException.ThrowIfNull(styleMarker);

        ComponentType = componentType;
        TemplateMarker = templateMarker;
        ScriptMarker = scriptMarker;
        StyleMarker = styleMarker;
    }

    internal Type ComponentType { get; }

    internal Type TemplateMarker { get; }

    internal Type ScriptMarker { get; }

    internal Type StyleMarker { get; }

    internal ComponentHotReloadChangeKind Classify(IReadOnlySet<Type>? updatedTypes)
    {
        if (updatedTypes is null)
        {
            return ComponentHotReloadChangeKind.ScriptReset;
        }

        // The runtime may report both the generated component type and the nested marker whose
        // method body carries the changed block revision. Prefer the compiler's block-specific
        // marker so a declaring-type hint does not turn a safe style or template update into the
        // conservative component-local script reset ([V01.01.12.05], #94).
        if (updatedTypes.Contains(ScriptMarker))
        {
            return ComponentHotReloadChangeKind.ScriptReset;
        }

        if (updatedTypes.Contains(TemplateMarker))
        {
            return ComponentHotReloadChangeKind.Template;
        }

        if (updatedTypes.Contains(StyleMarker))
        {
            return ComponentHotReloadChangeKind.StyleOnly;
        }

        return updatedTypes.Contains(ComponentType)
            ? ComponentHotReloadChangeKind.ScriptReset
            : ComponentHotReloadChangeKind.None;
    }
}
