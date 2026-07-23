using System;
using System.Collections.Generic;

using Assimalign.Viu;
using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Animates insertion, removal, and keyed reordering for a group of browser-rendered children.
/// </summary>
/// <remarks>
/// This is Viu's browser-hosted port of Vue 3.5's <c>TransitionGroup</c>:
/// https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-dom/src/components/TransitionGroup.ts.
/// Core supplies host-neutral keyed child snapshots and shared transition state; this component owns
/// CSS transition resolution and the DOM-specific FLIP measurement and mutation sequence. Position
/// reads are batched so each before/after pass crosses the browser interop boundary once. The
/// component is intended for the browser's single-threaded event loop and is not thread-safe.
/// </remarks>
public sealed class TransitionGroup : IComponentTemplate
{
    private static readonly IReadOnlyList<IComponentParameter>
        DeclaredParameters = CreateParameterDefinitions();

    private TransitionGroup()
    {
    }

    /// <inheritdoc/>
    public string? Name => "TransitionGroup";

    /// <inheritdoc/>
    public IReadOnlyList<IComponentParameter>? Parameters =>
        DeclaredParameters;

    /// <summary>Gets the AOT-safe registration for the browser transition-group built-in.</summary>
    public static ComponentRegistration Registration =>
        new(
            typeof(TransitionGroup),
            static () => new TransitionGroup(),
            "TransitionGroup");

    /// <inheritdoc/>
    public ComponentRenderer Setup(IComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ComponentTransitionScope transitionScope = new(context);
        Dictionary<object, TransitionRectangle> previousPositions = new();
        Dictionary<int, Action> moveCallbacks = new();
        DomTransitionClassNames classNames =
            Transition.ResolveClassNames(context.Arguments);
        DomTransitionClassNames previousClassNames = classNames;
        IReadOnlyList<KeyedComponentHostElement<int>> previousChildren =
            Array.Empty<KeyedComponentHostElement<int>>();

        context.Lifecycle.OnBeforeUpdate(
            () =>
            {
                previousClassNames = classNames;
                previousChildren =
                    RecordPreviousPositions(
                        context,
                        previousPositions);
            });
        context.Lifecycle.OnUpdated(
            () => RunMoveTransition(
                context,
                transitionScope,
                previousPositions,
                previousChildren,
                moveCallbacks,
                previousClassNames));
        context.Lifecycle.OnBeforeUnmount(
            () => FinishMoveCallbacks(moveCallbacks));

        return () =>
        {
            BaseTransitionProperties properties =
                Transition.ResolveTransitionProperties(context.Arguments);
            classNames =
                Transition.ResolveClassNames(context.Arguments);
            IReadOnlyList<IComponent> children =
                ResolveChildren(context);
            List<IComponent> transitionedChildren =
                new(children.Count);
            for (int index = 0; index < children.Count; index++)
            {
                IComponent child = children[index];
                if (child.Key is not null)
                {
                    transitionedChildren.Add(
                        transitionScope.Attach(child, properties));
                }
                else
                {
                    if (child is not ITextComponent
                        && context is IComponentWarningContext warningContext)
                    {
                        warningContext.Warn(
                            "<TransitionGroup> children must be keyed.");
                    }

                    transitionedChildren.Add(child);
                }
            }

            string? tag = ReadString(context.Arguments, "tag");
            return string.IsNullOrEmpty(tag)
                ? ComponentTree.Fragment(transitionedChildren)
                : ComponentTree.Element(
                    tag,
                    children: transitionedChildren);
        };
    }

    private static IReadOnlyList<KeyedComponentHostElement<int>>
        RecordPreviousPositions(
        IComponentContext context,
        Dictionary<object, TransitionRectangle> previousPositions)
    {
        previousPositions.Clear();
        IReadOnlyList<KeyedComponentHostElement<int>> children =
            ComponentHost.GetKeyedChildElements<int>(context);
        if (children.Count == 0)
        {
            return children;
        }

        DomTransitionOperations operations =
            DomTransitionOperations.Require();
        TransitionRectangle[] positions =
            operations.MeasurePositions(GetHandles(children));
        int count = Math.Min(children.Count, positions.Length);
        for (int index = 0; index < count; index++)
        {
            previousPositions[children[index].Key] =
                positions[index];
        }

        return children;
    }

