using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Browser;

/// <summary>Animates keyed Browser children as they enter, leave, and change position.</summary>
/// <remarks>
/// Each keyed child receives the ordinary Browser transition behavior. An outer structural
/// transition consumes Core's outgoing and incoming keyed-element snapshots to run a batched FLIP
/// position pass without querying mounted renderer internals. Unkeyed non-text children produce a
/// warning through <see cref="ComponentContext.Warn"/>. Specified by <c>[BLT-7]</c> through
/// <c>[BLT-9]</c>.
/// </remarks>
public sealed class TransitionGroup : IComponent
{
    private static readonly IReadOnlyList<ComponentParameter> Parameters = CreateParameters();
    private static readonly ComponentContract Contract = new(
        displayName: "TransitionGroup",
        flags: ComponentFlags.InheritFallthroughBindings,
        parameters: Parameters);

    private TransitionGroup()
    {
    }

    /// <summary>Gets the reflection-free component registration.</summary>
    public static ComponentRegistration Registration { get; } = new(
        ComponentReference.ForType(typeof(TransitionGroup)),
        Contract,
        static _ => new TransitionGroup());

    /// <inheritdoc/>
    public ComponentRenderer Setup(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ComponentTransitionScope scope = new(context);
        Dictionary<object, TransitionRectangle> previousPositions = [];
        Dictionary<int, Action> moveCallbacks = [];
        string measuredMoveClass = "v-move";
        DomTransitionClassNames measuredClassNames = new(
            "v-enter-from",
            "v-enter-active",
            "v-enter-to",
            "v-enter-from",
            "v-enter-active",
            "v-enter-to",
            "v-leave-from",
            "v-leave-active",
            "v-leave-to");

        context.Lifecycle.OnBeforeUnmount(() => FinishMoveCallbacks(moveCallbacks));

        return _ =>
        {
            IReadOnlyDictionary<string, object?> arguments = context.Bindings.Parameters;
            TransitionProperties childProperties =
                Transition.ResolveTransitionProperties(arguments);
            DomTransitionClassNames classNames = Transition.ResolveClassNames(arguments);
            string moveClass = ResolveMoveClass(arguments);
            IReadOnlyList<VirtualNode> children = ResolveChildren(context.Bindings.Slots);
            List<VirtualNode> transitionedChildren = new(children.Count);
            for (int index = 0; index < children.Count; index++)
            {
                VirtualNode child = children[index];
                if (child.Key is { } key)
                {
                    transitionedChildren.Add(scope.Attach(child, childProperties, key));
                }
                else
                {
                    if (child is not TextNode)
                    {
                        context.Warn("<TransitionGroup> children must be keyed.");
                    }

                    transitionedChildren.Add(child);
                }
            }

            TransitionProperties observerProperties = new()
            {
                OnBeforeUpdate = outgoing => RecordPreviousPositions(
                    scope,
                    outgoing,
                    previousPositions,
                    moveCallbacks,
                    classNames,
                    moveClass,
                    out measuredClassNames,
                    out measuredMoveClass),
                OnUpdated = incoming => RunMoveTransition(
                    incoming,
                    previousPositions,
                    moveCallbacks,
                    measuredClassNames,
                    measuredMoveClass),
            };
            TransitionNode observer = scope.Attach(
                new FragmentNode(transitionedChildren),
                observerProperties);
            return ReadString(arguments, "tag") is { Length: > 0 } tag
                ? new ElementNode(new QualifiedName(tag), children: [observer])
                : observer;
        };
    }

    private static void RecordPreviousPositions(
        ComponentTransitionScope scope,
        IReadOnlyList<TransitionElementSnapshot> children,
        Dictionary<object, TransitionRectangle> previousPositions,
        Dictionary<int, Action> moveCallbacks,
        DomTransitionClassNames classNames,
        string moveClass,
        out DomTransitionClassNames measuredClassNames,
        out string measuredMoveClass)
    {
        previousPositions.Clear();
        measuredClassNames = classNames;
        measuredMoveClass = moveClass;
        if (children.Count == 0)
        {
            return;
        }

        DomTransitionOperations operations = DomTransitionOperations.Require();
        int[] handles = GetHandles(children);
        for (int index = 0; index < handles.Length; index++)
        {
            int element = handles[index];
            if (moveCallbacks.TryGetValue(element, out Action? finishMove))
            {
                finishMove();
            }

            if (scope.FinishPendingEnter(element))
            {
                operations.EnterGenerations[element] =
                    operations.EnterGenerations.GetValueOrDefault(element) + 1;
                operations.RemoveTransitionClass(element, classNames.EnterFrom);
                operations.RemoveTransitionClass(element, classNames.AppearFrom);
            }
        }

        TransitionRectangle[] positions = operations.MeasurePositions(handles);
        int count = Math.Min(children.Count, positions.Length);
        for (int index = 0; index < count; index++)
        {
            previousPositions[children[index].Key] = positions[index];
        }
    }

