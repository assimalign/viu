namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// One runtime directive hook — the C# port of a hook on upstream's <c>ObjectDirective</c>
/// (<c>packages/runtime-core/src/directives.ts</c>,
/// https://vuejs.org/guide/reusability/custom-directives.html). Invoked by the renderer at the
/// matching pipeline point with the same four arguments upstream passes
/// (<c>el, binding, vnode, prevVNode</c>).
/// <para>
/// <paramref name="element"/> is the bound host node. It arrives as <see cref="object"/> — the
/// boxed platform node the generic renderer stored on <see cref="VirtualNode.El"/> — because
/// <see cref="VirtualNode"/> is not generic over the node type; RuntimeDom's directive helpers
/// unbox it to the concrete DOM element.
/// </para>
/// </summary>
/// <param name="element">The bound host node (the boxed platform node), or null before mount.</param>
/// <param name="binding">The binding carrying the value, argument, modifiers, and owning instance.</param>
/// <param name="node">The vnode the directive is attached to.</param>
/// <param name="previousNode">The previous vnode on update hooks; otherwise null.</param>
public delegate void DirectiveHook(object? element, DirectiveBinding binding, VirtualNode node, VirtualNode? previousNode);
