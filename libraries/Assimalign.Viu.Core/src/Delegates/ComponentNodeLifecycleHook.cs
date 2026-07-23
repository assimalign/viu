using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Handles a lifecycle phase for one value in the unified component tree.
/// </summary>
/// <remarks>
/// This is the unified-tree counterpart of Vue 3.5's <c>VNodeHook</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/vnode.ts.
/// Hooks are supplied through the reserved <c>onVnodeBeforeMount</c>,
/// <c>onVnodeMounted</c>, <c>onVnodeBeforeUpdate</c>, <c>onVnodeUpdated</c>,
/// <c>onVnodeBeforeUnmount</c>, and <c>onVnodeUnmounted</c> properties so compiled
/// templates retain Vue's helper contract.
/// </remarks>
/// <param name="component">The current component-tree value.</param>
/// <param name="previousComponent">
/// The previous value for update hooks, or null for mount and unmount hooks.
/// </param>
public delegate void ComponentNodeLifecycleHook(
    IComponent component,
    IComponent? previousComponent);