    private static void RunMoveTransition(
        IReadOnlyList<TransitionElementSnapshot> children,
        Dictionary<object, TransitionRectangle> previousPositions,
        Dictionary<int, Action> moveCallbacks,
        DomTransitionClassNames previousClassNames,
        string moveClass)
    {
        if (children.Count == 0 || previousPositions.Count == 0)
        {
            return;
        }

        DomTransitionOperations operations = DomTransitionOperations.Require();
        int[] handles = GetHandles(children);
        int transformProbe = handles[0];
        int root = operations.ParentNode(transformProbe);
        if (root == 0
            || !operations.HasCssTransform(transformProbe, root, moveClass))
        {
            return;
        }

        TransitionRectangle[] positions = operations.MeasurePositions(handles);
        List<int> moved = [];
        int count = Math.Min(children.Count, positions.Length);
        for (int index = 0; index < count; index++)
        {
            if (!previousPositions.TryGetValue(
                children[index].Key,
                out TransitionRectangle previousPosition))
            {
                continue;
            }

            TransitionRectangle currentPosition = positions[index];
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

            int element = handles[index];
            operations.SetMoveTransform(element, horizontalDelta, verticalDelta);
            moved.Add(element);
        }

        if (moved.Count == 0)
        {
            return;
        }

        operations.ForceReflow();
        for (int index = 0; index < moved.Count; index++)
        {
            int element = moved[index];
            operations.RemoveTransitionClass(element, previousClassNames.EnterFrom);
            operations.RemoveTransitionClass(element, previousClassNames.AppearFrom);
            operations.AddTransitionClass(element, moveClass);
            operations.ClearMoveStyles(element);
        }

        for (int index = 0; index < moved.Count; index++)
        {
            int element = moved[index];
            Action? finishMove = null;
            finishMove = () =>
            {
                if (!moveCallbacks.TryGetValue(element, out Action? current)
                    || !ReferenceEquals(current, finishMove))
                {
                    return;
                }

                moveCallbacks.Remove(element);
                operations.RemoveTransitionClass(element, moveClass);
            };
            moveCallbacks[element] = finishMove;
            operations.WhenMoveEnds(element, finishMove);
        }
    }

    private static IReadOnlyList<VirtualNode> ResolveChildren(
        IReadOnlyDictionary<string, ComponentSlot> slots)
    {
        if (!slots.TryGetValue("default", out ComponentSlot? slot))
        {
            return Array.Empty<VirtualNode>();
        }

        VirtualNode? rendered = slot(new Dictionary<string, object?>());
        return rendered switch
        {
            null => Array.Empty<VirtualNode>(),
            FragmentNode fragment => fragment.Children,
            _ => new[] { rendered },
        };
    }

    private static int[] GetHandles(IReadOnlyList<TransitionElementSnapshot> children)
    {
        int[] handles = new int[children.Count];
        for (int index = 0; index < children.Count; index++)
        {
            handles[index] = BrowserModelDirective.Handle(children[index].Element);
        }

        return handles;
    }

    private static void FinishMoveCallbacks(Dictionary<int, Action> moveCallbacks)
    {
        Action[] callbacks = new Action[moveCallbacks.Count];
        moveCallbacks.Values.CopyTo(callbacks, 0);
        for (int index = 0; index < callbacks.Length; index++)
        {
            callbacks[index]();
        }
    }

    private static IReadOnlyList<ComponentParameter> CreateParameters()
    {
        List<ComponentParameter> parameters =
        [
            new("tag"),
            new("moveClass"),
        ];
        for (int index = 0; index < Transition.ParameterDefinitions.Count; index++)
        {
            ComponentParameter parameter = Transition.ParameterDefinitions[index];
            if (!string.Equals(parameter.Name, "mode", StringComparison.Ordinal))
            {
                parameters.Add(parameter);
            }
        }

        return parameters.AsReadOnly();
    }

    private static string ResolveMoveClass(
        IReadOnlyDictionary<string, object?> arguments)
    {
        string? configured = ReadString(arguments, "moveClass");
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        return (ReadString(arguments, "name") ?? "v") + "-move";
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> arguments,
        string name) =>
        arguments.TryGetValue(name, out object? value) ? value as string : null;

    private static double NormalizeScale(double scale) =>
        double.IsFinite(scale) && scale != 0 ? scale : 1;
}
