using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal readonly record struct TransitionIdentity(
    VirtualNodeKind Kind,
    object Type,
    object? Key)
{
    internal static TransitionIdentity Create(VirtualNode value)
    {
        object type = value switch
        {
            ElementNode element => element.Name,
            ComponentNode component => component.Component,
            StaticNode staticContent => (staticContent.Format, staticContent.Content),
            _ => value.Kind,
        };
        return new TransitionIdentity(value.Kind, type, value.Key);
    }
}
