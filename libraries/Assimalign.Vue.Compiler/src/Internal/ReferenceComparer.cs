using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Assimalign.Vue.Compiler;

/// <summary>
/// An identity comparer for the transform's per-node side tables. The parse AST records compare by value, so
/// keying code-generation results by value would conflate distinct-but-equal nodes; the transform needs
/// reference identity instead. netstandard2.0 has no <c>ReferenceEqualityComparer</c>, so this small
/// equivalent is used.
/// </summary>
internal sealed class ReferenceComparer : IEqualityComparer<SyntaxNode>
{
    /// <summary>The shared instance.</summary>
    public static readonly ReferenceComparer Instance = new();

    private ReferenceComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(SyntaxNode? left, SyntaxNode? right) => ReferenceEquals(left, right);

    /// <inheritdoc />
    public int GetHashCode(SyntaxNode value) => RuntimeHelpers.GetHashCode(value);
}
