using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>
/// Identifies the registered template produced by an asynchronous-component loader.
/// </summary>
/// <remarks>
/// A target deliberately carries only a factory lookup identity. It never carries an activated
/// template and therefore cannot bypass <see cref="IComponentFactory"/>.
/// </remarks>
public readonly struct AsynchronousComponentTarget
{
    /// <summary>Creates a type-identified target.</summary>
    /// <param name="templateType">The type registered with the application component factory.</param>
    public AsynchronousComponentTarget(Type templateType)
    {
        ArgumentNullException.ThrowIfNull(templateType);
        TemplateType = templateType;
        TemplateName = null;
    }

    /// <summary>Creates a name-identified target.</summary>
    /// <param name="templateName">The name registered with the application component factory.</param>
    public AsynchronousComponentTarget(string templateName)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateName);
        TemplateType = null;
        TemplateName = templateName;
    }

    /// <summary>Gets the registered type, or null for a named target.</summary>
    public Type? TemplateType { get; }

    /// <summary>Gets the registered name, or null for a type target.</summary>
    public string? TemplateName { get; }

    /// <summary>Creates a target for a registered template type.</summary>
    /// <typeparam name="TTemplate">The registered component template type.</typeparam>
    /// <returns>The type-identified target.</returns>
    public static AsynchronousComponentTarget From<TTemplate>()
        where TTemplate : class, IComponentTemplate
    {
        return new AsynchronousComponentTarget(typeof(TTemplate));
    }

    internal ITemplateComponent CreateComponent(
        IComponentArguments arguments,
        IReadOnlyDictionary<string, ComponentSlot>? slots,
        object? key,
        IReadOnlyDictionary<string, ComponentEventListener>? listeners,
        IReadOnlyList<IComponentDirectiveBinding>? directives,
        IComponentReference? reference)
    {
        if (TemplateType is not null)
        {
            return new TemplateComponent(
                TemplateType,
                arguments,
                slots,
                key,
                listeners: listeners,
                directives: directives,
                reference: reference);
        }

        if (TemplateName is not null)
        {
            return new TemplateComponent(
                TemplateName,
                arguments,
                slots,
                key,
                listeners: listeners,
                directives: directives,
                reference: reference);
        }

        throw new InvalidOperationException(
            "An asynchronous component loader returned an uninitialized target.");
    }

    internal void Validate()
    {
        if (TemplateType is null && TemplateName is null)
        {
            throw new InvalidOperationException(
                "An asynchronous component loader returned an uninitialized target.");
        }
    }
}
