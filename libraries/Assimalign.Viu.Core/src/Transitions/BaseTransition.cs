using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Wraps one slot child with platform-neutral enter and leave behavior.
/// </summary>
/// <remarks>
/// This is Viu's host-generic port of Vue 3.5's <c>BaseTransition</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/BaseTransition.ts.
/// Host-specific packages such as <c>Assimalign.Viu.Browser</c> supply CSS-aware callbacks through
/// <see cref="BaseTransitionProperties"/>. The template is not thread-safe.
/// </remarks>
public sealed class BaseTransition : IComponentTemplate
{
    /// <summary>
    /// Gets the reserved argument carrying an already resolved
    /// <see cref="BaseTransitionProperties"/> instance.
    /// </summary>
    public const string PropertiesArgument = "$baseTransition";

    private static readonly IReadOnlyList<IComponentParameter> DeclaredParameters =
    [
        new ComponentParameter(PropertiesArgument),
        new ComponentParameter("mode"),
        new ComponentParameter("appear"),
        new ComponentParameter("persisted"),
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

    /// <inheritdoc/>
    public string? Name => "BaseTransition";

    /// <inheritdoc/>
    public ComponentFlags Flags => ComponentFlags.None;

    /// <inheritdoc/>
    public IReadOnlyList<IComponentParameter>? Parameters => DeclaredParameters;

    /// <summary>Gets an explicit AOT-safe registration for the built-in template.</summary>
    public static ComponentRegistration Registration =>
        new(
            typeof(BaseTransition),
            static () => new BaseTransition(),
            "BaseTransition");

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TransitionState state = new();
        ReactiveValue<int> invalidation = Reactive.Reference(0);
        IComponent? previousRaw = null;
        ITransitionedComponent? previousTransitioned = null;

        context.Lifecycle.OnMounted(() => state.IsMounted = true);
        context.Lifecycle.OnBeforeUnmount(() => state.IsUnmounting = true);

        return () =>
        {
            _ = invalidation.Value;
            IComponent? raw = ResolveChild(context);
            if (raw is null)
            {
                previousRaw = null;
                previousTransitioned = null;
                return ComponentTree.Comment();
            }

            if (state.IsLeaving)
            {
                return ComponentTree.Comment();
            }

            BaseTransitionProperties properties = ResolveProperties(context.Arguments);
            TransitionHooks enterHooks = new(raw, properties, state);
            IComponent next = TransitionComponents.Attach(raw, enterHooks);
            ITransitionedComponent? nextTransitioned =
                next as ITransitionedComponent;

            if (previousRaw is not null
                && previousTransitioned is not null
                && !TransitionComponents.IsSameType(previousRaw, raw))
            {
                TransitionHooks leavingHooks = previousTransitioned.Transition;
                if (string.Equals(properties.Mode, "out-in", StringComparison.Ordinal))
                {
                    state.IsLeaving = true;
                    leavingHooks.AfterLeave = () =>
                    {
                        state.IsLeaving = false;
                        invalidation.Value++;
                    };
                    previousRaw = raw;
                    previousTransitioned = nextTransitioned;
                    return ComponentTree.Comment();
                }

                if (string.Equals(properties.Mode, "in-out", StringComparison.Ordinal))
                {
                    leavingHooks.DelayLeave = (
                        element,
                        earlyRemove,
                        delayedLeave) =>
                    {
                        state.LeaveCallbacks[element] = _ => earlyRemove();
                        enterHooks.DelayedLeave = delayedLeave;
                    };
                }
            }

            previousRaw = raw;
            previousTransitioned = nextTransitioned;
            return next;
        };
    }

    internal static BaseTransitionProperties ResolveProperties(
        IComponentArguments arguments)
    {
        if (arguments[PropertiesArgument] is BaseTransitionProperties resolved)
        {
            return resolved;
        }

        return new BaseTransitionProperties
        {
            Mode = arguments["mode"] as string,
            Appear = arguments["appear"] is true,
            Persisted = arguments["persisted"] is true,
            OnBeforeEnter = ReadAction(arguments, "onBeforeEnter"),
            OnEnter = ReadEnterHook(arguments, "onEnter"),
            OnAfterEnter = ReadAction(arguments, "onAfterEnter"),
            OnEnterCancelled = ReadAction(arguments, "onEnterCancelled"),
            OnBeforeLeave = ReadAction(arguments, "onBeforeLeave"),
            OnLeave = ReadEnterHook(arguments, "onLeave"),
            OnAfterLeave = ReadAction(arguments, "onAfterLeave"),
            OnLeaveCancelled = ReadAction(arguments, "onLeaveCancelled"),
            OnBeforeAppear = ReadAction(arguments, "onBeforeAppear"),
            OnAppear = ReadEnterHook(arguments, "onAppear"),
            OnAfterAppear = ReadAction(arguments, "onAfterAppear"),
            OnAppearCancelled = ReadAction(arguments, "onAppearCancelled"),
        };
    }

    private static IComponent? ResolveChild(IComponentContext context)
    {
        if (!context.Slots.TryGetValue("default", out ComponentSlot? slot))
        {
            return null;
        }

        IComponent? child = slot(new ComponentArguments());
        if (child is not IFragmentComponent fragment)
        {
            return child;
        }

        IComponent? selected = null;
        for (int index = 0; index < fragment.Children.Count; index++)
        {
            IComponent candidate = fragment.Children[index];
            if (candidate.Kind == ComponentKind.Comment)
            {
                continue;
            }

            selected ??= candidate;
        }

        return selected;
    }

    private static Action<object>? ReadAction(
        IComponentArguments arguments,
        string name)
    {
        return arguments[name] as Action<object>;
    }

    private static TransitionEnterHook? ReadEnterHook(
        IComponentArguments arguments,
        string name)
    {
        object? value = arguments[name];
        return value switch
        {
            TransitionEnterHook hook => hook,
            Action<object> action => (element, done) =>
            {
                action(element);
                done();
            },
            _ => null,
        };
    }
}
