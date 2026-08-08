using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>Contains the host-neutral hydration walk for <see cref="Renderer{TNode}"/>.</summary>
public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private const string AllowMismatchAttribute = "data-allow-mismatch";

    private Dictionary<TNode, TNode?>? _hydrationTargetCursors;

    /// <summary>
    /// Adopts matching server-rendered host nodes as the mounted representation of an immutable
    /// client tree. A mismatch removes and remounts only the smallest mismatched host range.
    /// </summary>
    /// <param name="value">The non-null client root description.</param>
    /// <param name="container">The host container holding the server-rendered children.</param>
    /// <param name="application">Optional application composition for authored components.</param>
    /// <returns>The root component context, or null when the root is not authored.</returns>
    /// <exception cref="NotSupportedException">
    /// The host did not supply <see cref="RendererOptions{TNode}.CreateHydrationReader"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The container already owns a mounted tree.
    /// </exception>
    /// <remarks>
    /// Structural reads come only from the host reader; Core retains no platform vocabulary.
    /// Specified by <c>[HYD-1]</c> through <c>[HYD-5]</c>.
    /// </remarks>
    public ComponentContext? Hydrate(
        VirtualNode value,
        TNode container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireHostNode(container, nameof(container));
        Func<TNode, HydrationNodeReader<TNode>> createReader =
            _options.CreateHydrationReader
            ?? throw new NotSupportedException(
                "The active host does not provide the hydration-node reader required by Hydrate.");
        if (_containerTrees.ContainsKey(container))
        {
            throw new InvalidOperationException(
                "A container with a mounted tree cannot be hydrated again.");
        }

        Scheduler.FlushPreFlushCallbacks();
        HydrationNodeReader<TNode> reader = createReader(container)
            ?? throw new InvalidOperationException(
                "The host hydration-reader factory returned null.");
        MountedTree<TNode> tree = new()
        {
            Application = application,
        };
        _hydrationTargetCursors = new Dictionary<TNode, TNode?>(NodeComparer);
        try
        {
            TNode? first = reader.FirstChild(container);
            if (HasHostNode(first))
            {
                (MountedNode<TNode> mounted, TNode? next) = HydrateNode(
                    tree,
                    reader,
                    first!,
                    value,
                    container,
                    owner: null);
                tree.Root = mounted;
                RemoveExcessHydrationNodes(
                    tree,
                    reader,
                    next,
                    "Hydration children mismatch: the server rendered extra root nodes.");
            }
            else
            {
                ReportHydrationWarning(
                    tree,
                    "The hydration container was empty; performing a full client mount.");
                tree.Root = Mount(tree, value, container, default, owner: null);
            }

            _containerTrees.Add(container, tree);
        }
        finally
        {
            _hydrationTargetCursors = null;
        }

        QueueHostCommit();
        Scheduler.FlushAfterSynchronousRender();
        return tree.Root is MountedComponent<TNode> component
            ? component.Context
            : null;
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateNode(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        return value switch
        {
            ElementNode element => HydrateElement(
                tree,
                reader,
                node,
                element,
                container,
                owner),
            TextNode text => HydrateText(tree, reader, node, text, container, owner),
            CommentNode comment => HydrateComment(
                tree,
                reader,
                node,
                comment,
                container,
                owner),
            StaticNode staticContent => HydrateStatic(
                tree,
                reader,
                node,
                staticContent,
                container,
                owner),
            FragmentNode fragment => HydrateFragment(
                tree,
                reader,
                node,
                fragment,
                container,
                owner),
            ComponentNode component => HydrateComponent(
                tree,
                reader,
                node,
                component,
                container,
                owner),
            TeleportNode teleport => HydrateTeleport(
                tree,
                reader,
                node,
                teleport,
                container,
                owner),
            KeepAliveNode keepAlive => HydrateMismatch(
                tree,
                reader,
                node,
                keepAlive,
                container,
                owner),
            SuspenseNode => throw new NotSupportedException(
                "Suspense hydration is not implemented; render the boundary on the client."),
            TransitionNode transition => HydrateTransition(
                tree,
                reader,
                node,
                transition,
                container,
                owner),
            _ => throw new InvalidOperationException("Unknown virtual-node variant."),
        };
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateElement(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ElementNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        if (reader.Kind(node) != HydrationNodeKind.Element
            || !string.Equals(
                reader.ElementTag(node),
                value.Name.LocalName,
                StringComparison.OrdinalIgnoreCase))
        {
            return HydrateMismatch(tree, reader, node, value, container, owner);
        }

        List<DirectiveBinding> directiveBindings = ResolveDirectiveBindings(
            tree,
            value.Directives,
            owner);
        InvokeDirectiveHooks(
            tree,
            node,
            directiveBindings,
            value,
            previousValue: null,
            DirectiveHookKind.Created);

        bool hasChildOverride = false;
        for (int index = 0; index < value.Bindings.Count; index++)
        {
            ElementBinding binding = value.Bindings[index];
            if (string.Equals(
                    binding.Name.LocalName,
                    "innerHTML",
                    StringComparison.Ordinal)
                || string.Equals(
                    binding.Name.LocalName,
                    "textContent",
                    StringComparison.Ordinal))
            {
                hasChildOverride = true;
            }

            HydrateBinding(tree, reader, node, binding);
        }

        List<MountedNode<TNode>> children = hasChildOverride
            ? []
            : HydrateChildren(
                tree,
                reader,
                reader.FirstChild(node),
                value.Children,
                node,
                owner,
                closingMarker: null);
        InvokeVirtualNodeLifecycleHook(
            tree,
            owner,
            value,
            previousValue: null,
            "onVnodeBeforeMount");
        InvokeDirectiveHooks(
            tree,
            node,
            directiveBindings,
            value,
            previousValue: null,
            DirectiveHookKind.BeforeMount);
        MountedElement<TNode> mounted = new(
            value,
            node,
            children,
            directiveBindings,
            owner);
        BindDirectiveHostElements(mounted, directiveBindings);
        Register(tree, value, mounted);
        UpdateReference(tree, mounted, null, value.MountReference);
        QueueVirtualNodeLifecycleHook(
            tree,
            owner,
            mounted,
            value,
            previousValue: null,
            "onVnodeMounted");
        QueueDirectiveHooks(
            tree,
            mounted,
            value,
            previousValue: null,
            DirectiveHookKind.Mounted);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateText(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        TextNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        if (reader.Kind(node) != HydrationNodeKind.Text)
        {
            return HydrateMismatch(tree, reader, node, value, container, owner);
        }

        string serverText = reader.Data(node);
        if (!string.Equals(serverText, value.Text, StringComparison.Ordinal))
        {
            if (!IsMismatchAllowed(reader, node, "text"))
            {
                ReportHydrationWarning(
                    tree,
                    $"Hydration text mismatch: the server rendered '{serverText}', "
                        + $"but the client expected '{value.Text}'.");
            }

            _options.SetText(node, value.Text);
        }

        MountedLeaf<TNode> mounted = new(value, node, owner);
        Register(tree, value, mounted);
        UpdateReference(tree, mounted, null, value.MountReference);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateComment(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        CommentNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        if (reader.Kind(node) != HydrationNodeKind.Comment
            || IsStructuralStartMarker(reader.Data(node)))
        {
            return HydrateMismatch(tree, reader, node, value, container, owner);
        }

        MountedLeaf<TNode> mounted = new(value, node, owner);
        Register(tree, value, mounted);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateStatic(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        StaticNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        if (reader.Kind(node) is not (HydrationNodeKind.Element or HydrationNodeKind.Text))
        {
            return HydrateMismatch(tree, reader, node, value, container, owner);
        }

        MountedStatic<TNode> mounted = new(value, node, node, owner);
        Register(tree, value, mounted);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateFragment(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        FragmentNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        if (!IsCommentMarker(reader, node, HydrationMarkers.FragmentStartData))
        {
            return HydrateMismatch(tree, reader, node, value, container, owner);
        }

        TNode fragmentContainer = HasHostNode(reader.ParentNode(node))
            ? reader.ParentNode(node)!
            : container;
        TNode? closing = FindClosingMarker(
            reader,
            node,
            HydrationMarkers.FragmentStartData,
            HydrationMarkers.FragmentEndData);
        List<MountedNode<TNode>> children = HydrateChildren(
            tree,
            reader,
            reader.NextSibling(node),
            value.Children,
            fragmentContainer,
            owner,
            HydrationMarkers.FragmentEndData);
        TNode endAnchor;
        TNode? next;
        if (HasHostNode(closing))
        {
            endAnchor = closing!;
            next = reader.NextSibling(endAnchor);
        }
        else
        {
            endAnchor = _options.CreateComment(HydrationMarkers.FragmentEndData);
            _options.Insert(endAnchor, fragmentContainer, default);
            next = default;
            ReportHydrationWarning(
                tree,
                "Hydration fragment mismatch: the server fragment had no closing marker.");
        }

        MountedRange<TNode> mounted = new(value, node, endAnchor, children, owner);
        Register(tree, value, mounted);
        return (mounted, next);
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateComponent(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ComponentNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        IApplicationContext application = tree.Application
            ?? throw new InvalidOperationException(
                "Hydrating an authored component requires an application context.");
        int componentIdentifier = checked(++_nextComponentIdentifier);
        ApplicationWatchScheduler watchScheduler = new(componentIdentifier);
        ComponentRuntimeOptions runtimeOptions = new(
            application.Components,
            watchScheduler,
            application.Services,
            application.ErrorHandler,
            application.WarnHandler,
            application.EventObserver);
        ComponentActivation activation = ComponentActivation.Activate(
            value,
            runtimeOptions,
            owner,
            watchScheduler,
            suspenseBoundary: _activeSuspenseBoundary);

        MountedComponent<TNode>? mounted = null;
        VirtualNode? initialTree = null;
        ReactiveEffect renderEffect = activation.Scope.Run(
            () => new ReactiveEffect(
                () =>
                {
                    VirtualNode normalized = NormalizeComponentRoot(
                        activation,
                        activation.Render());
                    if (mounted is null)
                    {
                        initialTree = normalized;
                    }
                    else
                    {
                        mounted.PendingTree = normalized;
                    }
                }));

        try
        {
            renderEffect.Run();
            activation.Context.Run(activation.Lifecycle.InvokeBeforeMount);
            (MountedNode<TNode> subtree, TNode? next) = HydrateNode(
                tree,
                reader,
                node,
                initialTree ?? new CommentNode(string.Empty),
                container,
                activation.Context);
            mounted = new MountedComponent<TNode>(value, activation, subtree, owner)
            {
                RenderEffect = renderEffect,
            };
            SchedulerJob renderJob = new(
                () => UpdateMountedComponent(tree, mounted, container, force: false))
            {
                Identifier = componentIdentifier,
                Name = "component render",
                AllowRecurse = true,
            };
            mounted.RenderJob = renderJob;
            renderEffect.Scheduler = () => Scheduler.QueueJob(renderJob);
            Register(tree, value, mounted);
            UpdateReference(tree, mounted, null, value.MountReference);
            QueueMountedLifecycle(mounted);
            mounted.HotReloadRegistration = ComponentHotReload.RegisterMountedComponent(
                activation.Instance.GetType(),
                change =>
                {
                    if (change is ComponentHotReloadChangeKind.Template
                        or ComponentHotReloadChangeKind.ScriptReset)
                    {
                        QueueComponentRemount(tree, mounted, container);
                    }
                });
            return (mounted, next);
        }
        catch
        {
            renderEffect.Stop();
            activation.Release();
            throw;
        }
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateTeleport(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        TeleportNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        if (!IsCommentMarker(reader, node, HydrationMarkers.TeleportStartData))
        {
            return HydrateMismatch(tree, reader, node, value, container, owner);
        }

        TNode originContainer = HasHostNode(reader.ParentNode(node))
            ? reader.ParentNode(node)!
            : container;
        TNode? closing = FindClosingMarker(
            reader,
            node,
            HydrationMarkers.TeleportStartData,
            HydrationMarkers.TeleportEndData);
        TNode endAnchor;
        TNode? next;
        if (HasHostNode(closing))
        {
            endAnchor = closing!;
            next = reader.NextSibling(endAnchor);
        }
        else
        {
            endAnchor = _options.CreateComment(HydrationMarkers.TeleportEndData);
            _options.Insert(endAnchor, originContainer, default);
            next = default;
            ReportHydrationWarning(
                tree,
                "Hydration teleport mismatch: the server range had no closing marker.");
        }

        List<MountedNode<TNode>> children;
        if (value.IsDisabled)
        {
            children = HydrateChildren(
                tree,
                reader,
                reader.NextSibling(node),
                value.Children,
                originContainer,
                owner,
                HydrationMarkers.TeleportEndData);
        }
        else
        {
            _ = HydrateChildren(
                tree,
                reader,
                reader.NextSibling(node),
                Array.Empty<VirtualNode>(),
                originContainer,
                owner,
                HydrationMarkers.TeleportEndData);
            children = [];
        }

        TNode? targetContainer = default;
        TNode? targetAnchor = default;
        bool hasTarget = false;
        bool childrenMounted = value.IsDisabled;
        Func<string, TNode?>? resolveTarget = _options.ResolveTeleportTarget;
        TNode? resolvedTarget = resolveTarget is null
            ? default
            : resolveTarget(value.TargetIdentifier);
        if (HasHostNode(resolvedTarget))
        {
            TNode target = resolvedTarget!;
            targetContainer = target;
            hasTarget = true;
            HydrationNodeReader<TNode> targetReader = _options.CreateHydrationReader!(target)
                ?? throw new InvalidOperationException(
                    "The host hydration-reader factory returned null for a teleport target.");
            TNode? cursor = GetHydrationTargetCursor(targetReader, target);
            targetAnchor = FindFirstMarker(
                targetReader,
                cursor,
                HydrationMarkers.TeleportAnchorData);
            if (!HasHostNode(targetAnchor))
            {
                targetAnchor = _options.CreateComment(HydrationMarkers.TeleportAnchorData);
                _options.Insert(targetAnchor, target, default);
                ReportHydrationWarning(
                    tree,
                    "Hydration teleport mismatch: the target had no anchor marker.");
            }

            if (!value.IsDisabled)
            {
                children = HydrateChildren(
                    tree,
                    targetReader,
                    cursor,
                    value.Children,
                    target,
                    owner,
                    HydrationMarkers.TeleportAnchorData);
                childrenMounted = true;
            }

            SetHydrationTargetCursor(target, targetReader.NextSibling(targetAnchor!));
        }

        MountedTeleport<TNode> mounted = new(
            value,
            node,
            endAnchor,
            targetContainer,
            targetAnchor,
            hasTarget,
            children,
            owner)
        {
            ChildrenMounted = childrenMounted,
        };
        Register(tree, value, mounted);
        return (mounted, next);
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateTransition(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        TransitionNode value,
        TNode container,
        RuntimeComponentContext? owner)
    {
        VirtualNode childValue = EvaluateSlot(value.Invocation, "default", owner)
            ?? new CommentNode(string.Empty);
        (MountedNode<TNode> child, TNode? next) = HydrateNode(
            tree,
            reader,
            node,
            childValue,
            container,
            owner);
        MountedTransition<TNode> mounted = new(value, child, owner)
        {
            State = TransitionExecutionState.Entered,
        };
        Register(tree, value, mounted);
        return (mounted, next);
    }

    private List<MountedNode<TNode>> HydrateChildren(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode? first,
        IReadOnlyList<VirtualNode> values,
        TNode container,
        RuntimeComponentContext? owner,
        string? closingMarker)
    {
        List<MountedNode<TNode>> mounted = new(values.Count);
        TNode? cursor = first;
        for (int index = 0; index < values.Count; index++)
        {
            if (!HasHostNode(cursor)
                || (closingMarker is not null
                    && IsCommentMarker(reader, cursor!, closingMarker)))
            {
                mounted.Add(
                    Mount(
                        tree,
                        values[index],
                        container,
                        HasHostNode(cursor) ? cursor : default,
                        owner));
                continue;
            }

            if (values[index] is TextNode
                && reader.Kind(cursor!) == HydrationNodeKind.Text
                && index + 1 < values.Count
                && values[index + 1] is TextNode)
            {
                index = HydrateAdjacentTextRun(
                    tree,
                    reader,
                    cursor!,
                    container,
                    values,
                    index,
                    owner,
                    mounted,
                    out cursor);
                continue;
            }

            (MountedNode<TNode> child, TNode? next) = HydrateNode(
                tree,
                reader,
                cursor!,
                values[index],
                container,
                owner);
            mounted.Add(child);
            cursor = next;
        }

        List<TNode> excess = [];
        while (HasHostNode(cursor)
            && (closingMarker is null
                || !IsCommentMarker(reader, cursor!, closingMarker)))
        {
            excess.Add(cursor!);
            cursor = reader.NextSibling(cursor!);
        }

        if (excess.Count > 0)
        {
            if (!IsMismatchAllowed(reader, container, "children"))
            {
                ReportHydrationWarning(
                    tree,
                    "Hydration children mismatch: the server rendered more child nodes "
                        + "than the client tree.");
            }

            for (int index = 0; index < excess.Count; index++)
            {
                _options.Remove(excess[index]);
            }
        }

        return mounted;
    }

    private int HydrateAdjacentTextRun(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        TNode container,
        IReadOnlyList<VirtualNode> values,
        int startIndex,
        RuntimeComponentContext? owner,
        List<MountedNode<TNode>> mounted,
        out TNode? cursor)
    {
        TNode? afterRun = reader.NextSibling(node);
        TNode currentNode = node;
        string currentData = reader.Data(node);
        int index = startIndex;
        while (true)
        {
            TextNode value = (TextNode)values[index];
            bool hasMoreText = index + 1 < values.Count
                && values[index + 1] is TextNode;
            if (hasMoreText && currentData.Length > value.Text.Length)
            {
                string remainingText = currentData[value.Text.Length..];
                TNode overflow = _options.CreateText(remainingText);
                _options.Insert(
                    overflow,
                    container,
                    HasHostNode(afterRun) ? afterRun : default);
                if (!string.Equals(currentData, value.Text, StringComparison.Ordinal))
                {
                    _options.SetText(currentNode, value.Text);
                }

                MountedLeaf<TNode> split = new(value, currentNode, owner);
                mounted.Add(split);
                Register(tree, value, split);
                UpdateReference(tree, split, null, value.MountReference);
                currentNode = overflow;
                currentData = remainingText;
                index++;
                continue;
            }

            if (!string.Equals(currentData, value.Text, StringComparison.Ordinal))
            {
                _options.SetText(currentNode, value.Text);
            }

            MountedLeaf<TNode> last = new(value, currentNode, owner);
            mounted.Add(last);
            Register(tree, value, last);
            UpdateReference(tree, last, null, value.MountReference);
            break;
        }

        cursor = afterRun;
        return index;
    }

    private (MountedNode<TNode> Mounted, TNode? Next) HydrateMismatch(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        VirtualNode value,
        TNode fallbackContainer,
        RuntimeComponentContext? owner)
    {
        if (!IsMismatchAllowed(reader, node, "children"))
        {
            ReportHydrationWarning(
                tree,
                $"Hydration node mismatch: the server host node cannot represent "
                    + $"client node kind {value.Kind}.");
        }

        List<TNode> removalRange = ReadMismatchRange(reader, node);
        TNode? next = reader.NextSibling(removalRange[^1]);
        TNode? parent = reader.ParentNode(node);
        TNode replacementContainer = HasHostNode(parent)
            ? parent!
            : fallbackContainer;
        for (int index = 0; index < removalRange.Count; index++)
        {
            _options.Remove(removalRange[index]);
        }

        MountedNode<TNode> mounted = Mount(
            tree,
            value,
            replacementContainer,
            HasHostNode(next) ? next : default,
            owner);
        return (mounted, next);
    }

    private static List<TNode> ReadMismatchRange(
        HydrationNodeReader<TNode> reader,
        TNode first)
    {
        List<TNode> nodes = [first];
        if (reader.Kind(first) != HydrationNodeKind.Comment)
        {
            return nodes;
        }

        string start = reader.Data(first);
        string? end = start switch
        {
            HydrationMarkers.FragmentStartData => HydrationMarkers.FragmentEndData,
            HydrationMarkers.TeleportStartData => HydrationMarkers.TeleportEndData,
            _ => null,
        };
        if (end is null)
        {
            return nodes;
        }

        int depth = 0;
        TNode? cursor = reader.NextSibling(first);
        while (HasHostNode(cursor))
        {
            TNode current = cursor!;
            nodes.Add(current);
            if (reader.Kind(current) == HydrationNodeKind.Comment)
            {
                string data = reader.Data(current);
                if (string.Equals(data, start, StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (string.Equals(data, end, StringComparison.Ordinal))
                {
                    if (depth == 0)
                    {
                        break;
                    }

                    depth--;
                }
            }

            cursor = reader.NextSibling(current);
        }

        return nodes;
    }

    private void HydrateBinding(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ElementBinding binding)
    {
        if (IsNodeLifecycleBinding(binding))
        {
            return;
        }

        if (binding.Kind is ElementBindingKind.Event or ElementBindingKind.Property)
        {
            _options.PatchAttribute(node, null, binding);
            return;
        }

        if (binding.Value is null or bool)
        {
            return;
        }

        string name = binding.Name.ToString();
        string expected = binding.Value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
            : binding.Value.ToString() ?? string.Empty;
        string? actual = reader.Attribute(node, name);
        bool equivalent = string.Equals(
            binding.Name.LocalName,
            "class",
            StringComparison.OrdinalIgnoreCase)
                ? ClassValuesEquivalent(actual, expected)
                : string.Equals(
                    binding.Name.LocalName,
                    "style",
                    StringComparison.OrdinalIgnoreCase)
                    ? StyleValuesEquivalent(actual, expected)
                    : string.Equals(actual, expected, StringComparison.Ordinal);
        string category = string.Equals(
            binding.Name.LocalName,
            "class",
            StringComparison.OrdinalIgnoreCase)
                ? "class"
                : string.Equals(
                    binding.Name.LocalName,
                    "style",
                    StringComparison.OrdinalIgnoreCase)
                    ? "style"
                    : "attribute";
        if (!equivalent && !IsMismatchAllowed(reader, node, category))
        {
            ReportHydrationWarning(
                tree,
                $"Hydration {category} mismatch for '{name}': the server rendered "
                    + $"'{actual}', but the client expected '{expected}'.");
        }
    }

    private static bool ClassValuesEquivalent(string? serverValue, string clientValue)
    {
        HashSet<string> server = TokenizeClass(serverValue);
        HashSet<string> client = TokenizeClass(clientValue);
        return server.SetEquals(client);
    }

    private static HashSet<string> TokenizeClass(string? value)
    {
        HashSet<string> tokens = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
        {
            return tokens;
        }

        string[] values = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < values.Length; index++)
        {
            tokens.Add(values[index]);
        }

        return tokens;
    }

    private static bool StyleValuesEquivalent(string? serverValue, string clientValue)
    {
        Dictionary<string, string> server = ParseStyle(serverValue);
        Dictionary<string, string> client = ParseStyle(clientValue);
        if (server.Count != client.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> declaration in client)
        {
            if (!server.TryGetValue(declaration.Key, out string? actual)
                || !string.Equals(actual, declaration.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> ParseStyle(string? value)
    {
        Dictionary<string, string> declarations = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return declarations;
        }

        string[] segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < segments.Length; index++)
        {
            int separator = segments[index].IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            string name = segments[index][..separator].Trim();
            string declarationValue = segments[index][(separator + 1)..].Trim();
            declarations[name] = declarationValue;
        }

        return declarations;
    }

    private static bool IsMismatchAllowed(
        HydrationNodeReader<TNode> reader,
        TNode node,
        string category)
    {
        TNode? cursor = node;
        while (HasHostNode(cursor))
        {
            TNode current = cursor!;
            if (reader.Kind(current) == HydrationNodeKind.Element)
            {
                string? value = reader.Attribute(current, AllowMismatchAttribute);
                if (value is not null)
                {
                    if (value.Length == 0)
                    {
                        return true;
                    }

                    string[] categories = value.Split(
                        [',', ' ', '\t', '\r', '\n'],
                        StringSplitOptions.RemoveEmptyEntries);
                    for (int index = 0; index < categories.Length; index++)
                    {
                        if (string.Equals(
                                categories[index],
                                category,
                                StringComparison.OrdinalIgnoreCase)
                            || (string.Equals(
                                    category,
                                    "text",
                                    StringComparison.OrdinalIgnoreCase)
                                && string.Equals(
                                    categories[index],
                                    "children",
                                    StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            cursor = reader.ParentNode(current);
        }

        return false;
    }

    private static bool IsStructuralStartMarker(string data) =>
        string.Equals(
            data,
            HydrationMarkers.FragmentStartData,
            StringComparison.Ordinal)
        || string.Equals(
            data,
            HydrationMarkers.TeleportStartData,
            StringComparison.Ordinal);

    private static bool IsCommentMarker(
        HydrationNodeReader<TNode> reader,
        TNode node,
        string marker) =>
        reader.Kind(node) == HydrationNodeKind.Comment
        && string.Equals(reader.Data(node), marker, StringComparison.Ordinal);

    private static TNode? FindFirstMarker(
        HydrationNodeReader<TNode> reader,
        TNode? first,
        string marker)
    {
        TNode? cursor = first;
        while (HasHostNode(cursor))
        {
            if (IsCommentMarker(reader, cursor!, marker))
            {
                return cursor;
            }

            cursor = reader.NextSibling(cursor!);
        }

        return default;
    }

    private static TNode? FindClosingMarker(
        HydrationNodeReader<TNode> reader,
        TNode start,
        string openingMarker,
        string closingMarker)
    {
        int depth = 0;
        TNode? cursor = reader.NextSibling(start);
        while (HasHostNode(cursor))
        {
            TNode current = cursor!;
            if (reader.Kind(current) == HydrationNodeKind.Comment)
            {
                string data = reader.Data(current);
                if (string.Equals(data, openingMarker, StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (string.Equals(data, closingMarker, StringComparison.Ordinal))
                {
                    if (depth == 0)
                    {
                        return current;
                    }

                    depth--;
                }
            }

            cursor = reader.NextSibling(current);
        }

        return default;
    }

    private TNode? GetHydrationTargetCursor(
        HydrationNodeReader<TNode> reader,
        TNode target)
    {
        if (_hydrationTargetCursors is not null
            && _hydrationTargetCursors.TryGetValue(target, out TNode? cursor))
        {
            return cursor;
        }

        return reader.FirstChild(target);
    }

    private void SetHydrationTargetCursor(TNode target, TNode? cursor)
    {
        _hydrationTargetCursors?[target] = cursor;
    }

    private void RemoveExcessHydrationNodes(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode? first,
        string warning)
    {
        if (!HasHostNode(first))
        {
            return;
        }

        List<TNode> excess = [];
        TNode? cursor = first;
        while (HasHostNode(cursor))
        {
            excess.Add(cursor!);
            cursor = reader.NextSibling(cursor!);
        }

        ReportHydrationWarning(tree, warning);
        for (int index = 0; index < excess.Count; index++)
        {
            _options.Remove(excess[index]);
        }
    }

    private static void ReportHydrationWarning(MountedTree<TNode> tree, string message)
    {
        tree.Application?.WarnHandler?.Invoke(message);
    }
}
