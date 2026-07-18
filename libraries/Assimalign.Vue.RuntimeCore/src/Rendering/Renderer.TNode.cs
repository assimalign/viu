using System;
using System.Buffers;
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
/// class/style/props/text updates); unflagged vnodes take the full diff. Array children take the
/// keyed diff by default (head/tail sync then longest-increasing-subsequence minimal moves,
/// [V01.01.03.03]); an <see cref="PatchFlags.UnkeyedFragment"/> keeps the positional fast path.
/// Component slots ([V01.01.03.09]) are installed on the instance and their
/// <see cref="Shared.SlotFlags"/> stability drives whether a parent re-render forces the child.
/// Created through <see cref="RendererFactory.CreateRenderer{TNode}"/>.
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
/// <typeparam name="TNode">The platform node type; <c>default</c> means "no node".</typeparam>
public sealed class Renderer<TNode>
    where TNode : notnull
{
    private static readonly EqualityComparer<TNode> NodeComparer = EqualityComparer<TNode>.Default;

    /// <summary>
    /// Test seam: counts every <see cref="Patch"/> entry so a block-tree test can pin that a
    /// re-render of a tree with N static nodes and K dynamic bindings visits O(K) vnodes, not O(N)
    /// (issue [V01.01.03.15]). Reset it before the patch under test. Ambient static, single-threaded.
    /// </summary>
    internal static int PatchVisitCount;

    /// <summary>
    /// Test seam: counts every <see cref="Unmount"/> entry so a block-unmount test can pin that
    /// tearing down a tree of N static nodes with K dynamic descendants visits O(K) vnodes, not O(N) —
    /// the dynamicChildren fast path (issue [V01.01.03.15.01]). Reset it before the unmount under test.
    /// Ambient static, single-threaded.
    /// </summary>
    internal static int UnmountVisitCount;

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
                // Root unmount: no owning component instance for error routing (upstream: null).
                Unmount(current, parentComponent: null, doRemove: true);
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
    /// <c>createAppAPI(render)</c>; see <see cref="Application{TNode}"/>).
    /// </summary>
    /// <param name="rootComponent">The root component definition.</param>
    /// <param name="rootProperties">Props for the root component, or null.</param>
    /// <returns>The app; mount it into a container.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public Application<TNode> CreateApplication(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        return new Application<TNode>(this, rootComponent, rootProperties);
    }

    private void Patch(
        VirtualNode? current,
        VirtualNode next,
        TNode container,
        TNode? anchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
    {
        PatchVisitCount++;
        if (ReferenceEquals(current, next))
        {
            return;
        }
        if (current is not null && !IsSameVirtualNodeType(current, next))
        {
            // Mismatched type: unmount and remount in place (upstream parity).
            anchor = GetNextHostNode(current);
            Unmount(current, parentComponent, doRemove: true);
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
        // Set the template ref (upstream setRef call site: the end of patch). A mismatched-type
        // replacement already cleared the old ref through the unmount above (current is now null),
        // so only the new binding is applied here.
        if (next.Reference is { } reference)
        {
            SetReference(reference, current?.Reference, next, isUnmount: false, parentComponent);
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
            // Upstream processFragment routing: a stable fragment carrying a block tree patches only
            // its dynamic descendants (positionally, never the keyed diff); every other fragment —
            // keyed/unkeyed v-for, or a hand-written fragment — takes the full children diff. The
            // block container is the fragment's parent, not its anchor (fragments own no element).
            if ((int)next.PatchFlag > 0
                && (next.PatchFlag & PatchFlags.StableFragment) != 0
                && next.DynamicChildren is not null
                && current.DynamicChildren is not null)
            {
                PatchBlockChildren(current.DynamicChildren, next.DynamicChildren, container, elementNamespace, parentComponent);
            }
            else
            {
                PatchChildren(current, next, container, (TNode)next.Anchor!, elementNamespace, parentComponent);
            }
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
        // Directive 'created' fires after children and before props (upstream mountElement order).
        InvokeDirectiveHooks(node, null, DirectiveHookKind.Created);
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
        InvokeDirectiveHooks(node, null, DirectiveHookKind.BeforeMount);
        _options.Insert(element, container, anchor);
        QueuePostHook(node, null, "onVnodeMounted");
        // Directive 'mounted' runs post-flush, after the subtree is in the host (upstream:
        // queuePostRenderEffect), so children fire child-first via the stable post-flush order.
        QueuePostDirectiveHooks(node, null, DirectiveHookKind.Mounted);
    }

    private void PatchElement(VirtualNode current, VirtualNode next, string? elementNamespace, ComponentInstance? parentComponent)
    {
        next.El = current.El;
        var element = (TNode)next.El!;
        InvokeHook(next, current, "onVnodeBeforeUpdate");
        InvokeDirectiveHooks(next, current, DirectiveHookKind.BeforeUpdate);
        var patchFlag = (int)next.PatchFlag;
        if (next.DynamicChildren is not null)
        {
            // Block element: patch only the dynamic descendants the block collected — the static
            // subtree is skipped entirely (on WASM every skipped node is a skipped interop call).
            // The block still patches its OWN props through its patch flag; a flag of 0 means the
            // props are static, so ApplyPatchFlagProperties leaves them untouched (upstream parity:
            // the `else if (!optimized && dynamicChildren == null)` full-props branch is not taken).
            PatchBlockChildren(current.DynamicChildren ?? [], next.DynamicChildren, element, ChildrenNamespace(next, elementNamespace), parentComponent);
            ApplyPatchFlagProperties(element, current, next, elementNamespace);
        }
        else if (patchFlag > 0)
        {
            // Compiled leaf: only what the flags name can have changed; the children are static (or
            // the single dynamic text handled by the TEXT flag). Every skipped patchProp visit is a
            // skipped interop call on WASM.
            ApplyPatchFlagProperties(element, current, next, elementNamespace);
        }
        else
        {
            // Unoptimized (or Bail) and not a block: full props diff and full children diff.
            PatchProperties(element, next.ElementTag!, current.Properties, next.Properties, elementNamespace);
            PatchChildren(current, next, element, default, ChildrenNamespace(next, elementNamespace), parentComponent);
        }
        QueuePostHook(next, current, "onVnodeUpdated");
        QueuePostDirectiveHooks(next, current, DirectiveHookKind.Updated);
    }

    private void ApplyPatchFlagProperties(TNode element, VirtualNode current, VirtualNode next, string? elementNamespace)
    {
        // The compiled prop fast paths (upstream patchElement's patchFlag block): update only what
        // the flag names. FULL_PROPS forces a full prop-bag diff; otherwise class, style, and the
        // dynamicProps list are each visited independently; TEXT updates only the element text.
        // NEED_PATCH / NEED_HYDRATION carry no prop work here — refs, directives, and vnode hooks
        // are processed at their own call sites (documented N/A). A non-positive flag (a block with
        // static props) falls through untouched.
        var patchFlag = next.PatchFlag;
        if ((int)patchFlag <= 0)
        {
            return;
        }
        if ((patchFlag & PatchFlags.FullProps) != 0)
        {
            PatchProperties(element, next.ElementTag!, current.Properties, next.Properties, elementNamespace);
        }
        else
        {
            if ((patchFlag & PatchFlags.Class) != 0)
            {
                var previousClass = current.Properties?["class"];
                var nextClass = next.Properties?["class"];
                if (!Equals(previousClass, nextClass))
                {
                    _options.PatchProperty(element, next.ElementTag!, "class", previousClass, nextClass, elementNamespace);
                }
            }
            if ((patchFlag & PatchFlags.Style) != 0)
            {
                _options.PatchProperty(
                    element,
                    next.ElementTag!,
                    "style",
                    current.Properties?["style"],
                    next.Properties?["style"],
                    elementNamespace);
            }
            if ((patchFlag & PatchFlags.Props) != 0 && next.DynamicProperties is not null)
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
        if ((patchFlag & PatchFlags.Text) != 0)
        {
            if (!string.Equals(current.TextChildren, next.TextChildren, StringComparison.Ordinal))
            {
                _options.SetElementText(element, next.TextChildren ?? string.Empty);
            }
        }
    }

    private void PatchBlockChildren(
        IReadOnlyList<VirtualNode> oldChildren,
        IReadOnlyList<VirtualNode> newChildren,
        TNode fallbackContainer,
        string? elementNamespace,
        ComponentInstance? parentComponent)
    {
        // The C# port of patchBlockChildren (renderer.ts): patch the paired dynamic descendants the
        // block collected, never the static ones — so a tree of N static nodes with K dynamic
        // bindings costs O(K) patch visits. Each pair resolves its container the way upstream does:
        // the real host parent when the child may insert or move nodes (a fragment, a replaced type,
        // or a component), otherwise the block element itself — which the patch never reads for an
        // in-place prop/text update, so the parentNode call is skipped.
        for (var index = 0; index < newChildren.Count; index++)
        {
            var oldChild = oldChildren[index];
            var newChild = newChildren[index];
            var container =
                oldChild.El is not null
                && (oldChild.Type == VirtualNodeType.Fragment
                    || !IsSameVirtualNodeType(oldChild, newChild)
                    || oldChild.Type == VirtualNodeType.Component)
                    ? _options.ParentNode((TNode)oldChild.El)!
                    : fallbackContainer;
            Patch(oldChild, newChild, container, default, elementNamespace, parentComponent);
        }
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
        // Test-utilities stubbing ([V01.01.11.02]): a registered stub replaces the real child
        // definition here, so the stub's placeholder renders instead. Inert in production.
        if (parentComponent?.AppContext?.ResolveStub(definition) is { } stub)
        {
            definition = stub;
        }
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
            // Upstream renderComponentRoot clears the block stack in its catch (blockStack.length
            // = 0): a render that threw mid-block must not leak its open accumulator into later
            // renders when an ErrorCaptured hook or the app-level errorHandler swallows the error.
            BlockStack.ClearAfterRenderFailure();
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
        // Directives applied to the component vnode transfer onto its rendered root so the element
        // pipeline fires their hooks (upstream renderComponentRoot: root.dirs = root.dirs ?
        // root.dirs.concat(vnode.dirs) : vnode.dirs, with a dev warning on a non-element root).
        if (instance.VirtualNode.Directives is { } componentDirectives)
        {
            if (normalized.Type is not (VirtualNodeType.Element or VirtualNodeType.Component or VirtualNodeType.Comment))
            {
                RuntimeWarnings.Warn(
                    "Runtime directive used on component with non-element root node. The directives will not "
                    + "function as intended.");
            }
            // Clone a reused root before attaching so a shared/cached vnode is not corrupted.
            if (normalized.El is not null)
            {
                normalized = VirtualNodeFactory.Clone(normalized);
            }
            normalized.Directives = normalized.Directives is null
                ? componentDirectives
                : ConcatenateDirectives(normalized.Directives, componentDirectives);
        }
        return normalized;
    }

    private static List<DirectiveBinding> ConcatenateDirectives(List<DirectiveBinding> first, List<DirectiveBinding> second)
    {
        var combined = new List<DirectiveBinding>(first.Count + second.Count);
        combined.AddRange(first);
        combined.AddRange(second);
        return combined;
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
            // The subtree's owner is this instance (upstream: unmount(subTree, instance, ...)).
            Unmount(instance.Subtree, instance, doRemove);
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
                UnmountChildren(current.ArrayChildren!, parentComponent, doRemove: true);
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
                // Upstream patchChildren routing: only an explicit UnkeyedFragment flag keeps the
                // positional fast path; every other array-vs-array case takes the keyed diff
                // (upstream's default), which reconciles keyed and keyless children alike.
                if ((int)next.PatchFlag > 0 && (next.PatchFlag & PatchFlags.UnkeyedFragment) != 0)
                {
                    PatchUnkeyedChildren(current.ArrayChildren!, next.ArrayChildren!, container, anchor, elementNamespace, parentComponent);
                }
                else
                {
                    PatchKeyedChildren(current.ArrayChildren!, next.ArrayChildren!, container, anchor, elementNamespace, parentComponent);
                }
            }
            else
            {
                UnmountChildren(current.ArrayChildren!, parentComponent, doRemove: true);
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
            UnmountChildren(previousChildren, parentComponent, doRemove: true, startIndex: commonLength);
        }
        else
        {
            MountChildren(nextChildren, container, anchor, elementNamespace, parentComponent, startIndex: commonLength);
        }
    }

    private void PatchKeyedChildren(
        VirtualNode[] previousChildren,
        VirtualNode[] nextChildren,
        TNode container,
        TNode? parentAnchor,
        string? elementNamespace,
        ComponentInstance? parentComponent)
    {
        // The C# port of patchKeyedChildren in @vue/runtime-core renderer.ts
        // (https://github.com/vuejs/core/blob/main/packages/runtime-core/src/renderer.ts): head
        // sync, tail sync, common-sequence mount/unmount, then a key->new-index map plus a
        // longest-increasing-subsequence over the middle so the reorder issues the fewest host
        // moves — each avoided move is an avoided insert interop call on WASM.
        var i = 0;
        var l2 = nextChildren.Length;
        var e1 = previousChildren.Length - 1;
        var e2 = l2 - 1;

        // 1. sync from start: (a b) c / (a b) d e
        while (i <= e1 && i <= e2)
        {
            var n1 = previousChildren[i];
            var n2 = nextChildren[i] = VirtualNodeFactory.Normalize(nextChildren[i]);
            if (!IsSameVirtualNodeType(n1, n2))
            {
                break;
            }
            Patch(n1, n2, container, default, elementNamespace, parentComponent);
            i++;
        }

        // 2. sync from end: a (b c) / d e (b c)
        while (i <= e1 && i <= e2)
        {
            var n1 = previousChildren[e1];
            var n2 = nextChildren[e2] = VirtualNodeFactory.Normalize(nextChildren[e2]);
            if (!IsSameVirtualNodeType(n1, n2))
            {
                break;
            }
            Patch(n1, n2, container, default, elementNamespace, parentComponent);
            e1--;
            e2--;
        }

        if (i > e1)
        {
            // 3. common sequence + mount: the old run is exhausted, so mount the extra new nodes.
            if (i <= e2)
            {
                var nextPosition = e2 + 1;
                var anchor = nextPosition < l2 ? AsHostNode(nextChildren[nextPosition].El) : parentAnchor;
                while (i <= e2)
                {
                    var n2 = nextChildren[i] = VirtualNodeFactory.Normalize(nextChildren[i]);
                    Patch(null, n2, container, anchor, elementNamespace, parentComponent);
                    i++;
                }
            }
        }
        else if (i > e2)
        {
            // 4. common sequence + unmount: the new run is exhausted, so unmount the extra old nodes.
            while (i <= e1)
            {
                Unmount(previousChildren[i], parentComponent, doRemove: true);
                i++;
            }
        }
        else
        {
            // 5. unknown sequence: a b [c d e] f g -> a b [e d c h] f g.
            PatchUnknownSequence(
                previousChildren, nextChildren, container, parentAnchor, elementNamespace, parentComponent, i, l2, e1, e2);
        }
    }

    private void PatchUnknownSequence(
        VirtualNode[] previousChildren,
        VirtualNode[] nextChildren,
        TNode container,
        TNode? parentAnchor,
        string? elementNamespace,
        ComponentInstance? parentComponent,
        int start,
        int l2,
        int e1,
        int e2)
    {
        var s1 = start;
        var s2 = start;

        // 5.1 build a key->new-index map for the new-children middle, warning on duplicate keys
        // and on a keyed/keyless mix (both upstream dev warnings; the sink compiles out in release).
        var keyToNewIndexMap = new Dictionary<object, int>();
        var hasKeyed = false;
        var hasKeyless = false;
        for (var index = s2; index <= e2; index++)
        {
            var nextChild = nextChildren[index] = VirtualNodeFactory.Normalize(nextChildren[index]);
            if (nextChild.Key is not null)
            {
                hasKeyed = true;
                if (keyToNewIndexMap.ContainsKey(nextChild.Key))
                {
                    RuntimeWarnings.Warn(
                        $"Duplicate keys found during update: \"{nextChild.Key}\". Make sure keys are unique.");
                }
                keyToNewIndexMap[nextChild.Key] = index;
            }
            else if (nextChild.Type != VirtualNodeType.Comment)
            {
                hasKeyless = true;
            }
        }
        if (hasKeyed && hasKeyless)
        {
            RuntimeWarnings.Warn(
                "Mixed keyed and unkeyed children detected during update. Give every iterated child a "
                + "key (or none) so the keyed diff can track them reliably.");
        }

        // 5.2 patch matched old children, unmount the ones with no new counterpart, and record each
        // matched new slot's old index (offset by +1; 0 marks a brand-new node).
        var toBePatched = e2 - s2 + 1;
        var newIndexToOldIndexMap = ArrayPool<int>.Shared.Rent(toBePatched);
        int[]? sequence = null;
        try
        {
            Array.Clear(newIndexToOldIndexMap, 0, toBePatched);
            var patched = 0;
            var moved = false;
            var maxNewIndexSoFar = 0;
            for (var index = s1; index <= e1; index++)
            {
                var previousChild = previousChildren[index];
                if (patched >= toBePatched)
                {
                    // Every new node is already matched, so any remaining old node is a removal.
                    Unmount(previousChild, parentComponent, doRemove: true);
                    continue;
                }
                var newIndex = -1;
                if (previousChild.Key is not null)
                {
                    if (keyToNewIndexMap.TryGetValue(previousChild.Key, out var mapped))
                    {
                        newIndex = mapped;
                    }
                }
                else
                {
                    // Keyless: match the first same-type new node not already claimed.
                    for (var j = s2; j <= e2; j++)
                    {
                        if (newIndexToOldIndexMap[j - s2] == 0 && IsSameVirtualNodeType(previousChild, nextChildren[j]))
                        {
                            newIndex = j;
                            break;
                        }
                    }
                }
                if (newIndex < 0)
                {
                    Unmount(previousChild, parentComponent, doRemove: true);
                }
                else
                {
                    newIndexToOldIndexMap[newIndex - s2] = index + 1;
                    if (newIndex >= maxNewIndexSoFar)
                    {
                        maxNewIndexSoFar = newIndex;
                    }
                    else
                    {
                        moved = true;
                    }
                    Patch(previousChild, nextChildren[newIndex], container, default, elementNamespace, parentComponent);
                    patched++;
                }
            }

            // 5.3 walk the middle back-to-front, mounting new nodes and moving only the reused nodes
            // that fall outside the longest increasing subsequence — the minimal set of host moves.
            var sequenceLength = 0;
            if (moved)
            {
                sequence = ArrayPool<int>.Shared.Rent(toBePatched);
                sequenceLength = GetSequence(newIndexToOldIndexMap, toBePatched, sequence);
            }
            var stableCursor = sequenceLength - 1;
            for (var index = toBePatched - 1; index >= 0; index--)
            {
                var nextChildIndex = s2 + index;
                var nextChild = nextChildren[nextChildIndex];
                var anchor = nextChildIndex + 1 < l2 ? AsHostNode(nextChildren[nextChildIndex + 1].El) : parentAnchor;
                if (newIndexToOldIndexMap[index] == 0)
                {
                    // A new node: mount it before the next child's host node.
                    Patch(null, nextChild, container, anchor, elementNamespace, parentComponent);
                }
                else if (moved)
                {
                    // No stable subsequence (e.g. a full reverse) or this node is not part of it →
                    // move it; otherwise it is already in place, so advance the stable cursor.
                    if (stableCursor < 0 || index != sequence![stableCursor])
                    {
                        Move(nextChild, container, anchor);
                    }
                    else
                    {
                        stableCursor--;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(newIndexToOldIndexMap);
            if (sequence is not null)
            {
                ArrayPool<int>.Shared.Return(sequence);
            }
        }
    }

    private void Move(VirtualNode node, TNode container, TNode? anchor)
    {
        // The C# port of move() in renderer.ts: relocate an already-mounted vnode's host node(s)
        // before `anchor`. Components move their subtree; fragments and static nodes move their
        // whole owned range; element/text/comment nodes move as a single host node (descendants
        // travel with them).
        switch (node.Type)
        {
            case VirtualNodeType.Component:
                Move(((ComponentInstance)node.Component!).Subtree!, container, anchor);
                return;
            case VirtualNodeType.Fragment:
                _options.Insert((TNode)node.El!, container, anchor);
                var fragmentChildren = node.ArrayChildren!;
                for (var index = 0; index < fragmentChildren.Length; index++)
                {
                    Move(fragmentChildren[index], container, anchor);
                }
                _options.Insert((TNode)node.Anchor!, container, anchor);
                return;
            case VirtualNodeType.Static:
                MoveStaticNode(node, container, anchor);
                return;
            default:
                _options.Insert((TNode)node.El!, container, anchor);
                return;
        }
    }

    private void MoveStaticNode(VirtualNode node, TNode container, TNode? anchor)
    {
        // Move every host node from the start through the end anchor inclusive (upstream:
        // moveStaticNode); the next sibling is captured before each move mutates the sibling links.
        var currentNode = (TNode)node.El!;
        var endNode = (TNode)node.Anchor!;
        while (!NodeComparer.Equals(currentNode, endNode))
        {
            var nextNode = _options.NextSibling(currentNode);
            _options.Insert(currentNode, container, anchor);
            currentNode = nextNode!;
        }
        _options.Insert(endNode, container, anchor);
    }

    private static int GetSequence(int[] source, int length, int[] result)
    {
        // Longest increasing subsequence — the C# port of getSequence in renderer.ts. Fills
        // `result[0..count)` with indices into `source` (0..length) forming an increasing
        // subsequence of maximal length, skipping zero entries (brand-new nodes). `result` must
        // have length >= `length`; `predecessors` is a rented scratch buffer (upstream's `p`).
        var predecessors = ArrayPool<int>.Shared.Rent(length);
        try
        {
            Array.Copy(source, predecessors, length);
            result[0] = 0;
            var resultLength = 1;
            for (var i = 0; i < length; i++)
            {
                var value = source[i];
                if (value == 0)
                {
                    continue;
                }
                var last = result[resultLength - 1];
                if (source[last] < value)
                {
                    predecessors[i] = last;
                    result[resultLength++] = i;
                    continue;
                }
                var low = 0;
                var high = resultLength - 1;
                while (low < high)
                {
                    var middle = (low + high) >> 1;
                    if (source[result[middle]] < value)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }
                if (value < source[result[low]])
                {
                    if (low > 0)
                    {
                        predecessors[i] = result[low - 1];
                    }
                    result[low] = i;
                }
            }
            var cursor = resultLength;
            var predecessor = result[cursor - 1];
            while (cursor-- > 0)
            {
                result[cursor] = predecessor;
                predecessor = predecessors[predecessor];
            }
            return resultLength;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(predecessors);
        }
    }

    private static TNode? AsHostNode(object? node) => node is null ? default : (TNode)node;

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

    private void UnmountChildren(
        IReadOnlyList<VirtualNode> children,
        ComponentInstance? parentComponent,
        bool doRemove,
        bool optimized = false,
        int startIndex = 0)
    {
        // The C# port of unmountChildren (renderer.ts): tear down each child, threading the owner so a
        // throwing function template-ref routes through its error-capture chain, and the optimized flag
        // so a block's dynamic descendants do not re-walk their own static children.
        for (var index = startIndex; index < children.Count; index++)
        {
            Unmount(children[index], parentComponent, doRemove, optimized);
        }
    }

    private void Unmount(VirtualNode node, ComponentInstance? parentComponent, bool doRemove, bool optimized = false)
    {
        UnmountVisitCount++;
        // A Bail vnode forces the full, unoptimized children walk on teardown (upstream unmount:
        // `if (patchFlag === PatchFlags.BAIL) optimized = false`).
        if (node.PatchFlag == PatchFlags.Bail)
        {
            optimized = false;
        }
        // Unset any template ref first (upstream unmount order: setRef with isUnmount before the
        // node's teardown hooks). parentComponent is the owner, so a throwing function ref routes
        // through its error-capture chain instead of surfacing to the host ([V01.01.03.15.01]).
        if (node.Reference is { } reference)
        {
            SetReference(reference, oldReference: null, node, isUnmount: true, parentComponent);
        }
        // Cleanup order (upstream parity): hooks and child teardown run before node removal.
        InvokeHook(node, null, "onVnodeBeforeUnmount");
        // Directive teardown fires only for element vnodes (upstream: shouldInvokeDirs = ELEMENT &&
        // dirs); a component's transferred directives fire through its root element's unmount.
        if (node.Type == VirtualNodeType.Element)
        {
            InvokeDirectiveHooks(node, null, DirectiveHookKind.BeforeUnmount);
        }
        if (node.Type == VirtualNodeType.Component)
        {
            UnmountComponent(node, doRemove);
        }
        else
        {
            var dynamicChildren = node.DynamicChildren;
            if (dynamicChildren is not null
                && !node.HasOnce
                && (node.Type != VirtualNodeType.Fragment
                    || ((int)node.PatchFlag > 0 && (node.PatchFlag & PatchFlags.StableFragment) != 0)))
            {
                // Fast path for block nodes: tear down only the collected dynamic descendants — the
                // static subtree leaves with the host removal below, so on WASM every skipped visit is
                // a skipped interop round-trip. Their own static children are not re-walked
                // (optimized: true). A v-once block (HasOnce) and a non-stable (v-for) fragment are
                // excluded because their teardown-relevant descendants are absent from DynamicChildren
                // (upstream #5154 / #1153).
                UnmountChildren(dynamicChildren, parentComponent, doRemove: false, optimized: true);
            }
            else if ((node.Type == VirtualNodeType.Fragment
                    && (int)node.PatchFlag > 0
                    && (node.PatchFlag & (PatchFlags.KeyedFragment | PatchFlags.UnkeyedFragment)) != 0)
                || (!optimized && (node.ShapeFlag & ShapeFlags.ArrayChildren) != 0))
            {
                // Full walk: a keyed/unkeyed v-for fragment, or any unoptimized array-children vnode,
                // visits every child so their teardown hooks and refs fire. Their platform nodes leave
                // with the range/host removal below (doRemove: false here). The `> 0` gate keeps the
                // fragment test off the negative Bail/Cached sentinels (repo PatchFlags convention).
                UnmountChildren(node.ArrayChildren ?? [], parentComponent, doRemove: false);
            }
            if (doRemove)
            {
                switch (node.Type)
                {
                    case VirtualNodeType.Fragment:
                        // One anchored walk removes the fragment's whole owned range.
                        RemoveFragment(node);
                        break;
                    case VirtualNodeType.Static:
                        RemoveStaticNode(node);
                        break;
                    default:
                        // Element/Text/Comment: a single host removal takes the node and, for an
                        // element, its descendants' platform nodes with it.
                        if (node.El is not null)
                        {
                            _options.Remove((TNode)node.El);
                        }
                        break;
                }
            }
        }
        QueuePostHook(node, null, "onVnodeUnmounted");
        if (node.Type == VirtualNodeType.Element)
        {
            QueuePostDirectiveHooks(node, null, DirectiveHookKind.Unmounted);
        }
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

    // --- template refs ([V01.01.03.14]) --------------------------------------------------------

    private static void SetReference(
        TemplateReference reference,
        TemplateReference? oldReference,
        VirtualNode vnode,
        bool isUnmount,
        ComponentInstance? owner)
    {
        // The C# port of setRef (rendererTemplateRef.ts): the applied value is the mounted element,
        // or a component's exposed surface, or null on unmount.
        var value = isUnmount ? null : ResolveReferenceValue(vnode);
        // Unset the previous ref-object when the binding changed to a different ref (upstream's
        // "unset old ref" block): only a ref-object is nulled; a changed function old is simply
        // dropped, never invoked with null (upstream parity).
        if (oldReference is { } previous && previous != reference && previous.ReferenceObject is { } previousObject)
        {
            previousObject.Value = null;
        }
        if (value is not null)
        {
            // #1789: a non-null value is applied after render, id -1 so the ref is populated before
            // user mounted/updated hooks observe it (upstream: doSet.id = -1; queuePostRenderEffect).
            // Deviates from upstream Vue 3 parity per the #30 acceptance criteria: upstream invokes a
            // function ref synchronously inside setRef, but Vuecs defers it here like a ref-object so
            // that no template ref is ever applied synchronously mid-patch ("never synchronously
            // mid-patch"). Both ref kinds therefore observe identical, post-flush timing.
            var applied = value;
            Scheduler.QueuePostFlushCallback(
                new SchedulerJob(() => ApplyReference(reference, applied, owner)) { Identifier = -1 });
        }
        else
        {
            // Unmount (or a falsy value): applied synchronously (upstream doSet()).
            ApplyReference(reference, null, owner);
        }
    }

    private static void ApplyReference(TemplateReference reference, object? value, ComponentInstance? owner)
    {
        if (reference.ReferenceObject is { } referenceObject)
        {
            referenceObject.Value = value;
        }
        else if (reference.Function is { } function)
        {
            // A function ref (the v-for collection pattern) can run arbitrary user code; route its
            // exceptions through the owner's error-capture chain (upstream: callWithErrorHandling
            // with ErrorCodes.FUNCTION_REF) so one throwing ref cannot abandon the flush.
            try
            {
                function(value);
            }
            catch (Exception exception)
            {
                ComponentErrorHandling.Handle(exception, owner, "template ref function");
            }
        }
    }

    private static object? ResolveReferenceValue(VirtualNode vnode)
    {
        // Upstream getComponentPublicInstance: a component ref receives the exposed surface, else the
        // public instance. Vuecs has no public instance proxy, so the ComponentInstance itself stands
        // in for instance.proxy (documented deviation: the fallback surface is the instance). An
        // element ref receives the platform node.
        if (vnode.Type == VirtualNodeType.Component && vnode.Component is ComponentInstance instance)
        {
            return instance.Exposed ?? instance;
        }
        return vnode.El;
    }

    // --- directive hooks ([V01.01.03.13]) ------------------------------------------------------

    private static void InvokeDirectiveHooks(VirtualNode node, VirtualNode? previousNode, DirectiveHookKind kind)
    {
        // Hot path: a vnode with no directives costs exactly this null check (upstream: if (dirs)).
        var bindings = node.Directives;
        if (bindings is null)
        {
            return;
        }
        // The C# port of invokeDirectiveHook (directives.ts): update hooks refresh oldValue from
        // the previous vnode's binding at the same index, then each defined hook is invoked with
        // (el, binding, vnode, prevVnode); an exception routes through error handling with the
        // directive-hook info code and does not abort the remaining bindings or the patch pipeline.
        var oldBindings = previousNode?.Directives;
        for (var index = 0; index < bindings.Count; index++)
        {
            var binding = bindings[index];
            if (oldBindings is not null && index < oldBindings.Count)
            {
                binding.OldValue = oldBindings[index].Value;
            }
            var hook = SelectDirectiveHook(binding.Directive, kind);
            if (hook is not null)
            {
                try
                {
                    hook(node.El, binding, node, previousNode);
                }
                catch (Exception exception)
                {
                    ComponentErrorHandling.Handle(exception, binding.Instance, "directive hook");
                }
            }
        }
    }

    private static void QueuePostDirectiveHooks(VirtualNode node, VirtualNode? previousNode, DirectiveHookKind kind)
    {
        if (node.Directives is null)
        {
            return;
        }
        // Mounted/updated/unmounted directive hooks run post-flush like lifecycle hooks, keeping
        // the same child-before-parent order via the stable post-flush queue.
        Scheduler.QueuePostFlushCallback(new SchedulerJob(() => InvokeDirectiveHooks(node, previousNode, kind)));
    }

    private static DirectiveHook? SelectDirectiveHook(IDirective directive, DirectiveHookKind kind) => kind switch
    {
        DirectiveHookKind.Created => directive.Created,
        DirectiveHookKind.BeforeMount => directive.BeforeMount,
        DirectiveHookKind.Mounted => directive.Mounted,
        DirectiveHookKind.BeforeUpdate => directive.BeforeUpdate,
        DirectiveHookKind.Updated => directive.Updated,
        DirectiveHookKind.BeforeUnmount => directive.BeforeUnmount,
        _ => directive.Unmounted,
    };
}
