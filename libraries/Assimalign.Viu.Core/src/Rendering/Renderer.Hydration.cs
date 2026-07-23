using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// Contains the host-neutral hydration walk for <see cref="Renderer{TNode}"/>.
/// </summary>
public sealed partial class Renderer<TNode>
{
    private const string AllowMismatchAttribute = "data-allow-mismatch";
    private const string FragmentStartMarker = "[";
    private const string FragmentEndMarker = "]";
    private const string TeleportStartMarker = "teleport start";
    private const string TeleportEndMarker = "teleport end";
    private const string TeleportTargetMarker = "teleport anchor";

    private Dictionary<TNode, TNode?>? _hydrationTargetCursors;

    /// <summary>
    /// Adopts server-rendered host nodes as the mounted representation of a component tree.
    /// </summary>
    /// <remarks>
    /// Matching nodes are retained, interactive bindings and directive hooks are attached, and
    /// later reactive updates patch the adopted nodes. A structural mismatch is recovered at the
    /// smallest subtree by removing that server range and mounting the client component in its
    /// place. This follows Vue 3.5's <c>createHydrationFunctions</c> contract:
    /// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/hydration.ts.
    /// </remarks>
    /// <param name="component">The client component tree.</param>
    /// <param name="container">The host container holding server-rendered children.</param>
    /// <param name="application">
    /// The application composition context required by templates and directives.
    /// </param>
    /// <returns>The root template context, or null when the root is not a template.</returns>
    /// <exception cref="NotSupportedException">
    /// The host did not supply <see cref="RendererOptions{TNode}.CreateHydrationReader"/>.
    /// </exception>
    public IComponentContext? Hydrate(
        IComponent component,
        TNode container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(container);
        Func<TNode, HydrationNodeReader<TNode>> createReader =
            _options.CreateHydrationReader
            ?? throw new NotSupportedException(
                "This host does not provide CreateHydrationReader, which hydration requires.");
        if (_containerTrees.ContainsKey(container))
        {
            throw new InvalidOperationException(
                "A container with a mounted component tree cannot be hydrated again.");
        }

        MountedTree<TNode> tree = new()
        {
            Application = application,
        };
        HydrationNodeReader<TNode> reader = createReader(container);
        TNode? firstChild = reader.FirstChild(container);
        _hydrationTargetCursors = new Dictionary<TNode, TNode?>(NodeComparer);
        try
        {
            if (HasHostNode(firstChild))
            {
                (MountedRenderNode<TNode> mounted, TNode? next) = HydrateNode(
                    tree,
                    reader,
                    firstChild!,
                    component,
                    container,
                    elementNamespace: null,
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
                Warn(
                    tree,
                    "Attempting to hydrate existing markup, but the container is empty. "
                    + "Performing a full client mount.");
                tree.Root = Mount(
                    tree,
                    component,
                    container,
                    default,
                    elementNamespace: null,
                    owner: null);
            }

            _containerTrees.Add(container, tree);
        }
        finally
        {
            _hydrationTargetCursors = null;
        }

        QueueHostCommit();
        Scheduler.FlushAfterSynchronousRender();
        return tree.Root is MountedTemplateNode<TNode> template
            ? template.Instance.Context
            : null;
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateNode(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IComponent component,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner)
    {
        return component.Kind switch
        {
            ComponentKind.Element => HydrateElement(
                tree,
                reader,
                node,
                RequireElement(component),
                container,
                elementNamespace,
                owner),
            ComponentKind.Text => HydrateText(
                tree,
                reader,
                node,
                RequireText(component),
                container,
                owner),
            ComponentKind.Comment => HydrateComment(
                tree,
                reader,
                node,
                RequireComment(component),
                container,
                owner),
            ComponentKind.Static => HydrateStatic(
                tree,
                reader,
                node,
                RequireStatic(component),
                container,
                elementNamespace,
                owner),
            ComponentKind.Fragment => HydrateFragment(
                tree,
                reader,
                node,
                RequireFragment(component),
                container,
                elementNamespace,
                owner),
            ComponentKind.Template => HydrateTemplate(
                tree,
                reader,
                node,
                RequireTemplate(component),
                container,
                elementNamespace,
                owner),
            ComponentKind.Teleport => HydrateTeleport(
                tree,
                reader,
                node,
                RequireTeleport(component),
                container,
                elementNamespace,
                owner),
            _ => throw new InvalidOperationException(
                $"Unknown component kind: {component.Kind}."),
        };
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateText(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ITextComponent component,
        TNode container,
        ComponentContext? owner)
    {
        if (reader.Kind(node) != HydrationNodeKind.Text)
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace: null,
                owner);
        }

        string serverText = reader.Data(node);
        if (!string.Equals(serverText, component.Text, StringComparison.Ordinal))
        {
            if (!IsMismatchAllowed(reader, node, "text"))
            {
                Warn(
                    tree,
                    $"Hydration text mismatch: the server rendered \"{serverText}\", "
                    + $"but the client expected \"{component.Text}\".");
            }

            _options.SetText(node, component.Text);
        }

        MountedLeafNode<TNode> mounted = new(component, node, owner);
        Register(tree, component, mounted);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateComment(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ICommentComponent component,
        TNode container,
        ComponentContext? owner)
    {
        if (reader.Kind(node) != HydrationNodeKind.Comment
            || IsStructuralStartMarker(reader.Data(node)))
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace: null,
                owner);
        }

        MountedLeafNode<TNode> mounted = new(component, node, owner);
        Register(tree, component, mounted);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateStatic(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IStaticComponent component,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner)
    {
        HydrationNodeKind kind = reader.Kind(node);
        if (kind is not (HydrationNodeKind.Element or HydrationNodeKind.Text))
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace,
                owner);
        }

