using System;

namespace Assimalign.Viu.Shared;

/// <summary>
/// Bitmask describing what kind of node a vnode is and what kind of children it carries, so the
/// runtime can branch on cheap bitwise checks instead of type tests. Mirrors the
/// <c>ShapeFlags</c> enum in <c>@vue/shared</c> (<c>packages/shared/src/shapeFlags.ts</c>)
/// bit-for-bit. Set by vnode creation (<c>createVNode</c>) and children normalization at
/// runtime; the low bits encode the vnode's own type, the children bits encode the shape of
/// <c>children</c>.
/// </summary>
[Flags]
public enum ShapeFlags
{
    /// <summary>
    /// The vnode is a plain element (e.g. <c>&lt;div&gt;</c>).
    /// Upstream: <c>ELEMENT = 1</c>.
    /// </summary>
    Element = 1,

    /// <summary>
    /// The vnode is a functional (stateless) component.
    /// Upstream: <c>FUNCTIONAL_COMPONENT = 1 &lt;&lt; 1</c>.
    /// </summary>
    FunctionalComponent = 1 << 1,

    /// <summary>
    /// The vnode is a stateful component (options/setup-based component instance).
    /// Upstream: <c>STATEFUL_COMPONENT = 1 &lt;&lt; 2</c>.
    /// </summary>
    StatefulComponent = 1 << 2,

    /// <summary>
    /// The vnode's children are a single text string.
    /// Upstream: <c>TEXT_CHILDREN = 1 &lt;&lt; 3</c>.
    /// </summary>
    TextChildren = 1 << 3,

    /// <summary>
    /// The vnode's children are an array of vnodes.
    /// Upstream: <c>ARRAY_CHILDREN = 1 &lt;&lt; 4</c>.
    /// </summary>
    ArrayChildren = 1 << 4,

    /// <summary>
    /// The vnode's children are a slots object (component children).
    /// Upstream: <c>SLOTS_CHILDREN = 1 &lt;&lt; 5</c>.
    /// </summary>
    SlotsChildren = 1 << 5,

    /// <summary>
    /// The vnode is a <c>&lt;Teleport&gt;</c> built-in.
    /// Upstream: <c>TELEPORT = 1 &lt;&lt; 6</c>.
    /// </summary>
    Teleport = 1 << 6,

    /// <summary>
    /// The vnode is a <c>&lt;Suspense&gt;</c> built-in.
    /// Upstream: <c>SUSPENSE = 1 &lt;&lt; 7</c>.
    /// </summary>
    Suspense = 1 << 7,

    /// <summary>
    /// The component should be kept alive (it is inside a <c>&lt;KeepAlive&gt;</c> and must be
    /// cached rather than unmounted).
    /// Upstream: <c>COMPONENT_SHOULD_KEEP_ALIVE = 1 &lt;&lt; 8</c>.
    /// </summary>
    ComponentShouldKeepAlive = 1 << 8,

    /// <summary>
    /// The component has been kept alive (it is being re-activated from the
    /// <c>&lt;KeepAlive&gt;</c> cache rather than freshly mounted).
    /// Upstream: <c>COMPONENT_KEPT_ALIVE = 1 &lt;&lt; 9</c>.
    /// </summary>
    ComponentKeptAlive = 1 << 9,

    /// <summary>
    /// Composite mask matching any component vnode, stateful or functional.
    /// Upstream: <c>COMPONENT = ShapeFlags.STATEFUL_COMPONENT | ShapeFlags.FUNCTIONAL_COMPONENT</c>.
    /// </summary>
    Component = StatefulComponent | FunctionalComponent,
}
