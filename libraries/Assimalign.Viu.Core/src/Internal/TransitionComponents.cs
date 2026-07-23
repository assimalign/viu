using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

internal interface ITransitionedComponent : IComponent
{
    TransitionHooks Transition { get; }

    IComponent Inner { get; }
}

internal static class TransitionComponents
{
    internal static IComponent Attach(
        IComponent component,
        TransitionHooks transition)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(transition);
        IComponent inner = component is ITransitionedComponent existing
            ? existing.Inner
            : component;
        return inner switch
        {
            IElementComponent element =>
                new TransitionedElementComponent(element, transition),
            ITemplateComponent template =>
                new TransitionedTemplateComponent(template, transition),
            IFragmentComponent fragment =>
                new TransitionedFragmentComponent(fragment, transition),
            _ => inner,
        };
    }

    internal static TransitionHooks? Get(IComponent component)
    {
        return (component as ITransitionedComponent)?.Transition;
    }

    internal static bool IsSameType(IComponent left, IComponent right)
    {
        IComponent leftInner = left is ITransitionedComponent leftTransitioned
            ? leftTransitioned.Inner
            : left;
        IComponent rightInner = right is ITransitionedComponent rightTransitioned
            ? rightTransitioned.Inner
            : right;
        if (leftInner.Kind != rightInner.Kind
            || !Equals(leftInner.Key, rightInner.Key))
        {
            return false;
        }

        return (leftInner, rightInner) switch
        {
            (IElementComponent leftElement, IElementComponent rightElement) =>
                string.Equals(
                    leftElement.Tag,
                    rightElement.Tag,
                    StringComparison.Ordinal),
            (ITemplateComponent leftTemplate, ITemplateComponent rightTemplate) =>
                leftTemplate.TemplateType == rightTemplate.TemplateType
                && string.Equals(
                    leftTemplate.TemplateName,
                    rightTemplate.TemplateName,
                    StringComparison.Ordinal),
            _ => true,
        };
    }

    internal static TransitionIdentity Identity(IComponent component)
    {
        IComponent inner = component is ITransitionedComponent transitioned
            ? transitioned.Inner
            : component;
        object type = inner switch
        {
            IElementComponent element => element.Tag,
            ITemplateComponent { TemplateType: Type templateType } => templateType,
            ITemplateComponent { TemplateName: string name } => name,
            _ => inner.Kind,
        };
        return new TransitionIdentity(inner.Kind, type, inner.Key);
    }
}

internal sealed class TransitionedElementComponent :
    IElementComponent,
    ITransitionedComponent
{
    private readonly IElementComponent _inner;

    internal TransitionedElementComponent(
        IElementComponent inner,
        TransitionHooks transition)
    {
        _inner = inner;
        Transition = transition;
    }

    public ComponentKind Kind => _inner.Kind;

    public object? Key => _inner.Key;

    public IComponentReference? Reference => _inner.Reference;

    public ComponentOptimization Optimization => _inner.Optimization;

    public string Tag => _inner.Tag;

    public IComponentAttributeCollection Attributes => _inner.Attributes;

    public IReadOnlyList<IComponent> Children => _inner.Children;

    public IReadOnlyList<IComponentDirectiveBinding> Directives => _inner.Directives;

    public TransitionHooks Transition { get; }

    public IComponent Inner => _inner;
}

internal sealed class TransitionedTemplateComponent :
    ITemplateComponent,
    ITransitionedComponent
{
    private readonly ITemplateComponent _inner;

    internal TransitionedTemplateComponent(
        ITemplateComponent inner,
        TransitionHooks transition)
    {
        _inner = inner;
        Transition = transition;
    }

    public ComponentKind Kind => _inner.Kind;

    public object? Key => _inner.Key;

    public IComponentReference? Reference => _inner.Reference;

    public ComponentOptimization Optimization => _inner.Optimization;

    public Type? TemplateType => _inner.TemplateType;

    public string? TemplateName => _inner.TemplateName;

    public IComponentArguments Arguments => _inner.Arguments;

    public IReadOnlyDictionary<string, ComponentSlot>? Slots => _inner.Slots;

    public IReadOnlyDictionary<string, ComponentEventListener>? Listeners =>
        _inner.Listeners;

    public IReadOnlyList<IComponentDirectiveBinding> Directives =>
        _inner.Directives;

    public TransitionHooks Transition { get; }

    public IComponent Inner => _inner;
}

internal sealed class TransitionedFragmentComponent :
    IFragmentComponent,
    ITransitionedComponent
{
    private readonly IFragmentComponent _inner;
    private readonly IReadOnlyList<IComponent> _children;

    internal TransitionedFragmentComponent(
        IFragmentComponent inner,
        TransitionHooks transition)
    {
        _inner = inner;
        Transition = transition;
        List<IComponent> children = new(inner.Children.Count);
        bool attached = false;
        for (int index = 0; index < inner.Children.Count; index++)
        {
            IComponent child = inner.Children[index];
            if (!attached && child.Kind != ComponentKind.Comment)
            {
                children.Add(TransitionComponents.Attach(child, transition));
                attached = true;
            }
            else
            {
                children.Add(child);
            }
        }

        _children = children.AsReadOnly();
    }

    public ComponentKind Kind => _inner.Kind;

    public object? Key => _inner.Key;

    public IComponentReference? Reference => _inner.Reference;

    public ComponentOptimization Optimization => _inner.Optimization;

    public IReadOnlyList<IComponent> Children => _children;

    public TransitionHooks Transition { get; }

    public IComponent Inner => _inner;
}
