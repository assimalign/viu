using System;
using System.Collections.Generic;
using System.Text;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The client-side hydration walk — the C# port of <c>createHydrationFunctions</c> in
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/hydration.ts</c>,
/// https://vuejs.org/guide/scaling-up/ssr.html#client-hydration). Instead of creating platform
/// nodes, the walker <b>adopts</b> the server-rendered DOM: elements are matched by tag and have
/// only their dynamic props reconciled (event listeners always attached), text is adopted with its
/// content asserted, fragments are consumed between their <c>[</c>/<c>]</c> comment anchors,
/// components hydrate their subtree over the existing nodes, and teleported content hydrates at its
/// resolved target. A server/client mismatch never crashes: it logs a recoverable warning and falls
/// back to a client-side render of just the mismatched subtree, so the rest of the tree is still
/// adopted and every listener ends up attached exactly once.
/// <para>
/// The walk drives entirely off a <see cref="HydrationNodeReader{TNode}"/> for reads and the
/// existing <see cref="RendererOptions{TNode}"/> for writes, so it is platform-agnostic; the browser
/// supplies a reader backed by one batched interop snapshot, keeping the walk free of per-node
/// boundary crossings.
/// </para>
/// </summary>
public sealed partial class Renderer<TNode>
{
    private const string AllowMismatchAttribute = "data-allow-mismatch";

    // Armed by the component branch immediately before MountComponent so the component's first render
    // effect (ComponentUpdateFunction) hydrates its subtree against the server node stamped on its
    // vnode.El instead of mounting fresh — the seam upstream's componentUpdateFn reaches through its
    // closed-over hydrateNode. Non-null only during a synchronous hydration pass; single-threaded.
    private HydrationNodeReader<TNode>? _componentHydrationReader;

    /// <summary>
    /// Hydrates <paramref name="node"/> against the existing server-rendered content of
    /// <paramref name="container"/> — the C# port of the root <c>hydrate(vnode, container)</c>
    /// (<c>packages/runtime-core/src/hydration.ts</c>). When the container is empty it falls back to a
    /// full client mount (upstream parity). Pending post-flush callbacks are drained before returning,
    /// so mounted lifecycle hooks fire synchronously with the hydrate call.
    /// </summary>
    /// <param name="node">The client vnode tree to hydrate onto the server DOM.</param>
    /// <param name="container">The container holding the server-rendered markup.</param>
    /// <exception cref="NotSupportedException">The renderer options provide no <c>CreateHydrationReader</c>.</exception>
    internal void Hydrate(VirtualNode node, TNode container)
    {
        if (_options.CreateHydrationReader is null)
        {
            throw new NotSupportedException(
                "This renderer's options do not provide CreateHydrationReader, which hydration requires.");
        }
        var reader = _options.CreateHydrationReader(container);
        var firstChild = reader.FirstChild(container);
        try
        {
            if (TryNode(firstChild, out var start))
            {
                HydrateNode(reader, start, node, parentComponent: null, optimized: false);
            }
            else
            {
                // Empty container: nothing to adopt, so mount fresh (upstream parity).
                RuntimeWarnings.Warn(
                    "Attempting to hydrate existing markup but container is empty. Performing full mount instead.");
                Patch(null, node, container, default, null, null);
            }
        }
        finally
        {
            _componentHydrationReader = null;
        }
        _containerRoots[container] = node;
        Scheduler.FlushAfterSynchronousRender();
    }

