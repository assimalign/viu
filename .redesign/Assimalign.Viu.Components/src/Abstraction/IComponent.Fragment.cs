using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Describes a keyless or keyed group of sibling components.</summary>
public interface IFragmentComponent : IComponent
{
    /// <summary>Gets the fragment's children.</summary>
    IReadOnlyList<IComponent> Children { get; }
}

