namespace Assimalign.Viu.Syntax.SingleFileComponent;

/// <summary>
/// The aggregate result for <see cref="VueSingleFileComponentSyntaxParser"/>: the tag-based descriptor,
/// source-ordered blocks, recoverable container diagnostics, and registered embedded-language parses.
/// </summary>
public sealed record VueSingleFileComponentSyntaxParserResult
    : AggregateSyntaxParserResult<SingleFileComponentBlock>
{
    /// <summary>Creates a tag-based aggregate parse result.</summary>
    /// <param name="descriptor">The parsed tag-based descriptor.</param>
    /// <param name="blocks">All preserved blocks in source order.</param>
    /// <param name="diagnostics">The recoverable container diagnostics in report order.</param>
    public VueSingleFileComponentSyntaxParserResult(
        VueSingleFileComponentDescriptor descriptor,
        SyntaxList<SingleFileComponentBlock> blocks,
        SyntaxList<Diagnostic> diagnostics)
        : base(blocks, diagnostics)
    {
        Descriptor = descriptor;
    }

    /// <summary>The tag-based descriptor, produced even when the source is malformed.</summary>
    public VueSingleFileComponentDescriptor Descriptor { get; }
}
