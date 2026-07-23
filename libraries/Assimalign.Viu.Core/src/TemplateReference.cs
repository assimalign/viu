using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu;

/// <summary>
/// Adapts a reactive object reference or callback to a component-tree template reference.
/// </summary>
/// <remarks>
/// String references are intentionally unsupported because Viu has no reflection-backed component
/// proxy. This mirrors Vue 3.5's object and function template-reference forms:
/// https://vuejs.org/guide/essentials/template-refs.html.
/// </remarks>
public sealed class TemplateReference :
    IComponentReference,
    IEquatable<TemplateReference>
{
    private readonly IReactiveReference<object?>? _reference;
    private readonly Action<object?>? _callback;

    private TemplateReference(
        IReactiveReference<object?>? reference,
        Action<object?>? callback)
    {
        _reference = reference;
        _callback = callback;
    }

    /// <summary>Creates a binding backed by a reactive object reference.</summary>
    /// <param name="reference">The reactive reference to assign.</param>
    /// <returns>The template-reference binding.</returns>
    public static TemplateReference FromReference(
        IReactiveReference<object?> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new TemplateReference(reference, callback: null);
    }

    /// <summary>Creates a binding backed by a callback.</summary>
    /// <param name="callback">The callback invoked with the mounted value and later null.</param>
    /// <returns>The template-reference binding.</returns>
    public static TemplateReference FromCallback(Action<object?> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return new TemplateReference(reference: null, callback);
    }

    /// <inheritdoc/>
    public void Set(object? value)
    {
        if (_reference is not null)
        {
            _reference.Value = value;
        }
        else
        {
            _callback!(value);
        }
    }

    /// <inheritdoc/>
    public bool Equals(TemplateReference? other)
    {
        return other is not null
            && ReferenceEquals(_reference, other._reference)
            && Equals(_callback, other._callback);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is TemplateReference other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reference, _callback);
    }

    internal static IComponentReference? FromValue(
        object? value,
        Action<string>? warningHandler = null)
    {
        switch (value)
        {
            case null:
                return null;
            case IComponentReference reference:
                return reference;
            case IReactiveReference<object?> reactiveReference:
                return FromReference(reactiveReference);
            case Action<object?> callback:
                return FromCallback(callback);
            default:
                warningHandler?.Invoke(
                    $"Invalid template reference of type \"{value.GetType().Name}\". "
                    + "Use IComponentReference, IReactiveReference<object?>, or "
                    + "Action<object?>.");
                return null;
        }
    }
}
