using System;
using System.Collections.Generic;

using Assimalign.Vue.RuntimeCore;

namespace Assimalign.Vue.RuntimeDom;

/// <summary>
/// The DOM <c>&lt;Transition&gt;</c> built-in — the C# port of upstream's <c>Transition</c>
/// (https://github.com/vuejs/core/blob/main/packages/runtime-dom/src/components/Transition.ts,
/// https://vuejs.org/guide/built-ins/transition.html). It resolves CSS-class-based enter/leave hooks
/// from its <c>name</c>/<c>type</c>/<c>duration</c>/<c>css</c> and per-phase class-override props
/// (upstream <c>resolveTransitionProps</c>), then renders the platform-agnostic
/// <see cref="BaseTransition"/> with those hooks and the passed-through slot — so a single element or
/// component child animates on insert/remove.
/// <para>
/// The class choreography — <c>v-enter-from</c>/<c>-active</c>/<c>-to</c> and the leave counterparts,
/// a forced reflow, the next-frame to-class swap, and <c>transitionend</c>/<c>animationend</c>
/// end-detection with a computed-duration fallback — runs through the injectable
/// <see cref="DomTransitionOperations"/>, so the whole flow is exercised DOM-free in tests and by the
/// browser bridge in production. Referenced by the compiled render through
/// <see cref="DomRenderHelpers._Transition"/>. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public sealed class Transition : IComponentDefinition
{
    private const string TransitionType = "transition";
    private const string AnimationType = "animation";

    /// <summary>The shared component instance the compiled render references via <see cref="DomRenderHelpers._Transition"/>.</summary>
    public static readonly Transition Instance = new();

    private Transition()
    {
    }

    /// <inheritdoc/>
    public string? Name => "Transition";

    /// <inheritdoc/>
    // The transition renders its child; it owns no element, so attribute fallthrough is off.
    public bool InheritAttributes => false;

    /// <inheritdoc/>
    public Func<VirtualNode?> Setup(ComponentProperties properties, ComponentSetupContext context)
    {
        var instance = ComponentInstance.Current!;
        return () =>
        {
            var resolved = ResolveTransitionProperties(instance.VirtualNode.Properties);
            var childProperties = new VirtualNodeProperties(1);
            childProperties.Set(BaseTransition.PropertiesKey, resolved);
            // h(BaseTransition, resolveTransitionProps(props), slots): forward the single slot through.
            return VirtualNodeFactory.Component(BaseTransition.Instance, childProperties, context.Slots);
        };
    }

    /// <summary>
    /// Builds the CSS-class enter/leave hook set for a raw transition prop bag (upstream:
    /// <c>resolveTransitionProps</c>). Shared with <see cref="TransitionGroup"/>, which resolves the
    /// same hooks per child. With <c>css: false</c> the class/end-detection work is skipped and only the
    /// user JS hooks pass through.
    /// </summary>
    /// <param name="raw">The transition component's raw props, or null.</param>
    /// <returns>The resolved base-transition properties.</returns>
    internal static BaseTransitionProperties ResolveTransitionProperties(VirtualNodeProperties? raw)
    {
        var mode = ReadString(raw, "mode");
        var appear = ReadBool(raw, "appear");
        var persisted = ReadBool(raw, "persisted");

        var (userBeforeEnter, userEnter, userAfterEnter, userEnterCancelled) =
            (ReadHook(raw, "onBeforeEnter"), ReadEnterHook(raw, "onEnter"), ReadHook(raw, "onAfterEnter"), ReadHook(raw, "onEnterCancelled"));
        var (userBeforeLeave, userLeave, userAfterLeave, userLeaveCancelled) =
            (ReadHook(raw, "onBeforeLeave"), ReadEnterHook(raw, "onLeave"), ReadHook(raw, "onAfterLeave"), ReadHook(raw, "onLeaveCancelled"));
        // Appear hooks default to their enter counterparts (upstream).
        var userBeforeAppear = ReadHook(raw, "onBeforeAppear") ?? userBeforeEnter;
        var userAppear = raw is not null && raw.ContainsName("onAppear") ? ReadEnterHook(raw, "onAppear") : userEnter;
        var userAfterAppear = ReadHook(raw, "onAfterAppear") ?? userAfterEnter;
        var userAppearCancelled = ReadHook(raw, "onAppearCancelled") ?? userEnterCancelled;

        // css === false: no class choreography, only the raw JS hooks (upstream returns baseProps).
        if (raw is not null && raw.TryGetValue("css", out var cssValue) && cssValue is false)
        {
            return new BaseTransitionProperties
            {
                Mode = mode,
                Appear = appear,
                Persisted = persisted,
                OnBeforeEnter = userBeforeEnter,
                OnEnter = userEnter.Hook,
                OnAfterEnter = userAfterEnter,
                OnEnterCancelled = userEnterCancelled,
                OnBeforeLeave = userBeforeLeave,
                OnLeave = userLeave.Hook,
                OnAfterLeave = userAfterLeave,
                OnLeaveCancelled = userLeaveCancelled,
                OnBeforeAppear = userBeforeAppear,
                OnAppear = userAppear.Hook,
                OnAfterAppear = userAfterAppear,
                OnAppearCancelled = userAppearCancelled,
            };
        }

        var name = ReadString(raw, "name") ?? "v";
        var type = ReadString(raw, "type");
        var enterFromClass = ClassName(raw, "enterFromClass", name + "-enter-from");
        var enterActiveClass = ClassName(raw, "enterActiveClass", name + "-enter-active");
        var enterToClass = ClassName(raw, "enterToClass", name + "-enter-to");
        var appearFromClass = ClassName(raw, "appearFromClass", enterFromClass);
        var appearActiveClass = ClassName(raw, "appearActiveClass", enterActiveClass);
        var appearToClass = ClassName(raw, "appearToClass", enterToClass);
        var leaveFromClass = ClassName(raw, "leaveFromClass", name + "-leave-from");
        var leaveActiveClass = ClassName(raw, "leaveActiveClass", name + "-leave-active");
        var leaveToClass = ClassName(raw, "leaveToClass", name + "-leave-to");
        var (enterDuration, leaveDuration) = NormalizeDuration(raw is not null && raw.TryGetValue("duration", out var d) ? d : null);

        // Removes the enter to+active classes and marks the cancelled flag (upstream finishEnter).
        void FinishEnter(DomTransitionOperations ops, int element, bool isAppear, Action? done, bool cancelled)
        {
            ops.EnterCancelledFlags[element] = cancelled;
            ops.RemoveTransitionClass(element, isAppear ? appearToClass : enterToClass);
            ops.RemoveTransitionClass(element, isAppear ? appearActiveClass : enterActiveClass);
            done?.Invoke();
        }

        // Removes all leave classes and clears the leaving flag (upstream finishLeave).
        void FinishLeave(DomTransitionOperations ops, int element, Action? done)
        {
            ops.LeavingFlags[element] = false;
            ops.RemoveTransitionClass(element, leaveFromClass);
            ops.RemoveTransitionClass(element, leaveToClass);
            ops.RemoveTransitionClass(element, leaveActiveClass);
            done?.Invoke();
        }

        // The enter/appear hook: add the active+from classes are added by onBeforeEnter; here the
        // next frame removes from-class, adds to-class, and (without an explicit user callback) waits
        // on the transition end (upstream makeEnterHook).
        TransitionEnterHook MakeEnterHook(bool isAppear) => (element, done) =>
        {
            var ops = DomTransitionOperations.Require();
            var handle = (int)element;
            var userHook = isAppear ? userAppear : userEnter;
            void Resolve() => FinishEnter(ops, handle, isAppear, done, cancelled: false);
            userHook.Hook?.Invoke(element, Resolve);
            ops.NextFrame(() =>
            {
                ops.RemoveTransitionClass(handle, isAppear ? appearFromClass : enterFromClass);
                ops.AddTransitionClass(handle, isAppear ? appearToClass : enterToClass);
                if (!userHook.IsExplicit)
                {
                    ops.WhenTransitionEnds(handle, type, enterDuration, Resolve);
                }
            });
        };

        return new BaseTransitionProperties
        {
            Mode = mode,
            Appear = appear,
            Persisted = persisted,
            OnBeforeEnter = element =>
            {
                var ops = DomTransitionOperations.Require();
                var handle = (int)element;
                userBeforeEnter?.Invoke(element);
                ops.AddTransitionClass(handle, enterFromClass);
                ops.AddTransitionClass(handle, enterActiveClass);
            },
            OnBeforeAppear = element =>
            {
                var ops = DomTransitionOperations.Require();
                var handle = (int)element;
                userBeforeAppear?.Invoke(element);
                ops.AddTransitionClass(handle, appearFromClass);
                ops.AddTransitionClass(handle, appearActiveClass);
            },
            OnEnter = MakeEnterHook(false),
            OnAppear = MakeEnterHook(true),
            // onAfterEnter/onAfterLeave/onBeforeLeave pass through from the user (upstream baseProps).
            OnAfterEnter = userAfterEnter,
            OnAfterAppear = userAfterAppear,
            OnBeforeLeave = userBeforeLeave,
            OnAfterLeave = userAfterLeave,
            OnLeave = (element, done) =>
            {
                var ops = DomTransitionOperations.Require();
                var handle = (int)element;
                ops.LeavingFlags[handle] = true;
                void Resolve() => FinishLeave(ops, handle, done);
                ops.AddTransitionClass(handle, leaveFromClass);
                if (!ops.EnterCancelledFlags.GetValueOrDefault(handle))
                {
                    ops.ForceReflow();
                    ops.AddTransitionClass(handle, leaveActiveClass);
                }
                else
                {
                    ops.AddTransitionClass(handle, leaveActiveClass);
                    ops.ForceReflow();
                }
                ops.NextFrame(() =>
                {
                    if (!ops.LeavingFlags.GetValueOrDefault(handle))
                    {
                        return;
                    }
                    ops.RemoveTransitionClass(handle, leaveFromClass);
                    ops.AddTransitionClass(handle, leaveToClass);
                    if (!userLeave.IsExplicit)
                    {
                        ops.WhenTransitionEnds(handle, type, leaveDuration, Resolve);
                    }
                });
                userLeave.Hook?.Invoke(element, Resolve);
            },
            OnEnterCancelled = element =>
            {
                var ops = DomTransitionOperations.Require();
                FinishEnter(ops, (int)element, isAppear: false, done: null, cancelled: true);
                userEnterCancelled?.Invoke(element);
            },
            OnAppearCancelled = element =>
            {
                var ops = DomTransitionOperations.Require();
                FinishEnter(ops, (int)element, isAppear: true, done: null, cancelled: true);
                userAppearCancelled?.Invoke(element);
            },
            OnLeaveCancelled = element =>
            {
                var ops = DomTransitionOperations.Require();
                FinishLeave(ops, (int)element, done: null);
                userLeaveCancelled?.Invoke(element);
            },
        };
    }

    // --- raw-prop readers -----------------------------------------------------------------------

    private static string? ReadString(VirtualNodeProperties? raw, string name)
        => raw is not null && raw.TryGetValue(name, out var value) ? value as string : null;

    private static bool ReadBool(VirtualNodeProperties? raw, string name)
        => raw is not null && raw.TryGetValue(name, out var value) && value is true;

    private static string ClassName(VirtualNodeProperties? raw, string name, string fallback)
        => raw is not null && raw.TryGetValue(name, out var value) && value is string text && text.Length > 0
            ? text
            : fallback;

    private static Action<object>? ReadHook(VirtualNodeProperties? raw, string name)
        => raw is not null && raw.TryGetValue(name, out var value) ? value as Action<object> : null;

    // An explicit (el, done) hook waits for its own done; a fire-and-forget (el) hook auto-completes.
    private static (TransitionEnterHook? Hook, bool IsExplicit) ReadEnterHook(VirtualNodeProperties? raw, string name)
    {
        if (raw is null || !raw.TryGetValue(name, out var value))
        {
            return (null, false);
        }
        return value switch
        {
            TransitionEnterHook hook => (hook, true),
            Action<object> action => ((element, done) => { action(element); done(); }, false),
            _ => (null, false),
        };
    }

    // Upstream normalizeDuration: a scalar applies to both phases; an {enter, leave} map splits them.
    // A negative value marks "no explicit duration" so end-detection reads getComputedStyle instead.
    private static (int Enter, int Leave) NormalizeDuration(object? duration)
    {
        switch (duration)
        {
            case null:
                return (-1, -1);
            case IReadOnlyDictionary<string, object?> map:
                return (
                    NumberOf(map.TryGetValue("enter", out var enter) ? enter : null),
                    NumberOf(map.TryGetValue("leave", out var leave) ? leave : null));
            default:
                var scalar = NumberOf(duration);
                return (scalar, scalar);
        }
    }

    private static int NumberOf(object? value) => value switch
    {
        int number => number,
        long number => (int)number,
        double number => (int)number,
        float number => (int)number,
        string text when int.TryParse(text, out var parsed) => parsed,
        _ => -1,
    };
}
