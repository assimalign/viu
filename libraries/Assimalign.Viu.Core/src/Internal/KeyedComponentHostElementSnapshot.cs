using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal sealed class KeyedComponentHostElementSnapshot
{
    internal KeyedComponentHostElementSnapshot(
        IComponent component,
        object key,
        object element)
    {
        Component = component;
        Key = key;
        Element = element;
    }

    internal IComponent Component { get; }

    internal object Key { get; }

    internal object Element { get; }
}
