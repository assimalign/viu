using System;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Produces and recognizes the <b>synthetic</b> <see cref="SourceLocation"/>s carried by CSS nodes that are
/// built programmatically through the construction surface (<see cref="CssSyntaxFactory"/> and
/// <see cref="CssStylesheetBuilder"/>) rather than parsed from source text. It is the single, documented,
/// tested divergence from the base cluster's exact-slice <see cref="SourceLocation"/> invariant.
/// </summary>
/// <remarks>
/// <para>
/// Every node a <see cref="CssSyntaxParser"/> emits upholds the exact-slice invariant:
/// <c>Location.Source == source.Substring(Start.Offset, End.Offset - Start.Offset)</c> — the literal
/// characters the node spans in the parser's source string (see <see cref="SourceLocation"/>). A
/// programmatically constructed node has <em>no</em> source string, so that invariant cannot hold and does
/// not apply — the invariant is scoped to parser output, and construction is a distinct path. Rather than
/// invent a fake span into a non-existent source, a constructed node is stamped with a synthetic location:
/// its <see cref="SourceLocation.Start"/> and <see cref="SourceLocation.End"/> are the sentinel
/// <see cref="SyntheticPosition"/> (offset <c>-1</c>, which no real parse position ever takes), and its
/// <see cref="SourceLocation.Source"/> carries the exact text the node contributes to serialization where
/// the canonical serializer reads that text back (selector leaves — see <see cref="CssSyntaxFactory"/>),
/// or the empty string for nodes the serializer composes from typed properties.
/// </para>
/// <para>
/// The sentinel offset — not the <see cref="SourceLocation.Source"/> — is what marks a node synthetic, so
/// <see cref="IsSynthetic(SourceLocation)"/> is exact and total. Because the sentinel is a constant and the
/// carried <see cref="SourceLocation.Source"/> is derived deterministically from the node's inputs, two
/// synthetic locations built from the same inputs compare equal and hash equally — the record value
/// semantics the incremental-generator cache depends on hold for constructed graphs exactly as they do for
/// parsed ones. A synthetic node never compares equal to a parsed node with the same text, because their
/// offsets differ (<c>-1</c> versus a real position).
/// </para>
/// </remarks>
public static class CssSyntheticLocation
{
    /// <summary>
    /// The sentinel position stamped on both ends of every synthetic location. Its <see cref="Position.Offset"/>
    /// is <c>-1</c> — a value no real parse position takes (parser offsets are zero-based and non-negative) —
    /// which is the marker <see cref="IsSynthetic(SourceLocation)"/> tests. Its line and column are <c>0</c>,
    /// outside the one-based coordinate space real positions use, reinforcing that it names no point in any
    /// source.
    /// </summary>
    public static readonly Position SyntheticPosition = new(Offset: -1, Line: 0, Column: 0);

    /// <summary>
    /// Creates a synthetic <see cref="SourceLocation"/> carrying <paramref name="source"/> as the exact text
    /// the node contributes to serialization (empty when the serializer composes the node's text from typed
    /// properties). Both ends are <see cref="SyntheticPosition"/>.
    /// </summary>
    /// <param name="source">The node's contributed text, or the empty string when it has none.</param>
    /// <returns>A synthetic location for a constructed node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static SourceLocation Create(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new SourceLocation(SyntheticPosition, SyntheticPosition, source);
    }

    /// <summary>Creates a synthetic <see cref="SourceLocation"/> with an empty <see cref="SourceLocation.Source"/>.</summary>
    /// <returns>A synthetic location with no contributed text.</returns>
    public static SourceLocation Create() => Create(string.Empty);

    /// <summary>
    /// Reports whether <paramref name="location"/> is synthetic — i.e. was stamped by the construction surface
    /// rather than produced by a parser — by testing the sentinel offset. A synthetic location is exempt from
    /// the exact-slice invariant.
    /// </summary>
    /// <param name="location">The location to classify.</param>
    /// <returns><see langword="true"/> when the location is synthetic; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> is <see langword="null"/>.</exception>
    public static bool IsSynthetic(SourceLocation location)
    {
        if (location is null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        return location.Start.Offset < 0;
    }

    /// <summary>
    /// Reports whether <paramref name="node"/> carries a synthetic <see cref="SourceLocation"/> — a
    /// convenience over <see cref="IsSynthetic(SourceLocation)"/> for <c>node.Location</c>.
    /// </summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> when the node's location is synthetic; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static bool IsSynthetic(CssSyntaxNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return IsSynthetic(node.Location);
    }
}
