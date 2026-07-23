using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assimalign.Viu.Components;

/// <summary>An immutable authoring-time directive binding.</summary>
public sealed class ComponentDirectiveBinding : IComponentDirectiveBinding
{
    /// <summary>Creates a binding to a registered directive.</summary>
    /// <param name="directiveName">The registered directive name.</param>
    /// <param name="value">The current bound value.</param>
    /// <param name="argument">The optional directive argument.</param>
    /// <param name="modifiers">The optional directive modifiers.</param>
    public ComponentDirectiveBinding(
        string directiveName,
        object? value = null,
        string? argument = null,
        IReadOnlyDictionary<string, bool>? modifiers = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directiveName);
        DirectiveName = directiveName;
        Value = value;
        Argument = argument;
        Modifiers = CopyModifiers(modifiers);
    }

    /// <inheritdoc/>
    public string DirectiveName { get; }

    /// <inheritdoc/>
    public object? Value { get; }

    /// <inheritdoc/>
    public string? Argument { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, bool> Modifiers { get; }

    private static IReadOnlyDictionary<string, bool> CopyModifiers(
        IReadOnlyDictionary<string, bool>? modifiers)
    {
        if (modifiers is null || modifiers.Count == 0)
        {
            return ReadOnlyDictionary<string, bool>.Empty;
        }

        Dictionary<string, bool> snapshot = new(modifiers.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, bool> modifier in modifiers)
        {
            ArgumentException.ThrowIfNullOrEmpty(modifier.Key);
            snapshot.Add(modifier.Key, modifier.Value);
        }

        return new ReadOnlyDictionary<string, bool>(snapshot);
    }
}
