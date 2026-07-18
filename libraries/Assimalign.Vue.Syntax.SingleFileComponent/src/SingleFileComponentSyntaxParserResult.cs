namespace Assimalign.Vue.Syntax.SingleFileComponent;

/// <summary>
/// The <see cref="SingleFileComponentSyntaxParser"/> result: the parity
/// <see cref="SingleFileComponentDescriptor"/> (exactly what
/// <see cref="SingleFileComponentParser.Parse(string)"/> produces), the same blocks flattened into the
/// base node list in source order, the errors surfaced as the uniform
/// <see cref="Assimalign.Vue.Syntax.SyntaxParserResult.Diagnostics"/>, and — when registrations are
/// configured — the dispatched per-block parses on
/// <see cref="AggregateSyntaxParserResult{T}.SourceResults"/>.
/// </summary>
public sealed record SingleFileComponentSyntaxParserResult : AggregateSyntaxParserResult<SingleFileComponentBlock>
{
    /// <summary>Creates the result.</summary>
    /// <param name="descriptor">The parsed descriptor.</param>
    /// <param name="blocks">The descriptor's blocks, flattened in source order.</param>
    /// <param name="diagnostics">The recoverable diagnostics, in report order.</param>
    public SingleFileComponentSyntaxParserResult(
        SingleFileComponentDescriptor descriptor,
        SyntaxList<SingleFileComponentBlock> blocks,
        SyntaxList<Diagnostic> diagnostics)
        : base(blocks, diagnostics)
    {
        Descriptor = descriptor;
    }

    /// <summary>The parsed descriptor (always produced, even for malformed input).</summary>
    public SingleFileComponentDescriptor Descriptor { get; }
}
