using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Decorates one child with Browser CSS enter, appear, and leave behavior.</summary>
/// <remarks>
/// The component emits a host-neutral <see cref="TransitionNode"/> carrying a lazy child slot and
/// resolved <see cref="TransitionProperties"/>. Core owns identity, cancellation, mode sequencing,
/// and deferred removal; Browser owns CSS class names, double-frame scheduling, forced reflow, and
/// transition-end detection. The component is not thread-safe and runs on the browser event loop.
/// Specified by <c>[BLT-7]</c> and <c>[BLT-8]</c>.
/// </remarks>
public sealed class Transition : IComponent
{
    private static readonly IReadOnlyList<ComponentParameter> Parameters =
    [
        new("name"),
        new("type"),
        new("css"),
        new("duration"),
        new("mode"),
        new("appear"),
        new("persisted"),
        new("enterFromClass"),
        new("enterActiveClass"),
        new("enterToClass"),
        new("appearFromClass"),
        new("appearActiveClass"),
        new("appearToClass"),
        new("leaveFromClass"),
        new("leaveActiveClass"),
        new("leaveToClass"),
        new("onBeforeEnter"),
        new("onEnter"),
        new("onAfterEnter"),
        new("onEnterCancelled"),
        new("onBeforeLeave"),
        new("onLeave"),
        new("onAfterLeave"),
        new("onLeaveCancelled"),
        new("onBeforeAppear"),
        new("onAppear"),
        new("onAfterAppear"),
        new("onAppearCancelled"),
    ];

    private static readonly ComponentContract Contract = new(
        displayName: "Transition",
        flags: ComponentFlags.None,
        parameters: Parameters);

    internal static IReadOnlyList<ComponentParameter> ParameterDefinitions => Parameters;

    private Transition()
    {
    }

    /// <summary>Gets the reflection-free component registration.</summary>
    public static ComponentRegistration Registration { get; } = new(
        ComponentReference.ForType(typeof(Transition)),
        Contract,
        static _ => new Transition());

