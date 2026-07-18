using System;

using Assimalign.Vue.Syntax;
using Assimalign.Vue.Syntax.SingleFileComponent;
using Assimalign.Vue.Syntax.Templates;

namespace Assimalign.Vue.Syntax.Generators;

/// <summary>
/// The composition root of the <c>Assimalign.Vue.Syntax.*</c> cluster ([V01.01.05.09]/[V01.01.06.02]):
/// the one place the language parsers meet. It constructs the <c>.viu</c>
/// <see cref="SingleFileComponentSyntaxParser"/> and registers per-block parsers on the aggregate
/// registration seam — pairing a <see cref="SyntaxSourcePredicate"/> over each block's embedded
/// <see cref="SyntaxSource"/> (content, block name, <c>lang</c>) with the parser that understands it.
/// The <see cref="TemplateSyntaxParser"/> is registered for <c>@template</c> blocks; the language
/// libraries never reference one another, so this registration — not a hard-wired call inside the
/// single-file-component library — is what dispatches <c>@template</c> markup to the template compiler.
/// </summary>
/// <remarks>
/// The constructed parser is stateless and recoverable (malformed input yields diagnostics, never
/// throws), so a single instance is shared across every <c>.viu</c> parse in the generator pipeline.
/// As the browser-language parsers land ([V01.01.06.04] scoped CSS, and the HTML/JavaScript scaffolds),
/// their registrations are added here and nowhere else.
/// </remarks>
internal static class SingleFileComponentParserComposition
{
    /// <summary>The well-known <c>@template</c> block name (lowercase, case-sensitive — see the .viu FORMAT.md).</summary>
    private const string TemplateBlockName = "template";

    /// <summary>
    /// Builds the <c>.viu</c> parser with the cluster's per-block registrations applied.
    /// </summary>
    /// <returns>The composed single-file-component parser.</returns>
    public static SingleFileComponentSyntaxParser Create()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();

        // The registration seam in action: route @template blocks to the template compiler's parser.
        // First matching registration wins in registration order (base AggregateSyntaxParser contract).
        options.RegisterParser(IsTemplateBlock, new TemplateSyntaxParser());

        return new SingleFileComponentSyntaxParser(options);
    }

    /// <summary>Whether <paramref name="source"/> is a <c>@template</c> block's embedded source.</summary>
    /// <param name="source">The embedded block source to test.</param>
    /// <returns><see langword="true"/> for the <c>@template</c> block.</returns>
    public static bool IsTemplateBlock(SyntaxSource source)
        => string.Equals(source.Name, TemplateBlockName, StringComparison.Ordinal);
}
