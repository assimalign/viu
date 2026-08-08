using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

public sealed partial class Renderer<TNode>
    where TNode : notnull
{
    private MountedComponent<TNode> MountComponent(
        MountedTree<TNode> tree,
        ComponentNode value,
        TNode container,
        TNode? anchor,
        RuntimeComponentContext? owner)
    {
        IApplicationContext application = tree.Application
            ?? throw new InvalidOperationException(
                "Mounting an authored component requires an application context.");
        int componentIdentifier = checked(++_nextComponentIdentifier);
        ApplicationWatchScheduler watchScheduler = new(componentIdentifier);
        ComponentRuntimeOptions runtimeOptions = new(
            application.Components,
            watchScheduler,
            application.Services,
            application.ErrorHandler,
            application.WarnHandler,
            application.EventObserver);
        var componentHost = new ComponentHost(runtimeOptions);
        ComponentActivation activation = componentHost.ActivatePersistent(
            value,
            owner,
            watchScheduler,
            _activeSuspenseBoundary);

        MountedComponent<TNode>? mounted = null;
        ReactiveEffect renderEffect = activation.Scope.Run(
            () => new ReactiveEffect(
                () =>
                {
                    VirtualNode? rendered = activation.Render();
                    VirtualNode normalized = NormalizeComponentRoot(
                        activation,
                        rendered);
                    if (mounted is not null)
                    {
                        mounted.PendingTree = normalized;
                    }
                    else
                    {
                        _pendingInitialComponentTree = normalized;
                    }
                }));

        try
        {
            renderEffect.Run();
            VirtualNode initialTree = _pendingInitialComponentTree
                ?? new CommentNode(string.Empty);
            _pendingInitialComponentTree = null;
            activation.Context.Run(activation.Lifecycle.InvokeBeforeMount);
            MountedNode<TNode> subtree = Mount(
                tree,
                initialTree,
                container,
                anchor,
                activation.Context);
            mounted = new MountedComponent<TNode>(
                value,
                activation,
                subtree,
                owner)
            {
                RenderEffect = renderEffect,
            };
            SchedulerJob renderJob = new(
                () => UpdateMountedComponent(tree, mounted, container, force: false))
            {
                Identifier = componentIdentifier,
                Name = "component render",
                AllowRecurse = true,
            };
            mounted.RenderJob = renderJob;
            renderEffect.Scheduler = () => Scheduler.QueueJob(renderJob);
            Register(tree, value, mounted);
            UpdateReference(tree, mounted, null, value.MountReference);
            QueueMountedLifecycle(mounted);
            mounted.HotReloadRegistration = ComponentHotReload.RegisterMountedComponent(
                activation.Instance.GetType(),
                change =>
                {
                    if (change is ComponentHotReloadChangeKind.Template
                        or ComponentHotReloadChangeKind.ScriptReset)
                    {
                        QueueComponentRemount(tree, mounted, container);
                    }
                });
            return mounted;
        }
        catch
        {
            _pendingInitialComponentTree = null;
            renderEffect.Stop();
            activation.Release();
            throw;
        }
    }

    private VirtualNode? _pendingInitialComponentTree;

    private void PatchComponent(
        MountedTree<TNode> tree,
        MountedComponent<TNode> mounted,
        ComponentNode next,
        TNode container)
    {
        ComponentNode previous = (ComponentNode)mounted.Value;
        SlotFlags previousSlotFlags = mounted.Context.EffectiveSlotFlags;
        mounted.Activation.Update(next);
        SlotFlags nextSlotFlags = mounted.Context.EffectiveSlotFlags;
        bool mustForwardMountReference =
            mounted.Instance is AsynchronousComponentWrapper
            && !ReferenceEquals(previous.MountReference, next.MountReference);
        if (mustForwardMountReference
            || next.RenderPlan.PatchFlags != PatchFlags.Cached
                && ShouldUpdateComponent(
                    previous,
                    next,
                    previousSlotFlags,
                    nextSlotFlags))
        {
            Scheduler.InvalidateJob(mounted.RenderJob);
            UpdateMountedComponent(tree, mounted, container, force: true);
        }

        UpdateReference(tree, mounted, previous.MountReference, next.MountReference);
        ReplaceValue(tree, mounted, next);
    }

    private static bool ShouldUpdateComponent(
        ComponentNode previous,
        ComponentNode next,
        SlotFlags previousSlotFlags,
        SlotFlags nextSlotFlags)
    {
        PatchFlags patchFlags = next.RenderPlan.PatchFlags;
        if ((int)patchFlags > 0
            && (patchFlags & PatchFlags.DynamicSlots) != 0)
        {
            return true;
        }

        ComponentInvocation previousInvocation = previous.Invocation;
        ComponentInvocation nextInvocation = next.Invocation;
        if (previousInvocation.Directives.Count > 0
            || nextInvocation.Directives.Count > 0)
        {
            return true;
        }

        if (!HaveSameSlotStructure(
                previousInvocation.Slots,
                nextInvocation.Slots)
            || nextSlotFlags == SlotFlags.Dynamic
            || previousSlotFlags != nextSlotFlags)
        {
            return true;
        }

        return !HaveSameArguments(
            previousInvocation.Arguments,
            nextInvocation.Arguments);
    }

    private static bool HaveSameSlotStructure(
        IReadOnlyDictionary<string, ComponentSlot> previous,
        IReadOnlyDictionary<string, ComponentSlot> next)
    {
        if (previous.Count != next.Count)
        {
            return false;
        }

        foreach (string name in previous.Keys)
        {
            if (!next.ContainsKey(name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HaveSameArguments(
        IReadOnlyDictionary<string, object?> previous,
        IReadOnlyDictionary<string, object?> next)
    {
        if (ReferenceEquals(previous, next))
        {
            return true;
        }

        if (previous.Count != next.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, object?> argument in previous)
        {
            if (!next.TryGetValue(argument.Key, out object? nextValue)
                || !Equals(argument.Value, nextValue))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateMountedComponent(
        MountedTree<TNode> tree,
        MountedComponent<TNode> mounted,
        TNode fallbackContainer,
        bool force)
    {
        if (mounted.IsUnmounted || mounted.Activation.IsReleased)
        {
            return;
        }

        mounted.Context.Run(mounted.Lifecycle.InvokeBeforeUpdate);
        mounted.PendingTree = null;
        if (force)
        {
            mounted.RenderEffect.Run();
        }
        else
        {
            mounted.RenderEffect.RunIfDirty();
        }

        if (mounted.PendingTree is null)
        {
            return;
        }

        TNode parent = HostParentOrFallback(
            mounted.Subtree.FirstHostNode,
            fallbackContainer);
        TNode? anchor = GetNextHostNode(mounted.Subtree);
        mounted.Subtree = Patch(
            tree,
            mounted.Subtree,
            mounted.PendingTree,
            parent,
            anchor,
            mounted.Context);
        mounted.PendingTree = null;
        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (!mounted.IsUnmounted)
                    {
                        mounted.Context.Run(mounted.Lifecycle.InvokeUpdated);
                    }
                })
            {
                Name = "component updated lifecycle",
            });
        QueueHostCommit();
    }

    private static VirtualNode NormalizeComponentRoot(
        ComponentActivation activation,
        VirtualNode? rendered)
    {
        VirtualNode root = rendered ?? new CommentNode(string.Empty);
        ComponentContract contract = activation.Registration.Contract;
        ComponentInvocation invocation = activation.Context.Invocation;
        bool transfersFallthrough =
            (contract.Flags & ComponentFlags.InheritFallthroughBindings) != 0
            && activation.Context.Bindings.FallthroughBindings.Count > 0;
        bool transfersDirectives = invocation.Directives.Count > 0;
        bool transfersLifecycleBindings = HasComponentNodeLifecycleBindings(invocation);
        if (!transfersFallthrough
            && !transfersDirectives
            && !transfersLifecycleBindings)
        {
            return root;
        }

        if (root is not ElementNode element)
        {
            activation.Context.Warn(
                "Component root bindings and directives require one element root.");
            return root;
        }

        List<ElementBinding> bindings = new(element.Bindings);
        if (transfersFallthrough)
        {
            foreach (KeyValuePair<string, object?> fallthrough in
                activation.Context.Bindings.FallthroughBindings)
            {
                ElementBinding binding = fallthrough.Value as ElementBinding
                    ?? ElementBinding.Attribute(
                        new QualifiedName(fallthrough.Key),
                        fallthrough.Value);
                MergeFallthroughBinding(bindings, binding);
            }
        }

        if (transfersLifecycleBindings)
        {
            foreach (KeyValuePair<string, object?> argument in invocation.Arguments)
            {
                if (!IsComponentNodeLifecycleBindingName(argument.Key))
                {
                    continue;
                }

                ElementBinding binding = argument.Value as ElementBinding
                    ?? ElementBinding.Property(argument.Key, argument.Value);
                MergeFallthroughBinding(bindings, binding);
            }
        }

        List<DirectiveInvocation> directives = new(element.Directives);
        directives.AddRange(invocation.Directives);
        return new ElementNode(
            element.Name,
            bindings,
            element.Children,
            directives,
            element.Key,
            element.MountReference,
            element.RenderPlan);
    }

    private static bool HasComponentNodeLifecycleBindings(ComponentInvocation invocation)
    {
        foreach (string name in invocation.Arguments.Keys)
        {
            if (IsComponentNodeLifecycleBindingName(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsComponentNodeLifecycleBindingName(string name) =>
        name.StartsWith("onVnode", StringComparison.Ordinal);

    private static void MergeFallthroughBinding(
        List<ElementBinding> bindings,
        ElementBinding fallthrough)
    {
        for (int index = 0; index < bindings.Count; index++)
        {
            ElementBinding existing = bindings[index];
            if (existing.Kind != fallthrough.Kind || existing.Name != fallthrough.Name)
            {
                continue;
            }

            if (existing.Kind is not ElementBindingKind.Event
                && string.Equals(
                    existing.Name.LocalName,
                    "class",
                    StringComparison.Ordinal))
            {
                bindings[index] = RecreateBinding(
                    existing,
                    MergeClassValues(existing.Value, fallthrough.Value));
            }
            else if (existing.Kind is not ElementBindingKind.Event
                && string.Equals(
                    existing.Name.LocalName,
                    "style",
                    StringComparison.Ordinal))
            {
                bindings[index] = RecreateBinding(
                    existing,
                    MergeStyleValues(existing.Value, fallthrough.Value));
            }
            else if (existing.Kind is ElementBindingKind.Event
                && existing.Value is Delegate rootListener
                && fallthrough.Value is Delegate parentListener
                && rootListener.GetType() == parentListener.GetType())
            {
                bindings[index] = ElementBinding.Event(
                    existing.Name.LocalName,
                    Delegate.Combine(rootListener, parentListener));
            }
            else
            {
                bindings[index] = fallthrough;
            }

            return;
        }

        bindings.Add(fallthrough);
    }

    private static ElementBinding RecreateBinding(
        ElementBinding binding,
        object? value) => binding.Kind switch
        {
            ElementBindingKind.Attribute => ElementBinding.Attribute(binding.Name, value),
            ElementBindingKind.Property => ElementBinding.Property(binding.Name.LocalName, value),
            _ => throw new InvalidOperationException(
                "Only attribute and property bindings carry class or style values."),
        };

    private static string MergeClassValues(object? rootValue, object? parentValue)
    {
        var result = new StringBuilder();
        AppendClassValue(result, rootValue);
        AppendClassValue(result, parentValue);
        return result.ToString();
    }

    private static void AppendClassValue(StringBuilder result, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string text:
                AppendClassToken(result, text);
                return;
            case IReadOnlyDictionary<string, object?> conditions:
                foreach (KeyValuePair<string, object?> condition in conditions)
                {
                    if (IsClassConditionTruthy(condition.Value))
                    {
                        AppendClassToken(result, condition.Key);
                    }
                }

                return;
            case IDictionary dictionary:
                foreach (DictionaryEntry condition in dictionary)
                {
                    if (condition.Key is string name
                        && IsClassConditionTruthy(condition.Value))
                    {
                        AppendClassToken(result, name);
                    }
                }

                return;
            case IEnumerable values:
                foreach (object? entry in values)
                {
                    AppendClassValue(result, entry);
                }

                return;
            default:
                AppendClassToken(result, value.ToString());
                return;
        }
    }

    private static void AppendClassToken(StringBuilder result, string? value)
    {
        string token = value?.Trim() ?? string.Empty;
        if (token.Length == 0)
        {
            return;
        }

        if (result.Length > 0)
        {
            result.Append(' ');
        }

        result.Append(token);
    }

    private static bool IsClassConditionTruthy(object? value) => value switch
    {
        null => false,
        bool condition => condition,
        string text => text.Length > 0,
        sbyte number => number != 0,
        byte number => number != 0,
        short number => number != 0,
        ushort number => number != 0,
        int number => number != 0,
        uint number => number != 0,
        long number => number != 0,
        ulong number => number != 0,
        float number => number != 0 && !float.IsNaN(number),
        double number => number != 0 && !double.IsNaN(number),
        decimal number => number != 0,
        _ => true,
    };

    private static IReadOnlyDictionary<string, object?> MergeStyleValues(
        object? rootValue,
        object? parentValue)
    {
        Dictionary<string, object?> declarations = new(StringComparer.Ordinal);
        AppendStyleValue(declarations, rootValue);
        AppendStyleValue(declarations, parentValue);
        return new ReadOnlyDictionary<string, object?>(declarations);
    }

    private static void AppendStyleValue(
        Dictionary<string, object?> declarations,
        object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string text:
                AppendStyleText(declarations, text);
                return;
            case IReadOnlyDictionary<string, object?> values:
                foreach (KeyValuePair<string, object?> declaration in values)
                {
                    declarations[declaration.Key] = declaration.Value;
                }

                return;
            case IDictionary dictionary:
                foreach (DictionaryEntry declaration in dictionary)
                {
                    if (declaration.Key is string name)
                    {
                        declarations[name] = declaration.Value;
                    }
                }

                return;
            case IEnumerable values:
                foreach (object? entry in values)
                {
                    AppendStyleValue(declarations, entry);
                }

                return;
            default:
                AppendStyleText(declarations, value.ToString() ?? string.Empty);
                return;
        }
    }

    private static void AppendStyleText(
        Dictionary<string, object?> declarations,
        string value)
    {
        int declarationStart = 0;
        int parenthesesDepth = 0;
        for (int index = 0; index <= value.Length; index++)
        {
            if (index < value.Length)
            {
                if (value[index] == '(')
                {
                    parenthesesDepth++;
                }
                else if (value[index] == ')' && parenthesesDepth > 0)
                {
                    parenthesesDepth--;
                }
            }

            if (index < value.Length
                && (value[index] != ';' || parenthesesDepth != 0))
            {
                continue;
            }

            ReadOnlySpan<char> declaration = value.AsSpan(
                declarationStart,
                index - declarationStart);
            int separator = declaration.IndexOf(':');
            if (separator > 0)
            {
                string name = declaration[..separator].Trim().ToString();
                string declarationValue = declaration[(separator + 1)..].Trim().ToString();
                if (name.Length > 0 && declarationValue.Length > 0)
                {
                    declarations[name] = declarationValue;
                }
            }

            declarationStart = index + 1;
        }
    }

    private static void QueueMountedLifecycle(MountedComponent<TNode> mounted)
    {
        Scheduler.QueuePostFlushCallback(
            new SchedulerJob(
                () =>
                {
                    if (!mounted.IsUnmounted)
                    {
                        mounted.Context.Run(mounted.Lifecycle.InvokeMounted);
                    }
                })
            {
                Name = "component mounted lifecycle",
            });
    }

    private void UnmountComponent(
        MountedTree<TNode> tree,
        MountedComponent<TNode> mounted,
        bool removeHostNodes)
    {
        mounted.RenderJob.IsDisposed = true;
        Scheduler.InvalidateJob(mounted.RenderJob);
        mounted.RenderEffect.Stop();
        mounted.HotReloadRegistration?.Dispose();
        mounted.HotReloadRegistration = null;
        mounted.Activation.ReleaseClient(
            () => Unmount(tree, mounted.Subtree, removeHostNodes));
    }

    private void QueueComponentRemount(
        MountedTree<TNode> tree,
        MountedComponent<TNode> mounted,
        TNode fallbackContainer)
    {
        Scheduler.QueueJob(
            new SchedulerJob(
                () =>
                {
                    if (mounted.IsUnmounted)
                    {
                        return;
                    }

                    ComponentNode request = (ComponentNode)mounted.Value;
                    TNode parent = HostParentOrFallback(
                        mounted.FirstHostNode,
                        fallbackContainer);
                    TNode? anchor = GetNextHostNode(mounted);
                    RuntimeComponentContext? owner = mounted.Owner;
                    bool replacesRoot = ReferenceEquals(tree.Root, mounted);
                    Unmount(tree, mounted, removeHostNodes: true);
                    MountedNode<TNode> replacement = Mount(
                        tree,
                        request,
                        parent,
                        anchor,
                        owner);
                    if (replacesRoot)
                    {
                        tree.Root = replacement;
                    }
                    else if (tree.Root is not null)
                    {
                        ReplaceMountedNodeReference(tree.Root, mounted, replacement);
                    }

                    QueueHostCommit();
                })
            {
                Name = "component hot-reload remount",
            });
    }

    internal static void CollectViewsForBuiltIn(
        MountedNode<TNode> mounted,
        List<MountedComponentView<TNode>> views) =>
        CollectMountedComponentViews(mounted, views);
}