    // The C# port of hydrateNode: adopt one server node for one vnode and return the server node that
    // follows the adopted range (what the caller advances its cursor to). A type/structure mismatch
    // routes through HandleMismatch, which recovers by client-rendering just this subtree.
    private TNode? HydrateNode(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        bool optimized)
    {
        optimized = optimized || vnode.DynamicChildren is not null;
        var kind = reader.Kind(node);
        var isFragmentStart = kind == HydrationNodeKind.Comment
            && string.Equals(reader.Data(node), HydrationMarkers.FragmentStart, StringComparison.Ordinal);
        // Adopt the node onto the vnode up front (upstream: vnode.el = node); a mismatch resets it.
        vnode.El = node;
        TNode? nextNode;
        switch (vnode.Type)
        {
            case VirtualNodeType.Text:
                nextNode = HydrateText(reader, node, vnode, parentComponent, kind, isFragmentStart);
                break;
            case VirtualNodeType.Comment:
                if (kind != HydrationNodeKind.Comment || isFragmentStart)
                {
                    nextNode = HandleMismatch(reader, node, vnode, parentComponent, isFragmentStart);
                }
                else
                {
                    nextNode = reader.NextSibling(node);
                }
                break;
            case VirtualNodeType.Static:
                nextNode = HydrateStatic(reader, node, vnode, parentComponent, kind, isFragmentStart);
                break;
            case VirtualNodeType.Fragment:
                nextNode = isFragmentStart
                    ? HydrateFragment(reader, node, vnode, parentComponent, optimized)
                    : HandleMismatch(reader, node, vnode, parentComponent, isFragmentStart);
                break;
            case VirtualNodeType.Element:
                if (kind != HydrationNodeKind.Element
                    || !string.Equals(reader.ElementTag(node), vnode.ElementTag, StringComparison.OrdinalIgnoreCase))
                {
                    nextNode = HandleMismatch(reader, node, vnode, parentComponent, isFragmentStart);
                }
                else
                {
                    nextNode = HydrateElement(reader, node, vnode, parentComponent, optimized);
                }
                break;
            case VirtualNodeType.Component:
                nextNode = HydrateComponent(reader, node, vnode, parentComponent, optimized, isFragmentStart);
                break;
            case VirtualNodeType.Teleport:
                if (kind != HydrationNodeKind.Comment)
                {
                    nextNode = HandleMismatch(reader, node, vnode, parentComponent, isFragmentStart);
                }
                else
                {
                    nextNode = HydrateTeleport(reader, node, vnode, parentComponent, optimized);
                }
                break;
            default:
                throw new InvalidOperationException($"Unknown vnode type: {vnode.Type}.");
        }
        // Template ref is applied after adoption (upstream setRef call at the end of hydrateNode).
        if (vnode.Reference is { } reference && vnode.El is not null)
        {
            SetReference(reference, oldReference: null, vnode, isUnmount: false, parentComponent);
        }
        return nextNode;
    }

    private TNode? HydrateText(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        HydrationNodeKind kind,
        bool isFragmentStart)
    {
        if (kind != HydrationNodeKind.Text)
        {
            // #5728: an empty text vnode has no server node (adjacent to a slot) — create it in place
            // rather than treat the following node as a mismatch (upstream Text case).
            if (string.IsNullOrEmpty(vnode.TextChildren))
            {
                var created = _options.CreateText(string.Empty);
                vnode.El = created;
                if (TryNode(reader.ParentNode(node), out var textParent))
                {
                    _options.Insert(created, textParent, node);
                }
                return node;
            }
            return HandleMismatch(reader, node, vnode, parentComponent, isFragmentStart);
        }
        var serverText = reader.Data(node);
        if (!string.Equals(serverText, vnode.TextChildren, StringComparison.Ordinal))
        {
            if (!IsMismatchAllowed(reader, node, HydrationMismatchType.Text))
            {
                WarnMismatch(
                    reader,
                    node,
                    $"Hydration text mismatch: rendered on server \"{serverText}\", expected on client "
                    + $"\"{vnode.TextChildren}\".");
            }
            // Recover: adopt the node but correct its content to the client value (upstream parity).
            _options.SetText(node, vnode.TextChildren ?? string.Empty);
        }
        return reader.NextSibling(node);
    }

    private TNode? HydrateStatic(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        HydrationNodeKind kind,
        bool isFragmentStart)
    {
        // Upstream Static case: a static vnode adopts a run of top-level server nodes, capturing the
        // first as El and the last as the Anchor. When the markup was emitted inside a fragment the run
        // starts after the [ marker.
        var current = node;
        if (isFragmentStart && TryNode(reader.NextSibling(node), out var afterMarker))
        {
            current = afterMarker;
            kind = reader.Kind(current);
        }
        if (kind is not (HydrationNodeKind.Element or HydrationNodeKind.Text))
        {
            return HandleMismatch(reader, node, vnode, parentComponent, isFragmentStart);
        }
        vnode.El = current;
        // The runtime does not know the static node count the compiler records upstream (staticCount);
        // Viu emits Static content as a single adopted node run bounded by the client vnode, so the
        // adopted node stands as both El and Anchor. Multi-node static hydration arrives with the
        // SSR compiler transforms ([V01.01.07.02]).
        vnode.Anchor = current;
        var next = reader.NextSibling(current);
        return isFragmentStart && TryNode(next, out var afterStatic) ? reader.NextSibling(afterStatic) : next;
    }

