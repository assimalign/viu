using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Shared;

namespace Assimalign.Viu;

/// <summary>
/// Mounts, patches, moves, and unmounts immutable component trees through host-supplied operations.
/// </summary>
/// <remarks>
/// This is the host-neutral foundation of Vue 3.5's renderer:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/renderer.ts.
/// Mounted host state remains internal and never leaks back onto the public
/// <see cref="IComponent"/> values. The renderer is not thread-safe.
/// </remarks>
/// <typeparam name="TNode">The platform node type.</typeparam>
public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private static readonly EqualityComparer<TNode> NodeComparer =
        EqualityComparer<TNode>.Default;

    private readonly RendererOptions<TNode> _options;
    private readonly Dictionary<TNode, MountedTree<TNode>> _containerTrees =
        new(NodeComparer);
    private int _nextComponentIdentifier;

    /// <summary>Counts patch dispatches for block-tree behavior tests.</summary>
    internal static int PatchVisitCount;

    /// <summary>Counts internal unmount dispatches for teardown behavior tests.</summary>
    internal static int UnmountVisitCount;

    internal Renderer(RendererOptions<TNode> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Insert);
        ArgumentNullException.ThrowIfNull(options.Remove);
        ArgumentNullException.ThrowIfNull(options.CreateElement);
        ArgumentNullException.ThrowIfNull(options.CreateText);
        ArgumentNullException.ThrowIfNull(options.CreateComment);
        ArgumentNullException.ThrowIfNull(options.SetText);
        ArgumentNullException.ThrowIfNull(options.ParentNode);
        ArgumentNullException.ThrowIfNull(options.NextSibling);
        ArgumentNullException.ThrowIfNull(options.PatchAttribute);
        _options = options;
    }

    /// <summary>
    /// Renders a component tree into a host container. Passing null unmounts the current root.
    /// </summary>
    /// <param name="component">The next immutable tree, or null to unmount.</param>
    /// <param name="container">The host container.</param>
    /// <param name="application">
    /// The application composition context used to activate template components. Primitive-only
    /// trees may omit it. A mounted container retains the first supplied context.
    /// </param>
    /// <returns>The root template context, or null when the root is not a template.</returns>
    public IComponentContext? Render(
        IComponent? component,
        TNode container,
        IApplicationContext? application = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        _containerTrees.TryGetValue(container, out MountedTree<TNode>? tree);

        if (component is null)
        {
            if (tree?.Root is not null)
            {
                Unmount(tree, tree.Root, removeHostNodes: true);
                tree.Components.Clear();
                tree.Root = null;
                _containerTrees.Remove(container);
            }

            QueueHostCommit();
            Scheduler.FlushAfterSynchronousRender();
            return null;
        }

        if (tree is null)
        {
            tree = new MountedTree<TNode>
            {
                Application = application,
            };
            tree.Root = Mount(
                tree,
                component,
                container,
                default,
                elementNamespace: null,
                owner: null);
            _containerTrees.Add(container, tree);
        }
        else
        {
            if (application is not null
                && tree.Application is not null
                && !ReferenceEquals(application, tree.Application))
            {
                throw new InvalidOperationException(
                    "A mounted container cannot change its application context.");
            }

            tree.Application ??= application;
            tree.Root = Patch(
                tree,
                tree.Root,
                component,
                container,
                default,
                elementNamespace: null,
                owner: null);
        }

        QueueHostCommit();
        Scheduler.FlushAfterSynchronousRender();
        return tree.Root is MountedTemplateNode<TNode> template
            ? template.Instance.Context
            : null;
    }

    internal IReadOnlyList<MountedTemplateNode<TNode>> GetMountedTemplates(
        TNode container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (!_containerTrees.TryGetValue(
            container,
            out MountedTree<TNode>? tree)
            || tree.Root is null)
        {
            return Array.Empty<MountedTemplateNode<TNode>>();
        }

        List<MountedTemplateNode<TNode>> templates = [];
        CollectMountedTemplates(tree.Root, templates);
        return templates.AsReadOnly();
    }

    private static void CollectMountedTemplates(
        MountedRenderNode<TNode> mounted,
        List<MountedTemplateNode<TNode>> templates)
    {
        switch (mounted)
        {
            case MountedTemplateNode<TNode> template:
                templates.Add(template);
                CollectMountedTemplates(template.Subtree, templates);
                break;
            case MountedElementNode<TNode> element:
                CollectMountedTemplates(element.Children, templates);
                break;
            case MountedFragmentNode<TNode> fragment:
                CollectMountedTemplates(fragment.Children, templates);
                break;
            case MountedTeleportNode<TNode> teleport:
                CollectMountedTemplates(teleport.Children, templates);
                break;
        }
    }

    private static void CollectMountedTemplates(
        IReadOnlyList<MountedRenderNode<TNode>> mounted,
        List<MountedTemplateNode<TNode>> templates)
    {
        for (int index = 0; index < mounted.Count; index++)
        {
            CollectMountedTemplates(mounted[index], templates);
        }
    }

    private static IReadOnlyList<object> GetRootElementObjects(
        MountedRenderNode<TNode> mounted)
    {
        List<object> elements = [];
        CollectRootElementObjects(mounted, elements);
        return elements.AsReadOnly();
    }

    private static void CollectRootElementObjects(
        MountedRenderNode<TNode> mounted,
        List<object> elements)
    {
        switch (mounted)
        {
            case MountedElementNode<TNode> element:
                elements.Add(element.HostNode);
                break;
            case MountedTemplateNode<TNode> template:
                CollectRootElementObjects(template.Subtree, elements);
                break;
            case MountedFragmentNode<TNode> fragment:
                CollectRootElementObjects(fragment.Children, elements);
                break;
            case MountedTeleportNode<TNode> teleport:
                CollectRootElementObjects(teleport.Children, elements);
                break;
        }
    }

    private static void CollectRootElementObjects(
        IReadOnlyList<MountedRenderNode<TNode>> mounted,
        List<object> elements)
    {
        for (int index = 0; index < mounted.Count; index++)
        {
            CollectRootElementObjects(mounted[index], elements);
        }
    }

    private MountedRenderNode<TNode> Patch(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode>? current,
        IComponent next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        PatchVisitCount++;
        if (current is null)
        {
            return Mount(tree, next, container, anchor, elementNamespace, owner);
        }

        if (ReferenceEquals(current.Component, next))
        {
            return current;
        }

        if (!IsSameComponentType(current.Component, next))
        {
            TNode? replacementAnchor = GetNextHostNode(current);
            ComponentContext? replacementOwner = current.Owner;
            Unmount(tree, current, removeHostNodes: true);
            return Mount(
                tree,
                next,
                container,
                replacementAnchor,
                elementNamespace,
                replacementOwner);
        }

        switch (next.Kind)
        {
            case ComponentKind.Element:
                PatchElement(
                    tree,
                    (MountedElementNode<TNode>)current,
                    RequireElement(next),
                    elementNamespace);
                break;
            case ComponentKind.Text:
                PatchText(tree, (MountedLeafNode<TNode>)current, RequireText(next));
                break;
            case ComponentKind.Comment:
                PatchComment(tree, (MountedLeafNode<TNode>)current, RequireComment(next));
                break;
            case ComponentKind.Static:
                PatchStatic(tree, (MountedStaticNode<TNode>)current, RequireStatic(next));
                break;
            case ComponentKind.Fragment:
                PatchFragment(
                    tree,
                    (MountedFragmentNode<TNode>)current,
                    RequireFragment(next),
                    container,
                    elementNamespace);
                break;
            case ComponentKind.Template:
                PatchTemplate(
                    tree,
                    (MountedTemplateNode<TNode>)current,
                    RequireTemplate(next),
                    container,
                    elementNamespace);
                break;
            case ComponentKind.Teleport:
                PatchTeleport(
                    tree,
                    (MountedTeleportNode<TNode>)current,
                    RequireTeleport(next),
                    container,
                    elementNamespace);
                break;
            default:
                throw new InvalidOperationException($"Unknown component kind: {next.Kind}.");
        }

        return current;
    }

    private MountedRenderNode<TNode> Mount(
        MountedTree<TNode> tree,
        IComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        return component.Kind switch
        {
            ComponentKind.Element => MountElement(
                tree,
                RequireElement(component),
                container,
                anchor,
                elementNamespace,
                owner),
            ComponentKind.Text => MountText(
                tree,
                RequireText(component),
                container,
                anchor,
                owner),
            ComponentKind.Comment => MountComment(
                tree,
                RequireComment(component),
                container,
                anchor,
                owner),
            ComponentKind.Static => MountStatic(
                tree,
                RequireStatic(component),
                container,
                anchor,
                elementNamespace,
                owner),
            ComponentKind.Fragment => MountFragment(
                tree,
                RequireFragment(component),
                container,
                anchor,
                elementNamespace,
                owner),
            ComponentKind.Template => MountTemplate(
                tree,
                RequireTemplate(component),
                container,
                anchor,
                elementNamespace,
                owner),
            ComponentKind.Teleport => MountTeleport(
                tree,
                RequireTeleport(component),
                container,
                anchor,
                elementNamespace,
                owner),
            _ => throw new InvalidOperationException(
                $"Unknown component kind: {component.Kind}."),
        };
    }

    private MountedElementNode<TNode> MountElement(
        MountedTree<TNode> tree,
        IElementComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        string? ownNamespace = ElementNamespace(component.Tag, elementNamespace);
        TNode element = _options.CreateElement(component.Tag, ownNamespace);
        if (owner?.ScopeIdentifier is { } scopeIdentifier)
        {
            _options.SetScopeIdentifier?.Invoke(element, scopeIdentifier);
        }

        List<DirectiveBinding> directiveBindings = ResolveDirectiveBindings(
            tree,
            component.Directives,
            owner);
        TransitionHooks? transition = TransitionComponents.Get(component);
        BindDirectiveTransitions(directiveBindings, transition);
        InvokeDirectiveHooks(
            tree,
            element,
            directiveBindings,
            component,
            previousComponent: null,
            DirectiveHookKind.Created);
        List<MountedRenderNode<TNode>> children = MountChildren(
            tree,
            component.Children,
            element,
            default,
            ChildrenNamespace(component.Tag, ownNamespace),
            owner);
        MountAttributes(element, component.Tag, component.Attributes, ownNamespace);
        InvokeComponentNodeLifecycleHook(
            tree,
            owner,
            component,
            previousComponent: null,
            "onVnodeBeforeMount");
        InvokeDirectiveHooks(
            tree,
            element,
            directiveBindings,
            component,
            previousComponent: null,
            DirectiveHookKind.BeforeMount);
        if (transition is { Persisted: false })
        {
            transition.BeforeEnter(element);
        }

        _options.Insert(element, container, anchor);

        MountedElementNode<TNode> mounted = new(
            component,
            element,
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
            element);
        QueueComponentNodeLifecycleHook(
            tree,
            owner,
            mounted,
            component,
            previousComponent: null,
            "onVnodeMounted");
        if (transition is { Persisted: false })
        {
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(
                    () =>
                    {
                        if (!mounted.IsUnmounted)
                        {
                            transition.Enter(element);
                            QueueHostCommit();
                        }
                    })
                {
                    Name = "transition enter",
                });
        }

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
                                element,
                                mounted.DirectiveBindings,
                                RequireElement(mounted.Component),
                                previousComponent: null,
                                DirectiveHookKind.Mounted);
                            QueueHostCommit();
                        }
                    })
                {
                    Name = "directive mounted lifecycle",
                });
        }

        return mounted;
    }

    private MountedLeafNode<TNode> MountText(
        MountedTree<TNode> tree,
        ITextComponent component,
        TNode container,
        TNode? anchor,
        ComponentContext? owner)
    {
        TNode hostNode = _options.CreateText(component.Text);
        _options.Insert(hostNode, container, anchor);
        MountedLeafNode<TNode> mounted = new(component, hostNode, owner);
        Register(tree, component, mounted);
        return mounted;
    }

    private MountedLeafNode<TNode> MountComment(
        MountedTree<TNode> tree,
        ICommentComponent component,
        TNode container,
        TNode? anchor,
        ComponentContext? owner)
    {
        TNode hostNode = _options.CreateComment(component.Text ?? string.Empty);
        _options.Insert(hostNode, container, anchor);
        MountedLeafNode<TNode> mounted = new(component, hostNode, owner);
        Register(tree, component, mounted);
        return mounted;
    }

    private MountedStaticNode<TNode> MountStatic(
        MountedTree<TNode> tree,
        IStaticComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        InsertStaticContentDelegate<TNode>? insertStaticContent =
            _options.InsertStaticContent;
        if (insertStaticContent is null)
        {
            throw new NotSupportedException(
                "This host does not provide InsertStaticContent, which static components require.");
        }

        (TNode first, TNode last) = insertStaticContent(
            component.Content,
            container,
            anchor,
            elementNamespace);
        MountedStaticNode<TNode> mounted = new(component, first, last, owner);
        Register(tree, component, mounted);
        return mounted;
    }

    private MountedFragmentNode<TNode> MountFragment(
        MountedTree<TNode> tree,
        IFragmentComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        TNode startAnchor = _options.CreateText(string.Empty);
        TNode endAnchor = _options.CreateText(string.Empty);
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);
        List<MountedRenderNode<TNode>> children = MountChildren(
            tree,
            component.Children,
            container,
            endAnchor,
            elementNamespace,
            owner);

        MountedFragmentNode<TNode> mounted =
            new(component, startAnchor, endAnchor, children, owner);
        Register(tree, component, mounted);
        return mounted;
    }

    private MountedTemplateNode<TNode> MountTemplate(
        MountedTree<TNode> tree,
        ITemplateComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        if (IsSuspenseComponent(component))
        {
            return MountSuspense(
                tree,
                component,
                container,
                anchor,
                elementNamespace,
                owner);
        }

        IApplicationContext application = tree.Application
            ?? throw new InvalidOperationException(
                "Template components require an application context. Supply it to Render.");
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
        SchedulerJob mountedJob = new(instance.InvokeMounted)
        {
            Name = "component mounted lifecycle",
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
                    subtree = Mount(
                        tree,
                        initialRendered,
                        container,
                        anchor,
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
                Name = "component render",
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
                OwnTemplateReference(instance, component),
                ComponentReferenceValue(instance.Context));
            Scheduler.QueuePostFlushCallback(mountedJob);
            QueueComponentNodeLifecycleHook(
                tree,
                owner,
                mounted,
                component,
                previousComponent: null,
                "onVnodeMounted");
            return mounted;
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

    private void PatchTemplate(
        MountedTree<TNode> tree,
        MountedTemplateNode<TNode> mounted,
        ITemplateComponent next,
        TNode container,
        string? elementNamespace)
    {
        if (mounted.SuspenseState is not null)
        {
            PatchSuspense(
                tree,
                mounted,
                next,
                container,
                elementNamespace);
            return;
        }

        ITemplateComponent current = RequireTemplate(mounted.Component);
        mounted.FallbackContainer = container;
        mounted.ElementNamespace = elementNamespace;
        mounted.Transition = TransitionComponents.Get(next);
        bool forwardsRootBehavior =
            mounted.Instance.Template is IComponentRootBehaviorForwarder;
        bool shouldUpdate =
            forwardsRootBehavior
                && !Equals(current.Reference, next.Reference)
            || ShouldUpdateTemplate(mounted, current, next);
        if (shouldUpdate)
        {
            mounted.PendingNodeLifecycleComponent = next;
            mounted.PreviousNodeLifecycleComponent = current;
            mounted.Instance.Update(next);
            Scheduler.InvalidateJob(mounted.RenderJob);
            mounted.RenderEffect.Run();
            QueueComponentNodeLifecycleHook(
                tree,
                mounted.Owner,
                mounted,
                next,
                current,
                "onVnodeUpdated");
        }
        else
        {
            mounted.Instance.UpdateRequest(next);
        }

        UpdateReference(
            tree,
            mounted,
            forwardsRootBehavior ? null : current.Reference,
            forwardsRootBehavior ? null : next.Reference,
            ComponentReferenceValue(mounted.Instance.Context));
        ReplaceRegistration(tree, mounted, next);
    }

    private static IComponentReference? OwnTemplateReference(
        MountedComponent instance,
        ITemplateComponent component)
    {
        return instance.Template is IComponentRootBehaviorForwarder
            ? null
            : component.Reference;
    }

    private static bool ShouldUpdateTemplate(
        MountedTemplateNode<TNode> mounted,
        ITemplateComponent current,
        ITemplateComponent next)
    {
        PatchFlags patchFlags = next.Optimization.PatchFlags;
        if (patchFlags > 0
            && (patchFlags & PatchFlags.DynamicSlots) != 0)
        {
            return true;
        }

        if (current.Directives.Count > 0 || next.Directives.Count > 0)
        {
            return true;
        }

        IReadOnlyDictionary<string, ComponentSlot>? previousSlots = current.Slots;
        IReadOnlyDictionary<string, ComponentSlot>? nextSlots = next.Slots;
        if ((previousSlots is not null || nextSlots is not null)
            && (nextSlots is null
                || ResolveSlotFlags(
                    nextSlots,
                    mounted.Owner?.Slots) != SlotFlags.Stable))
        {
            return true;
        }

        IComponentArguments previousArguments = current.Arguments;
        IComponentArguments nextArguments = next.Arguments;
        if (ReferenceEquals(previousArguments, nextArguments))
        {
            return false;
        }

        IReadOnlyList<string>? dynamicProperties =
            next.Optimization.DynamicProperties;
        if (patchFlags > 0
            && (patchFlags & PatchFlags.Props) != 0
            && dynamicProperties is not null)
        {
            for (int index = 0; index < dynamicProperties.Count; index++)
            {
                string name = dynamicProperties[index];
                if (mounted.Instance.Context.IsDeclaredEventListener(name))
                {
                    continue;
                }

                if (previousArguments.Contains(name)
                    != nextArguments.Contains(name)
                    || !Equals(
                        previousArguments[name],
                        nextArguments[name]))
                {
                    return true;
                }
            }

            return false;
        }

        int previousCount = 0;
        foreach (KeyValuePair<string, object?> previous in previousArguments)
        {
            if (mounted.Instance.Context.IsDeclaredEventListener(previous.Key))
            {
                continue;
            }

            previousCount++;
            if (!nextArguments.Contains(previous.Key)
                || !Equals(
                    previous.Value,
                    nextArguments[previous.Key]))
            {
                return true;
            }
        }

        int nextCount = 0;
        foreach (KeyValuePair<string, object?> nextArgument in nextArguments)
        {
            if (!mounted.Instance.Context.IsDeclaredEventListener(nextArgument.Key))
            {
                nextCount++;
            }
        }

        return previousCount != nextCount;
    }

    private static SlotFlags ResolveSlotFlags(
        IReadOnlyDictionary<string, ComponentSlot> slots,
        IReadOnlyDictionary<string, ComponentSlot>? ownerSlots)
    {
        SlotFlags flags = slots is IComponentSlotCollection collection
            ? collection.Flags
            : SlotFlags.Stable;
        if (flags != SlotFlags.Forwarded)
        {
            return flags;
        }

        return ownerSlots is IComponentSlotCollection
            {
                Flags: SlotFlags.Stable,
            }
                ? SlotFlags.Stable
                : SlotFlags.Dynamic;
    }

    private MountedTeleportNode<TNode> MountTeleport(
        MountedTree<TNode> tree,
        ITeleportComponent component,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        TNode startAnchor = _options.CreateComment("teleport start");
        TNode endAnchor = _options.CreateComment("teleport end");
        _options.Insert(startAnchor, container, anchor);
        _options.Insert(endAnchor, container, anchor);

        bool hasTarget = false;
        TNode targetContainer = default!;
        TNode? targetAnchor = default;
        bool childrenMounted = false;
        List<MountedRenderNode<TNode>> children = [];
        if (!component.IsDeferred)
        {
            hasTarget = TryResolveTeleportTarget(
                component.Target,
                out targetContainer);
            if (hasTarget)
            {
                targetAnchor = _options.CreateText(string.Empty);
                _options.Insert(targetAnchor, targetContainer, default);
            }
            else
            {
                WarnUnresolvedTeleportTarget(tree, component.Target);
            }

            childrenMounted = component.IsDisabled || hasTarget;
            children = childrenMounted
                ? MountChildren(
                    tree,
                    component.Children,
                    component.IsDisabled ? container : targetContainer,
                    component.IsDisabled ? endAnchor : targetAnchor,
                    elementNamespace,
                    owner)
                : [];
        }
        else if (component.IsDisabled)
        {
            // Vue mounts a disabled deferred Teleport in place immediately. Only target resolution
            // waits for the post-flush phase.
            childrenMounted = true;
            children = MountChildren(
                tree,
                component.Children,
                container,
                endAnchor,
                elementNamespace,
                owner);
        }

        MountedTeleportNode<TNode> mounted = new(
            component,
            startAnchor,
            endAnchor,
            hasTarget ? targetContainer : default,
            targetAnchor,
            hasTarget,
            childrenMounted,
            children,
            elementNamespace,
            owner);
        Register(tree, component, mounted);
        if (component.IsDeferred)
        {
            QueueDeferredTeleportMount(
                tree,
                mounted,
                container,
                elementNamespace);
        }

        return mounted;
    }

    private void PatchTeleport(
        MountedTree<TNode> tree,
        MountedTeleportNode<TNode> mounted,
        ITeleportComponent next,
        TNode container,
        string? elementNamespace)
    {
        ITeleportComponent current = RequireTeleport(mounted.Component);
        if (mounted.PendingMountJob is { } pendingMount)
        {
            pendingMount.IsDisposed = true;
            mounted.PendingMountJob = null;
            if (next.IsDeferred)
            {
                QueueDeferredTeleportPatch(
                    tree,
                    mounted,
                    next,
                    container,
                    elementNamespace);
                return;
            }
        }

        TNode mainContainer = HostParentOrFallback(
            mounted.StartAnchor,
            container);
        bool nextHasTarget = TryResolveTeleportTarget(
            next.Target,
            out TNode nextTargetContainer);
        if (!nextHasTarget)
        {
            WarnUnresolvedTeleportTarget(tree, next.Target);
        }

        TNode? nextTargetAnchor = mounted.TargetAnchor;
        if (nextHasTarget)
        {
            if (!mounted.HasTarget || !HasHostNode(nextTargetAnchor))
            {
                nextTargetAnchor = _options.CreateText(string.Empty);
            }

            if (!mounted.HasTarget
                || !NodeComparer.Equals(
                    mounted.TargetContainer!,
                    nextTargetContainer))
            {
                _options.Insert(
                    nextTargetAnchor!,
                    nextTargetContainer,
                    default);
            }
        }

        bool shouldMountChildren = next.IsDisabled || nextHasTarget;
        if (shouldMountChildren)
        {
            TNode nextChildrenContainer = next.IsDisabled
                ? mainContainer
                : nextTargetContainer;
            TNode? nextChildrenAnchor = next.IsDisabled
                ? mounted.EndAnchor
                : nextTargetAnchor;
            if (mounted.ChildrenMounted)
            {
                TNode currentChildrenContainer = current.IsDisabled
                    ? mainContainer
                    : mounted.TargetContainer!;
                if (!NodeComparer.Equals(
                    currentChildrenContainer,
                    nextChildrenContainer))
                {
                    for (int index = 0; index < mounted.Children.Count; index++)
                    {
                        Move(
                            mounted.Children[index],
                            nextChildrenContainer,
                            nextChildrenAnchor);
                    }
                }

                bool patchedBlock = TryPatchBlockChildren(
                    tree,
                    current.Optimization,
                    next.Optimization,
                    nextChildrenContainer,
                    elementNamespace);
                if (patchedBlock)
                {
                    CarryForwardStaticChildren(
                        tree,
                        mounted.Children,
                        current.Children,
                        next.Children);
                }
                else
                {
                    mounted.Children = PatchChildren(
                        tree,
                        mounted.Children,
                        next.Children,
                        nextChildrenContainer,
                        nextChildrenAnchor,
                        elementNamespace,
                        next.Optimization.PatchFlags,
                        mounted.Owner);
                }
            }
            else
            {
                mounted.Children = MountChildren(
                    tree,
                    next.Children,
                    nextChildrenContainer,
                    nextChildrenAnchor,
                    elementNamespace,
                    mounted.Owner);
            }
        }
        else if (mounted.ChildrenMounted)
        {
            for (int index = 0; index < mounted.Children.Count; index++)
            {
                Unmount(tree, mounted.Children[index], removeHostNodes: true);
            }

            mounted.Children = [];
        }

        if (mounted.HasTarget && !nextHasTarget)
        {
            _options.Remove(mounted.TargetAnchor!);
            nextTargetAnchor = default;
        }

        mounted.TargetContainer = nextHasTarget
            ? nextTargetContainer
            : default;
        mounted.TargetAnchor = nextTargetAnchor;
        mounted.HasTarget = nextHasTarget;
        mounted.ChildrenMounted = shouldMountChildren;
        mounted.ElementNamespace = elementNamespace;
        ReplaceRegistration(tree, mounted, next);
    }

    private void QueueDeferredTeleportMount(
        MountedTree<TNode> tree,
        MountedTeleportNode<TNode> mounted,
        TNode fallbackContainer,
        string? elementNamespace)
    {
        SchedulerJob job = null!;
        job = new SchedulerJob(
            () =>
            {
                if (mounted.IsUnmounted
                    || !ReferenceEquals(mounted.PendingMountJob, job))
                {
                    return;
                }

                mounted.PendingMountJob = null;
                ITeleportComponent component =
                    RequireTeleport(mounted.Component);
                bool hasTarget = TryResolveTeleportTarget(
                    component.Target,
                    out TNode targetContainer);
                TNode? targetAnchor = default;
                if (hasTarget)
                {
                    targetAnchor = _options.CreateText(string.Empty);
                    _options.Insert(
                        targetAnchor,
                        targetContainer,
                        default);
                }
                else
                {
                    WarnUnresolvedTeleportTarget(
                        tree,
                        component.Target);
                }

                TNode mainContainer = HostParentOrFallback(
                    mounted.StartAnchor,
                    fallbackContainer);
                bool shouldMountChildren =
                    component.IsDisabled || hasTarget;
                if (!mounted.ChildrenMounted
                    && shouldMountChildren)
                {
                    mounted.Children = MountChildren(
                        tree,
                        component.Children,
                        component.IsDisabled
                            ? mainContainer
                            : targetContainer,
                        component.IsDisabled
                            ? mounted.EndAnchor
                            : targetAnchor,
                        elementNamespace,
                        mounted.Owner);
                }
                else if (mounted.ChildrenMounted
                    && !component.IsDisabled
                    && hasTarget)
                {
                    for (int index = 0;
                        index < mounted.Children.Count;
                        index++)
                    {
                        Move(
                            mounted.Children[index],
                            targetContainer,
                            targetAnchor);
                    }
                }

                mounted.TargetContainer = hasTarget
                    ? targetContainer
                    : default;
                mounted.TargetAnchor = targetAnchor;
                mounted.HasTarget = hasTarget;
                mounted.ChildrenMounted = shouldMountChildren;
                QueueHostCommit();
            })
        {
            Name = "deferred teleport mount",
        };
        mounted.PendingMountJob = job;
        Scheduler.QueuePostFlushCallback(job);
    }

    private void QueueDeferredTeleportPatch(
        MountedTree<TNode> tree,
        MountedTeleportNode<TNode> mounted,
        ITeleportComponent next,
        TNode container,
        string? elementNamespace)
    {
        SchedulerJob job = null!;
        job = new SchedulerJob(
            () =>
            {
                if (mounted.IsUnmounted
                    || !ReferenceEquals(mounted.PendingMountJob, job))
                {
                    return;
                }

                mounted.PendingMountJob = null;
                PatchTeleport(
                    tree,
                    mounted,
                    next,
                    container,
                    elementNamespace);
                QueueHostCommit();
            })
        {
            Name = "deferred teleport update",
        };
        mounted.PendingMountJob = job;
        Scheduler.QueuePostFlushCallback(job);
    }

    private void PatchElement(
        MountedTree<TNode> tree,
        MountedElementNode<TNode> mounted,
        IElementComponent next,
        string? elementNamespace)
    {
        IElementComponent current = RequireElement(mounted.Component);
        ComponentOptimization currentOptimization = current.Optimization;
        ComponentOptimization nextOptimization = next.Optimization;
        PatchFlags patchFlags = nextOptimization.PatchFlags;
        if (patchFlags == PatchFlags.Cached)
        {
            mounted.Transition = TransitionComponents.Get(next);
            UpdateReference(
                tree,
                mounted,
                current.Reference,
                next.Reference,
                mounted.HostNode);
            ReplaceRegistration(tree, mounted, next);
            return;
        }

        List<DirectiveBinding> nextDirectiveBindings = ResolveDirectiveBindings(
            tree,
            next.Directives,
            mounted.Owner,
            mounted.DirectiveBindings);
        TransitionHooks? nextTransition = TransitionComponents.Get(next);
        BindDirectiveTransitions(nextDirectiveBindings, nextTransition);
        BindDirectiveHostElements(mounted, nextDirectiveBindings);
        InvokeComponentNodeLifecycleHook(
            tree,
            mounted.Owner,
            next,
            current,
            "onVnodeBeforeUpdate");
        InvokeDirectiveHooks(
            tree,
            mounted.HostNode,
            nextDirectiveBindings,
            next,
            current,
            DirectiveHookKind.BeforeUpdate);
        string? ownNamespace = ElementNamespace(next.Tag, elementNamespace);
        string? childNamespace = ChildrenNamespace(next.Tag, ownNamespace);

        bool patchedBlock = TryPatchBlockChildren(
            tree,
            currentOptimization,
            nextOptimization,
            mounted.HostNode,
            childNamespace);

        if (patchedBlock)
        {
            PatchOptimizedAttributes(
                mounted.HostNode,
                next.Tag,
                current.Attributes,
                next.Attributes,
                nextOptimization,
                ownNamespace);
            // Vue's patchElement applies the TEXT fast path even after patchBlockChildren:
            // https://github.com/vuejs/core/blob/v3.5.25/packages/runtime-core/src/renderer.ts
            if ((patchFlags & PatchFlags.Text) != 0)
            {
                mounted.Children = PatchUnkeyedChildren(
                    tree,
                    mounted.Children,
                    next.Children,
                    mounted.HostNode,
                    default,
                    childNamespace,
                    mounted.Owner);
            }
        }
        else if (currentOptimization.IsBlock || nextOptimization.IsBlock)
        {
            PatchAttributes(
                mounted.HostNode,
                next.Tag,
                current.Attributes,
                next.Attributes,
                ownNamespace);
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                next.Children,
                mounted.HostNode,
                default,
                childNamespace,
                PatchFlags.Bail,
                mounted.Owner);
        }
        else if ((int)patchFlags > 0)
        {
            PatchOptimizedAttributes(
                mounted.HostNode,
                next.Tag,
                current.Attributes,
                next.Attributes,
                nextOptimization,
                ownNamespace);
            if ((patchFlags & PatchFlags.Text) != 0)
            {
                mounted.Children = PatchUnkeyedChildren(
                    tree,
                    mounted.Children,
                    next.Children,
                    mounted.HostNode,
                    default,
                    childNamespace,
                    mounted.Owner);
            }
        }
        else
        {
            PatchAttributes(
                mounted.HostNode,
                next.Tag,
                current.Attributes,
                next.Attributes,
                ownNamespace);
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                next.Children,
                mounted.HostNode,
                default,
                childNamespace,
                nextOptimization.PatchFlags,
                mounted.Owner);
        }

        UpdateReference(
            tree,
            mounted,
            current.Reference,
            next.Reference,
            mounted.HostNode);
        ReplaceRegistration(tree, mounted, next);
        mounted.DirectiveBindings = nextDirectiveBindings;
        mounted.Transition = nextTransition;
        QueueComponentNodeLifecycleHook(
            tree,
            mounted.Owner,
            mounted,
            next,
            current,
            "onVnodeUpdated");
        if (nextDirectiveBindings.Count > 0)
        {
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(
                    () =>
                    {
                        if (!mounted.IsUnmounted)
                        {
                            InvokeDirectiveHooks(
                                tree,
                                mounted.HostNode,
                                mounted.DirectiveBindings,
                                RequireElement(mounted.Component),
                                current,
                                DirectiveHookKind.Updated);
                            QueueHostCommit();
                        }
                    })
                {
                    Name = "directive updated lifecycle",
                });
        }
    }

    private void PatchText(
        MountedTree<TNode> tree,
        MountedLeafNode<TNode> mounted,
        ITextComponent next)
    {
        ITextComponent current = RequireText(mounted.Component);
        if (!string.Equals(current.Text, next.Text, StringComparison.Ordinal))
        {
            _options.SetText(mounted.HostNode, next.Text);
        }

        ReplaceRegistration(tree, mounted, next);
    }

    private void PatchComment(
        MountedTree<TNode> tree,
        MountedLeafNode<TNode> mounted,
        ICommentComponent next)
    {
        ReplaceRegistration(tree, mounted, next);
    }

    private void PatchStatic(
        MountedTree<TNode> tree,
        MountedStaticNode<TNode> mounted,
        IStaticComponent next)
    {
        ReplaceRegistration(tree, mounted, next);
    }

    private void PatchFragment(
        MountedTree<TNode> tree,
        MountedFragmentNode<TNode> mounted,
        IFragmentComponent next,
        TNode container,
        string? elementNamespace)
    {
        IFragmentComponent current = RequireFragment(mounted.Component);
        PatchFlags patchFlags = next.Optimization.PatchFlags;
        bool isStableBlock =
            (int)patchFlags > 0
            && (patchFlags & PatchFlags.StableFragment) != 0
            && TryPatchBlockChildren(
                tree,
                current.Optimization,
                next.Optimization,
                container,
                elementNamespace);

        if (!isStableBlock)
        {
            mounted.Children = PatchChildren(
                tree,
                mounted.Children,
                next.Children,
                container,
                mounted.EndAnchor,
                elementNamespace,
                patchFlags,
                mounted.Owner);
        }

        ReplaceRegistration(tree, mounted, next);
    }

    private bool TryPatchBlockChildren(
        MountedTree<TNode> tree,
        ComponentOptimization current,
        ComponentOptimization next,
        TNode fallbackContainer,
        string? elementNamespace)
    {
        IReadOnlyList<IComponent>? currentChildren = current.DynamicChildren;
        IReadOnlyList<IComponent>? nextChildren = next.DynamicChildren;
        if (currentChildren is null
            || nextChildren is null
            || currentChildren.Count != nextChildren.Count)
        {
            return false;
        }

        for (int index = 0; index < currentChildren.Count; index++)
        {
            if (!tree.Components.ContainsKey(currentChildren[index]))
            {
                return false;
            }
        }

        for (int index = 0; index < nextChildren.Count; index++)
        {
            MountedRenderNode<TNode> currentChild =
                tree.Components[currentChildren[index]];
            TNode childContainer = HostParentOrFallback(
                currentChild.FirstHostNode,
                fallbackContainer);
            MountedRenderNode<TNode> patchedChild = Patch(
                tree,
                currentChild,
                nextChildren[index],
                childContainer,
                default,
                elementNamespace,
                currentChild.Owner);
            if (!ReferenceEquals(patchedChild, currentChild))
            {
                // Block patching deliberately bypasses the parent children diff. Thread a
                // type-changing replacement back through the mounted ownership graph so later
                // moves and unmounts never retain the removed mounted node.
                ReplaceMountedNodeReferences(
                    tree,
                    currentChild,
                    patchedChild);
            }
        }

        return true;
    }

    private static void ReplaceMountedNodeReferences(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> current,
        MountedRenderNode<TNode> replacement)
    {
        if (tree.Root is null)
        {
            return;
        }

        if (ReferenceEquals(tree.Root, current))
        {
            tree.Root = replacement;
            return;
        }

        HashSet<MountedRenderNode<TNode>> visited =
            new(ReferenceEqualityComparer.Instance);
        ReplaceMountedNodeReferences(
            tree.Root,
            current,
            replacement,
            visited);
    }

    private static void ReplaceMountedNodeReferences(
        MountedRenderNode<TNode> mounted,
        MountedRenderNode<TNode> current,
        MountedRenderNode<TNode> replacement,
        HashSet<MountedRenderNode<TNode>> visited)
    {
        if (!visited.Add(mounted))
        {
            return;
        }

        switch (mounted)
        {
            case MountedElementNode<TNode> element:
                ReplaceMountedNodeReferences(
                    element.Children,
                    current,
                    replacement,
                    visited);
                break;
            case MountedFragmentNode<TNode> fragment:
                ReplaceMountedNodeReferences(
                    fragment.Children,
                    current,
                    replacement,
                    visited);
                break;
            case MountedTeleportNode<TNode> teleport:
                ReplaceMountedNodeReferences(
                    teleport.Children,
                    current,
                    replacement,
                    visited);
                break;
            case MountedTemplateNode<TNode> template:
                if (ReferenceEquals(template.Subtree, current))
                {
                    template.Subtree = replacement;
                }
                else
                {
                    ReplaceMountedNodeReferences(
                        template.Subtree,
                        current,
                        replacement,
                        visited);
                }

                ReplaceKeepAliveNodeReferences(
                    template.KeepAliveState,
                    current,
                    replacement,
                    visited);
                ReplaceSuspenseNodeReferences(
                    template.SuspenseState,
                    current,
                    replacement,
                    visited);
                break;
        }
    }

    private static void ReplaceMountedNodeReferences(
        List<MountedRenderNode<TNode>> mounted,
        MountedRenderNode<TNode> current,
        MountedRenderNode<TNode> replacement,
        HashSet<MountedRenderNode<TNode>> visited)
    {
        for (int index = 0; index < mounted.Count; index++)
        {
            if (ReferenceEquals(mounted[index], current))
            {
                mounted[index] = replacement;
            }
            else
            {
                ReplaceMountedNodeReferences(
                    mounted[index],
                    current,
                    replacement,
                    visited);
            }
        }
    }

    private static void ReplaceKeepAliveNodeReferences(
        MountedKeepAliveState<TNode>? state,
        MountedRenderNode<TNode> current,
        MountedRenderNode<TNode> replacement,
        HashSet<MountedRenderNode<TNode>> visited)
    {
        if (state is null)
        {
            return;
        }

        if (ReferenceEquals(state.ActiveNode, current))
        {
            state.ActiveNode = replacement;
        }
        else if (state.ActiveNode is { } active)
        {
            ReplaceMountedNodeReferences(
                active,
                current,
                replacement,
                visited);
        }

        foreach (KeepAliveCacheEntry<TNode> entry in state.Cache.Values)
        {
            if (ReferenceEquals(entry.Node, current))
            {
                entry.Node = replacement;
                entry.ComponentName = ComponentName(replacement);
            }
            else
            {
                ReplaceMountedNodeReferences(
                    entry.Node,
                    current,
                    replacement,
                    visited);
            }
        }
    }

    private static void ReplaceSuspenseNodeReferences(
        MountedSuspenseState<TNode>? state,
        MountedRenderNode<TNode> current,
        MountedRenderNode<TNode> replacement,
        HashSet<MountedRenderNode<TNode>> visited)
    {
        if (state is null)
        {
            return;
        }

        if (ReferenceEquals(state.ContentBranch, current))
        {
            state.ContentBranch = replacement;
        }
        else
        {
            ReplaceMountedNodeReferences(
                state.ContentBranch,
                current,
                replacement,
                visited);
        }

        if (ReferenceEquals(state.FallbackBranch, current))
        {
            state.FallbackBranch = replacement;
        }
        else if (state.FallbackBranch is { } fallback)
        {
            ReplaceMountedNodeReferences(
                fallback,
                current,
                replacement,
                visited);
        }
    }

    private static void CarryForwardStaticChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<MountedRenderNode<TNode>> mountedChildren,
        IReadOnlyList<IComponent> currentChildren,
        IReadOnlyList<IComponent> nextChildren)
    {
        int count = Math.Min(
            mountedChildren.Count,
            Math.Min(currentChildren.Count, nextChildren.Count));
        for (int index = 0; index < count; index++)
        {
            MountedRenderNode<TNode> mounted = mountedChildren[index];
            IComponent current = currentChildren[index];
            IComponent next = nextChildren[index];
            if (!IsSameComponentType(current, next))
            {
                continue;
            }

            if (ReferenceEquals(mounted.Component, current)
                && !ReferenceEquals(current, next))
            {
                ReplaceRegistration(tree, mounted, next);
            }

            switch (mounted)
            {
                case MountedElementNode<TNode> element
                    when current is IElementComponent currentElement
                    && next is IElementComponent nextElement:
                    CarryForwardStaticChildren(
                        tree,
                        element.Children,
                        currentElement.Children,
                        nextElement.Children);
                    break;
                case MountedFragmentNode<TNode> fragment
                    when current is IFragmentComponent currentFragment
                    && next is IFragmentComponent nextFragment:
                    CarryForwardStaticChildren(
                        tree,
                        fragment.Children,
                        currentFragment.Children,
                        nextFragment.Children);
                    break;
                case MountedTeleportNode<TNode> teleport
                    when current is ITeleportComponent currentTeleport
                    && next is ITeleportComponent nextTeleport:
                    CarryForwardStaticChildren(
                        tree,
                        teleport.Children,
                        currentTeleport.Children,
                        nextTeleport.Children);
                    break;
            }
        }
    }

    private List<MountedRenderNode<TNode>> PatchChildren(
        MountedTree<TNode> tree,
        List<MountedRenderNode<TNode>> current,
        IReadOnlyList<IComponent> next,
        TNode container,
        TNode? parentAnchor,
        string? elementNamespace,
        PatchFlags patchFlags,
        ComponentContext? owner)
    {
        if ((int)patchFlags > 0
            && (patchFlags & PatchFlags.UnkeyedFragment) != 0)
        {
            return PatchUnkeyedChildren(
                tree,
                current,
                next,
                container,
                parentAnchor,
                elementNamespace,
                owner);
        }

        return PatchKeyedChildren(
            tree,
            current,
            next,
            container,
            parentAnchor,
            elementNamespace,
            owner);
    }

    private List<MountedRenderNode<TNode>> PatchUnkeyedChildren(
        MountedTree<TNode> tree,
        List<MountedRenderNode<TNode>> current,
        IReadOnlyList<IComponent> next,
        TNode container,
        TNode? parentAnchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        int commonCount = Math.Min(current.Count, next.Count);
        List<MountedRenderNode<TNode>> result = new(next.Count);
        for (int index = 0; index < commonCount; index++)
        {
            result.Add(
                Patch(
                    tree,
                    current[index],
                    next[index],
                    container,
                    parentAnchor,
                    elementNamespace,
                    owner));
        }

        for (int index = commonCount; index < current.Count; index++)
        {
            Unmount(tree, current[index], removeHostNodes: true);
        }

        for (int index = commonCount; index < next.Count; index++)
        {
            result.Add(
                Mount(
                    tree,
                    next[index],
                    container,
                    parentAnchor,
                    elementNamespace,
                    owner));
        }

        return result;
    }

    private List<MountedRenderNode<TNode>> PatchKeyedChildren(
        MountedTree<TNode> tree,
        List<MountedRenderNode<TNode>> current,
        IReadOnlyList<IComponent> next,
        TNode container,
        TNode? parentAnchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        int nextCount = next.Count;
        MountedRenderNode<TNode>?[] nextMounted =
            new MountedRenderNode<TNode>?[nextCount];
        int[] nextIndexToCurrentIndex = new int[nextCount];
        bool[] claimedNextIndices = new bool[nextCount];
        Dictionary<object, int> keyedNextIndices = new();
        bool hasKeyedChild = false;
        bool hasKeylessChild = false;
        Action<string>? warn = tree.Application?.WarnHandler;

        for (int index = 0; index < nextCount; index++)
        {
            object? key = next[index].Key;
            if (key is not null)
            {
                hasKeyedChild = true;
                if (keyedNextIndices.ContainsKey(key))
                {
                    warn?.Invoke(
                        $"Duplicate keys found during update: \"{key}\". "
                        + "Make sure keys are unique.");
                }

                keyedNextIndices[key] = index;
            }
            else if (next[index] is not ICommentComponent)
            {
                hasKeylessChild = true;
            }
        }

        if (hasKeyedChild && hasKeylessChild)
        {
            warn?.Invoke(
                "Mixed keyed and unkeyed children detected during update. "
                + "Give every iterated child a key (or none) so the keyed diff "
                + "can track them reliably.");
        }

        int highestNextIndex = -1;
        bool moved = false;
        for (int currentIndex = 0; currentIndex < current.Count; currentIndex++)
        {
            MountedRenderNode<TNode> currentChild = current[currentIndex];
            int nextIndex = FindNextIndex(
                currentChild,
                next,
                keyedNextIndices,
                claimedNextIndices);
            if (nextIndex < 0)
            {
                Unmount(tree, currentChild, removeHostNodes: true);
                continue;
            }

            claimedNextIndices[nextIndex] = true;
            nextIndexToCurrentIndex[nextIndex] = currentIndex + 1;
            if (nextIndex < highestNextIndex)
            {
                moved = true;
            }
            else
            {
                highestNextIndex = nextIndex;
            }

            nextMounted[nextIndex] = Patch(
                tree,
                currentChild,
                next[nextIndex],
                container,
                parentAnchor,
                elementNamespace,
                owner);
        }

        int[] stableSequence = moved
            ? GetLongestIncreasingSubsequence(nextIndexToCurrentIndex)
            : Array.Empty<int>();
        int stableCursor = stableSequence.Length - 1;

        for (int nextIndex = nextCount - 1; nextIndex >= 0; nextIndex--)
        {
            TNode? anchor = nextIndex + 1 < nextCount
                ? nextMounted[nextIndex + 1]!.FirstHostNode
                : parentAnchor;

            if (nextIndexToCurrentIndex[nextIndex] == 0)
            {
                nextMounted[nextIndex] = Mount(
                    tree,
                    next[nextIndex],
                    container,
                    anchor,
                    elementNamespace,
                    owner);
            }
            else if (moved)
            {
                if (stableCursor < 0 || stableSequence[stableCursor] != nextIndex)
                {
                    Move(nextMounted[nextIndex]!, container, anchor);
                }
                else
                {
                    stableCursor--;
                }
            }
        }

        List<MountedRenderNode<TNode>> result = new(nextCount);
        for (int index = 0; index < nextMounted.Length; index++)
        {
            result.Add(nextMounted[index]!);
        }

        return result;
    }

    private static int FindNextIndex(
        MountedRenderNode<TNode> current,
        IReadOnlyList<IComponent> next,
        IReadOnlyDictionary<object, int> keyedNextIndices,
        IReadOnlyList<bool> claimedNextIndices)
    {
        object? key = current.Component.Key;
        if (key is not null)
        {
            return keyedNextIndices.TryGetValue(key, out int index)
                && !claimedNextIndices[index]
                && IsSameComponentType(current.Component, next[index])
                    ? index
                    : -1;
        }

        for (int index = 0; index < next.Count; index++)
        {
            if (!claimedNextIndices[index]
                && next[index].Key is null
                && IsSameComponentType(current.Component, next[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int[] GetLongestIncreasingSubsequence(IReadOnlyList<int> source)
    {
        int[] predecessors = new int[source.Count];
        int[] result = new int[source.Count];
        int resultLength = 0;

        for (int index = 0; index < source.Count; index++)
        {
            int value = source[index];
            if (value == 0)
            {
                continue;
            }

            int low = 0;
            int high = resultLength;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (source[result[middle]] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            predecessors[index] = low > 0 ? result[low - 1] : -1;
            result[low] = index;
            if (low == resultLength)
            {
                resultLength++;
            }
        }

        int[] sequence = new int[resultLength];
        if (resultLength == 0)
        {
            return sequence;
        }

        int cursor = result[resultLength - 1];
        for (int index = resultLength - 1; index >= 0; index--)
        {
            sequence[index] = cursor;
            cursor = predecessors[cursor];
        }

        return sequence;
    }

    private List<MountedRenderNode<TNode>> MountChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<IComponent> children,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentContext? owner)
    {
        List<MountedRenderNode<TNode>> mounted = new(children.Count);
        for (int index = 0; index < children.Count; index++)
        {
            mounted.Add(
                Mount(
                    tree,
                    children[index],
                    container,
                    anchor,
                    elementNamespace,
                    owner));
        }

        return mounted;
    }

    private void Move(
        MountedRenderNode<TNode> mounted,
        TNode container,
        TNode? anchor)
    {
        switch (mounted)
        {
            case MountedTemplateNode<TNode> template:
                Move(template.Subtree, container, anchor);
                break;
            case MountedTeleportNode<TNode> teleport:
                _options.Insert(teleport.StartAnchor, container, anchor);
                if (RequireTeleport(teleport.Component).IsDisabled)
                {
                    for (int index = 0; index < teleport.Children.Count; index++)
                    {
                        Move(teleport.Children[index], container, anchor);
                    }
                }

                _options.Insert(teleport.EndAnchor, container, anchor);
                break;
            case MountedFragmentNode<TNode> fragment:
                _options.Insert(fragment.StartAnchor, container, anchor);
                for (int index = 0; index < fragment.Children.Count; index++)
                {
                    Move(fragment.Children[index], container, anchor);
                }

                _options.Insert(fragment.EndAnchor, container, anchor);
                break;
            case MountedStaticNode<TNode> staticNode:
                MoveRange(
                    staticNode.FirstHostNode,
                    staticNode.LastHostNode,
                    container,
                    anchor);
                break;
            default:
                _options.Insert(mounted.FirstHostNode, container, anchor);
                break;
        }
    }

    private void MoveRange(
        TNode first,
        TNode last,
        TNode container,
        TNode? anchor)
    {
        TNode current = first;
        while (!NodeComparer.Equals(current, last))
        {
            TNode? next = _options.NextSibling(current);
            _options.Insert(current, container, anchor);
            current = RequireHostNode(next, "A mounted host range ended before its last node.");
        }

        _options.Insert(last, container, anchor);
    }

    private void RemoveElement(MountedElementNode<TNode> mounted)
    {
        TransitionHooks? transition = mounted.Transition;
        if (transition is null || transition.Persisted)
        {
            _options.Remove(mounted.HostNode);
            return;
        }

        object element = mounted.HostNode;
        void Remove() => _options.Remove(mounted.HostNode);
        void Leave() => transition.Leave(element, Remove);
        if (transition.DelayLeave is { } delayLeave)
        {
            delayLeave(element, Remove, Leave);
        }
        else
        {
            Leave();
        }
    }

    private void MountAttributes(
        TNode element,
        string elementTag,
        IComponentAttributeCollection attributes,
        string? elementNamespace)
    {
        for (int index = 0; index < attributes.Count; index++)
        {
            IComponentAttribute attribute = attributes[index];
            if (!string.Equals(attribute.Name, "value", StringComparison.Ordinal)
                && !IsComponentNodeLifecycleName(attribute.Name))
            {
                _options.PatchAttribute(
                    element,
                    elementTag,
                    attribute.Name,
                    previousValue: null,
                    attribute.Value,
                    elementNamespace);
            }
        }

        if (attributes.TryGetValue("value", out object? value))
        {
            _options.PatchAttribute(
                element,
                elementTag,
                "value",
                previousValue: null,
                value,
                elementNamespace);
        }
    }

    private void PatchAttributes(
        TNode element,
        string elementTag,
        IComponentAttributeCollection current,
        IComponentAttributeCollection next,
        string? elementNamespace)
    {
        if (ReferenceEquals(current, next))
        {
            return;
        }

        for (int index = 0; index < current.Count; index++)
        {
            IComponentAttribute attribute = current[index];
            if (!IsComponentNodeLifecycleName(attribute.Name)
                && !next.TryGetValue(attribute.Name, out _))
            {
                _options.PatchAttribute(
                    element,
                    elementTag,
                    attribute.Name,
                    attribute.Value,
                    nextValue: null,
                    elementNamespace);
            }
        }

        for (int index = 0; index < next.Count; index++)
        {
            IComponentAttribute attribute = next[index];
            if (IsComponentNodeLifecycleName(attribute.Name))
            {
                continue;
            }

            current.TryGetValue(attribute.Name, out object? previousValue);
            if (!Equals(previousValue, attribute.Value)
                || string.Equals(attribute.Name, "value", StringComparison.Ordinal))
            {
                _options.PatchAttribute(
                    element,
                    elementTag,
                    attribute.Name,
                    previousValue,
                    attribute.Value,
                    elementNamespace);
            }
        }
    }

    private void PatchOptimizedAttributes(
        TNode element,
        string elementTag,
        IComponentAttributeCollection current,
        IComponentAttributeCollection next,
        ComponentOptimization optimization,
        string? elementNamespace)
    {
        PatchFlags patchFlags = optimization.PatchFlags;
        if ((int)patchFlags <= 0)
        {
            return;
        }

        if ((patchFlags & PatchFlags.FullProps) != 0)
        {
            PatchAttributes(
                element,
                elementTag,
                current,
                next,
                elementNamespace);
            return;
        }

        if ((patchFlags & PatchFlags.Class) != 0)
        {
            PatchAttribute(
                element,
                elementTag,
                "class",
                current,
                next,
                elementNamespace);
        }

        if ((patchFlags & PatchFlags.Style) != 0)
        {
            PatchAttribute(
                element,
                elementTag,
                "style",
                current,
                next,
                elementNamespace);
        }

        if ((patchFlags & PatchFlags.Props) != 0
            && optimization.DynamicProperties is not null)
        {
            for (int index = 0; index < optimization.DynamicProperties.Count; index++)
            {
                PatchAttribute(
                    element,
                    elementTag,
                    optimization.DynamicProperties[index],
                    current,
                    next,
                    elementNamespace);
            }
        }
    }

    private void PatchAttribute(
        TNode element,
        string elementTag,
        string attributeName,
        IComponentAttributeCollection current,
        IComponentAttributeCollection next,
        string? elementNamespace)
    {
        if (IsComponentNodeLifecycleName(attributeName))
        {
            return;
        }

        current.TryGetValue(attributeName, out object? previousValue);
        next.TryGetValue(attributeName, out object? nextValue);
        if (!Equals(previousValue, nextValue)
            || string.Equals(attributeName, "value", StringComparison.Ordinal))
        {
            _options.PatchAttribute(
                element,
                elementTag,
                attributeName,
                previousValue,
                nextValue,
                elementNamespace);
        }
    }

    private void Unmount(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> mounted,
        bool removeHostNodes,
        bool optimized = false)
    {
        UnmountVisitCount++;
        ComponentOptimization optimization =
            mounted.Component.Optimization;
        if (optimization.PatchFlags == PatchFlags.Bail)
        {
            optimized = false;
        }

        tree.Components.Remove(mounted.Component);
        mounted.IsUnmounted = true;
        ClearReference(tree, mounted);
        IComponent unmountedComponent = mounted.Component;
        InvokeComponentNodeLifecycleHook(
            tree,
            mounted.Owner,
            unmountedComponent,
            previousComponent: null,
            "onVnodeBeforeUnmount");
        bool queuedUnmountedHook = false;

        switch (mounted)
        {
            case MountedTemplateNode<TNode> template:
                if (template.SuspenseState is not null)
                {
                    UnmountSuspense(
                        tree,
                        template,
                        removeHostNodes);
                    break;
                }

                template.RenderJob.IsDisposed = true;
                template.MountedJob.IsDisposed = true;
                template.UpdatedJob.IsDisposed = true;
                Scheduler.InvalidateJob(template.RenderJob);
                template.Instance.Unmount(
                    () =>
                    {
                        if (template.KeepAliveState is { } keepAliveState)
                        {
                            UnmountKeepAlive(
                                tree,
                                keepAliveState,
                                template.Subtree,
                                removeHostNodes);
                        }
                        else
                        {
                            Unmount(
                                tree,
                                template.Subtree,
                                removeHostNodes);
                        }
                    });
                break;
            case MountedTeleportNode<TNode> teleport:
                if (teleport.PendingMountJob is { } pendingMount)
                {
                    pendingMount.IsDisposed = true;
                    teleport.PendingMountJob = null;
                }

                bool removeTeleportedChildren =
                    removeHostNodes
                    || !RequireTeleport(teleport.Component).IsDisabled;
                for (int index = 0; index < teleport.Children.Count; index++)
                {
                    Unmount(
                        tree,
                        teleport.Children[index],
                        removeTeleportedChildren,
                        optimized: teleport.Children[index]
                            .Component
                            .Optimization
                            .DynamicChildren is not null);
                }

                if (teleport.HasTarget)
                {
                    _options.Remove(teleport.TargetAnchor!);
                }

                if (removeHostNodes)
                {
                    RemoveRange(
                        teleport.StartAnchor,
                        teleport.EndAnchor);
                }

                break;
            case MountedElementNode<TNode> element:
                IElementComponent elementComponent =
                    RequireElement(element.Component);
                InvokeDirectiveHooks(
                    tree,
                    element.HostNode,
                    element.DirectiveBindings,
                    elementComponent,
                    previousComponent: null,
                    DirectiveHookKind.BeforeUnmount);
                if (!TryUnmountBlockChildren(
                    tree,
                    elementComponent,
                    element.Children,
                    isFragment: false))
                {
                    if (!optimized || optimization.HasOnce)
                    {
                        UnmountChildren(tree, element.Children);
                    }
                    else
                    {
                        ReleaseSkippedMountedChildren(
                            tree,
                            element.Children);
                    }
                }

                if (removeHostNodes)
                {
                    RemoveElement(element);
                }

                QueueComponentNodeLifecycleHook(
                    tree,
                    mounted.Owner,
                    mounted: null,
                    unmountedComponent,
                    previousComponent: null,
                    "onVnodeUnmounted");
                queuedUnmountedHook = true;
                if (element.DirectiveBindings.Count > 0)
                {
                    Scheduler.QueuePostFlushCallback(
                        new SchedulerJob(
                            () =>
                            {
                                InvokeDirectiveHooks(
                                    tree,
                                    element.HostNode,
                                    element.DirectiveBindings,
                                    elementComponent,
                                    previousComponent: null,
                                    DirectiveHookKind.Unmounted);
                                QueueHostCommit();
                            })
                        {
                            Name = "directive unmounted lifecycle",
                        });
                }

                break;
            case MountedFragmentNode<TNode> fragment:
                IFragmentComponent fragmentComponent =
                    RequireFragment(fragment.Component);
                if (!TryUnmountBlockChildren(
                    tree,
                    fragmentComponent,
                    fragment.Children,
                    isFragment: true))
                {
                    PatchFlags patchFlags =
                        optimization.PatchFlags;
                    bool mustWalkFragment =
                        (int)patchFlags > 0
                        && (patchFlags
                            & (PatchFlags.KeyedFragment
                                | PatchFlags.UnkeyedFragment))
                            != 0;
                    if (!optimized
                        || optimization.HasOnce
                        || mustWalkFragment)
                    {
                        UnmountChildren(tree, fragment.Children);
                    }
                    else
                    {
                        ReleaseSkippedMountedChildren(
                            tree,
                            fragment.Children);
                    }
                }

                if (removeHostNodes)
                {
                    RemoveRange(fragment.StartAnchor, fragment.EndAnchor);
                }

                break;
            case MountedStaticNode<TNode> staticNode:
                if (removeHostNodes)
                {
                    RemoveRange(staticNode.FirstHostNode, staticNode.LastHostNode);
                }

                break;
            default:
                if (removeHostNodes)
                {
                    _options.Remove(mounted.FirstHostNode);
                }

                break;
        }

        if (!queuedUnmountedHook)
        {
            QueueComponentNodeLifecycleHook(
                tree,
                mounted.Owner,
                mounted: null,
                unmountedComponent,
                previousComponent: null,
                "onVnodeUnmounted");
        }
    }

    private void UnmountChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<MountedRenderNode<TNode>> children,
        bool optimized = false)
    {
        for (int index = 0; index < children.Count; index++)
        {
            Unmount(
                tree,
                children[index],
                removeHostNodes: false,
                optimized);
        }
    }

    private bool TryUnmountBlockChildren(
        MountedTree<TNode> tree,
        IComponent component,
        IReadOnlyList<MountedRenderNode<TNode>> mountedChildren,
        bool isFragment)
    {
        ComponentOptimization optimization =
            component.Optimization;
        IReadOnlyList<IComponent>? dynamicChildren =
            optimization.DynamicChildren;
        PatchFlags patchFlags = optimization.PatchFlags;
        if (dynamicChildren is null
            || optimization.HasOnce
            || (isFragment
                && ((int)patchFlags <= 0
                    || (patchFlags & PatchFlags.StableFragment) == 0)))
        {
            return false;
        }

        for (int index = 0;
            index < dynamicChildren.Count;
            index++)
        {
            if (tree.Components.TryGetValue(
                dynamicChildren[index],
                out MountedRenderNode<TNode>? child)
                && !child.IsUnmounted)
            {
                Unmount(
                    tree,
                    child,
                    removeHostNodes: false,
                    optimized: true);
            }
        }

        ReleaseSkippedMountedChildren(tree, mountedChildren);
        return true;
    }

    private void ReleaseSkippedMountedChildren(
        MountedTree<TNode> tree,
        IReadOnlyList<MountedRenderNode<TNode>> children)
    {
        for (int index = 0; index < children.Count; index++)
        {
            ReleaseSkippedMountedNode(tree, children[index]);
        }
    }

    private void ReleaseSkippedMountedNode(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> mounted)
    {
        if (mounted.IsUnmounted)
        {
            return;
        }

        if (RequiresUnmountVisit(mounted))
        {
            Unmount(
                tree,
                mounted,
                removeHostNodes: false,
                optimized: true);
            return;
        }

        if (tree.Components.TryGetValue(
            mounted.Component,
            out MountedRenderNode<TNode>? registered)
            && ReferenceEquals(registered, mounted))
        {
            tree.Components.Remove(mounted.Component);
        }

        mounted.IsUnmounted = true;
        if (mounted.ReferenceJob is { } referenceJob)
        {
            referenceJob.IsDisposed = true;
            Scheduler.InvalidateJob(referenceJob);
        }

        switch (mounted)
        {
            case MountedElementNode<TNode> element:
                ReleaseSkippedMountedChildren(
                    tree,
                    element.Children);
                break;
            case MountedFragmentNode<TNode> fragment:
                ReleaseSkippedMountedChildren(
                    tree,
                    fragment.Children);
                break;
        }
    }

    private static bool RequiresUnmountVisit(
        MountedRenderNode<TNode> mounted)
    {
        if (mounted is MountedTemplateNode<TNode>
            or MountedTeleportNode<TNode>)
        {
            return true;
        }

        if (mounted.Component.Reference is not null
            || HasComponentNodeLifecycleHook(
                mounted.Component,
                "onVnodeBeforeUnmount")
            || HasComponentNodeLifecycleHook(
                mounted.Component,
                "onVnodeUnmounted"))
        {
            return true;
        }

        return mounted is MountedElementNode<TNode> element
            && (element.DirectiveBindings.Count > 0
                || element.Transition is not null);
    }

    private void RemoveRange(TNode first, TNode last)
    {
        TNode current = first;
        while (!NodeComparer.Equals(current, last))
        {
            TNode? next = _options.NextSibling(current);
            _options.Remove(current);
            current = RequireHostNode(next, "A mounted host range ended before its last node.");
        }

        _options.Remove(last);
    }

    private TNode? GetNextHostNode(MountedRenderNode<TNode> mounted)
    {
        return _options.NextSibling(mounted.LastHostNode);
    }

    private TNode HostParentOrFallback(TNode node, TNode fallback)
    {
        TNode? parent = _options.ParentNode(node);
        return HasHostNode(parent) ? parent! : fallback;
    }

    private void QueueHostCommit()
    {
        Scheduler.QueueHostCommit(_options.Commit);
    }

    private void UpdateReference(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> mounted,
        IComponentReference? previousReference,
        IComponentReference? nextReference,
        object? value)
    {
        if (Equals(previousReference, nextReference))
        {
            return;
        }

        if (mounted.ReferenceJob is not null)
        {
            mounted.ReferenceJob.IsDisposed = true;
        }

        if (previousReference is not null)
        {
            InvokeReference(
                tree,
                mounted.Owner,
                previousReference,
                value: null);
        }

        if (nextReference is null)
        {
            mounted.ReferenceJob = null;
            return;
        }

        SchedulerJob job = new(
            () =>
            {
                if (!mounted.IsUnmounted
                    && Equals(
                        mounted.Component.Reference,
                        nextReference))
                {
                    InvokeReference(
                        tree,
                        mounted.Owner,
                        nextReference,
                        value);
                }
            })
        {
            Identifier = -1,
            Name = "template reference assignment",
        };
        mounted.ReferenceJob = job;
        Scheduler.QueuePostFlushCallback(job);
    }

    private static void ClearReference(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> mounted)
    {
        if (mounted.ReferenceJob is not null)
        {
            mounted.ReferenceJob.IsDisposed = true;
            mounted.ReferenceJob = null;
        }

        if (mounted is MountedTemplateNode<TNode>
            {
                Instance.Template: IComponentRootBehaviorForwarder,
            })
        {
            return;
        }

        if (mounted.Component.Reference is { } reference)
        {
            InvokeReference(
                tree,
                mounted.Owner,
                reference,
                value: null);
        }
    }

    private static void InvokeReference(
        MountedTree<TNode> tree,
        ComponentContext? owner,
        IComponentReference reference,
        object? value)
    {
        try
        {
            if (owner is null)
            {
                reference.Set(value);
            }
            else
            {
                owner.Run(() => reference.Set(value));
            }
        }
        catch (Exception exception)
        {
            if (owner is not null)
            {
                ComponentErrorHandling.Handle(
                    exception,
                    owner,
                    "template reference callback");
            }
            else if (tree.Application?.ErrorHandler is { } errorHandler)
            {
                errorHandler(
                    exception,
                    null,
                    "template reference callback");
            }
            else
            {
                throw;
            }
        }
    }

    private static object? ComponentReferenceValue(
        ComponentContext context)
    {
        return context.HasExposed
            ? context.Exposed
            : context;
    }

    private static bool HasHostNode(TNode? node)
    {
        return node is not null
            && !NodeComparer.Equals(node, default!);
    }

    private bool TryResolveTeleportTarget(
        object target,
        out TNode targetContainer)
    {
        if (target is TNode typedTarget && HasHostNode(typedTarget))
        {
            targetContainer = typedTarget;
            return true;
        }

        Func<object, TNode?>? resolver = _options.ResolveTeleportTarget;
        if (resolver is not null)
        {
            TNode? resolved = resolver(target);
            if (HasHostNode(resolved))
            {
                targetContainer = resolved!;
                return true;
            }
        }

        targetContainer = default!;
        return false;
    }

    private static void WarnUnresolvedTeleportTarget(
        MountedTree<TNode> tree,
        object target)
    {
        tree.Application?.WarnHandler?.Invoke(
            $"Failed to resolve teleport target \"{target}\".");
    }

    private static TNode RequireHostNode(TNode? node, string message)
    {
        if (!HasHostNode(node))
        {
            throw new InvalidOperationException(message);
        }

        return node!;
    }

    private static void Register(
        MountedTree<TNode> tree,
        IComponent component,
        MountedRenderNode<TNode> mounted)
    {
        tree.Components[component] = mounted;
    }

    private static void ReplaceRegistration(
        MountedTree<TNode> tree,
        MountedRenderNode<TNode> mounted,
        IComponent next)
    {
        IComponent current = mounted.Component;
        if (tree.Components.TryGetValue(current, out MountedRenderNode<TNode>? registered)
            && ReferenceEquals(registered, mounted))
        {
            tree.Components.Remove(current);
        }

        mounted.Component = next;
        tree.Components[next] = mounted;
    }

    private static bool IsSameComponentType(IComponent current, IComponent next)
    {
        if (current.Kind != next.Kind || !Equals(current.Key, next.Key))
        {
            return false;
        }

        return current.Kind switch
        {
            ComponentKind.Element => string.Equals(
                RequireElement(current).Tag,
                RequireElement(next).Tag,
                StringComparison.Ordinal),
            ComponentKind.Template => SameTemplateIdentity(
                RequireTemplate(current),
                RequireTemplate(next)),
            ComponentKind.Static => string.Equals(
                RequireStatic(current).Content,
                RequireStatic(next).Content,
                StringComparison.Ordinal),
            _ => true,
        };
    }

    private static bool SameTemplateIdentity(
        ITemplateComponent current,
        ITemplateComponent next)
    {
        if (current.TemplateType is not null || next.TemplateType is not null)
        {
            return current.TemplateType == next.TemplateType;
        }

        return string.Equals(
            current.TemplateName,
            next.TemplateName,
            StringComparison.Ordinal);
    }

    private static bool IsComponentNodeLifecycleName(string name)
    {
        return name.StartsWith("onVnode", StringComparison.Ordinal);
    }

    private static ComponentNodeLifecycleHook? GetComponentNodeLifecycleHook(
        IComponent component,
        string name)
    {
        object? value = component switch
        {
            IElementComponent element
                when element.Attributes.TryGetValue(name, out object? hook) =>
                    hook,
            ITemplateComponent template
                when template.Arguments.Contains(name) =>
                    template.Arguments[name],
            _ => null,
        };
        return value switch
        {
            null => null,
            ComponentNodeLifecycleHook hook => hook,
            Action<IComponent, IComponent?> action =>
                new ComponentNodeLifecycleHook(action),
            _ => throw new NotSupportedException(
                $"Component node lifecycle property \"{name}\" must be a "
                + $"{nameof(ComponentNodeLifecycleHook)}."),
        };
    }

    private static bool HasComponentNodeLifecycleHook(
        IComponent component,
        string name)
    {
        return component switch
        {
            IElementComponent element =>
                element.Attributes.TryGetValue(name, out object? hook)
                && hook is not null,
            ITemplateComponent template =>
                template.Arguments.Contains(name)
                && template.Arguments[name] is not null,
            _ => false,
        };
    }

    private static void InvokePendingTemplateNodeBeforeUpdateHook(
        MountedTree<TNode> tree,
        MountedTemplateNode<TNode>? mounted)
    {
        if (mounted?.PendingNodeLifecycleComponent is not { } next)
        {
            return;
        }

        ITemplateComponent? previous =
            mounted.PreviousNodeLifecycleComponent;
        mounted.PendingNodeLifecycleComponent = null;
        mounted.PreviousNodeLifecycleComponent = null;
        InvokeComponentNodeLifecycleHook(
            tree,
            mounted.Owner,
            next,
            previous,
            "onVnodeBeforeUpdate");
    }

    private static void InvokeComponentNodeLifecycleHook(
        MountedTree<TNode> tree,
        ComponentContext? owner,
        IComponent component,
        IComponent? previousComponent,
        string name)
    {
        try
        {
            ComponentNodeLifecycleHook? hook =
                GetComponentNodeLifecycleHook(component, name);
            if (hook is null)
            {
                return;
            }

            if (owner is null)
            {
                hook(component, previousComponent);
            }
            else
            {
                owner.Run(() => hook(component, previousComponent));
            }
        }
        catch (Exception exception)
        {
            if (owner is not null)
            {
                ComponentErrorHandling.Handle(
                    exception,
                    owner,
                    $"component node lifecycle hook \"{name}\"");
            }
            else if (tree.Application?.ErrorHandler is { } errorHandler)
            {
                errorHandler(
                    exception,
                    null,
                    $"component node lifecycle hook \"{name}\"");
            }
            else
            {
                throw;
            }
        }
    }

    private static void QueueComponentNodeLifecycleHook(
        MountedTree<TNode> tree,
        ComponentContext? owner,
        MountedRenderNode<TNode>? mounted,
        IComponent component,
        IComponent? previousComponent,
        string name)
    {
        if (!HasComponentNodeLifecycleHook(component, name))
        {
            return;
        }

        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (mounted is null || !mounted.IsUnmounted)
                    {
                        InvokeComponentNodeLifecycleHook(
                            tree,
                            owner,
                            component,
                            previousComponent,
                            name);
                    }
                })
            {
                Name = $"component node {name} lifecycle",
            });
    }

    private static string? ElementNamespace(string tag, string? parentNamespace)
    {
        if (string.Equals(tag, "svg", StringComparison.Ordinal))
        {
            return "svg";
        }

        if (string.Equals(tag, "math", StringComparison.Ordinal))
        {
            return "mathml";
        }

        return parentNamespace;
    }

    private static string? ChildrenNamespace(string tag, string? elementNamespace)
    {
        return string.Equals(elementNamespace, "svg", StringComparison.Ordinal)
            && string.Equals(tag, "foreignObject", StringComparison.Ordinal)
                ? null
                : elementNamespace;
    }

    private static List<DirectiveBinding> ResolveDirectiveBindings(
        MountedTree<TNode> tree,
        IReadOnlyList<IComponentDirectiveBinding> bindings,
        ComponentContext? owner,
        IReadOnlyList<DirectiveBinding>? previousBindings = null)
    {
        if (bindings.Count == 0)
        {
            return [];
        }

        IDirectiveResolver? resolver = tree.Application?.Directives;
        if (resolver is null)
        {
            if (tree.Application is null)
            {
                throw new InvalidOperationException(
                    "Directive-bearing components require an application context and directive "
                    + "resolver.");
            }

            tree.Application.WarnHandler?.Invoke(
                "Directive-bearing components were rendered without an application directive "
                + "resolver.");
            return [];
        }

        List<DirectiveBinding> resolved = new(bindings.Count);
        for (int index = 0; index < bindings.Count; index++)
        {
            IComponentDirectiveBinding binding = bindings[index];
            IDirective? directive = resolver.Resolve(binding.DirectiveName);
            if (directive is null)
            {
                tree.Application?.WarnHandler?.Invoke(
                    $"Failed to resolve directive \"{binding.DirectiveName}\".");
                continue;
            }

            object? previousValue = null;
            if (previousBindings is not null)
            {
                for (int previousIndex = 0;
                    previousIndex < previousBindings.Count;
                    previousIndex++)
                {
                    DirectiveBinding previous = previousBindings[previousIndex];
                    if (string.Equals(
                        previous.DirectiveName,
                        binding.DirectiveName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        previousValue = previous.Value;
                        break;
                    }
                }
            }

            resolved.Add(
                new DirectiveBinding(
                    binding.DirectiveName,
                    directive,
                    owner,
                    binding.Value,
                    previousValue,
                    binding.Argument,
                    binding.Modifiers));
        }

        return resolved;
    }

    private static void BindDirectiveHostElements(
        MountedElementNode<TNode> mounted,
        IReadOnlyList<DirectiveBinding> bindings)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            bindings[index].BindHostElements(
                tag => GetDirectiveHostElements(mounted.Children, tag));
        }
    }

    private static void BindDirectiveTransitions(
        IReadOnlyList<DirectiveBinding> bindings,
        TransitionHooks? transition)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            bindings[index].BindTransition(transition);
        }
    }

    private static IReadOnlyList<DirectiveHostElement> GetDirectiveHostElements(
        IReadOnlyList<MountedRenderNode<TNode>> children,
        string tag)
    {
        List<DirectiveHostElement> elements = [];
        for (int index = 0; index < children.Count; index++)
        {
            AddDirectiveHostElements(children[index], tag, elements);
        }

        return elements;
    }

    private static void AddDirectiveHostElements(
        MountedRenderNode<TNode> mounted,
        string tag,
        List<DirectiveHostElement> elements)
    {
        switch (mounted)
        {
            case MountedElementNode<TNode> element:
                IElementComponent component = RequireElement(element.Component);
                if (string.Equals(component.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    elements.Add(new DirectiveHostElement(component, element.HostNode));
                }

                for (int index = 0; index < element.Children.Count; index++)
                {
                    AddDirectiveHostElements(element.Children[index], tag, elements);
                }

                break;
            case MountedTemplateNode<TNode> template:
                AddDirectiveHostElements(template.Subtree, tag, elements);
                break;
            case MountedFragmentNode<TNode> fragment:
                for (int index = 0; index < fragment.Children.Count; index++)
                {
                    AddDirectiveHostElements(fragment.Children[index], tag, elements);
                }

                break;
        }
    }

    private static void InvokeDirectiveHooks(
        MountedTree<TNode> tree,
        TNode element,
        IReadOnlyList<DirectiveBinding> bindings,
        IElementComponent component,
        IElementComponent? previousComponent,
        DirectiveHookKind hookKind)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            DirectiveBinding binding = bindings[index];
            DirectiveHook? hook = hookKind switch
            {
                DirectiveHookKind.Created => binding.Directive.Created,
                DirectiveHookKind.BeforeMount => binding.Directive.BeforeMount,
                DirectiveHookKind.Mounted => binding.Directive.Mounted,
                DirectiveHookKind.BeforeUpdate => binding.Directive.BeforeUpdate,
                DirectiveHookKind.Updated => binding.Directive.Updated,
                DirectiveHookKind.BeforeUnmount => binding.Directive.BeforeUnmount,
                DirectiveHookKind.Unmounted => binding.Directive.Unmounted,
                _ => throw new InvalidOperationException(
                    $"Unknown directive hook kind: {hookKind}."),
            };
            if (hook is null)
            {
                continue;
            }

            try
            {
                hook(element, binding, component, previousComponent);
            }
            catch (Exception exception)
            {
                string diagnosticInformation =
                    $"{hookKind} directive lifecycle hook";
                if (binding.Context is ComponentContext owner)
                {
                    ComponentErrorHandling.Handle(
                        exception,
                        owner,
                        diagnosticInformation);
                }
                else if (tree.Application?.ErrorHandler is { } errorHandler)
                {
                    errorHandler(
                        exception,
                        binding.Context,
                        diagnosticInformation);
                }
                else
                {
                    throw;
                }
            }
        }
    }

    private static IElementComponent RequireElement(IComponent component)
    {
        return component as IElementComponent
            ?? throw InvalidSpecialization(component, nameof(IElementComponent));
    }

    private static ITemplateComponent RequireTemplate(IComponent component)
    {
        return component as ITemplateComponent
            ?? throw InvalidSpecialization(component, nameof(ITemplateComponent));
    }

    private static ITextComponent RequireText(IComponent component)
    {
        return component as ITextComponent
            ?? throw InvalidSpecialization(component, nameof(ITextComponent));
    }

    private static ICommentComponent RequireComment(IComponent component)
    {
        return component as ICommentComponent
            ?? throw InvalidSpecialization(component, nameof(ICommentComponent));
    }

    private static IStaticComponent RequireStatic(IComponent component)
    {
        return component as IStaticComponent
            ?? throw InvalidSpecialization(component, nameof(IStaticComponent));
    }

    private static IFragmentComponent RequireFragment(IComponent component)
    {
        return component as IFragmentComponent
            ?? throw InvalidSpecialization(component, nameof(IFragmentComponent));
    }

    private static ITeleportComponent RequireTeleport(IComponent component)
    {
        return component as ITeleportComponent
            ?? throw InvalidSpecialization(component, nameof(ITeleportComponent));
    }

    private static InvalidOperationException InvalidSpecialization(
        IComponent component,
        string contract)
    {
        return new InvalidOperationException(
            $"A {component.Kind} component must implement {contract}.");
    }

    private static NotSupportedException Unsupported(string componentKind)
    {
        return new NotSupportedException(
            $"{componentKind} components are not supported by the primitive renderer foundation.");
    }
}
