using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Bitmask describing a node's historical runtime shape and the form of its children. The closed
/// <see cref="VirtualNode"/> algebra is authoritative for current dispatch, but these numeric
/// values remain a frozen, additive-only contract for previously compiled output. Specified by
/// <c>[RND-FLAGS-1]</c> and <c>[RND-FLAGS-4]</c>.
/// </summary>
[Flags]
public enum ShapeFlags
{
    /// <summary>The node is a plain host element.</summary>
    Element = 1,

    /// <summary>The node represents stateless functional component behavior.</summary>
    FunctionalComponent = 1 << 1,

    /// <summary>The node represents an activated component instance with state and lifecycle.</summary>
    StatefulComponent = 1 << 2,

    /// <summary>The node's children collapse to one text payload.</summary>
    TextChildren = 1 << 3,

    /// <summary>The node's children are an ordered list.</summary>
    ArrayChildren = 1 << 4,

    /// <summary>The node's children are a named slot collection.</summary>
    SlotsChildren = 1 << 5,

    /// <summary>The node is a teleport structural value.</summary>
    Teleport = 1 << 6,

    /// <summary>The node is a suspense structural value.</summary>
    Suspense = 1 << 7,

    /// <summary>The component is inside a keep-alive boundary and should be retained.</summary>
    ComponentShouldKeepAlive = 1 << 8,

    /// <summary>The component is being reactivated from retained keep-alive state.</summary>
    ComponentKeptAlive = 1 << 9,

    /// <summary>Composite mask matching either functional or stateful component shape.</summary>
    Component = StatefulComponent | FunctionalComponent,
}
