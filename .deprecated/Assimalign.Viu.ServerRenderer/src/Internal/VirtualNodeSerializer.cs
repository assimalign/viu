using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Serializes a vnode tree to HTML by walking it — the C# port of <c>renderVNode</c> /
/// <c>renderElementVNode</c> / <c>renderVNodeChildren</c> in <c>@vue/server-renderer</c>
/// (<c>packages/server-renderer/src/render.ts</c>). This is the runtime fallback tier: it walks the
/// same vnodes the client renderer patches, so its output is the parity baseline the compiler-informed
/// string-concatenation fast path ([V01.01.07.02]) must match byte-for-byte. The walk is a single async
/// recursion that awaits inline at each async boundary (a child component's <c>ServerPrefetch</c>),
/// which produces the same in-order document as upstream's nested-buffer unroll while streaming each
/// completed subtree. Not thread-safe (single-threaded JS event-loop model).
/// </summary>
internal static class VirtualNodeSerializer
{
    /// <summary>Serializes one vnode, dispatching on its <see cref="VirtualNode.Type"/>.</summary>
    /// <param name="state">The write surface.</param>
    /// <param name="virtualNode">The vnode to serialize.</param>
    /// <param name="parent">The enclosing component instance, or null at the tree root.</param>
    public static async Task RenderVirtualNodeAsync(SsrRenderState state, VirtualNode virtualNode, ComponentInstance? parent)
    {
        switch (virtualNode.Type)
        {
            case VirtualNodeType.Element:
                await RenderElementAsync(state, virtualNode, parent).ConfigureAwait(false);
                break;
            case VirtualNodeType.Text:
                // Text nodes are escaped (upstream: push(escapeHtml(children))).
                state.Push(ServerRender.EscapeHtml(virtualNode.TextChildren));
                break;
            case VirtualNodeType.Comment:
                // Comment content is stripped of terminators (upstream: escapeHtmlComment); empty yields
                // the <!----> anchor.
                state.Push(ServerRender.SsrRenderComment(virtualNode.TextChildren));
                break;
            case VirtualNodeType.Static:
                // Static vnodes are raw pre-rendered markup, inserted verbatim (upstream: push(children)).
                state.Push(virtualNode.TextChildren ?? string.Empty);
                break;
            case VirtualNodeType.Fragment:
                await RenderFragmentAsync(state, virtualNode, parent).ConfigureAwait(false);
                break;
            case VirtualNodeType.Component:
                await RenderComponentAsync(state, virtualNode, parent).ConfigureAwait(false);
                break;
            case VirtualNodeType.Teleport:
                await RenderTeleportAsync(state, virtualNode, parent).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Serializes a component vnode: set up its instance, render its root, then walk the subtree.</summary>
    public static async Task RenderComponentAsync(SsrRenderState state, VirtualNode componentVirtualNode, ComponentInstance? parent)
    {
        var subtree = await ServerComponentRenderer.SetupAndRenderRootAsync(componentVirtualNode, parent).ConfigureAwait(false);
        var instance = (ComponentInstance)componentVirtualNode.Component!;
        await RenderVirtualNodeAsync(state, subtree, instance).ConfigureAwait(false);
        // Stream the completed component subtree (a no-op in string mode); this is the chunk boundary that
        // keeps the whole document from buffering and honors the writer's backpressure.
        await state.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>Serializes a <c>&lt;Teleport&gt;</c>: anchor pair in place, content buffered by target selector.</summary>
    public static Task RenderTeleportAsync(SsrRenderState state, VirtualNode teleportVirtualNode, ComponentInstance? parent)
    {
        var properties = teleportVirtualNode.Properties;
        // A DOM-node target is not serializable server-side, so only a string selector resolves; anything
        // else is treated as a missing target (skip the content), matching upstream's warn-and-skip.
        var target = properties?["to"] as string;
        var disabled = properties is not null && StyleAndClassNormalization.IsTruthy(properties["disabled"]);
        var children = teleportVirtualNode.ArrayChildren;
        return ServerRender.SsrRenderTeleportAsync(
            state,
            contentState => RenderChildrenAsync(contentState, children, parent),
            target,
            disabled);
    }

    /// <summary>Serializes a fragment's children wrapped in the <c>&lt;!--[--&gt;</c>/<c>&lt;!--]--&gt;</c> hydration anchors.</summary>
    public static async Task RenderFragmentAsync(SsrRenderState state, VirtualNode fragmentVirtualNode, ComponentInstance? parent)
    {
        state.Push(SsrMarkers.FragmentStart);
        await RenderChildrenAsync(state, fragmentVirtualNode.ArrayChildren, parent).ConfigureAwait(false);
        state.Push(SsrMarkers.FragmentEnd);
    }

    /// <summary>Serializes an array of child vnodes in order (upstream: <c>renderVNodeChildren</c>).</summary>
    public static async Task RenderChildrenAsync(SsrRenderState state, VirtualNode?[]? children, ComponentInstance? parent)
    {
        if (children is null)
        {
            return;
        }
        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            // Null entries are this model's comment-placeholder idiom (normalization usually replaces them,
            // but a hand-built array may carry one); emit the anchor so hydration stays aligned.
            if (child is null)
            {
                state.Push(SsrMarkers.EmptyComment);
                continue;
            }
            await RenderVirtualNodeAsync(state, child, parent).ConfigureAwait(false);
        }
    }

    private static async Task RenderElementAsync(SsrRenderState state, VirtualNode elementVirtualNode, ComponentInstance? parent)
    {
        var tag = elementVirtualNode.ElementTag!;
        var properties = elementVirtualNode.Properties;

        state.Push("<" + tag);
        state.Push(ServerRender.SsrRenderAttrs(properties, tag));

        if (DomKnowledge.IsVoidTag(tag))
        {
            // Void elements (<br>, <img>, …) have no closing tag and no children (WHATWG); upstream emits
            // no trailing slash.
            state.Push(">");
            return;
        }
        state.Push(">");

        // Children overrides mirror upstream renderElementVNode: innerHTML is raw (v-html), textContent and
        // a <textarea>'s value are escaped, and any of them suppresses the normal child walk.
        if (properties is not null)
        {
            if (properties.TryGetValue("innerHTML", out var innerHtml) && innerHtml is not null)
            {
                state.Push(DisplayStringFormatter.ToDisplayString(innerHtml));
                state.Push("</" + tag + ">");
                return;
            }
            if (properties.TryGetValue("textContent", out var textContent) && textContent is not null)
            {
                state.Push(ServerRender.EscapeHtml(DisplayStringFormatter.ToDisplayString(textContent)));
                state.Push("</" + tag + ">");
                return;
            }
            if (string.Equals(tag, "textarea", System.StringComparison.Ordinal)
                && properties.TryGetValue("value", out var value)
                && value is not null)
            {
                state.Push(ServerRender.EscapeHtml(DisplayStringFormatter.ToDisplayString(value)));
                state.Push("</" + tag + ">");
                return;
            }
        }

        if (elementVirtualNode.ShapeFlag.HasTextChildren())
        {
            state.Push(ServerRender.EscapeHtml(elementVirtualNode.TextChildren));
        }
        else if (elementVirtualNode.ShapeFlag.HasArrayChildren())
        {
            await RenderChildrenAsync(state, elementVirtualNode.ArrayChildren, parent).ConfigureAwait(false);
        }

        state.Push("</" + tag + ">");
    }
}
