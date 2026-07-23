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
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="directives">The directives applied to the element.</param>
    /// <param name="reference">The optional template-reference binding.</param>
    /// <returns>The new element component.</returns>
    public static IElementComponent Element(
        string tag,
        IComponentAttributeCollection? attributes = null,
        IReadOnlyList<IComponent>? children = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        return new ElementComponent(
            tag,
            attributes,
            children,
            key,
            optimization,
            directives,
            reference);
    }

    /// <summary>Creates a text component.</summary>
    /// <param name="text">The text content.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <returns>The new text component.</returns>
    public static ITextComponent Text(string text, ComponentOptimization? optimization = null)
    {
        return new TextComponent(text, optimization);
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
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <returns>The new static component.</returns>
    public static IStaticComponent Static(
        string content,
        object? key = null,
        ComponentOptimization? optimization = null)
    {
        return new StaticComponent(content, key, optimization);
    }

    /// <summary>Creates a fragment component.</summary>
    /// <param name="children">The grouped children.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <returns>The new fragment component.</returns>
    public static IFragmentComponent Fragment(
        IReadOnlyList<IComponent>? children = null,
        object? key = null,
        ComponentOptimization? optimization = null)
    {
        return new FragmentComponent(children, key, optimization);
    }

    /// <summary>Creates a teleport component.</summary>
    /// <param name="target">The target selector or platform container handle.</param>
    /// <param name="children">The teleported children.</param>
    /// <param name="isDisabled">Whether to render at the logical position.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="isDeferred">
    /// Whether target-side setup waits for the current render's post-flush phase. Disabled content
    /// still mounts at its logical position immediately.
    /// </param>
    /// <returns>The new teleport component.</returns>
    public static ITeleportComponent Teleport(
        object target,
        IReadOnlyList<IComponent>? children = null,
        bool isDisabled = false,
        object? key = null,
        ComponentOptimization? optimization = null,
        bool isDeferred = false)
    {
        return new TeleportComponent(
            target,
            children,
            isDisabled,
            key,
            optimization,
            isDeferred);
    }

    /// <summary>Creates a template component request.</summary>
    /// <param name="templateType">The explicitly registered template type.</param>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="listeners">The parent listeners for component-emitted events.</param>
    /// <param name="directives">The directives applied to the template's rendered root.</param>
    /// <param name="reference">The optional template-reference binding.</param>
    /// <returns>The new template component request.</returns>
    public static ITemplateComponent Template(
        Type templateType,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        return new TemplateComponent(
            templateType,
            arguments,
            slots,
            key,
            optimization,
            listeners,
            directives,
            reference);
    }

    /// <summary>
    /// Creates a named template request. Name resolution and activation are deferred until Core
    /// mounts the request.
    /// </summary>
    /// <param name="templateName">The registered template name.</param>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="listeners">The parent listeners for component-emitted events.</param>
    /// <param name="directives">The directives applied to the template's rendered root.</param>
    /// <param name="reference">The optional template-reference binding.</param>
    /// <returns>The new template component request.</returns>
    public static ITemplateComponent Template(
        string templateName,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        return new TemplateComponent(
            templateName,
            arguments,
            slots,
            key,
            optimization,
            listeners,
            directives,
            reference);
    }

    /// <summary>Creates a template component request from its generic type.</summary>
    /// <typeparam name="TComponent">The explicitly registered template type.</typeparam>
    /// <param name="arguments">The parent-supplied arguments.</param>
    /// <param name="slots">The parent-supplied slots.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">The compiler-produced optimization metadata.</param>
    /// <param name="listeners">The parent listeners for component-emitted events.</param>
    /// <param name="directives">The directives applied to the template's rendered root.</param>
    /// <param name="reference">The optional template-reference binding.</param>
    /// <returns>The new template component request.</returns>
    public static ITemplateComponent Template<TComponent>(
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
        where TComponent : class, IComponentTemplate
    {
        return new TemplateComponent(
            typeof(TComponent),
            arguments,
            slots,
            key,
            optimization,
            listeners,
            directives,
            reference);
    }
}
