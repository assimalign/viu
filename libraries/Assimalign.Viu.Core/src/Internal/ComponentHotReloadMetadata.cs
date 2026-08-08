using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

internal sealed class ComponentHotReloadMetadata
{
    internal ComponentHotReloadMetadata(
        Type componentType,
        string componentIdentifier,
        Type templateMarker,
        Type scriptMarker,
        Type styleMarker)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentException.ThrowIfNullOrEmpty(componentIdentifier);
        ArgumentNullException.ThrowIfNull(templateMarker);
        ArgumentNullException.ThrowIfNull(scriptMarker);
        ArgumentNullException.ThrowIfNull(styleMarker);

        ComponentType = componentType;
        ComponentIdentifier = componentIdentifier;
        TemplateMarker = templateMarker;
        ScriptMarker = scriptMarker;
        StyleMarker = styleMarker;
    }

    internal Type ComponentType { get; }

    internal string ComponentIdentifier { get; }

    internal Type TemplateMarker { get; }

    internal Type ScriptMarker { get; }

    internal Type StyleMarker { get; }

    internal ComponentHotReloadChangeKind Classify(IReadOnlySet<Type>? updatedTypes)
    {
        if (updatedTypes is null
            || updatedTypes.Contains(ComponentType)
            || updatedTypes.Contains(ScriptMarker))
        {
            return ComponentHotReloadChangeKind.ScriptReset;
        }

        if (updatedTypes.Contains(TemplateMarker))
        {
            return ComponentHotReloadChangeKind.Template;
        }

        return updatedTypes.Contains(StyleMarker)
            ? ComponentHotReloadChangeKind.StyleOnly
            : ComponentHotReloadChangeKind.None;
    }
}
