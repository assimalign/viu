using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

internal static class ComponentChildren
{
    internal static IReadOnlyList<IComponent> Copy(IReadOnlyList<IComponent>? children)
    {
        if (children is null || children.Count == 0)
        {
            return Array.Empty<IComponent>();
        }

        List<IComponent> snapshot = new(children.Count);
        for (int index = 0; index < children.Count; index++)
        {
            IComponent child = children[index];
            ArgumentNullException.ThrowIfNull(child);
            snapshot.Add(child);
        }

        return snapshot.AsReadOnly();
    }
}

