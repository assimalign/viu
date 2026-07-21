using System;
using System.Collections.Generic;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The DOM <c>&lt;TransitionGroup&gt;</c> built-in — the C# port of upstream's <c>TransitionGroup</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/components/TransitionGroup.ts,
/// https://vuejs.org/guide/built-ins/transition-group.html). It renders a list of keyed children under
/// a real <c>tag</c> (or a fragment), stamps each child with the same CSS-class enter/leave hooks
/// <see cref="Transition"/> resolves, and animates reordering with a FLIP move: after each update it
/// snapshots child positions (before via the render, after via <c>onUpdated</c>), applies an inverting
/// transform to every element that moved, forces a reflow, adds the <c>v-move</c> class, and removes it
/// on <c>transitionend</c>.
/// <para>
/// The position snapshots and the transform writes go through <see cref="DomTransitionOperations"/> in
/// the exact order upstream uses (finish pending callbacks, read all positions, then write all
/// transforms) so the read pass and the write pass never interleave — the documented synchronous layout
/// seam the bridge-backed implementation fulfils. Because a handle platform crosses the interop boundary
/// per read, the read pass is <b>batched</b>: <see cref="DomTransitionOperations.MeasurePositions"/> reads
/// every child's rectangle in one crossing, so a reorder of N children costs one crossing per pass, not N
/// ([V01.01.04.07.03]). Referenced by the compiled render through
/// <see cref="DomRenderHelpers._TransitionGroup"/>. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// <para>
/// Attribute fallthrough rides the standard single-root mechanism, not a parallel one ([V01.01.04.07.04]):
/// <see cref="Properties"/> declares <c>tag</c>/<c>moveClass</c> and every transition prop, so those are
/// consumed rather than emitted onto the wrapper, while <c>class</c>/<c>style</c>/arbitrary attributes —
/// everything undeclared — fall through onto the rendered <c>tag</c> element through
/// <c>renderComponentRoot</c>'s <c>mergeProps</c> (upstream
/// <c>packages/runtime-core/src/componentAttrs.ts</c>, as applied by <c>TransitionGroup.ts</c>'s
/// <c>createVNode(tag, null, children)</c>). The wrapper's fallthrough <c>class</c> and the children's
/// enter/leave/<c>*-move</c> choreography classes land on separate elements, so neither contaminates the
/// other. In fragment mode (no <c>tag</c>) the root is not an element, so there is no fallthrough target —
/// the undeclared attributes are silently dropped with no warning, exactly as the shared mechanism treats
/// any fragment/text root.
/// </para>
/// </summary>
public sealed class TransitionGroup : IComponentDefinition
{
    /// <summary>The shared component instance the compiled render references via <see cref="DomRenderHelpers._TransitionGroup"/>.</summary>
    public static readonly TransitionGroup Instance = new();

    // The declared props — upstream TransitionGroup's props are TransitionPropsValidators plus tag and
    // moveClass (packages/runtime-dom/src/components/TransitionGroup.ts). Declaring the full set is what
    // makes class/style/arbitrary attributes — everything NOT here — fall through onto the rendered tag
    // element via the standard single-root attrs merge (renderComponentRoot + mergeProps), while
    // tag/moveClass and every transition prop are consumed and never leak onto the wrapper. This is
    // BaseTransitionPropsValidators + DOMTransitionPropsValidators (the same set Transition consumes,
    // read from the raw vnode by ResolveTransitionProperties) plus the two group props.
    private static readonly IReadOnlyList<ComponentPropertyDefinition> DeclaredProperties =
    [
        // Group wrapper props (upstream: { tag: String, moveClass: String }).
        new ComponentPropertyDefinition("tag"),
        new ComponentPropertyDefinition("moveClass"),
        // BaseTransition props (upstream BaseTransitionPropsValidators).
        new ComponentPropertyDefinition("mode"),
        new ComponentPropertyDefinition("appear"),
        new ComponentPropertyDefinition("persisted"),
        new ComponentPropertyDefinition("onBeforeEnter"),
        new ComponentPropertyDefinition("onEnter"),
        new ComponentPropertyDefinition("onAfterEnter"),
        new ComponentPropertyDefinition("onEnterCancelled"),
        new ComponentPropertyDefinition("onBeforeLeave"),
        new ComponentPropertyDefinition("onLeave"),
        new ComponentPropertyDefinition("onAfterLeave"),
        new ComponentPropertyDefinition("onLeaveCancelled"),
        new ComponentPropertyDefinition("onBeforeAppear"),
        new ComponentPropertyDefinition("onAppear"),
        new ComponentPropertyDefinition("onAfterAppear"),
        new ComponentPropertyDefinition("onAppearCancelled"),
        // DOM transition props (upstream DOMTransitionPropsValidators).
        new ComponentPropertyDefinition("name"),
        new ComponentPropertyDefinition("type"),
        new ComponentPropertyDefinition("css"),
        new ComponentPropertyDefinition("duration"),
        new ComponentPropertyDefinition("enterFromClass"),
        new ComponentPropertyDefinition("enterActiveClass"),
        new ComponentPropertyDefinition("enterToClass"),
        new ComponentPropertyDefinition("appearFromClass"),
        new ComponentPropertyDefinition("appearActiveClass"),
        new ComponentPropertyDefinition("appearToClass"),
        new ComponentPropertyDefinition("leaveFromClass"),
        new ComponentPropertyDefinition("leaveActiveClass"),
        new ComponentPropertyDefinition("leaveToClass"),
    ];