    private TNode? HydrateElement(
        HydrationNodeReader<TNode> reader,
        TNode element,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        bool optimized)
    {
        optimized = optimized || vnode.DynamicChildren is not null;
        var properties = vnode.Properties;
        var patchFlag = vnode.PatchFlag;
        var tag = vnode.ElementTag!;
        // input/option force a value patch even when otherwise static (upstream forcePatch).
        var forcePatch = string.Equals(tag, "input", StringComparison.Ordinal)
            || string.Equals(tag, "option", StringComparison.Ordinal);
        var hasDynamicProperties = vnode.DynamicProperties is not null;
        // A CACHED (hoisted / v-once) element with no forced/dynamic props is trusted verbatim: adopt it
        // without inspecting children or props (upstream: the `patchFlag !== CACHED` gate). This is the
        // PatchFlag fast path — no per-attribute work on static subtrees.
        if (!forcePatch && !hasDynamicProperties && patchFlag == PatchFlags.Cached)
        {
            return reader.NextSibling(element);
        }
        InvokeDirectiveHooks(vnode, null, DirectiveHookKind.Created);

        // Children: an array of vnodes is walked; a single text-children element only asserts its text.
        var hasChildOverride = properties is not null
            && (properties.ContainsName("innerHTML") || properties.ContainsName("textContent"));
        if ((vnode.ShapeFlag & ShapeFlags.ArrayChildren) != 0 && !hasChildOverride)
        {
            var leftover = HydrateChildren(
                reader, reader.FirstChild(element), vnode, element, parentComponent, optimized);
            // Excess server nodes the client vdom did not account for: warn once, then remove them so
            // the adopted tree converges (upstream hydrateElement trailing while-loop).
            if (TryNode(leftover, out _) && !IsMismatchAllowed(reader, element, HydrationMismatchType.Children))
            {
                WarnMismatch(
                    reader,
                    element,
                    "Hydration children mismatch: server rendered more child nodes than the client vdom.");
            }
            var excess = leftover;
            while (TryNode(excess, out var excessNode))
            {
                var following = reader.NextSibling(excessNode);
                _options.Remove(excessNode);
                excess = following;
            }
        }
        else if ((vnode.ShapeFlag & ShapeFlags.TextChildren) != 0)
        {
            var clientText = vnode.TextChildren ?? string.Empty;
            var serverText = ReadElementText(reader, element);
            if (!string.Equals(serverText, clientText, StringComparison.Ordinal))
            {
                if (!IsMismatchAllowed(reader, element, HydrationMismatchType.Text))
                {
                    WarnMismatch(
                        reader,
                        element,
                        $"Hydration text content mismatch: rendered on server \"{serverText}\", expected on "
                        + $"client \"{clientText}\".");
                }
                _options.SetElementText(element, clientText);
            }
        }

        // Props: attach listeners and reconcile only the bindings that can differ. The PatchFlag fast
        // path keeps a static element off the per-prop loop — it either patches nothing, or only the
        // click listener (the common interactive case).
        if (properties is not null)
        {
            var elementNamespace = NamespaceForTag(tag);
            var mustIterate = forcePatch
                || hasDynamicProperties
                || !optimized
                || ((int)patchFlag > 0 && (patchFlag & (PatchFlags.FullProps | PatchFlags.NeedHydration)) != 0);
            if (mustIterate)
            {
                foreach (var (name, value) in properties)
                {
                    // Dev-mode mismatch reporting for class/style/attribute bindings (upstream propHasMismatch).
                    ReportPropertyMismatch(reader, element, name, value, vnode);
                    if (ShouldHydrateProperty(name, forcePatch, vnode.DynamicProperties))
                    {
                        _options.PatchProperty(element, tag, name, null, value, elementNamespace);
                    }
                }
            }
            else if (properties.TryGetValue("onClick", out var clickHandler))
            {
                // Fast path (upstream): a static element still needs its click listener attached.
                _options.PatchProperty(element, tag, "onClick", null, clickHandler, elementNamespace);
            }
        }

        InvokeHook(vnode, null, "onVnodeBeforeMount");
        InvokeDirectiveHooks(vnode, null, DirectiveHookKind.BeforeMount);
        QueuePostHook(vnode, null, "onVnodeMounted");
        QueuePostDirectiveHooks(vnode, null, DirectiveHookKind.Mounted);
        return reader.NextSibling(element);
    }

