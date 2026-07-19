using System;
using System.Collections.Generic;

using Assimalign.Viu.Shared;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The platform-agnostic transition state machine — the C# port of upstream's <c>BaseTransition</c>
/// component (<c>packages/runtime-core/src/components/BaseTransition.ts</c>,
/// https://vuejs.org/guide/built-ins/transition.html). It wraps a single element or component child,
/// resolves per-render enter/leave <see cref="TransitionHooks"/> from its
/// <see cref="BaseTransitionProperties"/>, stamps them onto the child so the renderer runs the
/// choreography around mount/remove, and coordinates the transition <c>mode</c> (<c>out-in</c> defers
/// the incoming child until the outgoing leave finishes; <c>in-out</c> defers the outgoing leave until
/// the incoming enter finishes) and <c>appear</c> (enter on initial mount).
/// <para>
/// This layer carries no CSS or DOM knowledge — the DOM <c>&lt;Transition&gt;</c>/
/// <c>&lt;TransitionGroup&gt;</c> (<c>Assimalign.Viu.RuntimeDom</c>) supply the class-based hooks. The
/// resolved properties are threaded in through the reserved <see cref="PropertiesKey"/> prop (the DOM
/// layer's <c>resolveTransitionProps</c> output) or, for direct use, assembled from individual props.
/// Referenced by the compiled render through <see cref="RenderHelpers._BaseTransition"/>. Not
/// thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public sealed class BaseTransition : IComponentDefinition
{
    /// <summary>
    /// The reserved prop name carrying a pre-resolved <see cref="BaseTransitionProperties"/> — the
    /// internal contract the DOM <c>&lt;Transition&gt;</c>/<c>&lt;TransitionGroup&gt;</c> pass their
    /// <c>resolveTransitionProps</c> output through. Not an HTML attribute; never emitted by the
    /// template compiler.
    /// </summary>
    public const string PropertiesKey = "$baseTransition";

    /// <summary>The shared component instance the compiled render references via <see cref="RenderHelpers._BaseTransition"/>.</summary>
    public static readonly BaseTransition Instance = new();

    private BaseTransition()
    {
    }

    /// <inheritdoc/>
    public string? Name => "BaseTransition";

    /// <inheritdoc/>
    // The transition owns no element of its own; it renders its child, so attribute fallthrough is off.
    public bool InheritAttributes => false;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var instance = ComponentInstance.Current!;
        var state = UseTransitionState();
        return () => Render(instance, state, context);
    }

    private static VirtualNode? Render(ComponentInstance instance, TransitionState state, ComponentSetupContext context)
    {
        var children = GetTransitionRawChildren(context);
        if (children is null || children.Count == 0)
        {
            return null;
        }
        var child = children[0];
        if (children.Count > 1)
        {
            var hasFound = false;
            foreach (var candidate in children)
            {
                if (candidate.Type != VirtualNodeType.Comment)
                {
                    if (hasFound)
                    {
                        RuntimeWarnings.Warn(
                            "<transition> can only be used on a single element or component. "
                            + "Use <transition-group> for lists.");
                        break;
                    }
                    child = candidate;
                    hasFound = true;
                }
            }
        }

        var properties = ReadProperties(instance);
        if (properties is null)
        {
            // No transition configuration: render the child untouched (upstream: css === false with no
            // JS hooks still returns the child; a plain wrapper is a passthrough).
            return child;
        }

        // out-in currently playing its leave: render an empty placeholder until afterLeave re-renders.
        if (state.IsLeaving)
        {
            return EmptyPlaceholder(child);
        }

        var innerChild = GetInnerChild(child);
        if (innerChild is null)
        {
            return EmptyPlaceholder(child);
        }

        // #11061: keep enterHooks fresh across a clone via the postClone capture (reassigns this local).
        TransitionHooks enterHooks = null!;
        enterHooks = ResolveTransitionHooks(innerChild, properties, state, instance, hooks => enterHooks = hooks);
        if (innerChild.Type != VirtualNodeType.Comment)
        {
            SetTransitionHooks(innerChild, enterHooks);
        }

        var oldChild = instance.Subtree;
        var oldInnerChild = oldChild is null ? null : GetInnerChild(oldChild);

        // Switching between different views: coordinate the outgoing leave per mode.
        if (oldInnerChild is not null
            && oldInnerChild.Type != VirtualNodeType.Comment
            && !IsSameVirtualNodeType(innerChild, oldInnerChild))
        {
            var leavingHooks = ResolveTransitionHooks(oldInnerChild, properties, state, instance, null);
            // Update the outgoing tree's hooks in case of a dynamic transition.
            SetTransitionHooks(oldInnerChild, leavingHooks);

            if (string.Equals(properties.Mode, "out-in", StringComparison.Ordinal)
                && innerChild.Type != VirtualNodeType.Comment)
            {
                state.IsLeaving = true;
                // Return a placeholder now; re-render to mount the incoming child once the leave finishes.
                leavingHooks.AfterLeave = () =>
                {
                    state.IsLeaving = false;
                    if (instance.UpdateJob is { IsDisposed: false } job && !instance.IsUnmounted)
                    {
                        Scheduler.QueueJob(job);
                    }
                    leavingHooks.AfterLeave = null;
                };
                return EmptyPlaceholder(child);
            }

            if (string.Equals(properties.Mode, "in-out", StringComparison.Ordinal)
                && innerChild.Type != VirtualNodeType.Comment)
            {
                var capturedOldInnerChild = oldInnerChild;
                var capturedEnterHooks = enterHooks;
                leavingHooks.DelayLeave = (element, earlyRemove, delayedLeave) =>
                {
                    var leavingCache = GetLeavingNodesForType(state, capturedOldInnerChild);
                    leavingCache[capturedOldInnerChild.Key?.ToString() ?? "undefined"] = capturedOldInnerChild;
                    // Early-removal path: remove the outgoing element immediately if it is toggled again.
                    state.LeaveCallbacks[element] = _ =>
                    {
                        earlyRemove();
                        state.LeaveCallbacks.Remove(element);
                        capturedEnterHooks.DelayedLeave = null;
                    };
                    // The incoming element's enter fires this, starting the real (deferred) leave.
                    capturedEnterHooks.DelayedLeave = () =>
                    {
                        delayedLeave();
                        capturedEnterHooks.DelayedLeave = null;
                    };
                };
            }
        }

        return child;
    }

    // --- shared transition helpers (upstream BaseTransition.ts exports) --------------------------

    /// <summary>
    /// Creates the shared per-component <see cref="TransitionState"/> and wires the mount/unmount
    /// lifecycle hooks that drive it (upstream: <c>useTransitionState</c>). Must be called during
    /// <c>Setup</c>.
    /// </summary>
    /// <returns>The transition state.</returns>
    internal static TransitionState UseTransitionState()
    {
        var state = new TransitionState();
        Lifecycle.OnMounted(() => state.IsMounted = true);
        Lifecycle.OnBeforeUnmount(() => state.IsUnmounting = true);
        return state;
    }

    /// <summary>
    /// Resolves the enter/leave hook set for <paramref name="vnode"/> (upstream:
    /// <c>resolveTransitionHooks</c>). The DOM <c>&lt;TransitionGroup&gt;</c> calls this directly to
    /// stamp per-item hooks.
    /// </summary>
    /// <param name="vnode">The vnode the hooks bind to.</param>
    /// <param name="properties">The resolved transition properties.</param>
    /// <param name="state">The shared transition state.</param>
    /// <param name="instance">The owning transition component instance.</param>
    /// <param name="postClone">A capture invoked after a <c>clone</c> re-resolves (upstream #11061), or null.</param>
    /// <returns>The resolved hooks.</returns>
    internal static TransitionHooks ResolveTransitionHooks(
        VirtualNode vnode,
        BaseTransitionProperties properties,
        TransitionState state,
        ComponentInstance instance,
        Action<TransitionHooks>? postClone)
        => new(vnode, properties, state, instance, postClone);

    /// <summary>
    /// Stamps resolved <paramref name="hooks"/> onto <paramref name="vnode"/> (upstream:
    /// <c>setTransitionHooks</c>), recursing into a mounted component's subtree so the eventual host
    /// element carries the transition.
    /// </summary>
    /// <param name="vnode">The vnode to stamp.</param>
    /// <param name="hooks">The resolved hooks.</param>
    internal static void SetTransitionHooks(VirtualNode vnode, TransitionHooks hooks)
    {
        if ((vnode.ShapeFlag & ShapeFlags.Component) != 0 && vnode.Component is ComponentInstance component)
        {
            vnode.Transition = hooks;
            if (component.Subtree is not null)
            {
                SetTransitionHooks(component.Subtree, hooks);
            }
        }
        else
        {
            vnode.Transition = hooks;
        }
    }

    /// <summary>
    /// Returns the per-type leaving-vnode cache for <paramref name="vnode"/> (upstream:
    /// <c>getLeavingNodesForType</c>), creating it on first use.
    /// </summary>
    /// <param name="state">The shared transition state.</param>
    /// <param name="vnode">The vnode whose type keys the cache.</param>
    /// <returns>The string-keyed leaving-vnode cache for the vnode's type.</returns>
    internal static Dictionary<string, VirtualNode> GetLeavingNodesForType(TransitionState state, VirtualNode vnode)
    {
        var typeKey = TypeKey(vnode);
        if (!state.LeavingVirtualNodes.TryGetValue(typeKey, out var cache))
        {
            cache = new Dictionary<string, VirtualNode>(StringComparer.Ordinal);
            state.LeavingVirtualNodes[typeKey] = cache;
        }
        return cache;
    }

    /// <summary>
    /// Whether two vnodes are the same transition subject (upstream: <c>isSameVNodeType</c>) — same
    /// node type, key, and (element) tag or (component) definition.
    /// </summary>
    /// <param name="left">The first vnode.</param>
    /// <param name="right">The second vnode.</param>
    /// <returns>Whether the two vnodes are the same type.</returns>
    internal static bool IsSameVirtualNodeType(VirtualNode left, VirtualNode right)
        => left.Type == right.Type
            && Equals(left.Key, right.Key)
            && (left.Type != VirtualNodeType.Element
                || string.Equals(left.ElementTag, right.ElementTag, StringComparison.Ordinal))
            && (left.Type != VirtualNodeType.Component
                || ReferenceEquals(left.ComponentType, right.ComponentType));

    private static object TypeKey(VirtualNode vnode)
        => vnode.ElementTag ?? vnode.ComponentType ?? (object)vnode.Type;

    // The inner child a transition animates. KeepAlive/Teleport unwrapping is deferred to their own
    // work items; a plain element/component/comment is its own inner child (upstream getInnerChild
    // returns the vnode for the non-KeepAlive/non-Teleport case).
    private static VirtualNode? GetInnerChild(VirtualNode vnode) => vnode;

    // Upstream emptyPlaceholder: a non-KeepAlive child renders nothing (a comment) while leaving.
    // Returning null yields a comment placeholder through the render-root normalization.
    private static VirtualNode? EmptyPlaceholder(VirtualNode child)
    {
        _ = child;
        return null;
    }

    private static List<VirtualNode>? GetTransitionRawChildren(ComponentSetupContext context)
    {
        var slots = context.Slots;
        if (slots is null || !slots.TryGetSlot("default", out var slot))
        {
            return null;
        }
        var rendered = slot(null);
        if (rendered is null || rendered.Length == 0)
        {
            return null;
        }
        // Flatten one level of fragments so a block-wrapped single child resolves to its element
        // (upstream getTransitionRawChildren recursively flattens; a single transition child needs
        // only this shallow unwrap).
        var result = new List<VirtualNode>(rendered.Length);
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

    private static BaseTransitionProperties? ReadProperties(ComponentInstance instance)
    {
        var bag = instance.VirtualNode.Properties;
        if (bag is null)
        {
            return null;
        }
        if (bag.TryGetValue(PropertiesKey, out var resolved) && resolved is BaseTransitionProperties preResolved)
        {
            return preResolved;
        }
        // Direct <BaseTransition> use: assemble from individual delegate/scalar props.
        return BuildFromBag(bag);
    }

    private static BaseTransitionProperties BuildFromBag(VirtualNodeProperties bag)
        => new()
        {
            Mode = bag.TryGetValue("mode", out var mode) ? mode as string : null,
            Appear = bag.TryGetValue("appear", out var appear) && appear is true,
            Persisted = bag.TryGetValue("persisted", out var persisted) && persisted is true,
            OnBeforeEnter = Hook(bag, "onBeforeEnter"),
            OnEnter = EnterHook(bag, "onEnter"),
            OnAfterEnter = Hook(bag, "onAfterEnter"),
            OnEnterCancelled = Hook(bag, "onEnterCancelled"),
            OnBeforeLeave = Hook(bag, "onBeforeLeave"),
            OnLeave = EnterHook(bag, "onLeave"),
            OnAfterLeave = Hook(bag, "onAfterLeave"),
            OnLeaveCancelled = Hook(bag, "onLeaveCancelled"),
            OnBeforeAppear = Hook(bag, "onBeforeAppear"),
            OnAppear = EnterHook(bag, "onAppear"),
            OnAfterAppear = Hook(bag, "onAfterAppear"),
            OnAppearCancelled = Hook(bag, "onAppearCancelled"),
        };

    private static Action<object>? Hook(VirtualNodeProperties bag, string name)
        => bag.TryGetValue(name, out var value) ? value as Action<object> : null;

    private static TransitionEnterHook? EnterHook(VirtualNodeProperties bag, string name)
    {
        if (!bag.TryGetValue(name, out var value))
        {
            return null;
        }
        // An explicit (el, done) hook is used directly; a fire-and-forget (el) hook auto-completes.
        return value switch
        {
            TransitionEnterHook enterHook => enterHook,
            Action<object> action => (element, done) => { action(element); done(); },
            _ => null,
        };
    }
}
