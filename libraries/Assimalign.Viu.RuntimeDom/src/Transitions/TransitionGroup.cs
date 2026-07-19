using System;
using System.Collections.Generic;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.RuntimeDom;

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
/// the exact three-phase order upstream uses (finish pending callbacks, read all positions, then write
/// all transforms) so the read pass and the write pass never interleave — the documented synchronous
/// layout seam the bridge-backed implementation fulfils. Referenced by the compiled render through
/// <see cref="DomRenderHelpers._TransitionGroup"/>. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public sealed class TransitionGroup : IComponentDefinition
{
    /// <summary>The shared component instance the compiled render references via <see cref="DomRenderHelpers._TransitionGroup"/>.</summary>
    public static readonly TransitionGroup Instance = new();

    private TransitionGroup()
    {
    }

    /// <inheritdoc/>
    public string? Name => "TransitionGroup";

    /// <inheritdoc/>
    // tag/moveClass and the transition props must not fall through as attributes onto the group tag.
    public bool InheritAttributes => false;

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
            // the prevChildren loop recording positionMap). The FLIP delta reads these in onUpdated.
            if (previousChildren is not null)
            {
                positionMap.Clear();
                var operations = DomTransitionOperations.Current;
                foreach (var child in previousChildren)
                {
                    BaseTransition.SetTransitionHooks(
                        child,
                        BaseTransition.ResolveTransitionHooks(child, resolved, state, instance, null));
                    if (operations is not null && child.El is { } element)
                    {
                        positionMap[child] = operations.MeasurePosition((int)element);
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

        // Three passes, no interleaving of reads and writes (upstream comment: prevent layout thrashing).
        // 1. finish any pending move/enter callbacks so a re-triggered FLIP measures settled positions.
        foreach (var child in previousChildren)
        {
            CallPendingCallbacks(state, moveCallbacks, child);
        }
        // 2. read every new position.
        var newPositions = new TransitionRectangle[previousChildren.Count];
        for (var index = 0; index < previousChildren.Count; index++)
        {
            if (previousChildren[index].El is { } element)
            {
                newPositions[index] = operations.MeasurePosition((int)element);
            }
        }
        // 3. write the inverting transforms for the children that moved.
        var moved = new List<VirtualNode>();
        for (var index = 0; index < previousChildren.Count; index++)
        {
            var child = previousChildren[index];
            if (child.El is not { } element || !positionMap.TryGetValue(child, out var oldPosition))
            {
                continue;
            }
            var newPosition = newPositions[index];
            var deltaX = oldPosition.Left - newPosition.Left;
            var deltaY = oldPosition.Top - newPosition.Top;
            if (deltaX != 0 || deltaY != 0)
            {
                operations.SetMoveTransform((int)element, deltaX, deltaY);
                moved.Add(child);
            }
        }

        // Force everything into position, then run the move class with the inverse transform cleared.
        operations.ForceReflow();
        foreach (var child in moved)
        {
            var element = (int)child.El!;
            operations.AddTransitionClass(element, moveClass);
            operations.ClearMoveStyles(element);
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
