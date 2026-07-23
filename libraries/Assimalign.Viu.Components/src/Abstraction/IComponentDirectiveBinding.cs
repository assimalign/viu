using System.Collections.Generic;

namespace Assimalign.Viu.Components;

/// <summary>
/// Describes one registered directive and its immutable authoring-time binding data.
/// </summary>
/// <remarks>
/// Core resolves <see cref="DirectiveName"/> against the application directive registry and creates
/// any renderer-owned hook state. This contract does not resolve or invoke a directive.
/// </remarks>
public interface IComponentDirectiveBinding
{
    /// <summary>Gets the registered directive name.</summary>
    string DirectiveName { get; }

    /// <summary>Gets the current bound value.</summary>
    object? Value { get; }

    /// <summary>Gets the optional directive argument.</summary>
    string? Argument { get; }

    /// <summary>Gets the immutable directive modifiers.</summary>
    IReadOnlyDictionary<string, bool> Modifiers { get; }
}
