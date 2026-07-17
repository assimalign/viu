using System;
using System.Collections.Generic;

using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The platform-agnostic mount/patch/unmount pipeline over injected node-ops — the C# port of
/// the renderer produced by <c>createRenderer</c> in <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/renderer.ts</c>, https://vuejs.org/api/custom-renderer.html).
/// The patch dispatcher routes by <see cref="VirtualNode.Type"/> and
/// <see cref="VirtualNode.ShapeFlag"/> to element, text, comment, static, fragment, and
/// component paths; mismatched node types unmount and remount; fragments mount between
/// start/end anchors; insertion honors the anchor throughout. A positive
/// <see cref="VirtualNode.PatchFlag"/> follows the compiled patch contract (targeted
/// class/style/props/text updates); unflagged vnodes take the full diff. Array children patch
/// positionally for now — keyed longest-increasing-subsequence reordering lands with
/// [V01.01.03.03]; the component path lands with [V01.01.03.06].
/// Created through <see cref="RendererFactory.CreateRenderer{TNode}"/>.
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TNode">The platform node type; <c>default</c> means "no node".</typeparam>
public sealed class Renderer<TNode>
    where TNode : notnull
{
    private static readonly EqualityComparer<TNode> NodeComparer = EqualityComparer<TNode>.Default;

    private readonly RendererOptions<TNode> _options;
    private readonly Dictionary<TNode, VirtualNode> _containerRoots = new(NodeComparer);

    internal Renderer(RendererOptions<TNode> options)
    {
        _options = options;
    }

    /// <summary>
    /// Renders <paramref name="node"/> into <paramref name="container"/> — mounts on first
    /// call, patches against the previous tree on subsequent calls, and unmounts everything
    /// when <paramref name="node"/> is null (upstream: <c>render(vnode, container)</c>).
    /// Pending pre- and post-flush scheduler callbacks are drained before returning, so
    /// lifecycle hooks fire synchronously with a direct render (upstream parity).
    /// </summary>
    /// <param name="node">The tree to render, or null to unmount.</param>
    /// <param name="container">The platform container node.</param>
    public void Render(VirtualNode? node, TNode container)
    {
        _containerRoots.TryGetValue(container, out var current);
        if (node is null)
        {
            if (current is not null)
            {
                Unmount(current, doRemove: true);
                _containerRoots.Remove(container);
            }
        }
        else
        {
            Patch(current, node, container, default, null);
            _containerRoots[container] = node;
        }
        Scheduler.FlushAfterSynchronousRender();
    }

    /// <summary>
    /// Binds <paramref name="renderFunction"/> reactively to <paramref name="container"/>:
    /// mounts immediately inside a tracked effect, then re-renders through the scheduler
    /// whenever a tracked dependency changes (upstream: <c>setupRenderEffect</c>; see
    /// <see cref="RenderEffect{TNode}"/>).
    /// </summary>
    /// <param name="renderFunction">The tracked function producing the tree.</param>
    /// <param name="container">The platform container node.</param>
    /// <returns>The live binding; stop or dispose it to detach.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="renderFunction"/> is null.</exception>
    public RenderEffect<TNode> CreateRenderEffect(Func<VirtualNode> renderFunction, TNode container)
    {
        ArgumentNullException.ThrowIfNull(renderFunction);
        return new RenderEffect<TNode>(this, renderFunction, container);
    }

    private void Patch(VirtualNode? current, VirtualNode next, TNode container, TNode? anchor, string? elementNamespace)
    {
        if (ReferenceEquals(current, next))
        {
            return;
        }
        if (current is not null && !IsSameVirtualNodeType(current, next))
        {
            // Mismatched type: unmount and remount in place (upstream parity).
            anchor = GetNextHostNode(current);
            Unmount(current, doRemove: true);
            current = null;
        }
        switch (next.Type)
        {
            case VirtualNodeType.Text:
                ProcessText(current, next, container, anchor);
                break;
            case VirtualNodeType.Comment:
                ProcessComment(current, next, container, anchor);
                break;
            case VirtualNodeType.Static:
                ProcessStatic(current, next, container, anchor, elementNamespace);
                break;
            case VirtualNodeType.Fragment:
                ProcessFragment(current, next, container, anchor, elementNamespace);
                break;
            case VirtualNodeType.Element:
                ProcessElement(current, next, container, anchor, elementNamespace);
                break;
            case VirtualNodeType.Component:
                throw new NotSupportedException(
                    "Component vnodes are not renderable yet — the component instance model lands with [V01.01.03.06].");
            default:
                throw new InvalidOperationException($"Unknown vnode type: {next.Type}.");
        }
    }

    private static bool IsSameVirtualNodeType(VirtualNode current, VirtualNode next)
        => current.Type == next.Type
            && Equals(current.Key, next.Key)
            && (next.Type != VirtualNodeType.Element
                || string.Equals(current.ElementTag, next.ElementTag, StringComparison.Ordinal));

    private void ProcessText(VirtualNode? current, VirtualNode next, TNode container, TNode? anchor)
    {
        if (current is null)
        {
            var textNode = _options.CreateText(next.TextChildren ?? string.Empty);
            next.El = textNode;
            _options.Insert(textNode, container, anchor);
        }
        else
        {
            next.El = current.El;
            if (!string.Equals(current.TextChildren, next.TextChildren, StringComparison.Ordinal))
            {
                _options.SetText((TNode)next.El!, next.TextChildren ?? string.Empty);
            }
        }
    }

    private void ProcessComment(VirtualNode? current, VirtualNode next, TNode container, TNode? anchor)
    {
        if (current is null)
        {
            var commentNode = _options.CreateComment(next.TextChildren ?? string.Empty);
            next.El = commentNode;
            _options.Insert(commentNode, container, anchor);
        }
        else
        {
            // Upstream parity: comment content is never patched.
            next.El = current.El;
        }
    }

    private void ProcessStatic(VirtualNode? current, VirtualNode next, TNode container, TNode? anchor, string? elementNamespace)
    {
        if (current is null)
        {
            if (_options.InsertStaticContent is null)
            {
                throw new NotSupportedException(
                    "This renderer's options do not provide InsertStaticContent, which mounting a Static vnode requires.");
            }
            var (first, last) = _options.InsertStaticContent(
                next.TextChildren ?? string.Empty, container, anchor, elementNamespace);
            next.El = first;
            next.Anchor = last;
        }
        else
        {
            // Static content is immutable by contract in production (upstream parity).
            next.El = current.El;
            next.Anchor = current.Anchor;
        }
    }

    private void ProcessFragment(VirtualNode? current, VirtualNode next, TNode container, TNode? anchor, string? elementNamespace)
    {
        if (current is null)
        {
            // Fragment anchors are empty text nodes (upstream parity).
            var start = _options.CreateText(string.Empty);
            var end = _options.CreateText(string.Empty);
            next.El = start;
            next.Anchor = end;
            _options.Insert(start, container, anchor);
            _options.Insert(end, container, anchor);
            MountChildren(next.ArrayChildren ?? [], container, end, elementNamespace);
        }
        else
        {
            next.El = current.El;
            next.Anchor = current.Anchor;
            PatchChildren(current, next, container, (TNode)next.Anchor!, elementNamespace);
        }
    }

    private void ProcessElement(VirtualNode? current, VirtualNode next, TNode container, TNode? anchor, string? elementNamespace)
    {
        // Namespace switching per upstream mountElement: an <svg>/<math> subtree switches
        // namespace; <foreignObject> children return to HTML (resolveChildrenNamespace).
        var tag = next.ElementTag!;
        if (string.Equals(tag, "svg", StringComparison.Ordinal))
        {
            elementNamespace = "svg";
        }
        else if (string.Equals(tag, "math", StringComparison.Ordinal))
        {
            elementNamespace = "mathml";
        }
        if (current is null)
        {
            MountElement(next, container, anchor, elementNamespace);
        }
        else
        {
            PatchElement(current, next, elementNamespace);
        }
    }

    private void MountElement(VirtualNode node, TNode container, TNode? anchor, string? elementNamespace)
    {
        var element = _options.CreateElement(node.ElementTag!, elementNamespace);
        node.El = element;
        // Children before props so e.g. a <select> value lands after its options (upstream order).
        if ((node.ShapeFlag & ShapeFlags.TextChildren) != 0)
        {
            _options.SetElementText(element, node.TextChildren ?? string.Empty);
        }
        else if ((node.ShapeFlag & ShapeFlags.ArrayChildren) != 0)
        {
            MountChildren(node.ArrayChildren!, element, default, ChildrenNamespace(node, elementNamespace));
        }
        if (node.Properties is not null)
        {
            foreach (var (name, value) in node.Properties)
            {
                if (IsReservedProperty(name) || string.Equals(name, "value", StringComparison.Ordinal))
                {
                    continue;
                }
                _options.PatchProperty(element, name, null, value, elementNamespace);
            }
            // "value" is patched last so it can depend on properties like <input type> and on
            // mounted <option> children (upstream parity).
            if (node.Properties.TryGetValue("value", out var valueProperty))
            {
                _options.PatchProperty(element, "value", null, valueProperty, elementNamespace);
            }
        }
        InvokeHook(node, null, "onVnodeBeforeMount");
        _options.Insert(element, container, anchor);
        QueuePostHook(node, null, "onVnodeMounted");
    }

    private void PatchElement(VirtualNode current, VirtualNode next, string? elementNamespace)
    {
        next.El = current.El;
        var element = (TNode)next.El!;
        InvokeHook(next, current, "onVnodeBeforeUpdate");
        var patchFlag = (int)next.PatchFlag;
        if (patchFlag > 0)
        {
            // Compiled patch contract: only what the flags name can have changed. On WASM every
            // skipped patchProp visit is a skipped interop call.
            if ((next.PatchFlag & PatchFlags.FullProps) != 0)
            {
                PatchProperties(element, current.Properties, next.Properties, elementNamespace);
            }
            else
            {
                if ((next.PatchFlag & PatchFlags.Class) != 0)
                {
                    var previousClass = current.Properties?["class"];
                    var nextClass = next.Properties?["class"];
                    if (!Equals(previousClass, nextClass))
                    {
                        _options.PatchProperty(element, "class", previousClass, nextClass, elementNamespace);
                    }
                }
                if ((next.PatchFlag & PatchFlags.Style) != 0)
                {
                    _options.PatchProperty(
                        element, "style", current.Properties?["style"], next.Properties?["style"], elementNamespace);
                }
                if ((next.PatchFlag & PatchFlags.Props) != 0 && next.DynamicProperties is not null)
                {
                    foreach (var name in next.DynamicProperties)
                    {
                        object? previousValue = null;
                        object? nextValue = null;
                        current.Properties?.TryGetValue(name, out previousValue);
                        next.Properties?.TryGetValue(name, out nextValue);
                        if (!Equals(previousValue, nextValue) || string.Equals(name, "value", StringComparison.Ordinal))
                        {
                            _options.PatchProperty(element, name, previousValue, nextValue, elementNamespace);
                        }
                    }
                }
            }
            if ((next.PatchFlag & PatchFlags.Text) != 0)
            {
                if (!string.Equals(current.TextChildren, next.TextChildren, StringComparison.Ordinal))
                {
                    _options.SetElementText(element, next.TextChildren ?? string.Empty);
                }
            }
        }
        else
        {
            // Unoptimized (or Bail): full props diff and full children diff.
            PatchProperties(element, current.Properties, next.Properties, elementNamespace);
            PatchChildren(current, next, element, default, ChildrenNamespace(next, elementNamespace));
        }
        QueuePostHook(next, current, "onVnodeUpdated");
    }

    private void PatchProperties(TNode element, VirtualNodeProperties? previous, VirtualNodeProperties? next, string? elementNamespace)
    {
        if (ReferenceEquals(previous, next))
        {
            return;
        }
        if (previous is not null)
        {
            foreach (var (name, value) in previous)
            {
                if (!IsReservedProperty(name) && (next is null || !next.ContainsName(name)))
                {
                    _options.PatchProperty(element, name, value, null, elementNamespace);
                }
            }
        }
        if (next is not null)
        {
            foreach (var (name, value) in next)
            {
                if (IsReservedProperty(name) || string.Equals(name, "value", StringComparison.Ordinal))
                {
                    continue;
                }
                object? previousValue = null;
                previous?.TryGetValue(name, out previousValue);
                if (!Equals(previousValue, value))
                {
                    _options.PatchProperty(element, name, previousValue, value, elementNamespace);
                }
            }
            // "value" is forced last: the live platform value can drift from the vnode value,
            // so equality against the previous vnode is not a safe skip (upstream parity).
            if (next.TryGetValue("value", out var nextValue))
            {
                object? previousBagValue = null;
                previous?.TryGetValue("value", out previousBagValue);
                _options.PatchProperty(element, "value", previousBagValue, nextValue, elementNamespace);
            }
        }
    }

    private void PatchChildren(VirtualNode current, VirtualNode next, TNode container, TNode? anchor, string? elementNamespace)
    {
        var previousShape = current.ShapeFlag;
        var nextShape = next.ShapeFlag;
        if ((nextShape & ShapeFlags.TextChildren) != 0)
        {
            if ((previousShape & ShapeFlags.ArrayChildren) != 0)
            {
                UnmountChildren(current.ArrayChildren!, doRemove: true);
            }
            if ((previousShape & ShapeFlags.TextChildren) == 0
                || !string.Equals(current.TextChildren, next.TextChildren, StringComparison.Ordinal))
            {
                _options.SetElementText(container, next.TextChildren ?? string.Empty);
            }
        }
        else if ((previousShape & ShapeFlags.ArrayChildren) != 0)
        {
            if ((nextShape & ShapeFlags.ArrayChildren) != 0)
            {
                PatchUnkeyedChildren(current.ArrayChildren!, next.ArrayChildren!, container, anchor, elementNamespace);
            }
            else
            {
                UnmountChildren(current.ArrayChildren!, doRemove: true);
            }
        }
        else
        {
            if ((previousShape & ShapeFlags.TextChildren) != 0)
            {
                _options.SetElementText(container, string.Empty);
            }
            if ((nextShape & ShapeFlags.ArrayChildren) != 0)
            {
                MountChildren(next.ArrayChildren!, container, anchor, elementNamespace);
            }
        }
    }

    private void PatchUnkeyedChildren(VirtualNode[] previousChildren, VirtualNode[] nextChildren, TNode container, TNode? anchor, string? elementNamespace)
    {
        // Positional diff (upstream patchUnkeyedChildren). Keyed minimal-move reordering lands
        // with [V01.01.03.03]; a positional key mismatch still replaces correctly via the
        // same-type check in Patch.
        var commonLength = Math.Min(previousChildren.Length, nextChildren.Length);
        for (var index = 0; index < commonLength; index++)
        {
            var nextChild = nextChildren[index] = VirtualNodeFactory.Normalize(nextChildren[index]);
            Patch(previousChildren[index], nextChild, container, anchor, elementNamespace);
        }
        if (previousChildren.Length > nextChildren.Length)
        {
            UnmountChildren(previousChildren, doRemove: true, startIndex: commonLength);
        }
        else
        {
            MountChildren(nextChildren, container, anchor, elementNamespace, startIndex: commonLength);
        }
    }

    private void MountChildren(VirtualNode[] children, TNode container, TNode? anchor, string? elementNamespace, int startIndex = 0)
    {
        for (var index = startIndex; index < children.Length; index++)
        {
            // Normalized in place so the array holds the mounted instances (upstream write-back
            // in mountChildren; cloning protects an already-mounted reused vnode's El).
            var child = children[index] = VirtualNodeFactory.Normalize(children[index]);
            Patch(null, child, container, anchor, elementNamespace);
        }
    }

    private void UnmountChildren(VirtualNode[] children, bool doRemove, int startIndex = 0)
    {
        for (var index = startIndex; index < children.Length; index++)
        {
            Unmount(children[index], doRemove);
        }
    }

    private void Unmount(VirtualNode node, bool doRemove)
    {
        // Cleanup order (upstream parity): hooks and child teardown run before node removal.
        InvokeHook(node, null, "onVnodeBeforeUnmount");
        switch (node.Type)
        {
            case VirtualNodeType.Fragment:
                // Children tear down without individual removals; the fragment range removal
                // below takes their nodes out in one anchored walk.
                UnmountChildren(node.ArrayChildren ?? [], doRemove: false);
                if (doRemove)
                {
                    RemoveFragment(node);
                }
                break;
            case VirtualNodeType.Static:
                if (doRemove)
                {
                    RemoveStaticNode(node);
                }
                break;
            default:
                if (node.Type == VirtualNodeType.Element && node.ArrayChildren is not null)
                {
                    // Walk children for their teardown hooks; their platform nodes leave with
                    // this element's single removal.
                    UnmountChildren(node.ArrayChildren, doRemove: false);
                }
                if (doRemove && node.El is not null)
                {
                    _options.Remove((TNode)node.El);
                }
                break;
        }
        QueuePostHook(node, null, "onVnodeUnmounted");
    }

    private void RemoveFragment(VirtualNode node)
    {
        // Remove every node from the start anchor through the end anchor inclusive — exactly
        // the fragment's owned range.
        var currentNode = (TNode)node.El!;
        var endAnchor = (TNode)node.Anchor!;
        while (!NodeComparer.Equals(currentNode, endAnchor))
        {
            var nextNode = _options.NextSibling(currentNode);
            _options.Remove(currentNode);
            currentNode = nextNode!;
        }
        _options.Remove(endAnchor);
    }

    private void RemoveStaticNode(VirtualNode node)
    {
        var currentNode = (TNode)node.El!;
        var endNode = (TNode)node.Anchor!;
        while (!NodeComparer.Equals(currentNode, endNode))
        {
            var nextNode = _options.NextSibling(currentNode);
            _options.Remove(currentNode);
            currentNode = nextNode!;
        }
        _options.Remove(endNode);
    }

    private TNode? GetNextHostNode(VirtualNode node)
    {
        if (node.Type is VirtualNodeType.Fragment or VirtualNodeType.Static)
        {
            return node.Anchor is null ? default : _options.NextSibling((TNode)node.Anchor);
        }
        return node.El is null ? default : _options.NextSibling((TNode)node.El);
    }

    private static string? ChildrenNamespace(VirtualNode node, string? elementNamespace)
        => string.Equals(elementNamespace, "svg", StringComparison.Ordinal)
            && string.Equals(node.ElementTag, "foreignObject", StringComparison.Ordinal)
            ? null
            : elementNamespace;

    private static bool IsReservedProperty(string name)
        => string.Equals(name, "key", StringComparison.Ordinal)
            || string.Equals(name, "ref", StringComparison.Ordinal)
            || name.StartsWith("onVnode", StringComparison.Ordinal);

    private static void InvokeHook(VirtualNode node, VirtualNode? previousNode, string name)
        => node.GetHook(name)?.Invoke(node, previousNode);

    private static void QueuePostHook(VirtualNode node, VirtualNode? previousNode, string name)
    {
        var hook = node.GetHook(name);
        if (hook is not null)
        {
            // Lifecycle hooks fire in the post-flush phase (upstream: queuePostRenderEffect).
            Scheduler.QueuePostFlushCallback(new SchedulerJob(() => hook(node, previousNode)));
        }
    }
}
