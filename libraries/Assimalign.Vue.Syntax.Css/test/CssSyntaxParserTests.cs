using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Css;

// Rule-level CSS parsing ([V01.01.06.04]): the parser produces a located, value-equatable stylesheet tree
// (qualified rules with parsed selector lists, at-rules, declarations) per CSS Syntax Module Level 3
// (https://www.w3.org/TR/css-syntax-3/), recoverable on malformed input and upholding the exact-slice
// SourceLocation invariant on every node.
public class CssSyntaxParserTests
{
    [Fact]
    public void ParseSyntax_SimpleRule_ProducesQualifiedRuleWithSelectorAndDeclaration()
    {
        var source = "a:hover {\n  color: red;\n}\n";

        var result = new CssSyntaxParser().ParseSyntax(source);

        result.Diagnostics.Count.ShouldBe(0);
        var stylesheet = result.Nodes.ShouldHaveSingleItem().ShouldBeOfType<CssStylesheetNode>();
        stylesheet.Kind.ShouldBe(CssSyntaxNodeKind.Stylesheet);
        stylesheet.Location.Source.ShouldBe(source);

        var rule = stylesheet.Rules.ShouldHaveSingleItem().ShouldBeOfType<CssQualifiedRuleNode>();
        rule.Prelude.ShouldBe("a:hover");
        rule.Location.Source.ShouldBe("a:hover {\n  color: red;\n}");

        var complex = rule.Selectors.Selectors.ShouldHaveSingleItem();
        complex.Parts.Count.ShouldBe(2);
        var type = complex.Parts[0].ShouldBeOfType<CssSimpleSelectorNode>();
        type.Selector.ShouldBe(CssSimpleSelectorKind.Type);
        type.Text.ShouldBe("a");
        var pseudo = complex.Parts[1].ShouldBeOfType<CssPseudoSelectorNode>();
        pseudo.Pseudo.ShouldBe(CssPseudoSelectorKind.Normal);
        pseudo.Name.ShouldBe("hover");
        pseudo.Location.Source.ShouldBe(":hover");

        var declaration = rule.Declarations.ShouldHaveSingleItem();
        declaration.Property.ShouldBe("color");
        declaration.Value.ShouldBe("red");
        declaration.Important.ShouldBeFalse();
        declaration.Location.Source.ShouldBe("color: red");
    }

    [Fact]
    public void ParseSyntax_CompoundAndCombinators_ProducesFlatPartList()
    {
        var source = ".a.b > #c d { color: red; }";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        var complex = ((CssQualifiedRuleNode)stylesheet.Rules[0]).Selectors.Selectors[0];

        // .a  .b  >  #c  (descendant)  d  — adjacent simples are separate parts; combinators are explicit.
        complex.Parts.Select(part => Describe(part)).ShouldBe(new[]
        {
            "class:.a", "class:.b", "combinator:Child", "id:#c", "combinator:Descendant", "type:d",
        });
    }

    [Fact]
    public void ParseSyntax_DeepSlottedGlobalPseudos_ParseArgumentsAndClassify()
    {
        var source = ":deep(.a) :slotted(.b) :global(.c) { color: red; }";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        var parts = ((CssQualifiedRuleNode)stylesheet.Rules[0]).Selectors.Selectors[0].Parts;

        var deep = parts.OfType<CssPseudoSelectorNode>().Single(p => p.Pseudo == CssPseudoSelectorKind.Deep);
        deep.Argument.ShouldNotBeNull();
        deep.Argument!.Location.Source.ShouldBe(".a");
        parts.OfType<CssPseudoSelectorNode>().Single(p => p.Pseudo == CssPseudoSelectorKind.Slotted)
            .Argument!.Location.Source.ShouldBe(".b");
        parts.OfType<CssPseudoSelectorNode>().Single(p => p.Pseudo == CssPseudoSelectorKind.Global)
            .Argument!.Location.Source.ShouldBe(".c");
    }

    [Fact]
    public void ParseSyntax_GroupedSelectors_SplitOnTopLevelCommas()
    {
        var source = "h1 .foo, .bar, .baz { color: red; }";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        var list = ((CssQualifiedRuleNode)stylesheet.Rules[0]).Selectors;

        list.Selectors.Count.ShouldBe(3);
        list.Selectors[0].Location.Source.ShouldBe("h1 .foo");
        list.Selectors[1].Location.Source.ShouldBe(".bar");
        list.Selectors[2].Location.Source.ShouldBe(".baz");
    }

