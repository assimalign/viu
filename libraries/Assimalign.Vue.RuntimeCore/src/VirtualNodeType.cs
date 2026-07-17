namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// Discriminates what a <see cref="VirtualNode"/> represents. Mirrors the vnode <c>type</c> union
/// in <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/vnode.ts</c>), where upstream uses a
/// string tag, a component object, or the <c>Text</c>/<c>Comment</c>/<c>Static</c>/<c>Fragment</c>
/// symbols — Vuecs replaces the symbol union with this enum so the patch dispatcher can branch
/// without type tests. See https://vuejs.org/guide/extras/rendering-mechanism.html.
/// </summary>
public enum VirtualNodeType
{
    /// <summary>A plain platform element (upstream: a string tag such as <c>"div"</c>).</summary>
    Element,

    /// <summary>A component vnode (upstream: a component options/definition object).</summary>
    Component,

    /// <summary>A text node (upstream: the <c>Text</c> symbol).</summary>
    Text,

    /// <summary>A comment node (upstream: the <c>Comment</c> symbol).</summary>
    Comment,

    /// <summary>
    /// A pre-rendered static chunk inserted in one operation (upstream: the <c>Static</c> symbol).
    /// </summary>
    Static,

    /// <summary>A keyless wrapper for multiple root nodes (upstream: the <c>Fragment</c> symbol).</summary>
    Fragment,
}
