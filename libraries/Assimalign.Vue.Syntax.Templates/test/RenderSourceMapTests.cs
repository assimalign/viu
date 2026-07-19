using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// Tests for the render source map <see cref="RenderFunctionEmitter"/> produces ([V01.01.05.08]): each
/// dynamic template expression emitted into the render body yields a <see cref="RenderSourceMapping"/> whose
/// generated position points at the expression's original template text (past any inserted <c>_ctx.</c>
/// prefix) and whose <see cref="RenderSourceMapping.TemplateLocation"/> is the exact template span. The
/// composition root ([V01.01.06.02]) turns these into <c>#line</c> span directives; the end-to-end
/// compiled-<c>#line</c> proof lives in the generator's
/// <c>SingleFileComponentTemplateSourceMapTests</c>. Upstream analogue: the source-map segments Vue 3.5's
/// <c>generate()</c> emits under <c>sourceMap</c> (<c>@vue/compiler-core</c> <c>codegen.ts</c>).
/// </summary>
public sealed class RenderSourceMapTests
{
    [Fact]
    public void Interpolation_MapsTheOriginalIdentifier_PastTheCtxPrefix()
    {
        var emitted = EmitPrefixed("<div>{{ message }}</div>");

        var mapping = emitted.SourceMappings.ShouldHaveSingleItem();
        // The template span is `message` itself, not the emitted `_ctx.message`.
        mapping.TemplateLocation.Source.ShouldBe("message");
        mapping.GeneratedLine.ShouldBe(0);
        // GeneratedColumn points at the original identifier inside the rewritten emission, so the emitted
        // text at that column is `message` — the alignment the #line directive's char offset relies on.
        emitted.Code.Substring(mapping.GeneratedColumn, "message".Length).ShouldBe("message");
    }

    [Fact]
    public void CompoundExpression_MapsEachReferencedIdentifierSeparately()
    {
        // `a + b` rewrites to a compound whose two identifier parts each carry their own template span, so
        // the map has one entry per referenced identifier, each pointing at its own source text.
        var emitted = EmitPrefixed("<div>{{ a + b }}</div>");

        var sources = emitted.SourceMappings.Select(mapping => mapping.TemplateLocation.Source).ToList();
        sources.ShouldContain("a");
        sources.ShouldContain("b");
        foreach (var mapping in emitted.SourceMappings)
        {
            emitted.Code.Substring(mapping.GeneratedColumn, mapping.TemplateLocation.Source.Length)
                .ShouldBe(mapping.TemplateLocation.Source);
        }
    }

    [Fact]
    public void StaticTemplate_ProducesNoMappings()
    {
        // No dynamic expression means nothing to map: the render source map is empty.
        EmitPrefixed("<div>static text</div>").SourceMappings.Count.ShouldBe(0);
    }

    [Fact]
    public void StaticDirectiveArgument_IsNotMapped_OnlyDynamicAccess()
    {
        // A static prop value emits as a string literal (no member access, no compile error to relocate),
        // so it is not mapped; only the dynamic `{{ count }}` interpolation is.
        var emitted = EmitPrefixed("<div class=\"box\">{{ count }}</div>");

        var mapping = emitted.SourceMappings.ShouldHaveSingleItem();
        mapping.TemplateLocation.Source.ShouldBe("count");
    }

    [Fact]
    public void SourceMap_IsDeterministic_AndParticipatesInResultEquality()
    {
        // The map rides inside the value-equatable result, so two runs over identical input produce equal
        // maps (and equal, equally-hashed results) — the incremental-generator caching contract.
        const string source = "<div :id=\"dynamicId\">{{ message }}</div>";
        var first = EmitPrefixed(source);
        var second = EmitPrefixed(source);

        second.SourceMappings.ShouldBe(first.SourceMappings);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    private static RenderFunctionEmitterResult EmitPrefixed(string source)
    {
        var root = TemplateParser.Parse(source, ParserOptions.CreateHtml());
        var transformOptions = TransformOptions.CreateDom();
        transformOptions.PrefixIdentifiers = true;
        transformOptions.BindingMetadata = BindingMetadata.Empty;
        var result = Transformer.Transform(root, transformOptions);
        return RenderFunctionEmitter.Emit(result);
    }
}
