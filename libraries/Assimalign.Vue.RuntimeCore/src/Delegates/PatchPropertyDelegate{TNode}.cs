namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The node-op that lands one vnode prop on a platform element — the C# port of the
/// <c>patchProp</c> option in <c>@vue/runtime-core</c>'s custom renderer API
/// (https://vuejs.org/api/custom-renderer.html). The platform decides property-vs-attribute,
/// class/style fast paths, and event listener wiring ([V01.01.04.02] for the browser).
/// </summary>
/// <typeparam name="TNode">The platform node type.</typeparam>
/// <param name="element">The element being patched.</param>
/// <param name="elementTag">
/// The element's tag. Upstream reads <c>el.tagName</c> inside <c>patchProp</c>; handle-based
/// platforms would pay an interop round-trip for that, so the renderer passes the tag it
/// already knows.
/// </param>
/// <param name="propertyName">The prop name (e.g. <c>"class"</c>, <c>"onClick"</c>).</param>
/// <param name="previousValue">The prior value, or null on mount.</param>
/// <param name="nextValue">The new value, or null to remove.</param>
/// <param name="elementNamespace">The element's namespace (<c>"svg"</c>, <c>"mathml"</c>, or null for HTML).</param>
public delegate void PatchPropertyDelegate<TNode>(
    TNode element,
    string elementTag,
    string propertyName,
    object? previousValue,
    object? nextValue,
    string? elementNamespace);