    private TransitionGroup()
    {
    }

    /// <inheritdoc/>
    public string? Name => "TransitionGroup";

    /// <summary>
    /// The declared props — <c>tag</c>/<c>moveClass</c> and the full transition prop set (upstream:
    /// <c>extend({}, TransitionPropsValidators, { tag, moveClass })</c> in
    /// <c>packages/runtime-dom/src/components/TransitionGroup.ts</c>). Declaring them keeps every one out
    /// of the fallthrough attrs, so only <c>class</c>/<c>style</c>/arbitrary attributes fall through onto
    /// the rendered <c>tag</c> element. <see cref="IComponentDefinition.InheritAttributes"/> stays at its
    /// default (true) — unlike <c>Transition</c>/<c>KeepAlive</c>, the group owns a real root element to
    /// inherit onto — so the standard single-root merge lands them (upstream never sets
    /// <c>inheritAttrs: false</c> on TransitionGroup).
    /// </summary>
    public IReadOnlyList<ComponentPropertyDefinition>? Properties => DeclaredProperties;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var instance = ComponentInstance.Current!;
        var state = BaseTransition.UseTransitionState();
        var positionMap = new Dictionary<VirtualNode, TransitionRectangle>();
        var moveCallbacks = new Dictionary<int, Action>();
        // Captured per setup call: the render updates them before the patch, so onUpdated (post-patch)
        // reads the correct outgoing set.
        List<VirtualNode>? previousChildren = null;
        List<VirtualNode>? currentChildren = null;

        Lifecycle.OnUpdated(() => RunFlipMove(instance, state, positionMap, moveCallbacks, previousChildren));