    /// <inheritdoc/>
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ComponentTransitionScope scope = new(context);
        return _ =>
        {
            ComponentSlot child = context.Bindings.Slots.TryGetValue(
                "default",
                out ComponentSlot? slot)
                    ? slot
                    : static _ => null;
            return scope.Attach(
                child,
                ResolveTransitionProperties(context.Bindings.Parameters));
        };
    }

    internal static TransitionProperties ResolveTransitionProperties(
        IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string? mode = ReadString(arguments, "mode");
        bool appear = ReadBoolean(arguments, "appear");
        bool persisted = ReadBoolean(arguments, "persisted");

        Action<object>? userBeforeEnter = ReadAction(arguments, "onBeforeEnter");
        PhaseHook userEnter = ReadPhaseHook(arguments, "onEnter");
        Action<object>? userAfterEnter = ReadAction(arguments, "onAfterEnter");
        Action<object>? userEnterCancelled = ReadAction(arguments, "onEnterCancelled");
        Action<object>? userBeforeLeave = ReadAction(arguments, "onBeforeLeave");
        PhaseHook userLeave = ReadPhaseHook(arguments, "onLeave");
        Action<object>? userAfterLeave = ReadAction(arguments, "onAfterLeave");
        Action<object>? userLeaveCancelled = ReadAction(arguments, "onLeaveCancelled");
        Action<object>? userBeforeAppear =
            ReadAction(arguments, "onBeforeAppear") ?? userBeforeEnter;
        PhaseHook userAppear = arguments.ContainsKey("onAppear")
            ? ReadPhaseHook(arguments, "onAppear")
            : userEnter;
        Action<object>? userAfterAppear =
            ReadAction(arguments, "onAfterAppear") ?? userAfterEnter;
        Action<object>? userAppearCancelled =
            ReadAction(arguments, "onAppearCancelled") ?? userEnterCancelled;

        if (arguments.TryGetValue("css", out object? css) && css is false)
        {
            return new TransitionProperties
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

        string? expectedType = ReadString(arguments, "type");
        DomTransitionClassNames classNames = ResolveClassNames(arguments);
        (int enterDuration, int leaveDuration) = NormalizeDuration(
            arguments.TryGetValue("duration", out object? duration) ? duration : null);

        void FinishEnter(
            DomTransitionOperations operations,
            int element,
            bool isAppear,
            bool cancelled)
        {
            operations.EnterGenerations[element] =
                operations.EnterGenerations.GetValueOrDefault(element) + 1;
            operations.EnterCancelledFlags[element] = cancelled;
            operations.RemoveTransitionClass(
                element,
                isAppear ? classNames.AppearFrom : classNames.EnterFrom);
            operations.RemoveTransitionClass(
                element,
                isAppear ? classNames.AppearTo : classNames.EnterTo);
            operations.RemoveTransitionClass(
                element,
                isAppear ? classNames.AppearActive : classNames.EnterActive);
        }

        void FinishLeave(
            DomTransitionOperations operations,
            int element,
            Action? complete)
        {
            operations.LeaveGenerations[element] =
                operations.LeaveGenerations.GetValueOrDefault(element) + 1;
            operations.LeavingFlags[element] = false;
            operations.RemoveTransitionClass(element, classNames.LeaveFrom);
            operations.RemoveTransitionClass(element, classNames.LeaveTo);
            operations.RemoveTransitionClass(element, classNames.LeaveActive);
            complete?.Invoke();
        }

        TransitionPhaseHook MakeEnterHook(bool isAppear) => (element, complete) =>
        {
            DomTransitionOperations operations = DomTransitionOperations.Require();
            int handle = BrowserModelDirective.Handle(element);
            PhaseHook userHook = isAppear ? userAppear : userEnter;
            int generation = operations.EnterGenerations.GetValueOrDefault(handle) + 1;
            operations.EnterGenerations[handle] = generation;
            operations.EnterCancelledFlags[handle] = false;
            if (userHook.IsExplicit)
            {
                userHook.Hook?.Invoke(element, complete);
            }
            else
            {
                userHook.SynchronousHook?.Invoke(element);
            }

            operations.NextFrame(() =>
            {
                if (operations.EnterGenerations.GetValueOrDefault(handle) != generation)
                {
                    return;
                }

                operations.RemoveTransitionClass(
                    handle,
                    isAppear ? classNames.AppearFrom : classNames.EnterFrom);
                if (operations.EnterCancelledFlags.GetValueOrDefault(handle))
                {
                    return;
                }

                operations.AddTransitionClass(
                    handle,
                    isAppear ? classNames.AppearTo : classNames.EnterTo);
                if (!userHook.IsExplicit)
                {
                    operations.WhenTransitionEnds(
                        handle,
                        expectedType,
                        enterDuration,
                        complete);
                }
            });
        };

        return new TransitionProperties
        {
            Mode = mode,
            Appear = appear,
            Persisted = persisted,
            OnBeforeEnter = element =>
            {
                userBeforeEnter?.Invoke(element);
                DomTransitionOperations operations = DomTransitionOperations.Require();
                int handle = BrowserModelDirective.Handle(element);
                operations.AddTransitionClass(handle, classNames.EnterFrom);
                operations.AddTransitionClass(handle, classNames.EnterActive);
            },
            OnBeforeAppear = element =>
            {
                userBeforeAppear?.Invoke(element);
                DomTransitionOperations operations = DomTransitionOperations.Require();
                int handle = BrowserModelDirective.Handle(element);
                operations.AddTransitionClass(handle, classNames.AppearFrom);
                operations.AddTransitionClass(handle, classNames.AppearActive);
            },
            OnEnter = MakeEnterHook(isAppear: false),
            OnAppear = MakeEnterHook(isAppear: true),
            OnAfterEnter = element =>
            {
                FinishEnter(
                    DomTransitionOperations.Require(),
                    BrowserModelDirective.Handle(element),
                    isAppear: false,
                    cancelled: false);
                userAfterEnter?.Invoke(element);
            },
            OnAfterAppear = element =>
            {
                FinishEnter(
                    DomTransitionOperations.Require(),
                    BrowserModelDirective.Handle(element),
                    isAppear: true,
                    cancelled: false);
                userAfterAppear?.Invoke(element);
            },
            OnEnterCancelled = element =>
            {
                FinishEnter(
                    DomTransitionOperations.Require(),
                    BrowserModelDirective.Handle(element),
                    isAppear: false,
                    cancelled: true);
                userEnterCancelled?.Invoke(element);
            },
            OnAppearCancelled = element =>
            {
                FinishEnter(
                    DomTransitionOperations.Require(),
                    BrowserModelDirective.Handle(element),
                    isAppear: true,
                    cancelled: true);
                userAppearCancelled?.Invoke(element);
            },
            OnBeforeLeave = userBeforeLeave,
            OnLeave = (element, complete) =>
            {
                DomTransitionOperations operations = DomTransitionOperations.Require();
                int handle = BrowserModelDirective.Handle(element);
                int generation = operations.LeaveGenerations.GetValueOrDefault(handle) + 1;
                operations.LeaveGenerations[handle] = generation;
                operations.LeavingFlags[handle] = true;
                void Resolve() => FinishLeave(operations, handle, complete);
                operations.AddTransitionClass(handle, classNames.LeaveFrom);
                if (!operations.EnterCancelledFlags.GetValueOrDefault(handle))
                {
                    operations.ForceReflow();
                    operations.AddTransitionClass(handle, classNames.LeaveActive);
                }
                else
                {
                    operations.AddTransitionClass(handle, classNames.LeaveActive);
                    operations.ForceReflow();
                }

                operations.NextFrame(() =>
                {
                    if (!operations.LeavingFlags.GetValueOrDefault(handle)
                        || operations.LeaveGenerations.GetValueOrDefault(handle) != generation)
                    {
                        return;
                    }

                    operations.RemoveTransitionClass(handle, classNames.LeaveFrom);
                    operations.AddTransitionClass(handle, classNames.LeaveTo);
                    if (!userLeave.IsExplicit)
                    {
                        operations.WhenTransitionEnds(
                            handle,
                            expectedType,
                            leaveDuration,
                            Resolve);
                    }
                });

                if (userLeave.IsExplicit)
                {
                    userLeave.Hook?.Invoke(element, Resolve);
                }
                else
                {
                    userLeave.SynchronousHook?.Invoke(element);
                }
            },
            OnAfterLeave = userAfterLeave,
            OnLeaveCancelled = element =>
            {
                FinishLeave(
                    DomTransitionOperations.Require(),
                    BrowserModelDirective.Handle(element),
                    complete: null);
                userLeaveCancelled?.Invoke(element);
            },
        };
    }

    internal static DomTransitionClassNames ResolveClassNames(
        IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string name = ReadString(arguments, "name") ?? "v";
        string enterFrom = ClassName(arguments, "enterFromClass", name + "-enter-from");
        string enterActive = ClassName(arguments, "enterActiveClass", name + "-enter-active");
        string enterTo = ClassName(arguments, "enterToClass", name + "-enter-to");
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

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> arguments,
        string name) =>
        arguments.TryGetValue(name, out object? value) ? value as string : null;

    private static bool ReadBoolean(
        IReadOnlyDictionary<string, object?> arguments,
        string name) =>
        arguments.TryGetValue(name, out object? value) && value is true;

    private static string ClassName(
        IReadOnlyDictionary<string, object?> arguments,
        string name,
        string fallback) =>
        arguments.TryGetValue(name, out object? value)
            && value is string text
            && text.Length > 0
                ? text
                : fallback;

    private static Action<object>? ReadAction(
        IReadOnlyDictionary<string, object?> arguments,
        string name) =>
        arguments.TryGetValue(name, out object? value) ? value as Action<object> : null;

    private static PhaseHook ReadPhaseHook(
        IReadOnlyDictionary<string, object?> arguments,
        string name)
    {
        if (!arguments.TryGetValue(name, out object? value))
        {
            return default;
        }

        return value switch
        {
            TransitionPhaseHook hook => new PhaseHook(hook, null, true),
            Action<object> action => new PhaseHook(
                (element, complete) =>
                {
                    action(element);
                    complete();
                },
                action,
                false),
            _ => default,
        };
    }

    private static (int Enter, int Leave) NormalizeDuration(object? duration)
    {
        if (duration is IReadOnlyDictionary<string, object?> map)
        {
            return (
                NumberOf(map.TryGetValue("enter", out object? enter) ? enter : null),
                NumberOf(map.TryGetValue("leave", out object? leave) ? leave : null));
        }

        int scalar = NumberOf(duration);
        return (scalar, scalar);
    }

    private static int NumberOf(object? value) => value switch
    {
        int number => number,
        long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
        double number when double.IsFinite(number)
            && number is >= int.MinValue and <= int.MaxValue => (int)number,
        float number when float.IsFinite(number)
            && number is >= int.MinValue and <= int.MaxValue => (int)number,
        string text when int.TryParse(text, out int parsed) => parsed,
        _ => -1,
    };

    private readonly record struct PhaseHook(
        TransitionPhaseHook? Hook,
        Action<object>? SynchronousHook,
        bool IsExplicit);
}
