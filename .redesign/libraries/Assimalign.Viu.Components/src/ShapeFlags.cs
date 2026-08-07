using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Reserved node-shape classification — a frozen value contract with no current runtime
/// consumer, retained so previously compiled output never observes a layout change. Specified by
/// <c>[RND-FLAGS-4]</c>; moved verbatim from the dissolved shared library.
/// </summary>
[Flags]
public enum ShapeFlags
{
    /// <summary>An element node.</summary>
    Element = 1,

    /// <summary>A functional component.</summary>
    FunctionalComponent = 1 << 1,

    /// <summary>A stateful component.</summary>
    StatefulComponent = 1 << 2,

    /// <summary>Children collapse to one text payload.</summary>
    TextChildren = 1 << 3,

    /// <summary>Children are an ordered array.</summary>
    ArrayChildren = 1 << 4,

    /// <summary>Children are named slots.</summary>
    SlotsChildren = 1 << 5,

    /// <summary>A teleport node.</summary>
    Teleport = 1 << 6,

    /// <summary>A suspense node.</summary>
    Suspense = 1 << 7,

    /// <summary>A component that should be cached by keep-alive.</summary>
    ComponentShouldKeepAlive = 1 << 8,

    /// <summary>A component currently retained by keep-alive.</summary>
    ComponentKeptAlive = 1 << 9,

    /// <summary>Any component shape.</summary>
    Component = StatefulComponent | FunctionalComponent,
}