    private TNode? HydrateChildren(
        HydrationNodeReader<TNode> reader,
        TNode? firstNode,
        VirtualNode parentVnode,
        TNode container,
        ComponentInstance? parentComponent,
        bool optimized)
    {
        optimized = optimized || parentVnode.DynamicChildren is not null;
        var children = parentVnode.ArrayChildren ?? [];
        var cursor = firstNode;
        var warnedInsufficient = false;
        for (var index = 0; index < children.Length; index++)
        {
            var child = optimized
                ? children[index]
                : children[index] = VirtualNodeFactory.Normalize(children[index]);
            if (TryNode(cursor, out var node))
            {
                // Consecutive client text vnodes served as one server text node: split it so each vnode
                // adopts its own node (upstream hydrateChildren adjacent-text handling).
                if (!optimized
                    && child.Type == VirtualNodeType.Text
                    && index + 1 < children.Length
                    && VirtualNodeFactory.Normalize(children[index + 1]).Type == VirtualNodeType.Text
                    && reader.Kind(node) == HydrationNodeKind.Text)
                {
                    var serverData = reader.Data(node);
                    var clientText = child.TextChildren ?? string.Empty;
                    if (serverData.Length > clientText.Length)
                    {
                        var split = _options.CreateText(serverData[clientText.Length..]);
                        if (TryNode(reader.NextSibling(node), out var afterNode))
                        {
                            _options.Insert(split, container, afterNode);
                        }
                        else
                        {
                            _options.Insert(split, container, default);
                        }
                        _options.SetText(node, clientText);
                    }
                }
                cursor = HydrateNode(reader, node, child, parentComponent, optimized);
            }
            else if (child.Type == VirtualNodeType.Text && string.IsNullOrEmpty(child.TextChildren))
            {
                // #7215: an empty text vnode with no server node — create it (upstream parity).
                var created = _options.CreateText(string.Empty);
                child.El = created;
                _options.Insert(created, container, default);
            }
            else
            {
                // Too few server nodes: warn once, then client-render the remaining vnodes (upstream).
                if (!warnedInsufficient)
                {
                    warnedInsufficient = true;
                    if (!IsMismatchAllowed(reader, container, HydrationMismatchType.Children))
                    {
                        WarnMismatch(
                            reader,
                            container,
                            "Hydration children mismatch: server rendered fewer child nodes than the client vdom.");
                    }
                }
                Patch(null, child, container, default, NamespaceForTag(parentVnode.ElementTag), parentComponent);
            }
        }
        return cursor;
    }

    private TNode? HydrateFragment(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        bool optimized)
    {
        // The [ marker is the fragment's El; its children hydrate between the marker and the matching ]
        // (upstream hydrateFragment). The parent container is the marker's parent.
        vnode.El = node;
        if (!TryNode(reader.ParentNode(node), out var container))
        {
            return HandleMismatch(reader, node, vnode, parentComponent, isFragment: true);
        }
        var leftover = HydrateChildren(reader, reader.NextSibling(node), vnode, container, parentComponent, optimized);
        if (TryNode(leftover, out var closing)
            && reader.Kind(closing) == HydrationNodeKind.Comment
            && string.Equals(reader.Data(closing), HydrationMarkers.FragmentEnd, StringComparison.Ordinal))
        {
            vnode.Anchor = closing;
            return reader.NextSibling(closing);
        }
        // The server content did not close the fragment where expected: synthesize a closing anchor so
        // later moves/unmounts still bound the fragment's range (upstream inserts a `]` comment).
        var anchor = _options.CreateComment(HydrationMarkers.FragmentEnd);
        vnode.Anchor = anchor;
        if (TryNode(leftover, out var before))
        {
            _options.Insert(anchor, container, before);
        }
        else
        {
            _options.Insert(anchor, container, default);
        }
        return leftover;
    }

