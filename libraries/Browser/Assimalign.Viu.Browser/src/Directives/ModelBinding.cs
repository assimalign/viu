using System;
using System.Collections.Generic;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Carries the current native-control model value, its compiler-produced write-back delegate,
/// and the immutable modifier names to a Browser directive token.
/// </summary>
/// <remarks>
/// The setter is explicit so Browser never discovers an authored member by reflection. Modifier
/// names are copied at construction and preserve source order. Specified by <c>[SFC-CG-7]</c>.
/// </remarks>
public sealed class ModelBinding
{
    /// <summary>Initializes one immutable native-control model carrier.</summary>
    /// <param name="value">The model value observed for the current render.</param>
    /// <param name="setter">The generated assignment delegate.</param>
    /// <param name="modifiers">The optional modifier names in source order.</param>
    public ModelBinding(
        object? value,
        Action<object?> setter,
        IReadOnlyList<string>? modifiers = null)
    {
        ArgumentNullException.ThrowIfNull(setter);
        Value = value;
        Setter = setter;
        if (modifiers is null || modifiers.Count == 0)
        {
            Modifiers = Array.Empty<string>();
            return;
        }

        string[] snapshot = new string[modifiers.Count];
        for (int index = 0; index < modifiers.Count; index++)
        {
            string modifier = modifiers[index];
            ArgumentException.ThrowIfNullOrEmpty(modifier);
            snapshot[index] = modifier;
        }

        Modifiers = Array.AsReadOnly(snapshot);
    }

    /// <summary>Gets the model value observed for the current render.</summary>
    public object? Value { get; }

    /// <summary>Gets the generated reflection-free assignment delegate.</summary>
    public Action<object?> Setter { get; }

    /// <summary>Gets the immutable modifier names in source order.</summary>
    public IReadOnlyList<string> Modifiers { get; }
}