        return () =>
        {
            var raw = instance.VirtualNode.Properties;
            var resolved = Transition.ResolveTransitionProperties(raw);
            var tag = ReadString(raw, "tag");
            previousChildren = currentChildren;
            var children = GetGroupChildren(context);
            currentChildren = children;

            // Stamp per-item hooks on keyed children (upstream: setTransitionHooks for child.key != null).
            foreach (var child in children)
            {
                if (child.Key is not null)
                {
                    BaseTransition.SetTransitionHooks(
                        child,
                        BaseTransition.ResolveTransitionHooks(child, resolved, state, instance, null));
                }
            }

            // Snapshot the outgoing children's pre-patch positions and refresh their hooks (upstream:
            // the prevChildren loop recording positionMap). The FLIP delta reads these in onUpdated. The
            // whole snapshot is one batched read crossing rather than one per child ([V01.01.04.07.03]).
            if (previousChildren is not null)
            {
                positionMap.Clear();
                var operations = DomTransitionOperations.Current;
                var measured = new List<VirtualNode>(previousChildren.Count);
                foreach (var child in previousChildren)
                {
                    BaseTransition.SetTransitionHooks(
                        child,
                        BaseTransition.ResolveTransitionHooks(child, resolved, state, instance, null));
                    if (operations is not null && child.El is not null)
                    {
                        measured.Add(child);
                    }
                }
                if (operations is not null && measured.Count > 0)
                {
                    var rectangles = operations.MeasurePositions(HandlesOf(measured));
                    for (var index = 0; index < measured.Count; index++)
                    {
                        positionMap[measured[index]] = rectangles[index];
                    }
                }
            }

            var childArray = children.ToArray();
            return tag is not null
                ? VirtualNodeFactory.Element(tag, null, childArray)
                : VirtualNodeFactory.Fragment(childArray, null);
        };
    }

    private static void RunFlipMove(
        ComponentInstance instance,
        TransitionState state,
        Dictionary<VirtualNode, TransitionRectangle> positionMap,
        Dictionary<int, Action> moveCallbacks,
        List<VirtualNode>? previousChildren)
    {
        if (previousChildren is null || previousChildren.Count == 0)
        {
            return;
        }
        var operations = DomTransitionOperations.Current;
        if (operations is null)
        {
            return;
        }
        var raw = instance.VirtualNode.Properties;
        var moveClass = ReadString(raw, "moveClass") ?? (ReadString(raw, "name") ?? "v") + "-move";

        // hasCSSTransform gate: measure once against a clone to skip the whole FLIP when the move class
        // adds no transform transition (upstream), avoiding pointless writes.
        if (previousChildren[0].El is not { } firstElement
            || (instance.VirtualNode.El ?? instance.Subtree?.El) is not { } rootElement
            || !operations.HasCssTransform((int)firstElement, (int)rootElement, moveClass))
        {
            return;
        }

        // No interleaving of reads and writes (upstream comment: prevent layout thrashing), and the reads
        // and the whole write frame are each one interop crossing ([V01.01.04.07.03]).
        // 1. finish any pending move/enter callbacks so a re-triggered FLIP measures settled positions.
        foreach (var child in previousChildren)
        {
            CallPendingCallbacks(state, moveCallbacks, child);
        }
        // 2. read every new position in ONE batched crossing (upstream: recordPosition per child, in a
        //    same-process JS loop; here the whole pass is a single boundary crossing).
        var measured = new List<VirtualNode>(previousChildren.Count);
        foreach (var child in previousChildren)
        {
            if (child.El is not null)
            {
                measured.Add(child);
            }
        }
        var newPositions = measured.Count > 0
            ? operations.MeasurePositions(HandlesOf(measured))
            : [];
        // 3. write the inverting transforms for the children that moved (upstream applyTranslation).
        var moved = new List<VirtualNode>();
        for (var index = 0; index < measured.Count; index++)
        {
            var child = measured[index];
            if (!positionMap.TryGetValue(child, out var oldPosition))
            {
                continue;
            }
            var newPosition = newPositions[index];
            var deltaX = oldPosition.Left - newPosition.Left;
            var deltaY = oldPosition.Top - newPosition.Top;
            if (deltaX != 0 || deltaY != 0)
            {
                operations.SetMoveTransform((int)child.El!, deltaX, deltaY);
                moved.Add(child);
            }
        }

        // 4. force everything into position (upstream forceReflow, once), then add the move class and clear
        //    the inverse transform so each moved child animates home. In buffered mode steps 3-4 are one
        //    command-buffer frame: transforms, the reflow barrier, then move class + clear, in this order.
        operations.ForceReflow();
        foreach (var child in moved)
        {
            var element = (int)child.El!;
            operations.AddTransitionClass(element, moveClass);
            operations.ClearMoveStyles(element);
        }
        // 5. register the move-end cleanup AFTER the write frame — the buffered WhenMoveEnds flushes the
        //    frame committed in steps 3-4 as one crossing, then attaches the transitionend listener.
        foreach (var child in moved)
        {
            var element = (int)child.El!;
            void MoveDone()
            {
                if (!moveCallbacks.Remove(element))
                {
                    return;
                }
                operations.RemoveTransitionClass(element, moveClass);
            }
            moveCallbacks[element] = MoveDone;
            operations.WhenMoveEnds(element, MoveDone);
        }
    }

    // Projects a child list to the int element handles the batched read/FLIP ops address them by.
    private static int[] HandlesOf(List<VirtualNode> children)
    {
        var handles = new int[children.Count];
        for (var index = 0; index < children.Count; index++)
        {
            handles[index] = (int)children[index].El!;
        }
        return handles;
    }

    // Upstream callPendingCbs: force-finish an in-flight move (moveCbKey) and enter (enterCbKey) so the
    // element is measured at its settled position rather than mid-animation.
    private static void CallPendingCallbacks(
        TransitionState state,
        Dictionary<int, Action> moveCallbacks,
        VirtualNode child)
    {
        if (child.El is not { } element)
        {
            return;
        }
        if (moveCallbacks.TryGetValue((int)element, out var moveDone))
        {
            moveDone();
        }
        if (state.EnterCallbacks.TryGetValue(element, out var enterDone))
        {
            enterDone(false);
        }
    }

    private static List<VirtualNode> GetGroupChildren(ComponentSetupContext context)
    {
        var result = new List<VirtualNode>();
        var slots = context.Slots;
        if (slots is null || !slots.TryGetSlot("default", out var slot))
        {
            return result;
        }
        var rendered = slot(null);
        if (rendered is null)
        {
            return result;
        }
        // Flatten one level of fragments (a v-for child renders a keyed-fragment of items).
        foreach (var node in rendered)
        {
            if (node is null)
            {
                continue;
            }
            if (node.Type == VirtualNodeType.Fragment && node.ArrayChildren is not null)
            {
                foreach (var fragmentChild in node.ArrayChildren)
                {
                    if (fragmentChild is not null)
                    {
                        result.Add(fragmentChild);
                    }
                }
            }
            else
            {
                result.Add(node);
            }
        }
        return result;
    }

    private static string? ReadString(VirtualNodeProperties? raw, string name)
        => raw is not null && raw.TryGetValue(name, out var value) ? value as string : null;
}
