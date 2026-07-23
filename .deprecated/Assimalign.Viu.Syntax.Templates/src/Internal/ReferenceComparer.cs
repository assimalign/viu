using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// An identity comparer for the transform's per-node side tables. The parse AST records compare by value, so
/// keying code-generation results by value would conflate distinct-but-equal nodes; the transform needs
/// reference identity instead. netstandard2.0 has no <c>ReferenceEqualityComparer</c>, so this small
/// equivalent is used.
/// </summary>
internal sealed class ReferenceComparer : IEqualityComparer<TemplateSyntaxNode>
{
    /// <summary>The shared instance.</summary>
    public static readonly ReferenceComparer Instance = new();

    private ReferenceComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(TemplateSyntaxNode? left, TemplateSyntaxNode? right) => ReferenceEquals(left, right);

    /// <inheritdoc />
    public int GetHashCode(TemplateSyntaxNode value) => RuntimeHelpers.GetHashCode(value);
}