    private static void RunMoveTransition(
        IComponentContext context,
        ComponentTransitionScope transitionScope,
        Dictionary<object, TransitionRectangle> previousPositions,
        IReadOnlyList<KeyedComponentHostElement<int>> previousChildren,
        Dictionary<int, Action> moveCallbacks,
        DomTransitionClassNames previousClassNames)
    {
        if (previousPositions.Count == 0
            || previousChildren.Count == 0)
        {
            return;
        }

        IReadOnlyList<KeyedComponentHostElement<int>> children =
            ComponentHost.GetKeyedChildElements<int>(context);
        IReadOnlyList<int> rootElements =
            ComponentHost.GetRootElements<int>(context);
        if (rootElements.Count == 0)
        {
            return;
        }

        DomTransitionOperations operations =
            DomTransitionOperations.Require();
        string? configuredMoveClass =
            ReadString(context.Arguments, "moveClass");
        string? configuredName =
            ReadString(context.Arguments, "name");
        string moveClass =
            !string.IsNullOrEmpty(configuredMoveClass)
                ? configuredMoveClass
                : (!string.IsNullOrEmpty(configuredName)
                    ? configuredName
                    : "v")
                + "-move";
        if (!operations.HasCssTransform(
            previousChildren[0].Element,
            rootElements[0],
            moveClass))
        {
            return;
        }

        for (int index = 0;
            index < previousChildren.Count;
            index++)
        {
            int element = previousChildren[index].Element;
            if (moveCallbacks.TryGetValue(
                element,
                out Action? finishMove))
            {
                finishMove();
            }

            if (transitionScope.FinishPendingEnter(element))
            {
                operations.EnterGenerations[element] =
                    operations.EnterGenerations.GetValueOrDefault(element)
                    + 1;
                operations.RemoveTransitionClass(
                    element,
                    previousClassNames.EnterFrom);
                operations.RemoveTransitionClass(
                    element,
                    previousClassNames.AppearFrom);
            }
        }

        if (children.Count == 0)
        {
            return;
        }

        TransitionRectangle[] currentPositions =
            operations.MeasurePositions(GetHandles(children));
        List<KeyedComponentHostElement<int>> moved = new();
        int count = Math.Min(children.Count, currentPositions.Length);
        for (int index = 0; index < count; index++)
        {
            KeyedComponentHostElement<int> child = children[index];
            if (!previousPositions.TryGetValue(
                child.Key,
                out TransitionRectangle previousPosition))
            {
                continue;
            }

            TransitionRectangle currentPosition =
                currentPositions[index];
            double horizontalDelta =
                (previousPosition.Left - currentPosition.Left)
                / NormalizeScale(currentPosition.HorizontalScale);
            double verticalDelta =
                (previousPosition.Top - currentPosition.Top)
                / NormalizeScale(currentPosition.VerticalScale);
            if (horizontalDelta == 0 && verticalDelta == 0)
            {
                continue;
            }

            operations.SetMoveTransform(
                child.Element,
                horizontalDelta,
                verticalDelta);
            moved.Add(child);
        }

        operations.ForceReflow();
        for (int index = 0; index < moved.Count; index++)
        {
            int element = moved[index].Element;
            operations.AddTransitionClass(element, moveClass);
            operations.ClearMoveStyles(element);
        }

        for (int index = 0; index < moved.Count; index++)
        {
            int element = moved[index].Element;
            Action? finishMove = null;
            finishMove = () =>
            {
                if (!moveCallbacks.TryGetValue(
                    element,
                    out Action? current)
                    || !ReferenceEquals(current, finishMove))
                {
                    return;
                }

                moveCallbacks.Remove(element);
                operations.RemoveTransitionClass(
                    element,
                    moveClass);
            };
            moveCallbacks[element] = finishMove;
            operations.WhenMoveEnds(element, finishMove);
        }
    }

    private static IReadOnlyList<IComponent> ResolveChildren(
        IComponentContext context)
    {
        if (!context.Slots.TryGetValue(
            "default",
            out ComponentSlot? slot))
        {
            return Array.Empty<IComponent>();
        }

        IComponent? rendered =
            slot(new ComponentArguments());
        return rendered switch
        {
            null => Array.Empty<IComponent>(),
            IFragmentComponent fragment => fragment.Children,
            _ => new[] { rendered },
        };
    }

    private static int[] GetHandles(
        IReadOnlyList<KeyedComponentHostElement<int>> children)
    {
        int[] handles = new int[children.Count];
        for (int index = 0; index < children.Count; index++)
        {
            handles[index] = children[index].Element;
        }

        return handles;
    }

    private static void FinishMoveCallbacks(
        Dictionary<int, Action> moveCallbacks)
    {
        Action[] callbacks =
            new Action[moveCallbacks.Count];
        moveCallbacks.Values.CopyTo(callbacks, 0);
        for (int index = 0; index < callbacks.Length; index++)
        {
            callbacks[index]();
        }
    }

    private static IReadOnlyList<IComponentParameter>
        CreateParameterDefinitions()
    {
        List<IComponentParameter> parameters =
            new(Transition.ParameterDefinitions.Count + 1)
            {
                new ComponentParameter("tag"),
                new ComponentParameter("moveClass"),
            };
        for (int index = 0;
            index < Transition.ParameterDefinitions.Count;
            index++)
        {
            IComponentParameter parameter =
                Transition.ParameterDefinitions[index];
            if (!string.Equals(
                parameter.Name,
                "mode",
                StringComparison.Ordinal))
            {
                parameters.Add(parameter);
            }
        }

        return parameters.AsReadOnly();
    }

    private static string? ReadString(
        IComponentArguments arguments,
        string name)
    {
        return arguments[name] as string;
    }

    private static double NormalizeScale(double scale)
    {
        return double.IsFinite(scale)
            && scale != 0
            ? scale
            : 1;
    }
}
