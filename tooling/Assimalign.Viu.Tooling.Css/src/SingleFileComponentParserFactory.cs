using System;

using Assimalign.Viu.Syntax;
using Assimalign.Viu.Syntax.Css;
using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Tooling.Css;

/// <summary>
/// The shared composition root of the <c>Assimalign.Viu.Syntax.*</c> cluster ([V01.01.05.09]/[V01.01.06.02]):
/// the one place the language parsers meet. It constructs the canonical <c>.viu</c>
/// <see cref="SingleFileComponentSyntaxParser"/> or tag-based <c>.vue</c>
/// <see cref="VueSingleFileComponentSyntaxParser"/> and registers per-block parsers on the aggregate
/// registration seam — pairing a <see cref="SyntaxSourcePredicate"/> over each block's embedded
/// <see cref="SyntaxSource"/> (content, block name, <c>lang</c>) with the parser that understands it.
/// The <see cref="TemplateSyntaxParser"/> is registered for template blocks and the
/// <see cref="CssSyntaxParser"/> for style blocks; the language libraries never reference one
/// another, so these registrations — not hard-wired calls inside the single-file-component library — are
/// what dispatch template markup to the template compiler and style CSS to the stylesheet
/// parser. Dispatch is by block <em>name</em>, so both the canonical <c>&lt;template&gt;</c>/<c>&lt;style&gt;</c>
/// tag containers and the legacy <c>@template</c>/<c>@style</c> containers route identically.
/// </summary>
/// <remarks>
/// This factory lives in the Tooling core because it is reused by <b>two build-time hosts</b>
/// ([V01.01.12.12]): the <c>Assimalign.Viu.Generators.Syntax</c> incremental source generator, and the
/// <c>ViuBundleCss</c> MSBuild task that re-runs the same deterministic CSS compilation over the same
/// <c>.viu</c> and <c>.vue</c> inputs to write the bundled stylesheet. Both hosts constructing their
/// format-appropriate parser from this one factory is what makes the generated <c>ExtractedStyles</c>
/// constant and the physical bundle byte-identical (a single, non-divergent generation path). The
/// constructed parsers are stateless and recoverable (malformed input yields diagnostics, never throws).
/// </remarks>
public static class SingleFileComponentParserFactory
{
    /// <summary>The well-known template block name (lowercase, case-sensitive — see the .viu FORMAT.md).</summary>
    private const string TemplateBlockName = "template";

    /// <summary>The well-known style block name (lowercase, case-sensitive — see the .viu FORMAT.md).</summary>
    private const string StyleBlockName = "style";

    /// <summary>
    /// Builds the full <c>.viu</c> parser with the cluster's per-block registrations applied — the parser the
    /// generator uses to compile both template and style blocks.
    /// </summary>
    /// <returns>The composed single-file-component parser.</returns>
    public static SingleFileComponentSyntaxParser Create()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();

        // The registration seam in action: route template blocks to the template compiler's parser and
        // style blocks to the CSS parser ([V01.01.06.04]). First matching registration wins in
        // registration order (base AggregateSyntaxParser contract).
        options.RegisterParser(IsTemplateBlock, new TemplateSyntaxParser());
        options.RegisterParser(IsStyleBlock, new CssSyntaxParser());

        return new SingleFileComponentSyntaxParser(options);
    }

    /// <summary>
    /// Builds the tag-based <c>.vue</c> compatibility parser with the same template and stylesheet
    /// registrations as <see cref="Create"/>. Only the outer container parser differs; embedded block
    /// languages follow the single shared compilation path.
    /// </summary>
    /// <returns>The composed tag-based single-file-component parser.</returns>
    public static VueSingleFileComponentSyntaxParser CreateVue()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(IsTemplateBlock, new TemplateSyntaxParser());
        options.RegisterParser(IsStyleBlock, new CssSyntaxParser());
        return new VueSingleFileComponentSyntaxParser(options);
    }

    /// <summary>
    /// Builds a <c>.viu</c> parser that dispatches <b>only</b> style blocks to the CSS parser — the
    /// parser the <c>ViuBundleCss</c> task uses when it only needs the compiled styles ([V01.01.12.12]).
    /// <para>
    /// This yields <em>byte-identical</em> style results to <see cref="Create"/>: the aggregate parser
    /// splits blocks first, then dispatches each block to its one registered parser, and a style
    /// block's parse is wholly independent of whether a template parser is registered (a different
    /// block, a different dispatch). Because <see cref="SingleFileComponentStyleCompiler"/> reads only the
    /// style source results, the compiled CSS is the same as the generator's — the single, non-divergent
    /// generation path is preserved. Omitting the template registration keeps the task from ever loading
    /// the template compiler (and its Roslyn dependency), so the task's MSBuild load stays lean.
    /// </para>
    /// </summary>
    /// <returns>The composed parser with only the style registration.</returns>
    public static SingleFileComponentSyntaxParser CreateForStyleExtraction()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(IsStyleBlock, new CssSyntaxParser());
        return new SingleFileComponentSyntaxParser(options);
    }

    /// <summary>
    /// Builds a tag-based <c>.vue</c> parser that dispatches only <c>&lt;style&gt;</c> blocks to the CSS
    /// parser. This is the compatibility counterpart of <see cref="CreateForStyleExtraction"/> used by
    /// the physical component-style bundler.
    /// </summary>
    /// <returns>The tag-based parser with only the stylesheet registration.</returns>
    public static VueSingleFileComponentSyntaxParser CreateVueForStyleExtraction()
    {
        var options = new AggregateSyntaxParserOptions<SingleFileComponentBlock>();
        options.RegisterParser(IsStyleBlock, new CssSyntaxParser());
        return new VueSingleFileComponentSyntaxParser(options);
    }

    /// <summary>Whether <paramref name="source"/> is a template block's embedded source.</summary>
    /// <param name="source">The embedded block source to test.</param>
    /// <returns><see langword="true"/> for the template block.</returns>
    public static bool IsTemplateBlock(SyntaxSource source)
        => string.Equals(source.Name, TemplateBlockName, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="source"/> is a style block's embedded source.</summary>
    /// <param name="source">The embedded block source to test.</param>
    /// <returns><see langword="true"/> for the style block.</returns>
    public static bool IsStyleBlock(SyntaxSource source)
        => string.Equals(source.Name, StyleBlockName, StringComparison.Ordinal);
}
