namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// A vnode lifecycle hook attached through props (<c>onVnodeBeforeMount</c>,
/// <c>onVnodeMounted</c>, <c>onVnodeBeforeUpdate</c>, <c>onVnodeUpdated</c>,
/// <c>onVnodeBeforeUnmount</c>, <c>onVnodeUnmounted</c>), invoked by the renderer at the
/// corresponding pipeline point. Mirrors <c>VNodeHook</c> in
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/vnode.ts</c>).
/// </summary>
/// <param name="node">The vnode the hook fires for.</param>
/// <param name="previousNode">On update hooks, the vnode being replaced; otherwise null.</param>
public delegate void VirtualNodeHook(VirtualNode node, VirtualNode? previousNode);
