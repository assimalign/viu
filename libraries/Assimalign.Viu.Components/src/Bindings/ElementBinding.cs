using System;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes one immutable element binding with an explicit host application kind.
/// </summary>
/// <remarks>
/// The explicit kind keeps host attributes, properties, and event subscriptions distinct without
/// inspecting the value at runtime. Specified by <c>[CMP-3]</c> and <c>[CMP-17]</c>.
/// </remarks>
public sealed class ElementBinding
{
    private ElementBinding(ElementBindingKind kind, QualifiedName name, object? value)
    {
        Kind = kind;
        Name = name;
        Value = value;
    }

    /// <summary>Gets how the host applies the binding.</summary>
    public ElementBindingKind Kind { get; }

    /// <summary>Gets the qualified binding name.</summary>
    public QualifiedName Name { get; }

    /// <summary>Gets the immutable value observed for this render.</summary>
    public object? Value { get; }

    /// <summary>Creates a markup-attribute binding.</summary>
    /// <param name="name">The qualified attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>The immutable binding.</returns>
    public static ElementBinding Attribute(QualifiedName name, object? value)
    {
        if (string.IsNullOrEmpty(name.LocalName))
        {
            throw new ArgumentException("The attribute local name cannot be empty.", nameof(name));
        }

        return new ElementBinding(ElementBindingKind.Attribute, name, value);
    }

    /// <summary>Creates a host-property binding.</summary>
    /// <param name="name">The non-empty host property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>The immutable binding.</returns>
    public static ElementBinding Property(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return new ElementBinding(ElementBindingKind.Property, new QualifiedName(name), value);
    }

    /// <summary>Creates a host-event binding.</summary>
    /// <param name="name">The non-empty host event name.</param>
    /// <param name="listener">The listener delegate installed by the host.</param>
    /// <returns>The immutable binding.</returns>
    public static ElementBinding Event(string name, Delegate listener)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(listener);
        return new ElementBinding(ElementBindingKind.Event, new QualifiedName(name), listener);
    }
}
