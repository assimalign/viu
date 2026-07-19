namespace Assimalign.Vue.Syntax.Css;

/// <summary>
/// One CSS token: its <see cref="Kind"/> and the half-open source range <c>[Start, End)</c> it spans.
/// The token holds offsets only (never a copied substring) so tokenizing is allocation-light; the parser
/// slices the source when it builds located nodes. A <c>readonly struct</c> so the token stream costs no
/// per-token heap allocation.
/// </summary>
/// <param name="Kind">The token category.</param>
/// <param name="Start">The inclusive start offset.</param>
/// <param name="End">The exclusive end offset.</param>
internal readonly record struct CssToken(CssTokenKind Kind, int Start, int End)
{
    /// <summary>The token's length in characters.</summary>
    public int Length => End - Start;
}