    private TNode? HydrateComponent(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        bool optimized,
        bool isFragmentStart)
    {
        // Upstream component case: the subtree hydrates against the current node (mountComponent runs with
        // vnode.el set, so componentUpdateFn adopts instead of mounts). The node that follows the component
        // depends on its root shape — a fragment root spans [ .. ], a teleport root spans its start/end
        // anchors, anything else is a single node.
        if (!TryNode(reader.ParentNode(node), out var container))
        {
            container = node;
        }
        TNode? nextNode;
        if (isFragmentStart)
        {
            nextNode = LocateClosingAnchor(reader, node, HydrationMarkers.FragmentStart, HydrationMarkers.FragmentEnd);
        }
        else if (reader.Kind(node) == HydrationNodeKind.Comment
            && string.Equals(reader.Data(node), HydrationMarkers.TeleportStart, StringComparison.Ordinal))
        {
            nextNode = LocateClosingAnchor(reader, node, HydrationMarkers.TeleportStart, HydrationMarkers.TeleportEnd);
        }
        else
        {
            nextNode = reader.NextSibling(node);
        }
        vnode.El = node;
        // Arm the subtree-hydration bridge, then mount: the component's render effect adopts the existing
        // nodes (see ComponentUpdateFunction). elementNamespace is null here — the subtree elements derive
        // their namespace from their own tags during HydrateElement.
        _componentHydrationReader = reader;
        MountComponent(vnode, container, default, null, parentComponent);
        return nextNode;
    }

    private TNode? HydrateTeleport(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        bool optimized)
    {
        // The C# port of TeleportImpl.hydrate (components/Teleport.ts). The main-tree anchors are the
        // <!--teleport start--> (El) and <!--teleport end--> (Anchor) comments; the children live in the
        // resolved target (enabled) or between the anchors in place (disabled).
        vnode.El = node;
        var disabled = IsTeleportDisabled(vnode);
        var state = new TeleportState();
        vnode.TeleportState = state;
        var target = ResolveTarget(vnode);
        state.Target = target;

        TNode? mainEnd;
        if (disabled)
        {
            // Disabled: the children are in place between the start and end anchors — adopt them, and the
            // node the walk stops at is the <!--teleport end--> anchor.
            mainEnd = TryNode(reader.ParentNode(node), out var container)
                ? HydrateChildren(reader, reader.NextSibling(node), vnode, container, parentComponent, optimized)
                : reader.NextSibling(node);
            vnode.Anchor = Box(mainEnd);
            if (target is TNode inPlaceTarget)
            {
                // The target still carries the anchor comment; record it so a disabled->enabled toggle
                // moves the children in front of it.
                state.TargetAnchor = Box(LocateTeleportAnchor(inPlaceTarget));
            }
        }
        else
        {
            // Enabled: the main tree carries only the adjacent start/end anchor pair; the children live in
            // the resolved target. Snapshot the target subtree (one more batched crossing on the browser —
            // a target lies outside the root's subtree) and adopt the range up to the anchor comment.
            mainEnd = reader.NextSibling(node);
            vnode.Anchor = Box(mainEnd);
            if (target is TNode targetContainer && _options.CreateHydrationReader is not null)
            {
                var targetReader = _options.CreateHydrationReader(targetContainer);
                var targetFirst = targetReader.FirstChild(targetContainer);
                state.TargetStart = Box(targetFirst);
                var leftover = HydrateChildren(targetReader, targetFirst, vnode, targetContainer, parentComponent, optimized);
                state.TargetAnchor = TryNode(leftover, out var anchor)
                    && targetReader.Kind(anchor) == HydrationNodeKind.Comment
                    && string.Equals(targetReader.Data(anchor), HydrationMarkers.TeleportAnchor, StringComparison.Ordinal)
                    ? anchor
                    : null;
            }
        }
        // The walk continues after the main-tree end anchor.
        return TryNode(mainEnd, out var end) ? reader.NextSibling(end) : default;
    }

