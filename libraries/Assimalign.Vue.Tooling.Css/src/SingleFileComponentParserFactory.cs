using System;

using Assimalign.Vue.Syntax;
using Assimalign.Vue.Syntax.Css;
using Assimalign.Vue.Syntax.SingleFileComponent;
using Assimalign.Vue.Syntax.Templates;

namespace Assimalign.Vue.Tooling.Css;

/// <summary>
/// The shared composition root of the <c>Assimalign.Vue.Syntax.*</c> cluster ([V01.01.05.09]/[V01.01.06.02]):
/// the one place the language parsers meet. It constructs the <c>.viu</c>
/// <see cref="SingleFileComponentSyntaxParser"/> and registers per-block parsers on the aggregate
/// registration seam — pairing a <see cref="SyntaxSourcePredicate"/> over each block's embedded
/// <see cref="SyntaxSource"/> (content, block name, <c>lang</c>) with the parser that understands it.
/// The <see cref="TemplateSyntaxParser"/> is registered for <c>@template</c> blocks and the
/// <see cref="CssSyntaxParser"/> for <c>@style</c> blocks; the language libraries never reference one
/// another, so these registrations — not hard-wired calls inside the single-file-component library — are
/// what dispatch <c>@template</c> markup to the template compiler and <c>@style</c> CSS to the stylesheet
/// parser.
/// </summary>
/// <remarks>
/// This factory lives in the Tooling core because it is reused by <b>two build-time hosts</b>
/// ([V01.01.12.12]): the <c>Assimalign.Vue.Syntax.Generators</c> incremental source generator, and the
/// <c>VuecsBundleCss</c> MSBuild task that re-runs the same deterministic CSS compilation over the same
/// <c>.viu</c> inputs to write the bundled stylesheet. Both hosts constructing the parser from this one
/// factory is what makes the generated <c>ExtractedStyles</c> constant and the physical bundle
/// byte-identical (a single, non-divergent generation path). The constructed parser is stateless and
/// recoverable (malformed input yields diagnostics, never throws), so a single instance is shared across
/// every <c>.viu</c> parse.
/// </remarks>
public static class SingleFileComponentParserFactory
{
    /// <summary>The well-known <c>@template</c> block name (lowercase, case-sensitive — see the .viu FORMAT.md).</summary>
    private const string TemplateBlockName = "template";

    /// <summary>The well-known <c>@style</c> block name (lowercase, case-sensitive — see the .viu FORMAT.md).</summary>
    private const string StyleBlockName = "style";

    /// <summary>
    /// Builds the full <c>.viu</c> parser with the cluster's per-block registrations applied — the parser the
    /// generator uses to compile both <c>@template</c> and <c>@style</c>.
    /// </summary>
    /// <returns>The composed single-file-component parser.</returns>
    public static SingleFileComponentSyntaxParser Create()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();

        // The registration seam in action: route @template blocks to the template compiler's parser and
        // @style blocks to the CSS parser ([V01.01.06.04]). First matching registration wins in
        // registration order (base AggregateSyntaxParser contract).
        options.RegisterParser(IsTemplateBlock, new TemplateSyntaxParser());
        options.RegisterParser(IsStyleBlock, new CssSyntaxParser());

        return new SingleFileComponentSyntaxParser(options);
    }

    /// <summary>
    /// Builds a <c>.viu</c> parser that dispatches <b>only</b> <c>@style</c> blocks to the CSS parser — the
    /// parser the <c>VuecsBundleCss</c> task uses when it only needs the compiled styles ([V01.01.12.12]).
    /// <para>
    /// This yields <em>byte-identical</em> <c>@style</c> results to <see cref="Create"/>: the aggregate parser
    /// splits blocks first, then dispatches each block to its one registered parser, and an <c>@style</c>
    /// block's parse is wholly independent of whether an <c>@template</c> parser is registered (a different
    /// block, a different dispatch). Because <see cref="SingleFileComponentStyleCompiler"/> reads only the
    /// <c>@style</c> source results, the compiled CSS is the same as the generator's — the single, non-divergent
    /// generation path is preserved. Omitting the <c>@template</c> registration keeps the task from ever loading
    /// the template compiler (and its Roslyn dependency), so the task's MSBuild load stays lean.
    /// </para>
    /// </summary>
    /// <returns>The composed parser with only the <c>@style</c> registration.</returns>
    public static SingleFileComponentSyntaxParser CreateForStyleExtraction()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(IsStyleBlock, new CssSyntaxParser());
        return new SingleFileComponentSyntaxParser(options);
    }

    /// <summary>Whether <paramref name="source"/> is a <c>@template</c> block's embedded source.</summary>
    /// <param name="source">The embedded block source to test.</param>
    /// <returns><see langword="true"/> for the <c>@template</c> block.</returns>
    public static bool IsTemplateBlock(SyntaxSource source)
        => string.Equals(source.Name, TemplateBlockName, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="source"/> is a <c>@style</c> block's embedded source.</summary>
    /// <param name="source">The embedded block source to test.</param>
    /// <returns><see langword="true"/> for the <c>@style</c> block.</returns>
    public static bool IsStyleBlock(SyntaxSource source)
        => string.Equals(source.Name, StyleBlockName, StringComparison.Ordinal);
}
