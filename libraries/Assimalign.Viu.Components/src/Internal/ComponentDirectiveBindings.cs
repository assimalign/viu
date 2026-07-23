using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

internal static class ComponentDirectiveBindings
{
    internal static IReadOnlyList<IComponentDirectiveBinding> Copy(
        IReadOnlyList<IComponentDirectiveBinding>? directives)
    {
        if (directives is null || directives.Count == 0)
        {
            return Array.Empty<IComponentDirectiveBinding>();
        }

        List<IComponentDirectiveBinding> snapshot = new(directives.Count);
        for (int index = 0; index < directives.Count; index++)
        {
            IComponentDirectiveBinding directive = directives[index];
            ArgumentNullException.ThrowIfNull(directive);
            snapshot.Add(directive);
        }

        return snapshot.AsReadOnly();
    }
}
