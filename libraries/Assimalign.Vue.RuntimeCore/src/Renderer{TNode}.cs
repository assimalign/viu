using System;
using System.Collections.Generic;

using Assimalign.Vue.Reactivity;
using Assimalign.Vue.Shared;

namespace Assimalign.Vue.RuntimeCore;

/// <summary>
/// The platform-agnostic mount/patch/unmount pipeline over injected node-ops — the C# port of
/// the renderer produced by <c>createRenderer</c> in <c>@vue/runtime-core</c>
/// (<c>packages/runtime-core/src/renderer.ts</c>, https://vuejs.org/api/custom-renderer.html).
/// The patch dispatcher routes by <see cref="VirtualNode.Type"/> and
/// <see cref="VirtualNode.ShapeFlag"/> to element, text, comment, static, fragment, and
/// component paths; mismatched node types unmount and remount; fragments mount between
/// start/end anchors; insertion honors the anchor throughout. Components mount through
/// <see cref="ComponentInstance"/>s with per-instance render effects whose scheduler jobs
/// carry the instance uid, so parents update before children ([V01.01.03.06]). A positive
/// <see cref="VirtualNode.PatchFlag"/> follows the compiled patch contract (targeted
/// class/style/props/text updates); unflagged vnodes take the full diff. Array children patch
/// positionally for now — keyed longest-increasing-subsequence reordering lands with
/// [V01.01.03.03]. Component slots ([V01.01.03.09]) are installed on the instance and their
/// <see cref="Shared.SlotFlags"/> stability drives whether a parent re-render forces the child.
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
            Patch(current, node, container, default, null, null);
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

    /// <summary>
    /// Creates the minimal application shell over this renderer (upstream:
    /// <c>createAppAPI(render)</c>; see <see cref="VueApplication{TNode}"/>).
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="rootProperties">Props for the root component, or null.</param>
    /// <returns>The app; mount it into a container.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public VueApplication<TNode> CreateApplication(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return new VueApplication<TNode>(this, rootComponent, rootProperties);
    }

    private void Patch(
        VirtualNode? current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
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
                ProcessFragment(current, next, container, anchor, elementNamespace, parentComponent);
                break;
            case VirtualNodeType.Element:
                ProcessElement(current, next, container, anchor, elementNamespace, parentComponent);
                break;
            case VirtualNodeType.Component:
                ProcessComponent(current, next, container, anchor, elementNamespace, parentComponent);
                break;
            default:
                throw new InvalidOperationException($"Unknown vnode type: {next.Type}.");
        }
    }

    private static bool IsSameVirtualNodeType(VirtualNode current, VirtualNode next)
        => current.Type == next.Type
            && Equals(current.Key, next.Key)
            && (next.Type != VirtualNodeType.Element
                || string.Equals(current.ElementTag, next.ElementTag, StringComparison.Ordinal))
            && (next.Type != VirtualNodeType.Component
                || ReferenceEquals(current.ComponentType, next.ComponentType));

    // --- text / comment / static ---------------------------------------------------------------

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

    // --- fragments -----------------------------------------------------------------------------

    private void ProcessFragment(
        VirtualNode? current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
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
            MountChildren(next.ArrayChildren ?? [], container, end, elementNamespace, parentComponent);
        }
        else
        {
            next.El = current.El;
            next.Anchor = current.Anchor;
            PatchChildren(current, next, container, (TNode)next.Anchor!, elementNamespace, parentComponent);
        }
    }

    // --- elements ------------------------------------------------------------------------------

    private void ProcessElement(
        VirtualNode? current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
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
            MountElement(next, container, anchor, elementNamespace, parentComponent);
        }
        else
        {
            PatchElement(current, next, elementNamespace, parentComponent);
        }
    }

    private void MountElement(VirtualNode node, TNode container, TNode? anchor, string? elementNamespace, ComponentInstance? parentComponent)
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
            MountChildren(node.ArrayChildren!, element, default, ChildrenNamespace(node, elementNamespace), parentComponent);
        }
        if (node.Properties is not null)
        {
            foreach (var (name, value) in node.Properties)
            {
                if (IsReservedProperty(name) || string.Equals(name, "value", StringComparison.Ordinal))
                {
                    continue;
                }
                _options.PatchProperty(element, node.ElementTag!, name, null, value, elementNamespace);
            }
            // "value" is patched last so it can depend on properties like <input type> and on
            // mounted <option> children (upstream parity).
            if (node.Properties.TryGetValue("value", out var valueProperty))
            {
                _options.PatchProperty(element, node.ElementTag!, "value", null, valueProperty, elementNamespace);
            }
        }
        InvokeHook(node, null, "onVnodeBeforeMount");
        _options.Insert(element, container, anchor);
        QueuePostHook(node, null, "onVnodeMounted");
    }

    private void PatchElement(VirtualNode current, VirtualNode next, string? elementNamespace, ComponentInstance? parentComponent)
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
                PatchProperties(element, next.ElementTag!, current.Properties, next.Properties, elementNamespace);
            }
            else
            {
                if ((next.PatchFlag & PatchFlags.Class) != 0)
                {
                    var previousClass = current.Properties?["class"];
                    var nextClass = next.Properties?["class"];
                    if (!Equals(previousClass, nextClass))
                    {
                        _options.PatchProperty(element, next.ElementTag!, "class", previousClass, nextClass, elementNamespace);
                    }
                }
                if ((next.PatchFlag & PatchFlags.Style) != 0)
                {
                    _options.PatchProperty(
                        element,
                        next.ElementTag!,
                        "style",
                        current.Properties?["style"],
                        next.Properties?["style"],
                        elementNamespace);
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
                            _options.PatchProperty(element, next.ElementTag!, name, previousValue, nextValue, elementNamespace);
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
            PatchProperties(element, next.ElementTag!, current.Properties, next.Properties, elementNamespace);
            PatchChildren(current, next, element, default, ChildrenNamespace(next, elementNamespace), parentComponent);
        }
        QueuePostHook(next, current, "onVnodeUpdated");
    }

    private void PatchProperties(TNode element, string elementTag, VirtualNodeProperties? previous, VirtualNodeProperties? next, string? elementNamespace)
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
                    _options.PatchProperty(element, elementTag, name, value, null, elementNamespace);
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
                    _options.PatchProperty(element, elementTag, name, previousValue, value, elementNamespace);
                }
            }
            // "value" is forced last: the live platform value can drift from the vnode value,
            // so equality against the previous vnode is not a safe skip (upstream parity).
            if (next.TryGetValue("value", out var nextValue))
            {
                object? previousBagValue = null;
                previous?.TryGetValue("value", out previousBagValue);
                _options.PatchProperty(element, elementTag, "value", previousBagValue, nextValue, elementNamespace);
            }
        }
    }

    // --- components ([V01.01.03.06]) -----------------------------------------------------------

    private void ProcessComponent(
        VirtualNode? current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
    {
        if (current is null)
        {
            MountComponent(next, container, anchor, elementNamespace, parentComponent);
        }
        else
        {
            UpdateComponent(current, next);
        }
    }

    private void MountComponent(VirtualNode next, TNode container, TNode? anchor, string? elementNamespace, ComponentInstance? parentComponent)
    {
        var definition = (IComponentDefinition)next.ComponentType!;
        var instance = new ComponentInstance(definition, next, parentComponent);
        next.Component = instance;
        ComponentPropertyResolution.Resolve(instance, next);
        ResolveSlots(instance, next);
        SetupComponent(instance);
        SetupComponentRenderEffect(instance, container, anchor, elementNamespace);
    }

    private static void SetupComponent(ComponentInstance instance)
    {
        // Setup runs exactly once per instance, with the instance current and inside its
        // effect scope so created effects/computeds stop with the component (upstream parity).
        var context = new ComponentSetupContext(instance);
        instance.PushCurrent();
        try
        {
            instance.RenderFunction = instance.Scope.Run(() => instance.Definition.Setup(instance.Properties, context));
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(exception, instance, "setup function");
        }
        finally
        {
            instance.PopCurrent();
        }
        instance.RenderFunction ??= static () => null;
    }

    private void SetupComponentRenderEffect(ComponentInstance instance, TNode container, TNode? anchor, string? elementNamespace)
    {
        // The per-instance render effect (upstream: setupRenderEffect): invalidation enqueues
        // a job carrying the instance uid, so parents flush before children; the effect
        // registers with the instance scope so teardown stops it.
        var job = new SchedulerJob(() => instance.Effect!.Run())
        {
            Identifier = instance.Uid,
            AllowRecurse = true,
            Name = instance.DisplayName,
        };
        instance.UpdateJob = job;
        instance.Scope.Run(() =>
        {
            instance.Effect = new ReactiveEffect(() => ComponentUpdateFunction(instance, container, anchor, elementNamespace))
            {
                AllowRecurse = true,
                Scheduler = () =>
                {
                    if (!instance.IsUnmounted)
                    {
                        Scheduler.QueueJob(job);
                    }
                },
            };
        });
        instance.Effect!.Run();
    }

    private void ComponentUpdateFunction(ComponentInstance instance, TNode container, TNode? anchor, string? elementNamespace)
    {
        if (instance.IsUnmounted)
        {
            return;
        }
        if (!instance.IsMounted)
        {
            // Upstream toggleRecurse: hooks run with self-triggering off, render with it on.
            instance.ToggleRecurse(false);
            instance.InvokeHooks(LifecycleHookKind.BeforeMount);
            instance.ToggleRecurse(true);
            var subtree = RenderComponentRoot(instance);
            instance.Subtree = subtree;
            Patch(null, subtree, container, anchor, elementNamespace, instance);
            instance.VirtualNode.El = subtree.El;
            instance.VirtualNode.Anchor = subtree.Anchor;
            instance.IsMounted = true;
            QueueInstanceHooks(instance, LifecycleHookKind.Mounted);
        }
        else
        {
            // A parent-driven update carries the pending vnode; props re-resolve with
            // self-triggering off so the write cannot requeue the running effect
            // (upstream: toggleRecurse(false) around updateComponentPreRender + beforeUpdate).
            instance.ToggleRecurse(false);
            var pending = instance.NextVirtualNode;
            if (pending is not null)
            {
                instance.NextVirtualNode = null;
                pending.Component = instance;
                instance.VirtualNode = pending;
                ComponentPropertyResolution.Resolve(instance, pending);
                ResolveSlots(instance, pending);
            }
            instance.InvokeHooks(LifecycleHookKind.BeforeUpdate);
            instance.ToggleRecurse(true);
            var previous = instance.Subtree!;
            var nextTree = RenderComponentRoot(instance);
            instance.Subtree = nextTree;
            var hostParent = _options.ParentNode((TNode)previous.El!);
            Patch(previous, nextTree, hostParent!, GetNextHostNode(previous), elementNamespace, instance);
            instance.VirtualNode.El = nextTree.El;
            instance.VirtualNode.Anchor = nextTree.Anchor;
            QueueInstanceHooks(instance, LifecycleHookKind.Updated);
        }
    }

    private static VirtualNode RenderComponentRoot(ComponentInstance instance)
    {
        // Upstream renderComponentRoot: run the render function with the instance current,
        // normalize, and apply single-element-root attrs fallthrough via mergeProps.
        VirtualNode? root = null;
        instance.PushCurrent();
        try
        {
            root = instance.RenderFunction!();
        }
        catch (Exception exception)
        {
            ComponentErrorHandling.Handle(exception, instance, "render function");
        }
        finally
        {
            instance.PopCurrent();
        }
        var normalized = VirtualNodeFactory.Normalize(root);
        if (instance.Definition.InheritAttributes
            && instance.Attributes.Count > 0
            && normalized.Type == VirtualNodeType.Element)
        {
            normalized = VirtualNodeFactory.Clone(normalized, instance.Attributes.ToProperties());
        }
        return normalized;
    }

    private void UpdateComponent(VirtualNode current, VirtualNode next)
    {
        var instance = (ComponentInstance)current.Component!;
        if (ShouldUpdateComponent(current, next))
        {
            // Cancel any queued reactive update and re-render synchronously with the pending
            // vnode (upstream: invalidateJob + instance.update()).
            instance.NextVirtualNode = next;
            Scheduler.InvalidateJob(instance.UpdateJob!);
            instance.Effect!.Run();
        }
        else
        {
            // Nothing relevant changed: adopt the new vnode without re-rendering.
            next.El = current.El;
            next.Anchor = current.Anchor;
            next.Component = instance;
            instance.VirtualNode = next;
        }
    }

    private static void ResolveSlots(ComponentInstance instance, VirtualNode vnode)
    {
        // Install the parent-provided slots on the instance (upstream: initSlots/updateSlots).
        // Runs at mount and on every update the component actually takes; when
        // ShouldUpdateComponent skips a stable-slots parent re-render, the child keeps its existing
        // slots, so the delegates it re-invokes stay the parent's latest committed ones.
        if ((vnode.ShapeFlag & ShapeFlags.SlotsChildren) != 0)
        {
            instance.Slots = vnode.SlotChildren;
        }
    }

    private static bool ShouldUpdateComponent(VirtualNode current, VirtualNode next)
    {
        // Upstream shouldUpdateComponent (packages/runtime-core/src/componentRenderUtils.ts).
        // Compiled dynamic slots (v-if/v-for/dynamic names in slot content) always force an update.
        if ((int)next.PatchFlag > 0 && (next.PatchFlag & PatchFlags.DynamicSlots) != 0)
        {
            return true;
        }
        // Non-stable slot children force a child update on any parent re-render so the child
        // reflects the parent's latest slot content (upstream: the !$stable branch). A FORWARDED
        // flag was already resolved to Stable/Dynamic at vnode creation, so only Stable is skipped.
        var previousSlots = current.SlotChildren;
        var nextSlots = next.SlotChildren;
        if ((previousSlots is not null || nextSlots is not null)
            && (nextSlots is null || nextSlots.Flag != SlotFlags.Stable))
        {
            return true;
        }
        var previousProperties = current.Properties;
        var nextProperties = next.Properties;
        if (ReferenceEquals(previousProperties, nextProperties))
        {
            return false;
        }
        if ((int)next.PatchFlag > 0
            && (next.PatchFlag & PatchFlags.Props) != 0
            && next.DynamicProperties is not null)
        {
            foreach (var name in next.DynamicProperties)
            {
                object? previousValue = null;
                object? nextValue = null;
                previousProperties?.TryGetValue(name, out previousValue);
                nextProperties?.TryGetValue(name, out nextValue);
                if (!Equals(previousValue, nextValue))
                {
                    return true;
                }
            }
            return false;
        }
        if (previousProperties is null)
        {
            return nextProperties is not null && nextProperties.Count > 0;
        }
        if (nextProperties is null)
        {
            return true;
        }
        if (previousProperties.Count != nextProperties.Count)
        {
            return true;
        }
        foreach (var (name, nextValue) in nextProperties)
        {
            if (!previousProperties.TryGetValue(name, out var previousValue) || !Equals(previousValue, nextValue))
            {
                return true;
            }
        }
        return false;
    }

    private void UnmountComponent(VirtualNode node, bool doRemove)
    {
        var instance = (ComponentInstance)node.Component!;
        // Teardown order (upstream parity): BeforeUnmount parent-first, then effects/scope,
        // then the subtree, then Unmounted queued post-flush (children queue theirs first, so
        // the stable post-flush order runs them child-first).
        instance.InvokeHooks(LifecycleHookKind.BeforeUnmount);
        if (instance.UpdateJob is not null)
        {
            instance.UpdateJob.IsDisposed = true;
        }
        instance.Scope.Stop();
        if (instance.Subtree is not null)
        {
            Unmount(instance.Subtree, doRemove);
        }
        instance.IsUnmounted = true;
        QueueInstanceHooks(instance, LifecycleHookKind.Unmounted);
    }

    private static void QueueInstanceHooks(ComponentInstance instance, LifecycleHookKind kind)
    {
        if (!instance.HasHooks(kind))
        {
            return;
        }
        // Post-flush phase (upstream: queuePostRenderEffect); stable ordering keeps
        // child-before-parent for Mounted/Unmounted.
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => instance.InvokeHooks(kind)));
    }

    // --- children ------------------------------------------------------------------------------

    private void PatchChildren(
        VirtualNode current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
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
                PatchUnkeyedChildren(current.ArrayChildren!, next.ArrayChildren!, container, anchor, elementNamespace, parentComponent);
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
                MountChildren(next.ArrayChildren!, container, anchor, elementNamespace, parentComponent);
            }
        }
    }

    private void PatchUnkeyedChildren(
        VirtualNode[] previousChildren,
        VirtualNode[] nextChildren,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
    {
        // Positional diff (upstream patchUnkeyedChildren). Keyed minimal-move reordering lands
        // with [V01.01.03.03]; a positional key mismatch still replaces correctly via the
        // same-type check in Patch.
        var commonLength = Math.Min(previousChildren.Length, nextChildren.Length);
        for (var index = 0; index < commonLength; index++)
        {
            var nextChild = nextChildren[index] = VirtualNodeFactory.Normalize(nextChildren[index]);
            Patch(previousChildren[index], nextChild, container, anchor, elementNamespace, parentComponent);
        }
        if (previousChildren.Length > nextChildren.Length)
        {
            UnmountChildren(previousChildren, doRemove: true, startIndex: commonLength);
        }
        else
        {
            MountChildren(nextChildren, container, anchor, elementNamespace, parentComponent, startIndex: commonLength);
        }
    }

    private void MountChildren(
        VirtualNode[] children,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent,
        int startIndex = 0)
    {
        for (var index = startIndex; index < children.Length; index++)
        {
            // Normalized in place so the array holds the mounted instances (upstream write-back
            // in mountChildren; cloning protects an already-mounted reused vnode's El).
            var child = children[index] = VirtualNodeFactory.Normalize(children[index]);
            Patch(null, child, container, anchor, elementNamespace, parentComponent);
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
            case VirtualNodeType.Component:
                UnmountComponent(node, doRemove);
                break;
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
        if (node.Type == VirtualNodeType.Component)
        {
            var instance = (ComponentInstance)node.Component!;
            return instance.Subtree is null ? default : GetNextHostNode(instance.Subtree);
        }
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
