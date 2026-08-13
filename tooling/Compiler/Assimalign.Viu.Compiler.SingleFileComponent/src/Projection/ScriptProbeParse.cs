using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// A parsed <c>@script</c> block: the block's class-body member region, parsed inside the synthetic
/// partial-class probe by <see cref="ScriptBlockAnalyzer.ParseProbe(string)"/>, together with the arithmetic
/// that maps a probe-tree offset back onto the block content. Consumers walk <see cref="Probe"/> and
/// translate every offset they keep through <see cref="TryGetContentOffset"/>, so the probe wrapper
/// and the leading-using split stay a private detail of the shared analyzer
/// ([V01.01.06.11]; folding consumer [V01.01.12.07.10]).
/// </summary>
/// <remarks>
/// <para>
/// The leading <c>using</c> run is split off before the probe parse, so the probe tree covers only the
/// member region. <see cref="MemberRegionOffset"/> re-adds the cut, which is why a translated offset is
/// relative to the <em>whole</em> block content and composes with the block's own
/// <c>ContentLocation</c> the same way a declared member's location does.
/// </para>
/// <para>
/// Translation is also the recovery guard. Roslyn error recovery routinely lets an unclosed construct
/// swallow the probe's own closing brace, which places the construct's closing token outside the text
/// the author actually wrote; <see cref="TryGetContentOffset"/> reports that offset as untranslatable
/// rather than returning a position the author never typed.
/// </para>
/// </remarks>
public sealed class ScriptProbeParse
{
    /// <summary>The result for content that declares no members (nothing to walk).</summary>
    public static readonly ScriptProbeParse None = new(null, 0, string.Empty, 0, 0);

    private readonly int probePrefixLength;

    internal ScriptProbeParse(
        ClassDeclarationSyntax? probe,
        int probePrefixLength,
        string memberRegionText,
        int memberRegionOffset,
        int memberRegionLineIndex)
    {
        Probe = probe;
        this.probePrefixLength = probePrefixLength;
        MemberRegionText = memberRegionText;
        MemberRegionOffset = memberRegionOffset;
        MemberRegionLineIndex = memberRegionLineIndex;
    }

    /// <summary>
    /// The synthetic partial class holding the member region's declarations, or <see langword="null"/>
    /// when the block declares nothing to parse.
    /// </summary>
    public ClassDeclarationSyntax? Probe { get; }

    /// <summary>The member-region text (the block content minus the hoisted leading using run).</summary>
    public string MemberRegionText { get; }

    /// <summary>The member region's start offset within the whole block content.</summary>
    public int MemberRegionOffset { get; }

    /// <summary>The member region's start line index (zero-based) within the whole block content.</summary>
    public int MemberRegionLineIndex { get; }

    /// <summary>
    /// Translates a probe-tree offset to an offset within the whole block content.
    /// </summary>
    /// <param name="probeOffset">An offset into the probe-wrapped text.</param>
    /// <param name="contentOffset">The corresponding block-content offset when translatable.</param>
    /// <returns>
    /// <see langword="true"/> when the offset falls inside the author's own member-region text;
    /// <see langword="false"/> for an offset in the probe's synthetic prefix or suffix — the position a
    /// recovered, unclosed construct reports for its closing token.
    /// </returns>
    public bool TryGetContentOffset(int probeOffset, out int contentOffset)
    {
        var regionOffset = probeOffset - probePrefixLength;
        if (regionOffset < 0 || regionOffset >= MemberRegionText.Length)
        {
            contentOffset = 0;
            return false;
        }

        contentOffset = MemberRegionOffset + regionOffset;
        return true;
    }
}
