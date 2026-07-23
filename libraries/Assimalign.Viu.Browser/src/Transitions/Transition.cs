using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The DOM <c>&lt;Transition&gt;</c> built-in — the C# port of upstream's <c>Transition</c>
/// (https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/components/Transition.ts,
/// https://vuejs.org/guide/built-ins/transition.html). It resolves CSS-class-based enter/leave hooks
/// from its <c>name</c>/<c>type</c>/<c>duration</c>/<c>css</c> and per-phase class-override properties
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
public sealed class Transition : IComponentTemplate
{
    private static readonly IReadOnlyList<IComponentParameter> DeclaredParameters =
    [
        new ComponentParameter("name"),
        new ComponentParameter("type"),
        new ComponentParameter("css"),
        new ComponentParameter("duration"),
        new ComponentParameter("mode"),
        new ComponentParameter("appear"),
        new ComponentParameter("persisted"),
        new ComponentParameter("enterFromClass"),
        new ComponentParameter("enterActiveClass"),
        new ComponentParameter("enterToClass"),
        new ComponentParameter("appearFromClass"),
        new ComponentParameter("appearActiveClass"),
        new ComponentParameter("appearToClass"),
        new ComponentParameter("leaveFromClass"),
        new ComponentParameter("leaveActiveClass"),
        new ComponentParameter("leaveToClass"),
        new ComponentParameter("onBeforeEnter"),
        new ComponentParameter("onEnter"),
        new ComponentParameter("onAfterEnter"),
        new ComponentParameter("onEnterCancelled"),
        new ComponentParameter("onBeforeLeave"),
        new ComponentParameter("onLeave"),
        new ComponentParameter("onAfterLeave"),
        new ComponentParameter("onLeaveCancelled"),
        new ComponentParameter("onBeforeAppear"),
        new ComponentParameter("onAppear"),
        new ComponentParameter("onAfterAppear"),
        new ComponentParameter("onAppearCancelled"),
    ];

    private Transition()
    {
    }

    /// <inheritdoc/>
    public string? Name => "Transition";

    /// <inheritdoc/>
    public ComponentFlags Flags => ComponentFlags.None;

    /// <inheritdoc/>
    public IReadOnlyList<IComponentParameter>? Parameters => DeclaredParameters;

    internal static IReadOnlyList<IComponentParameter> ParameterDefinitions =>
        DeclaredParameters;

