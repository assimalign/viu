using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Handles a lifecycle phase for one value in the unified component tree.
/// </summary>
/// <remarks>
/// The hook observes a single tree value's own mounting and patching, independently of any
/// component lifecycle, which is what lets a directive or a compiled template attach behavior to a
/// plain element.
/// Hooks are supplied through the reserved <c>onVnodeBeforeMount</c>,
/// <c>onVnodeMounted</c>, <c>onVnodeBeforeUpdate</c>, <c>onVnodeUpdated</c>,
/// <c>onVnodeBeforeUnmount</c>, and <c>onVnodeUnmounted</c> properties so compiled
/// templates bind these helper names literally.
/// </remarks>
/// <param name="component">The current component-tree value.</param>
/// <param name="previousComponent">
/// The previous value for update hooks, or null for mount and unmount hooks.
/// </param>
public delegate void ComponentNodeLifecycleHook(
    IComponent component,
    IComponent? previousComponent);
