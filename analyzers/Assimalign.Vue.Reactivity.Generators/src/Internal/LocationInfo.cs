using System;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Assimalign.Vue.Reactivity.Generators;

/// <summary>
/// A value-equatable snapshot of a <see cref="Location"/> — file path plus spans — so diagnostics can
/// ride inside the incremental generator's cached model without dragging the non-equatable
/// <see cref="Location"/> (and its <c>SyntaxTree</c>) into the cache.
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>Rebuilds a <see cref="Location"/> for reporting.</summary>
    /// <returns>The reconstructed location.</returns>
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    /// <summary>Captures the location of <paramref name="node"/>, or <see langword="null"/> when it has no source tree.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns>The captured location, or <see langword="null"/>.</returns>
    public static LocationInfo? From(SyntaxNode node)
    {
        var location = node.GetLocation();
        if (location.SourceTree is null)
        {
            return null;
        }
        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }
}
