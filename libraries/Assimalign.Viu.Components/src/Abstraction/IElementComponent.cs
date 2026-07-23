using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Describes a platform element in the component tree.</summary>
public interface IElementComponent : IComponent
{
    /// <summary>Gets the platform tag name.</summary>
    string Tag { get; }

    /// <summary>Gets the element attributes and event bindings.</summary>
    IComponentAttributeCollection Attributes { get; }

    /// <summary>Gets the element's children.</summary>
    IReadOnlyList<IComponent> Children { get; }

    /// <summary>Gets the immutable directives applied to this element.</summary>
    IReadOnlyList<IComponentDirectiveBinding> Directives { get; }
}
