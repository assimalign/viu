using System;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Bitmask describing what kind of node a tree value is and what kind of children it carries, so
/// the runtime can branch on cheap bitwise checks instead of type tests. The low bits encode the
/// node's own kind, the children bits encode the shape of its children. The bit layout is a frozen
/// contract between compiled output and the runtime — values are additive only. Specified by
/// <c>[RND-FLAGS-4]</c>.
/// </summary>
[Flags]
public enum ShapeFlags
{
    /// <summary>
    /// The node is a plain element (e.g. <c>&lt;div&gt;</c>).
    /// </summary>
    Element = 1,

    /// <summary>
    /// The node is a functional (stateless) component: it renders purely from its parameters and
    /// owns no instance state.
    /// </summary>
    FunctionalComponent = 1 << 1,

    /// <summary>
    /// The node is a stateful component — one that activates an instance owning reactive state
    /// and a lifecycle.
    /// </summary>
    StatefulComponent = 1 << 2,

    /// <summary>
    /// The node's children are a single text string.
    /// </summary>
    TextChildren = 1 << 3,

    /// <summary>
    /// The node's children are an ordered list of child nodes.
    /// </summary>
    ArrayChildren = 1 << 4,

    /// <summary>
    /// The node's children are a slot collection (component children).
    /// </summary>
    SlotsChildren = 1 << 5,

    /// <summary>
    /// The node is a <c>&lt;Teleport&gt;</c> built-in.
    /// </summary>
    Teleport = 1 << 6,

    /// <summary>
    /// The node is a <c>&lt;Suspense&gt;</c> built-in.
    /// </summary>
    Suspense = 1 << 7,

    /// <summary>
    /// The component should be kept alive (it is inside a <c>&lt;KeepAlive&gt;</c> and must be
    /// cached rather than unmounted).
    /// </summary>
    ComponentShouldKeepAlive = 1 << 8,

    /// <summary>
    /// The component has been kept alive (it is being re-activated from the
    /// <c>&lt;KeepAlive&gt;</c> cache rather than freshly mounted).
    /// </summary>
    ComponentKeptAlive = 1 << 9,

    /// <summary>
    /// Composite mask matching any component node, stateful or functional.
    /// </summary>
    Component = StatefulComponent | FunctionalComponent,
}
