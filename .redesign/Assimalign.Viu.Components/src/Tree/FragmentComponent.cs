using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>An immutable fragment component.</summary>
public sealed class FragmentComponent : IFragmentComponent
{
    /// <summary>Creates a fragment component.</summary>
    /// <param name="children">The grouped children.</param>
    /// <param name="key">The optional sibling identity.</param>
    public FragmentComponent(IReadOnlyList<IComponent>? children = null, object? key = null)
    {
        Children = ComponentChildren.Copy(children);
        Key = key;
    }

    /// <inheritdoc/>
    public ComponentKind Kind => ComponentKind.Fragment;

    /// <inheritdoc/>
    public object? Key { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IComponent> Children { get; }
}