    [Fact]
    public void ParseSyntax_MediaQuery_RecursesIntoNestedRules()
    {
        var source = "@media (max-width: 600px) {\n  .a { color: red; }\n}\n";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        var atRule = stylesheet.Rules.ShouldHaveSingleItem().ShouldBeOfType<CssAtRuleNode>();
        atRule.Name.ShouldBe("media");
        atRule.Prelude.ShouldBe("(max-width: 600px)");
        atRule.HasBlock.ShouldBeTrue();

        var nested = atRule.Body.ShouldHaveSingleItem().ShouldBeOfType<CssQualifiedRuleNode>();
        nested.Selectors.Selectors[0].Location.Source.ShouldBe(".a");
        nested.Declarations[0].Property.ShouldBe("color");
    }

    [Fact]
    public void ParseSyntax_Keyframes_ProducesKeyframeRules()
    {
        var source = "@keyframes spin {\n  from { opacity: 0; }\n  50%, 100% { opacity: 1; }\n}\n";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        var atRule = stylesheet.Rules.ShouldHaveSingleItem().ShouldBeOfType<CssAtRuleNode>();
        atRule.Name.ShouldBe("keyframes");
        atRule.Prelude.ShouldBe("spin");
        atRule.Body.Count.ShouldBe(2);

        var from = atRule.Body[0].ShouldBeOfType<CssKeyframeRuleNode>();
        from.Selector.ShouldBe("from");
        from.Declarations[0].Property.ShouldBe("opacity");
        atRule.Body[1].ShouldBeOfType<CssKeyframeRuleNode>().Selector.ShouldBe("50%, 100%");
    }

    [Fact]
    public void ParseSyntax_ImportStatement_HasNoBlock()
    {
        var source = "@import \"reset.css\";\n.a { color: red; }\n";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        var import = stylesheet.Rules[0].ShouldBeOfType<CssAtRuleNode>();
        import.Name.ShouldBe("import");
        import.HasBlock.ShouldBeFalse();
        import.Prelude.ShouldBe("\"reset.css\"");
        stylesheet.Rules[1].ShouldBeOfType<CssQualifiedRuleNode>();
    }

    [Fact]
    public void ParseSyntax_Important_StripsFlagFromValue()
    {
        var source = ".a { color: red !important; }";

        var declaration = ((CssQualifiedRuleNode)CssTestHelpers.ParseStylesheet(source).Rules[0]).Declarations[0];
        declaration.Value.ShouldBe("red");
        declaration.Important.ShouldBeTrue();
    }

    [Fact]
    public void ParseSyntax_EveryNode_UpholdsExactSliceInvariant()
    {
        var source =
            "/* c */\n" +
            "@media screen {\n" +
            "  .a.b > #c :deep(.inner) { color: red; margin: 0 !important; }\n" +
            "}\n" +
            "@keyframes spin { from { opacity: 0 } to { opacity: 1 } }\n" +
            "a[href^=\"http\"], .x::before { content: \"}\"; }\n";

        var stylesheet = CssTestHelpers.ParseStylesheet(source);
        CssTestHelpers.AssertExactSlice(stylesheet, source);
    }

    [Fact]
    public void ParseSyntax_SameSourceTwice_YieldsEqualResults()
    {
        var parser = new CssSyntaxParser();
        var source = "@media screen { .button:hover, .button:focus { padding: 1rem; } }";

        var first = parser.ParseSyntax(source);
        var second = parser.ParseSyntax(source);

        // The incremental-caching contract: equal input yields equal, equally-hashed results.
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Parse_UntypedEntryPoint_ReturnsStylesheetResult()
    {
        SyntaxParser parser = new CssSyntaxParser();
        var source = "body { margin: 0; }";

        var result = parser.Parse(new SyntaxSource { Text = source, Name = "site.css", Language = "css" });

        result.ShouldBeOfType<SyntaxParserResult<CssSyntaxNode>>();
        result.Nodes.ShouldHaveSingleItem().ShouldBeOfType<CssStylesheetNode>();
    }

    private static string Describe(CssSelectorPartNode part)
        => part switch
        {
            CssCombinatorNode combinator => "combinator:" + combinator.Combinator,
            CssPseudoSelectorNode pseudo => "pseudo:" + pseudo.Name,
            CssSimpleSelectorNode { Selector: CssSimpleSelectorKind.Class } simple => "class:" + simple.Text,
            CssSimpleSelectorNode { Selector: CssSimpleSelectorKind.Id } simple => "id:" + simple.Text,
            CssSimpleSelectorNode { Selector: CssSimpleSelectorKind.Type } simple => "type:" + simple.Text,
            CssSimpleSelectorNode simple => "simple:" + simple.Text,
            _ => "?",
        };
}