        MountedStaticNode<TNode> mounted = new(component, node, node, owner);
        Register(tree, component, mounted);
        return (mounted, reader.NextSibling(node));
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateElement(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IElementComponent component,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner)
    {
        if (reader.Kind(node) != HydrationNodeKind.Element
            || !string.Equals(
                reader.ElementTag(node),
                component.Tag,
                StringComparison.OrdinalIgnoreCase))
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace,
                owner);
        }

        string? ownNamespace = ElementNamespace(component.Tag, elementNamespace);
        bool forceValue = string.Equals(
                component.Tag,
                "input",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                component.Tag,
                "option",
                StringComparison.OrdinalIgnoreCase);
        if (!forceValue
            && component.Optimization.DynamicProperties is null
            && component.Optimization.PatchFlags == PatchFlags.Cached)
        {
            MountedElementNode<TNode> cached = new(
                component,
                node,
                [],
                [],
                owner);
            cached.Transition = TransitionComponents.Get(component);
            Register(tree, component, cached);
            UpdateReference(
                tree,
                cached,
                previousReference: null,
                component.Reference,
                node);
            return (cached, reader.NextSibling(node));
        }

        List<DirectiveBinding> directiveBindings = ResolveDirectiveBindings(
            tree,
            component.Directives,
            owner);
        TransitionHooks? transition = TransitionComponents.Get(component);
        BindDirectiveTransitions(directiveBindings, transition);
        InvokeDirectiveHooks(
            tree,
            node,
            directiveBindings,
            component,
            previousComponent: null,
            DirectiveHookKind.Created);

        bool hasChildOverride =
            component.Attributes.TryGetValue("innerHTML", out _)
            || component.Attributes.TryGetValue("textContent", out _);
        List<MountedRenderNode<TNode>> children = hasChildOverride
            ? []
            : HydrateChildren(
                tree,
                reader,
                reader.FirstChild(node),
                component.Children,
                node,
                ChildrenNamespace(component.Tag, ownNamespace),
                owner,
                closingMarker: null);
        HydrateAttributes(tree, reader, node, component, ownNamespace);
        InvokeComponentNodeLifecycleHook(
            tree,
            owner,
            component,
            previousComponent: null,
            "onVnodeBeforeMount");
        InvokeDirectiveHooks(
            tree,
            node,
            directiveBindings,
            component,
            previousComponent: null,
            DirectiveHookKind.BeforeMount);

        MountedElementNode<TNode> mounted = new(
            component,
            node,
            children,
            directiveBindings,
            owner);
        mounted.Transition = transition;
        BindDirectiveHostElements(mounted, directiveBindings);
        Register(tree, component, mounted);
        UpdateReference(
            tree,
            mounted,
            previousReference: null,
            component.Reference,
            node);
        QueueComponentNodeLifecycleHook(
            tree,
            owner,
            mounted,
            component,
            previousComponent: null,
            "onVnodeMounted");
        if (directiveBindings.Count > 0)
        {
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(
                    () =>
                    {
                        if (!mounted.IsUnmounted)
                        {
                            InvokeDirectiveHooks(
                                tree,
                                node,
                                mounted.DirectiveBindings,
                                RequireElement(mounted.Component),
                                previousComponent: null,
                                DirectiveHookKind.Mounted);
                            QueueHostCommit();
                        }
                    })
                {
                    Name = "hydrated directive mounted lifecycle",
                });
        }

        return (mounted, reader.NextSibling(node));
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateFragment(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IFragmentComponent component,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner)
    {
        if (!IsCommentMarker(reader, node, FragmentStartMarker))
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace,
                owner);
        }

        TNode fragmentContainer = HasHostNode(reader.ParentNode(node))
            ? reader.ParentNode(node)!
            : container;
        TNode? closing = FindClosingMarker(
            reader,
            node,
            FragmentStartMarker,
            FragmentEndMarker);
        List<MountedRenderNode<TNode>> children = HydrateChildren(
            tree,
            reader,
            reader.NextSibling(node),
            component.Children,
            fragmentContainer,
            elementNamespace,
            owner,
            FragmentEndMarker);
        TNode endAnchor;
        TNode? next;
        if (HasHostNode(closing))
        {
            endAnchor = closing!;
            next = reader.NextSibling(endAnchor);
        }
        else
        {
            endAnchor = _options.CreateComment(FragmentEndMarker);
            _options.Insert(endAnchor, fragmentContainer, default);
            next = default;
            Warn(
                tree,
                "Hydration fragment mismatch: the server fragment had no closing anchor.");
        }

        MountedFragmentNode<TNode> mounted = new(
            component,
            node,
            endAnchor,
            children,
            owner);
        Register(tree, component, mounted);
        return (mounted, next);
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateTemplate(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ITemplateComponent component,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner)
    {
        if (IsSuspenseComponent(component))
        {
            throw new NotSupportedException(
                "Suspense hydration is not implemented. Render the boundary on the client until "
                + "pending-branch hydration can coordinate with server output.");
        }

        IApplicationContext application = tree.Application
            ?? throw new InvalidOperationException(
                "Template components require an application context. Supply it to Hydrate.");
        int identifier = checked(++_nextComponentIdentifier);
        MountedComponent instance = MountedComponent.Create(
            application,
            component,
            owner,
            identifier);
        TransitionHooks? initialTransition =
            TransitionComponents.Get(component);
        MountedKeepAliveState<TNode>? keepAliveState =
            CreateKeepAliveState(instance);
        MountedRenderNode<TNode>? subtree = null;
        MountedTemplateNode<TNode>? mounted = null;
        ReactiveEffect? renderEffect = null;
        SchedulerJob? renderJob = null;
        TNode? initialNext = default;
        SchedulerJob mountedJob = new(instance.InvokeMounted)
        {
            Name = "hydrated component mounted lifecycle",
        };
        SchedulerJob updatedJob = new(instance.InvokeUpdated)
        {
            Name = "component updated lifecycle",
        };

        try
        {
            IComponent RenderSubtree()
            {
                IComponent rendered = instance.Render();
                TransitionHooks? transition =
                    mounted?.Transition ?? initialTransition;
                return transition is null
                    ? rendered
                    : TransitionComponents.Attach(rendered, transition);
            }

            void RenderComponent()
            {
                if (subtree is null)
                {
                    instance.InvokeBeforeMount();
                    InvokeComponentNodeLifecycleHook(
                        tree,
                        owner,
                        component,
                        previousComponent: null,
                        "onVnodeBeforeMount");
                    IComponent initialRendered = RenderSubtree();
                    (subtree, initialNext) = HydrateNode(
                        tree,
                        reader,
                        node,
                        initialRendered,
                        container,
                        elementNamespace,
                        instance.Context);
                    if (keepAliveState is not null)
                    {
                        InitializeKeepAlive(
                            tree,
                            keepAliveState,
                            instance,
                            subtree);
                    }

                    QueueHostCommit();
                    return;
                }

                instance.InvokeBeforeUpdate();
                InvokePendingTemplateNodeBeforeUpdateHook(
                    tree,
                    mounted);
                TNode fallbackContainer = mounted is null
                    ? container
                    : mounted.FallbackContainer;
                TNode updateContainer = HostParentOrFallback(
                    subtree.FirstHostNode,
                    fallbackContainer);
                TNode? updateAnchor = GetNextHostNode(subtree);
                IComponent rendered = RenderSubtree();
                subtree = keepAliveState is null
                    ? Patch(
                        tree,
                        subtree,
                        rendered,
                        updateContainer,
                        updateAnchor,
                        mounted?.ElementNamespace ?? elementNamespace,
                        instance.Context)
                    : PatchKeepAlive(
                        tree,
                        keepAliveState,
                        instance,
                        subtree,
                        rendered,
                        updateContainer,
                        updateAnchor,
                        mounted?.ElementNamespace ?? elementNamespace);
                if (mounted is not null)
                {
                    mounted.Subtree = subtree;
                }

                QueueHostCommit();
                Scheduler.QueuePostFlushCallback(updatedJob);
            }

            renderEffect = instance.CreateRenderEffect(
                RenderComponent,
                () => Scheduler.QueueJob(renderJob!));
            renderJob = new SchedulerJob(renderEffect.RunIfDirty)
            {
                Identifier = identifier,
                Name = "hydrated component render",
            };
            renderEffect.Run();

            mounted = new MountedTemplateNode<TNode>(
                component,
                instance,
                subtree!,
                renderEffect,
                renderJob,
                mountedJob,
                updatedJob,
                container,
                elementNamespace,
                owner);
            mounted.Transition = initialTransition;
            mounted.KeepAliveState = keepAliveState;
            instance.Context.RootElementResolver =
                () => GetRootElementObjects(mounted.Subtree);
            instance.Context.KeyedChildElementResolver =
                () => GetKeyedChildElementSnapshots(mounted.Subtree);
            instance.Context.HostCommitScheduler = QueueHostCommit;
            Register(tree, component, mounted);
            UpdateReference(
                tree,
                mounted,
                previousReference: null,
                component.Reference,
                ComponentReferenceValue(instance.Context));
            Scheduler.QueuePostFlushCallback(mountedJob);
            QueueComponentNodeLifecycleHook(
                tree,
                owner,
                mounted,
                component,
                previousComponent: null,
                "onVnodeMounted");
            return (mounted, initialNext);
        }
        catch
        {
            renderJob?.IsDisposed = true;
            mountedJob.IsDisposed = true;
            updatedJob.IsDisposed = true;
            instance.AbortMount(
                subtree is null
                    ? null
                    : () => Unmount(tree, subtree, removeHostNodes: true));
            if (keepAliveState is not null)
            {
                _options.Remove(keepAliveState.StorageContainer);
            }

            throw;
        }
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateTeleport(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        ITeleportComponent component,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner)
    {
        if (!IsCommentMarker(reader, node, TeleportStartMarker))
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace,
                owner);
        }

        TNode? mainEnd = FindClosingMarker(
            reader,
            node,
            TeleportStartMarker,
            TeleportEndMarker);
        if (!HasHostNode(mainEnd))
        {
            return HydrateMismatch(
                tree,
                reader,
                node,
                component,
                container,
                elementNamespace,
                owner);
        }

        TNode mainContainer = HasHostNode(reader.ParentNode(node))
            ? reader.ParentNode(node)!
            : container;
        bool hasTarget = TryResolveTeleportTarget(
            component.Target,
            out TNode targetContainer);
        if (!hasTarget)
        {
            WarnUnresolvedTeleportTarget(tree, component.Target);
        }

        List<MountedRenderNode<TNode>> children;
        TNode? targetAnchor = default;
        bool childrenMounted;
        if (component.IsDisabled)
        {
            children = HydrateChildren(
                tree,
                reader,
                reader.NextSibling(node),
                component.Children,
                mainContainer,
                elementNamespace,
                owner,
                TeleportEndMarker);
            childrenMounted = true;
            if (hasTarget)
            {
                HydrationNodeReader<TNode> targetReader =
                    _options.CreateHydrationReader!(targetContainer);
                TNode? cursor = GetHydrationTargetCursor(targetReader, targetContainer);
                targetAnchor = FindFirstMarker(
                    targetReader,
                    cursor,
                    TeleportTargetMarker);
                if (!HasHostNode(targetAnchor))
                {
                    targetAnchor = _options.CreateComment(TeleportTargetMarker);
                    _options.Insert(targetAnchor, targetContainer, default);
                }

                SetHydrationTargetCursor(
                    targetContainer,
                    targetReader.NextSibling(targetAnchor!));
            }
        }
        else if (hasTarget)
        {
            _ = HydrateChildren(
                tree,
                reader,
                reader.NextSibling(node),
                Array.Empty<IComponent>(),
                mainContainer,
                elementNamespace,
                owner,
                TeleportEndMarker);
            HydrationNodeReader<TNode> targetReader =
                _options.CreateHydrationReader!(targetContainer);
            TNode? cursor = GetHydrationTargetCursor(targetReader, targetContainer);
            children = HydrateChildren(
                tree,
                targetReader,
                cursor,
                component.Children,
                targetContainer,
                elementNamespace,
                owner,
                TeleportTargetMarker);
            targetAnchor = FindFirstMarker(
                targetReader,
                cursor,
                TeleportTargetMarker);
            if (!HasHostNode(targetAnchor))
            {
                targetAnchor = _options.CreateComment(TeleportTargetMarker);
                _options.Insert(targetAnchor, targetContainer, default);
                Warn(
                    tree,
                    "Hydration teleport mismatch: the server target had no closing anchor.");
            }

            SetHydrationTargetCursor(
                targetContainer,
                HasHostNode(targetAnchor)
                    ? targetReader.NextSibling(targetAnchor!)
                    : default);
            childrenMounted = true;
        }
        else
        {
            _ = HydrateChildren(
                tree,
                reader,
                reader.NextSibling(node),
                Array.Empty<IComponent>(),
                mainContainer,
                elementNamespace,
                owner,
                TeleportEndMarker);
            children = [];
            childrenMounted = false;
        }

        MountedTeleportNode<TNode> mounted = new(
            component,
            node,
            mainEnd!,
            hasTarget ? targetContainer : default,
            targetAnchor,
            hasTarget,
            childrenMounted,
            children,
            elementNamespace,
            owner);
        Register(tree, component, mounted);
        return (mounted, reader.NextSibling(mainEnd!));
    }

    private List<MountedRenderNode<TNode>> HydrateChildren(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode? first,
        IReadOnlyList<IComponent> components,
        TNode container,
        string? elementNamespace,
        ComponentContext? owner,
        string? closingMarker)
    {
        List<MountedRenderNode<TNode>> mounted = new(components.Count);
        TNode? cursor = first;
        for (int index = 0; index < components.Count; index++)
        {
            if (!HasHostNode(cursor)
                || (closingMarker is not null
                    && IsCommentMarker(reader, cursor!, closingMarker)))
            {
                mounted.Add(
                    Mount(
                        tree,
                        components[index],
                        container,
                        HasHostNode(cursor) ? cursor : default,
                        elementNamespace,
                        owner));
                continue;
            }

            if (components[index] is ITextComponent
                && reader.Kind(cursor!) == HydrationNodeKind.Text
                && index + 1 < components.Count
                && components[index + 1] is ITextComponent)
            {
                index = HydrateAdjacentTextRun(
                    tree,
                    reader,
                    cursor!,
                    container,
                    components,
                    index,
                    owner,
                    mounted,
                    out cursor);
                continue;
            }

            (MountedRenderNode<TNode> child, TNode? next) = HydrateNode(
                tree,
                reader,
                cursor!,
                components[index],
                container,
                elementNamespace,
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
                Warn(
                    tree,
                    "Hydration children mismatch: the server rendered more child nodes "
                    + "than the client component tree.");
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
        IReadOnlyList<IComponent> components,
        int startIndex,
        ComponentContext? owner,
        List<MountedRenderNode<TNode>> mounted,
        out TNode? cursor)
    {
        TNode? afterRun = reader.NextSibling(node);
        TNode currentNode = node;
        string currentData = reader.Data(node);
        int index = startIndex;
        while (true)
        {
            ITextComponent component = RequireText(components[index]);
            string clientText = component.Text;
            bool hasMoreText = index + 1 < components.Count
                && components[index + 1] is ITextComponent;
            if (hasMoreText && currentData.Length > clientText.Length)
            {
                string remainingText = currentData[clientText.Length..];
                TNode overflow = _options.CreateText(remainingText);
                _options.Insert(
                    overflow,
                    container,
                    HasHostNode(afterRun) ? afterRun : default);
                if (!string.Equals(currentData, clientText, StringComparison.Ordinal))
                {
                    _options.SetText(currentNode, clientText);
                }

                MountedLeafNode<TNode> text = new(component, currentNode, owner);
                mounted.Add(text);
                Register(tree, component, text);
                currentNode = overflow;
                currentData = remainingText;
                index++;
                continue;
            }

            if (!string.Equals(currentData, clientText, StringComparison.Ordinal))
            {
                _options.SetText(currentNode, clientText);
            }

            MountedLeafNode<TNode> last = new(component, currentNode, owner);
            mounted.Add(last);
            Register(tree, component, last);
            break;
        }

        cursor = afterRun;
        return index;
    }

    private (MountedRenderNode<TNode> Mounted, TNode? Next) HydrateMismatch(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IComponent component,
        TNode fallbackContainer,
        string? elementNamespace,
        ComponentContext? owner)
    {
        if (!IsMismatchAllowed(reader, node, "children"))
        {
            Warn(
                tree,
                $"Hydration node mismatch: the server host node cannot represent "
                + $"client component kind {component.Kind}.");
        }

        List<TNode> removalRange = ReadMismatchRange(reader, node);
        TNode? next = reader.NextSibling(removalRange[^1]);
        TNode parent = HasHostNode(reader.ParentNode(node))
            ? reader.ParentNode(node)!
            : fallbackContainer;
        for (int index = 0; index < removalRange.Count; index++)
        {
            _options.Remove(removalRange[index]);
        }

        MountedRenderNode<TNode> mounted = Mount(
            tree,
            component,
            parent,
            HasHostNode(next) ? next : default,
            elementNamespace,
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
            FragmentStartMarker => FragmentEndMarker,
            TeleportStartMarker => TeleportEndMarker,
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

    private void HydrateAttributes(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IElementComponent component,
        string? elementNamespace)
    {
        bool forceValue = string.Equals(
                component.Tag,
                "input",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                component.Tag,
                "option",
                StringComparison.OrdinalIgnoreCase);
        bool customElement = component.Tag.Contains('-', StringComparison.Ordinal);
        for (int index = 0; index < component.Attributes.Count; index++)
        {
            IComponentAttribute attribute = component.Attributes[index];
            if (IsComponentNodeLifecycleName(attribute.Name))
            {
                continue;
            }

            ReportHydrationAttributeMismatch(
                tree,
                reader,
                node,
                attribute);
            if (ShouldHydrateAttribute(
                attribute.Name,
                forceValue,
                customElement))
            {
                _options.PatchAttribute(
                    node,
                    component.Tag,
                    attribute.Name,
                    previousValue: null,
                    attribute.Value,
                    elementNamespace);
            }
        }
    }

    private static bool ShouldHydrateAttribute(
        string name,
        bool forceValue,
        bool customElement)
    {
        if (IsEventAttribute(name) || (name.Length > 0 && name[0] == '.'))
        {
            return true;
        }

        if (string.Equals(name, "innerHTML", StringComparison.Ordinal)
            || string.Equals(name, "textContent", StringComparison.Ordinal))
        {
            return true;
        }

        if (forceValue
            && (name.EndsWith("value", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    name,
                    "checked",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    name,
                    "selected",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    name,
                    "indeterminate",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return customElement;
    }

    private void ReportHydrationAttributeMismatch(
        MountedTree<TNode> tree,
        HydrationNodeReader<TNode> reader,
        TNode node,
        IComponentAttribute attribute)
    {
        if (IsEventAttribute(attribute.Name)
            || string.Equals(attribute.Name, "innerHTML", StringComparison.Ordinal)
            || string.Equals(attribute.Name, "textContent", StringComparison.Ordinal)
            || attribute.Value is null
            || attribute.Value is bool
            || IsMismatchAllowed(reader, node, AttributeMismatchCategory(attribute.Name)))
        {
            return;
        }

        string? actual = reader.Attribute(node, attribute.Name);
        string category = AttributeMismatchCategory(attribute.Name);
        bool equivalent;
        string expected;
        if (string.Equals(category, "class", StringComparison.Ordinal))
        {
            expected = StyleAndClassNormalization.NormalizeClass(attribute.Value);
            equivalent = ClassEquivalent(actual, expected);
        }
        else if (string.Equals(category, "style", StringComparison.Ordinal))
        {
            object? normalized =
                StyleAndClassNormalization.NormalizeStyle(attribute.Value);
            expected = StyleAndClassNormalization.StringifyStyle(normalized);
            equivalent = StyleEquivalent(actual, normalized);
        }
        else
        {
            expected = attribute.Value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
                : attribute.Value.ToString() ?? string.Empty;
            equivalent = string.Equals(actual, expected, StringComparison.Ordinal);
        }

        if (!equivalent)
        {
            Warn(
                tree,
                $"Hydration {category} mismatch for "
                + $"\"{attribute.Name}\": the server rendered \"{actual}\", "
                + $"but the client expected \"{expected}\".");
        }
    }

    private static bool ClassEquivalent(string? serverValue, string clientValue)
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

    private static bool StyleEquivalent(
        string? serverValue,
        object? clientValue)
    {
        Dictionary<string, object?> server =
            StyleAndClassNormalization.ParseStringStyle(
                serverValue ?? string.Empty);
        object? normalized =
            StyleAndClassNormalization.NormalizeStyle(clientValue);
        Dictionary<string, object?> client =
            StyleAndClassNormalization.ParseStringStyle(
                StyleAndClassNormalization.StringifyStyle(normalized));
        if (server.Count != client.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, object?> declaration in client)
        {
            if (!server.TryGetValue(declaration.Key, out object? actual)
                || !string.Equals(
                    actual?.ToString(),
                    declaration.Value?.ToString(),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string AttributeMismatchCategory(string name)
    {
        if (string.Equals(name, "class", StringComparison.OrdinalIgnoreCase))
        {
            return "class";
        }

        if (string.Equals(name, "style", StringComparison.OrdinalIgnoreCase))
        {
            return "style";
        }

        return "attribute";
    }

    private static bool IsEventAttribute(string name)
    {
        return name.Length > 2
            && name[0] == 'o'
            && name[1] == 'n'
            && !char.IsAsciiLetterLower(name[2]);
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
                string? value = reader.Attribute(
                    current,
                    AllowMismatchAttribute);
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

    private static bool IsStructuralStartMarker(string data)
    {
        return string.Equals(data, FragmentStartMarker, StringComparison.Ordinal)
            || string.Equals(data, TeleportStartMarker, StringComparison.Ordinal);
    }

    private static bool IsCommentMarker(
        HydrationNodeReader<TNode> reader,
        TNode node,
        string marker)
    {
        return reader.Kind(node) == HydrationNodeKind.Comment
            && string.Equals(
                reader.Data(node),
                marker,
                StringComparison.Ordinal);
    }

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
                if (string.Equals(
                    data,
                    openingMarker,
                    StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (string.Equals(
                    data,
                    closingMarker,
                    StringComparison.Ordinal))
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
        if (_hydrationTargetCursors is not null)
        {
            _hydrationTargetCursors[target] = cursor;
        }
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

        Warn(tree, warning);
        for (int index = 0; index < excess.Count; index++)
        {
            _options.Remove(excess[index]);
        }
    }

    private static void Warn(MountedTree<TNode> tree, string message)
    {
        tree.Application?.WarnHandler?.Invoke(message);
    }
}
