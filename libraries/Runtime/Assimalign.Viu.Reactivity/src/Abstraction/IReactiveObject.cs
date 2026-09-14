using System.Collections.Generic;

namespace Assimalign.Viu.Reactivity;

/// <summary>
/// The contract implemented by source-generated reactive objects. It is Viu's reflection-free,
/// trimming-safe alternative to intercepting member access at runtime (<c>[RCT-6]</c>).
/// </summary>
public interface IReactiveObject : IReactiveTraversable, IReactiveReadOnly
{
    /// <summary>
    /// Returns the dependency backing <paramref name="propertyName"/>, or
    /// <see langword="null"/> when the property is not reactive.
    /// </summary>
    /// <param name="propertyName">The case-sensitive declared property name.</param>
    /// <returns>The property's dependency cell, or <see langword="null"/>.</returns>
    Dependency? GetDependency(string propertyName);

    /// <summary>
    /// Captures generated member values by name for reflection-free inspection. Reads track
    /// normally; observers suspend tracking before calling. This cold operation allocates a
    /// snapshot and is not thread-safe. Older implementations expose no members by default.
    /// Specified by <c>[DVT-13]</c> and <c>[RCT-6]</c>.
    /// </summary>
    /// <returns>A name-to-value snapshot of the generated reactive members.</returns>
    IReadOnlyDictionary<string, object?> GetMemberValues() =>
        new Dictionary<string, object?>();

    /// <summary>
    /// Writes a supported scalar through its generated property setter, preserving ordinary
    /// equality checks and reactive notification. Names are ordinal; the value must already
    /// have the exact bool, int, long, double, decimal, or string CLR type. Read-only objects,
    /// unknown members, unsupported types, and mismatched values return false without writing.
    /// This event-loop operation is not thread-safe. Specified by <c>[DVT-13]</c>.
    /// </summary>
    /// <param name="propertyName">The case-sensitive generated member name.</param>
    /// <param name="value">The typed scalar value; null is accepted only for string members.</param>
    /// <returns>Whether the member accepted the value, including an equal-value assignment.</returns>
    bool TrySetMemberValue(string propertyName, object? value) => false;

    /// <summary>Gets whether generated property writes are rejected.</summary>
    new bool IsReadOnly => false;

    bool IReactiveReadOnly.IsReadOnly => IsReadOnly;
}