    /// <summary>Gets the AOT-safe registration for the browser transition built-in.</summary>
    public static ComponentRegistration Registration =>
        new(
            typeof(Transition),
            static () => new Transition(),
            "Transition");

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return () =>
        {
            BaseTransitionProperties resolved =
                ResolveTransitionProperties(context.Arguments);
            ComponentArguments arguments = new(
            [
                new KeyValuePair<string, object?>(
                    BaseTransition.PropertiesArgument,
                    resolved),
            ]);
            return ComponentTree.Template<BaseTransition>(
                arguments,
                context.Slots);
        };
    }

    /// <summary>
    /// Builds the CSS-class enter/leave hook set for a supplied transition argument bag (upstream:
    /// <c>resolveTransitionProps</c>). With <c>css: false</c> the class/end-detection work is skipped
    /// and only the user hooks pass through.
    /// </summary>
    /// <param name="arguments">The transition component's resolved arguments.</param>
    /// <returns>The resolved base-transition properties.</returns>
    internal static BaseTransitionProperties ResolveTransitionProperties(
        IComponentArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var mode = ReadString(arguments, "mode");
        var appear = ReadBool(arguments, "appear");
        var persisted = ReadBool(arguments, "persisted");

        var (userBeforeEnter, userEnter, userAfterEnter, userEnterCancelled) =
            (ReadHook(arguments, "onBeforeEnter"), ReadEnterHook(arguments, "onEnter"), ReadHook(arguments, "onAfterEnter"), ReadHook(arguments, "onEnterCancelled"));
        var (userBeforeLeave, userLeave, userAfterLeave, userLeaveCancelled) =
            (ReadHook(arguments, "onBeforeLeave"), ReadEnterHook(arguments, "onLeave"), ReadHook(arguments, "onAfterLeave"), ReadHook(arguments, "onLeaveCancelled"));
        // Appear hooks default to their enter counterparts (upstream).
        var userBeforeAppear = ReadHook(arguments, "onBeforeAppear") ?? userBeforeEnter;
        var userAppear =
            arguments.Contains("onAppear")
                ? ReadEnterHook(arguments, "onAppear")
                : userEnter;
        var userAfterAppear = ReadHook(arguments, "onAfterAppear") ?? userAfterEnter;
        var userAppearCancelled = ReadHook(arguments, "onAppearCancelled") ?? userEnterCancelled;

        // css === false: no class choreography, only the supplied JavaScript hooks.
        if (arguments.Contains("css") && arguments["css"] is false)
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

        var name = ReadString(arguments, "name") ?? "v";
        var type = ReadString(arguments, "type");
        DomTransitionClassNames classNames =
            ResolveClassNames(arguments);
        string enterFromClass = classNames.EnterFrom;
        string enterActiveClass = classNames.EnterActive;
        string enterToClass = classNames.EnterTo;
        string appearFromClass = classNames.AppearFrom;
        string appearActiveClass = classNames.AppearActive;
        string appearToClass = classNames.AppearTo;
        string leaveFromClass = classNames.LeaveFrom;
        string leaveActiveClass = classNames.LeaveActive;
        string leaveToClass = classNames.LeaveTo;
        var (enterDuration, leaveDuration) =
            NormalizeDuration(arguments["duration"]);

        // Removes the enter to+active classes and marks the cancelled flag (upstream finishEnter).
        void FinishEnter(
            DomTransitionOperations operations,
            int element,
            bool isAppear,
            bool cancelled)
        {
            operations.EnterCancelledFlags[element] = cancelled;
            operations.RemoveTransitionClass(element, isAppear ? appearToClass : enterToClass);
            operations.RemoveTransitionClass(element, isAppear ? appearActiveClass : enterActiveClass);
        }

        // Removes all leave classes and clears the leaving flag (upstream finishLeave).
        void FinishLeave(
            DomTransitionOperations operations,
            int element,
            Action? done)
        {
            operations.LeavingFlags[element] = false;
            operations.RemoveTransitionClass(element, leaveFromClass);
            operations.RemoveTransitionClass(element, leaveToClass);
            operations.RemoveTransitionClass(element, leaveActiveClass);
            done?.Invoke();
        }

        // The enter/appear hook: add the active+from classes are added by onBeforeEnter; here the
        // next frame removes from-class, adds to-class, and (without an explicit user callback) waits
        // on the transition end (upstream makeEnterHook).
        TransitionEnterHook MakeEnterHook(bool isAppear) => (element, done) =>
        {
            var operations = DomTransitionOperations.Require();
            var handle = (int)element;
            var userHook = isAppear ? userAppear : userEnter;
            int generation =
                operations.EnterGenerations.GetValueOrDefault(handle) + 1;
            operations.EnterGenerations[handle] = generation;
            operations.EnterCancelledFlags[handle] = false;
            void Resolve() => done();
            if (userHook.IsExplicit)
            {
                userHook.Hook?.Invoke(element, Resolve);
            }
            else
            {
                userHook.SynchronousHook?.Invoke(element);
            }
            operations.NextFrame(() =>
            {
                if (operations.EnterGenerations.GetValueOrDefault(handle)
                    != generation)
                {
                    return;
                }

                operations.RemoveTransitionClass(
                    handle,
                    isAppear ? appearFromClass : enterFromClass);
                if (operations.EnterCancelledFlags.GetValueOrDefault(handle))
                {
                    return;
                }

                operations.AddTransitionClass(
                    handle,
                    isAppear ? appearToClass : enterToClass);
                if (!userHook.IsExplicit)
                {
                    operations.WhenTransitionEnds(
                        handle,
                        type,
                        enterDuration,
                        Resolve);
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
                var operations = DomTransitionOperations.Require();
                var handle = (int)element;
                userBeforeEnter?.Invoke(element);
                operations.AddTransitionClass(handle, enterFromClass);
                operations.AddTransitionClass(handle, enterActiveClass);
            },
            OnBeforeAppear = element =>
            {
                var operations = DomTransitionOperations.Require();
                var handle = (int)element;
                userBeforeAppear?.Invoke(element);
                operations.AddTransitionClass(handle, appearFromClass);
                operations.AddTransitionClass(handle, appearActiveClass);
            },
            OnEnter = MakeEnterHook(false),
            OnAppear = MakeEnterHook(true),
            OnAfterEnter = element =>
            {
                DomTransitionOperations operations =
                    DomTransitionOperations.Require();
                FinishEnter(
                    operations,
                    (int)element,
                    isAppear: false,
                    cancelled: false);
                userAfterEnter?.Invoke(element);
            },
            OnAfterAppear = element =>
            {
                DomTransitionOperations operations =
                    DomTransitionOperations.Require();
                FinishEnter(
                    operations,
                    (int)element,
                    isAppear: true,
                    cancelled: false);
                userAfterAppear?.Invoke(element);
            },
            OnBeforeLeave = userBeforeLeave,
            OnAfterLeave = userAfterLeave,
            OnLeave = (element, done) =>
            {
                var operations = DomTransitionOperations.Require();
                var handle = (int)element;
                int generation =
                    operations.LeaveGenerations.GetValueOrDefault(handle) + 1;
                operations.LeaveGenerations[handle] = generation;
                operations.LeavingFlags[handle] = true;
                void Resolve() => FinishLeave(operations, handle, done);
                operations.AddTransitionClass(handle, leaveFromClass);
                if (!operations.EnterCancelledFlags.GetValueOrDefault(handle))
                {
                    operations.ForceReflow();
                    operations.AddTransitionClass(handle, leaveActiveClass);
                }
                else
                {
                    operations.AddTransitionClass(handle, leaveActiveClass);
                    operations.ForceReflow();
                }
                operations.NextFrame(() =>
                {
                    if (!operations.LeavingFlags.GetValueOrDefault(handle)
                        || operations.LeaveGenerations.GetValueOrDefault(handle)
                            != generation)
                    {
                        return;
                    }
                    operations.RemoveTransitionClass(handle, leaveFromClass);
                    operations.AddTransitionClass(handle, leaveToClass);
                    if (!userLeave.IsExplicit)
                    {
                        operations.WhenTransitionEnds(
                            handle,
                            type,
                            leaveDuration,
                            Resolve);
                    }
                });
                userLeave.Hook?.Invoke(element, Resolve);
            },
            OnEnterCancelled = element =>
            {
                var operations = DomTransitionOperations.Require();
                FinishEnter(
                    operations,
                    (int)element,
                    isAppear: false,
                    cancelled: true);
                userEnterCancelled?.Invoke(element);
            },
            OnAppearCancelled = element =>
            {
                var operations = DomTransitionOperations.Require();
                FinishEnter(
                    operations,
                    (int)element,
                    isAppear: true,
                    cancelled: true);
                userAppearCancelled?.Invoke(element);
            },
            OnLeaveCancelled = element =>
            {
                var operations = DomTransitionOperations.Require();
                FinishLeave(operations, (int)element, done: null);
                userLeaveCancelled?.Invoke(element);
            },
        };
    }

    // --- argument readers -----------------------------------------------------------------------

    private static string? ReadString(
        IComponentArguments arguments,
        string name)
        => arguments[name] as string;

    private static bool ReadBool(
        IComponentArguments arguments,
        string name)
        => arguments[name] is true;

    private static string ClassName(
        IComponentArguments arguments,
        string name,
        string fallback)
        => arguments[name] is string text && text.Length > 0
            ? text
            : fallback;

    internal static DomTransitionClassNames ResolveClassNames(
        IComponentArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string name = ReadString(arguments, "name") ?? "v";
        string enterFrom =
            ClassName(arguments, "enterFromClass", name + "-enter-from");
        string enterActive =
            ClassName(arguments, "enterActiveClass", name + "-enter-active");
        string enterTo =
            ClassName(arguments, "enterToClass", name + "-enter-to");
        return new DomTransitionClassNames(
            enterFrom,
            enterActive,
            enterTo,
            ClassName(arguments, "appearFromClass", enterFrom),
            ClassName(arguments, "appearActiveClass", enterActive),
            ClassName(arguments, "appearToClass", enterTo),
            ClassName(arguments, "leaveFromClass", name + "-leave-from"),
            ClassName(arguments, "leaveActiveClass", name + "-leave-active"),
            ClassName(arguments, "leaveToClass", name + "-leave-to"));
    }

    private static Action<object>? ReadHook(
        IComponentArguments arguments,
        string name)
        => arguments[name] as Action<object>;

    // An explicit (el, done) hook waits for its own done; a fire-and-forget (el) hook auto-completes.
    private static (
        TransitionEnterHook? Hook,
        Action<object>? SynchronousHook,
        bool IsExplicit) ReadEnterHook(
        IComponentArguments arguments,
        string name)
    {
        if (!arguments.Contains(name))
        {
            return (null, null, false);
        }
        object? value = arguments[name];
        return value switch
        {
            TransitionEnterHook hook => (hook, null, true),
            Action<object> action =>
                (
                    (element, done) =>
                    {
                        action(element);
                        done();
                    },
                    action,
                    false),
            _ => (null, null, false),
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