    // The C# port of handleMismatch: reset the vnode, discard the mismatched server node (and, for a
    // fragment, its whole [ .. ] range), then client-render the vnode where it stood so the tree
    // converges. Returns the server node that follows the discarded range.
    private TNode? HandleMismatch(
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode vnode,
        ComponentInstance? parentComponent,
        bool isFragment)
    {
        // A node-type/structure mismatch is a CHILDREN mismatch of the container, so it is suppressible by
        // data-allow-mismatch on the node (or its nearest element ancestor) — upstream isNodeMismatchAllowed.
        var allowFrom = reader.Kind(node) == HydrationNodeKind.Element
            ? node
            : TryNode(reader.ParentNode(node), out var parentElement) ? parentElement : node;
        if (!IsMismatchAllowed(reader, allowFrom, HydrationMismatchType.Children))
        {
            WarnNodeMismatch(reader, node, vnode);
        }
        vnode.El = null;
        if (isFragment)
        {
            // Remove the excess fragment children between the [ marker and its matching ].
            var end = LocateClosingAnchor(reader, node, HydrationMarkers.FragmentStart, HydrationMarkers.FragmentEnd);
            while (TryNode(reader.NextSibling(node), out var following)
                && !(TryNode(end, out var endNode) && NodeComparer.Equals(following, endNode)))
            {
                _options.Remove(following);
            }
        }
        var next = reader.NextSibling(node);
        if (!TryNode(reader.ParentNode(node), out var container))
        {
            container = node;
        }
        _options.Remove(node);
        var anchor = TryNode(next, out var anchorNode) ? anchorNode : default(TNode?);
        Patch(null, vnode, container, anchor, null, parentComponent);
        // #12063: a mismatch may change the parent component's host element.
        if (parentComponent is not null && ReferenceEquals(parentComponent.Subtree, vnode))
        {
            parentComponent.VirtualNode.El = vnode.El;
        }
        return next;
    }

