using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Creates unified-tree values for Vue-shaped dynamic component selections.
/// </summary>
/// <remarks>
/// Mirrors Vue 3.5's <c>resolveDynamicComponent</c> and <c>&lt;component :is&gt;</c> replacement
/// semantics: https://vuejs.org/api/built-in-special-elements.html#component. Because the approved
/// <see cref="IComponentFactory"/> contract intentionally has no registration-probe API, a plain
/// string is always an element tag. Use <see cref="Named"/> for an explicit registered name.
/// </remarks>
public static class DynamicComponents
{
    /// <summary>Creates an explicit registered-name selector.</summary>
    /// <param name="name">The component-factory registration name.</param>
    /// <returns>The dynamic component selector.</returns>
    public static DynamicComponentName Named(string name)
    {
        return new DynamicComponentName(name);
    }

    /// <summary>Normalizes an <c>is</c> selector without activating a component.</summary>
    /// <param name="source">
    /// A template type, named selector, asynchronous definition, existing tree value, element tag,
    /// or null.
    /// </param>
    /// <returns>The unchanged supported selector, or null for an empty or unsupported selector.</returns>
    public static object? ResolveDynamicComponent(object? source)
    {
        return source switch
        {
            string { Length: 0 } => null,
            string => source,
            Type => source,
            DynamicComponentName name when !string.IsNullOrEmpty(name.Name) => source,
            AsynchronousComponentDefinition => source,
            IComponent => source,
            _ => null,
        };
    }

    /// <summary>Creates the tree value selected by a dynamic component expression.</summary>
    /// <param name="source">The value normalized by <see cref="ResolveDynamicComponent"/>.</param>
    /// <param name="arguments">Arguments for a selected template.</param>
    /// <param name="slots">Slots for a selected template.</param>
    /// <param name="attributes">Attributes for a selected element tag.</param>
    /// <param name="children">Children for a selected element tag.</param>
    /// <param name="key">The optional sibling identity.</param>
    /// <param name="optimization">Compiler-produced optimization metadata.</param>
    /// <param name="listeners">Event listeners for a selected template.</param>
    /// <param name="directives">Runtime directives for the selected tree value.</param>
    /// <param name="reference">The optional template reference.</param>
    /// <returns>A template, element, existing tree value, or comment placeholder.</returns>
    public static IComponent DynamicComponent(
        object? source,
        IComponentArguments? arguments = null,
        IReadOnlyDictionary<string, ComponentSlot>? slots = null,
        IComponentAttributeCollection? attributes = null,
        IReadOnlyList<IComponent>? children = null,
        object? key = null,
        ComponentOptimization? optimization = null,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners = null,
        IReadOnlyList<IComponentDirectiveBinding>? directives = null,
        IComponentReference? reference = null)
    {
        object? resolved = ResolveDynamicComponent(source);
        return resolved switch
        {
            AsynchronousComponentDefinition definition =>
                definition.CreateComponent(
                    arguments,
                    slots,
                    key,
                    optimization,
                    listeners,
                    directives,
                    reference),
            Type templateType => new TemplateComponent(
                templateType,
                arguments,
                slots,
                key,
                optimization,
                listeners,
                directives,
                reference),
            DynamicComponentName name => new TemplateComponent(
                name.Name,
                arguments,
                slots,
                key,
                optimization,
                listeners,
                directives,
                reference),
            string elementTag => new ElementComponent(
                elementTag,
                attributes,
                children,
                key,
                optimization,
                directives,
                reference),
            ITemplateComponent template => CopyTemplate(
                template,
                arguments,
                slots,
                key,
                optimization,
                listeners,
                directives,
                reference),
            IComponent component => component,
            _ => ComponentTree.Comment(),
        };
    }

    private static ITemplateComponent CopyTemplate(
        ITemplateComponent template,
        IComponentArguments? arguments,
        IReadOnlyDictionary<string, ComponentSlot>? slots,
        object? key,
        ComponentOptimization? optimization,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners,
        IReadOnlyList<IComponentDirectiveBinding>? directives,
        IComponentReference? reference)
    {
        if (template.TemplateType is not null)
        {
            return new TemplateComponent(
                template.TemplateType,
                arguments ?? template.Arguments,
                slots ?? template.Slots,
                key ?? template.Key,
                optimization ?? template.Optimization,
                listeners ?? template.Listeners,
                directives ?? template.Directives,
                reference ?? template.Reference);
        }

        return new TemplateComponent(
            template.TemplateName!,
            arguments ?? template.Arguments,
            slots ?? template.Slots,
            key ?? template.Key,
            optimization ?? template.Optimization,
            listeners ?? template.Listeners,
            directives ?? template.Directives,
            reference ?? template.Reference);
    }
}
