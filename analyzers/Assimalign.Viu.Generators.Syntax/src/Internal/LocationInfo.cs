using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Assimalign.Viu.Syntax.Generators;

/// <summary>
/// A value-equatable snapshot of a source range in a <c>.viu</c> file — the file path plus zero-based
/// character offsets and line/character positions — so parser diagnostics can ride inside the
/// incremental generator's cached model without dragging the non-equatable Roslyn
/// <see cref="Location"/> (and its <c>SyntaxTree</c>) into the cache. Positions are stored zero-based
/// (Roslyn's convention); the base cluster's <c>Position</c> is one-based for line/column, so the
/// conversion happens once when this snapshot is built.
/// </summary>
internal readonly record struct LocationInfo(
    string FilePath,
    int StartOffset,
    int EndOffset,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter)
{
    /// <summary>Rebuilds a Roslyn <see cref="Location"/> on the <c>.viu</c> file for reporting.</summary>
    /// <returns>The reconstructed location.</returns>
    public Location ToLocation()
        => Location.Create(
            FilePath,
            TextSpan.FromBounds(StartOffset, EndOffset),
            new LinePositionSpan(
                new LinePosition(StartLine, StartCharacter),
                new LinePosition(EndLine, EndCharacter)));
}