    // The C# port of locateClosingAnchor: find the matching close comment for an open comment, honoring
    // nesting, and return the node after it.
    private TNode? LocateClosingAnchor(HydrationNodeReader<TNode> reader, TNode node, string open, string close)
    {
        var depth = 0;
        TNode? cursor = node;
        while (TryNode(cursor, out var current))
        {
            cursor = reader.NextSibling(current);
            if (TryNode(cursor, out var candidate) && reader.Kind(candidate) == HydrationNodeKind.Comment)
            {
                var data = reader.Data(candidate);
                if (string.Equals(data, open, StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (string.Equals(data, close, StringComparison.Ordinal))
                {
                    if (depth == 0)
                    {
                        return reader.NextSibling(candidate);
                    }
                    depth--;
                }
            }
        }
        return cursor;
    }

    private static bool ShouldHydrateProperty(string name, bool forcePatch, string[]? dynamicProperties)
    {
        // Upstream hydrateElement prop-qualification: value-like props on input/option (forcePatch), event
        // listeners (always attached), .-prefixed DOM properties, and the compiler's dynamic prop list.
        if (forcePatch
            && (name.EndsWith("value", StringComparison.Ordinal)
                || string.Equals(name, "indeterminate", StringComparison.Ordinal)))
        {
            return true;
        }
        if (VirtualNodeFactory.IsEventListenerName(name) && !IsReservedProperty(name))
        {
            return true;
        }
        if (name.Length > 0 && name[0] == '.')
        {
            return true;
        }
        if (dynamicProperties is not null)
        {
            foreach (var dynamicName in dynamicProperties)
            {
                if (string.Equals(dynamicName, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private string ReadElementText(HydrationNodeReader<TNode> reader, TNode element)
    {
        // The element's text content is the concatenation of its child text nodes. SSR emits a single
        // text child for a text-children element, so this is usually one read; a builder covers the
        // general case without allocating for the common single-child one.
        var child = reader.FirstChild(element);
        if (!TryNode(child, out var first))
        {
            return string.Empty;
        }
        if (reader.Kind(first) == HydrationNodeKind.Text && !TryNode(reader.NextSibling(first), out _))
        {
            return reader.Data(first);
        }
        var builder = new StringBuilder();
        var cursor = child;
        while (TryNode(cursor, out var node))
        {
            if (reader.Kind(node) == HydrationNodeKind.Text)
            {
                builder.Append(reader.Data(node));
            }
            cursor = reader.NextSibling(node);
        }
        return builder.ToString();
    }

    private static string? NamespaceForTag(string? tag)
        => string.Equals(tag, "svg", StringComparison.Ordinal) ? "svg"
            : string.Equals(tag, "math", StringComparison.Ordinal) ? "mathml"
            : null;

    private TNode? LocateTeleportAnchor(TNode target)
    {
        // Best-effort scan for the <!--teleport anchor--> inside a disabled teleport's target using the
        // write-side node-ops (the target is already resolved and its nodes registered). Rarely walked.
        if (_options.CreateHydrationReader is null)
        {
            return default;
        }
        var reader = _options.CreateHydrationReader(target);
        var cursor = reader.FirstChild(target);
        while (TryNode(cursor, out var node))
        {
            if (reader.Kind(node) == HydrationNodeKind.Comment
                && string.Equals(reader.Data(node), HydrationMarkers.TeleportAnchor, StringComparison.Ordinal))
            {
                return node;
            }
            cursor = reader.NextSibling(node);
        }
        return default;
    }

    // --- mismatch reporting + the data-allow-mismatch escape hatch -----------------------------

    private void ReportPropertyMismatch(
        HydrationNodeReader<TNode> reader,
        TNode element,
        string name,
        object? clientValue,
        VirtualNode vnode)
    {
        // A focused port of propHasMismatch for the class/style/attribute bindings the AC names: compare
        // the server-rendered value against the client vnode's and warn (honoring data-allow-mismatch).
        // Directives, event listeners, reserved props, and DOM-property (.-prefixed) bindings never carry
        // an observable attribute mismatch and are skipped.
        if (VirtualNodeFactory.IsEventListenerName(name)
            || IsReservedProperty(name)
            || (name.Length > 0 && name[0] == '.')
            || string.Equals(name, "value", StringComparison.Ordinal)
            || string.Equals(name, "innerHTML", StringComparison.Ordinal)
            || string.Equals(name, "textContent", StringComparison.Ordinal))
        {
            return;
        }
        if (string.Equals(name, "class", StringComparison.Ordinal))
        {
            var server = reader.Attribute(element, "class");
            if (!ClassEquivalent(server, clientValue) && !IsMismatchAllowed(reader, element, HydrationMismatchType.Class))
            {
                WarnMismatch(
                    reader,
                    element,
                    $"Hydration class mismatch: rendered on server \"{server}\", expected on client "
                    + $"\"{clientValue}\".");
            }
            return;
        }
        var mismatchType = string.Equals(name, "style", StringComparison.Ordinal)
            ? HydrationMismatchType.Style
            : HydrationMismatchType.Attribute;
        var serverValue = reader.Attribute(element, name);
        var clientText = clientValue?.ToString();
        // A boolean/absent attribute: a false/null client value with an absent server attribute matches.
        if (clientValue is null or false)
        {
            if (serverValue is not null && !IsMismatchAllowed(reader, element, mismatchType))
            {
                WarnMismatch(reader, element, $"Hydration attribute mismatch on \"{name}\": server rendered \"{serverValue}\", client expected none.");
            }
            return;
        }
        if (!string.Equals(serverValue ?? string.Empty, clientText ?? string.Empty, StringComparison.Ordinal)
            && !IsMismatchAllowed(reader, element, mismatchType))
        {
            WarnMismatch(
                reader,
                element,
                $"Hydration {(mismatchType == HydrationMismatchType.Style ? "style" : "attribute")} mismatch on "
                + $"\"{name}\": rendered on server \"{serverValue}\", expected on client \"{clientText}\".");
        }
    }

    private static bool ClassEquivalent(string? server, object? clientValue)
    {
        var client = clientValue as string;
        if (server is null && string.IsNullOrEmpty(client))
        {
            return true;
        }
        // Compare as unordered token sets so attribute-order differences are not reported as mismatches.
        var serverTokens = Tokenize(server);
        var clientTokens = Tokenize(client);
        if (serverTokens.Count != clientTokens.Count)
        {
            return false;
        }
        foreach (var token in clientTokens)
        {
            if (!serverTokens.Contains(token))
            {
                return false;
            }
        }
        return true;
    }

    private static HashSet<string> Tokenize(string? value)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(value))
        {
            return set;
        }
        foreach (var token in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            set.Add(token);
        }
        return set;
    }

    private bool IsMismatchAllowed(HydrationNodeReader<TNode> reader, TNode element, HydrationMismatchType type)
    {
        // Upstream isMismatchAllowed: text/children mismatches consult the nearest ancestor carrying a
        // data-allow-mismatch attribute; class/style/attribute mismatches consult only the element.
        var target = element;
        if (type is HydrationMismatchType.Text or HydrationMismatchType.Children)
        {
            var found = false;
            var cursor = (TNode?)element;
            while (TryNode(cursor, out var current) && reader.Kind(current) == HydrationNodeKind.Element)
            {
                if (reader.Attribute(current, AllowMismatchAttribute) is not null)
                {
                    target = current;
                    found = true;
                    break;
                }
                cursor = reader.ParentNode(current);
            }
            if (!found)
            {
                return false;
            }
        }
        if (reader.Kind(target) != HydrationNodeKind.Element)
        {
            return false;
        }
        return IsMismatchAllowedByAttribute(reader.Attribute(target, AllowMismatchAttribute), type);
    }

    private static bool IsMismatchAllowedByAttribute(string? attribute, HydrationMismatchType type)
    {
        if (attribute is null)
        {
            return false;
        }
        if (attribute.Length == 0)
        {
            // A bare data-allow-mismatch allows every kind (upstream: attr === '').
            return true;
        }
        var typeToken = MismatchTypeToken(type);
        foreach (var raw in attribute.Split(','))
        {
            var token = raw.Trim();
            if (string.Equals(token, typeToken, StringComparison.Ordinal))
            {
                return true;
            }
            // Allowing "attribute" also allows class and style (they are attributes) — upstream parity.
            if ((type is HydrationMismatchType.Class or HydrationMismatchType.Style)
                && string.Equals(token, "attribute", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string MismatchTypeToken(HydrationMismatchType type) => type switch
    {
        HydrationMismatchType.Text => "text",
        HydrationMismatchType.Children => "children",
        HydrationMismatchType.Class => "class",
        HydrationMismatchType.Style => "style",
        _ => "attribute",
    };

    private void WarnNodeMismatch(HydrationNodeReader<TNode> reader, TNode node, VirtualNode vnode)
    {
        var kind = reader.Kind(node);
        var served = kind switch
        {
            HydrationNodeKind.Text => "a text node",
            HydrationNodeKind.Comment => string.Equals(reader.Data(node), HydrationMarkers.FragmentStart, StringComparison.Ordinal)
                ? "the start of a fragment"
                : "a comment node",
            HydrationNodeKind.Element => $"<{reader.ElementTag(node)}>",
            _ => "an unknown node",
        };
        WarnMismatch(
            reader,
            node,
            $"Hydration node mismatch: rendered on server {served}, expected on client a {DescribeVirtualNode(vnode)}.");
    }

    private void WarnMismatch(HydrationNodeReader<TNode> reader, TNode node, string message)
        => RuntimeWarnings.Warn($"{message} (at {DescribeNodePath(reader, node)})");

    private string DescribeNodePath(HydrationNodeReader<TNode> reader, TNode node)
    {
        // Name the offending node's path from the root: html > body > div — enough for a developer to
        // locate the mismatch (upstream passes the element to warn(); Viu materializes the tag chain).
        var segments = new List<string>();
        var cursor = (TNode?)node;
        var guard = 0;
        while (TryNode(cursor, out var current) && guard++ < 64)
        {
            var kind = reader.Kind(current);
            if (kind == HydrationNodeKind.Element)
            {
                segments.Add(reader.ElementTag(current).ToLowerInvariant());
            }
            else if (segments.Count == 0)
            {
                segments.Add(kind == HydrationNodeKind.Comment ? "#comment" : "#text");
            }
            cursor = reader.ParentNode(current);
        }
        segments.Reverse();
        return segments.Count == 0 ? "root" : string.Join(" > ", segments);
    }

    private static string DescribeVirtualNode(VirtualNode vnode) => vnode.Type switch
    {
        VirtualNodeType.Element => $"<{vnode.ElementTag}>",
        VirtualNodeType.Text => "text node",
        VirtualNodeType.Comment => "comment node",
        VirtualNodeType.Fragment => "fragment",
        VirtualNodeType.Component => "component",
        VirtualNodeType.Teleport => "teleport",
        VirtualNodeType.Static => "static block",
        _ => "node",
    };

    private static bool TryNode(TNode? candidate, out TNode node)
    {
        // Uniform "is this a real node" test across a reference-type TNode (null == none) and a
        // value-type TNode (default == the "no node" sentinel, e.g. the browser's 0 handle).
        if (candidate is not null && !NodeComparer.Equals(candidate, default!))
        {
            node = candidate;
            return true;
        }
        node = default!;
        return false;
    }

    // Boxes a real node for storage on the object-typed VirtualNode.El/Anchor and TeleportState handles,
    // collapsing the "no node" sentinel (null, or a value-type default) to null so those fields stay null
    // when absent (matching how the mount path leaves them).
    private static object? Box(TNode? node) => TryNode(node, out var value) ? value : null;
}
