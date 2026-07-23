using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>Creates values in the proposed unified component tree.</summary>
public static class ComponentTree
{
    /// <summary>Creates an element component.</summary>
    /// <param name="tag">The platform tag name.</param>
    /// <param name="attributes">The attributes and event bindings.</param>
    /// <param name="children">The element children.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <returns>The new element component.</returns>
    public static IElementComponent Element(
        string tag,
        IComponentAttributeCollection? attributes = null,
        IReadOnlyList<IComponent>? children = null,
        object? key = null)
    {
        return new ElementComponent(tag, attributes, children, key);
    }

    /// <summary>Creates a text component.</summary>
    /// <param name="text">The text content.</param>
    /// <returns>The new text component.</returns>
    public static ITextComponent Text(string text)
    {
        return new TextComponent(text);
    }

    /// <summary>Creates a comment component.</summary>
    /// <param name="text">The optional comment content.</param>
    /// <returns>The new comment component.</returns>
    public static ICommentComponent Comment(string? text = null)
    {
        return new CommentComponent(text);
    }

    /// <summary>Creates a static component.</summary>
    /// <param name="content">The platform-specific static markup.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <returns>The new static component.</returns>
    public static IStaticComponent Static(string content, object? key = null)
    {
        return new StaticComponent(content, key);
    }

    /// <summary>Creates a fragment component.</summary>
    /// <param name="children">The grouped children.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <returns>The new fragment component.</returns>
    public static IFragmentComponent Fragment(
        IReadOnlyList<IComponent>? children = null,
        object? key = null)
    {
        return new FragmentComponent(children, key);
    }

    /// <summary>Creates a teleport component.</summary>
    /// <param name="target">The target selector or platform container handle.</param>
    /// <param name="children">The teleported children.</param>
    /// <param name="isDisabled">Whether to render at the logical position.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <returns>The new teleport component.</returns>
    public static ITeleportComponent Teleport(
        object target,
        IReadOnlyList<IComponent>? children = null,
        bool isDisabled = false,
        object? key = null)
    {
        return new TeleportComponent(target, children, isDisabled, key);
    }

    /// <summary>Creates a template component request.</summary>
    /// <param name="templateType">The explicitly registered template type.</param>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <returns>The new template component request.</returns>
    public static ITemplateComponent Template(
        Type templateType,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null)
    {
        return new TemplateComponent(templateType, arguments, slots, key);
    }

    /// <summary>Creates a template component request from its generic type.</summary>
    /// <typeparam name="TComponent">The explicitly registered template type.</typeparam>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <returns>The new template component request.</returns>
    public static ITemplateComponent Template<TComponent>(
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null)
        where TComponent : class, IComponentTemplate
    {
        return new TemplateComponent(typeof(TComponent), arguments, slots, key);
    }
}

