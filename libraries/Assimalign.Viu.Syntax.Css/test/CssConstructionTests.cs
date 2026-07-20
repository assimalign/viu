using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Css;

// The programmatic CSS construction and deterministic emission surface ([V01.01.12.11]): building the
// existing record-graph node types from code (CssSyntaxFactory / CssStylesheetBuilder) and serializing them
// through the same canonical serializer path CssStylesheetWriter / CssScopedRewriter use, per CSS Syntax
// Module Level 3 (https://www.w3.org/TR/css-syntax-3/). Pins construction + serialization against fixed
// expected CSS text (including nested @media), the synthetic-location divergence from the exact-slice
// SourceLocation invariant, byte-identical determinism (the incremental-cache contract), and value equality.
public class CssConstructionTests
{
    // ---- construction of the node types ----

    [Fact]
    public void Declaration_SetsPropertyValueImportant()
    {
        var declaration = CssSyntaxFactory.Declaration("color", "red", important: true);

        declaration.Property.ShouldBe("color");
        declaration.Value.ShouldBe("red");
        declaration.Important.ShouldBeTrue();
        declaration.Kind.ShouldBe(CssSyntaxNodeKind.Declaration);
    }

    [Fact]
    public void QualifiedRule_RendersPreludeFromSelectors()
    {
        var rule = ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red"));

        rule.Prelude.ShouldBe(".foo");
        rule.Selectors.Selectors.Count.ShouldBe(1);
        rule.Declarations.Count.ShouldBe(1);
    }

    [Fact]
    public void QualifiedRule_RendersPreludeFromCompoundAndCombinator()
    {
        var selector = CssSyntaxFactory.ComplexSelector(new CssSelectorPartNode[]
        {
            CssSyntaxFactory.SimpleSelector(CssSimpleSelectorKind.Class, ".dark"),
            CssSyntaxFactory.Combinator(CssCombinatorKind.Descendant),
            CssSyntaxFactory.SimpleSelector(CssSimpleSelectorKind.Class, ".foo"),
            CssSyntaxFactory.Pseudo("hover"),
        });

        var rule = CssSyntaxFactory.QualifiedRule(selector, Array.Empty<CssDeclarationNode>());

        rule.Prelude.ShouldBe(".dark .foo:hover");
    }

    // ---- serialization pinned to fixed canonical CSS text ----

    [Fact]
    public void Write_SimpleRule_ProducesFixedCanonicalText()
    {
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red")),
        });

        CssStylesheetWriter.Write(stylesheet).ShouldBe(".foo {\n  color: red;\n}\n");
    }

    [Fact]
    public void Write_ImportantDeclaration_EmitsImportant()
    {
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red", important: true)),
        });

        CssStylesheetWriter.Write(stylesheet).ShouldBe(".foo {\n  color: red !important;\n}\n");
    }

    [Fact]
    public void Write_PseudoAndCombinatorSelectors_SerializeFromParts()
    {
        var selector = CssSyntaxFactory.ComplexSelector(new CssSelectorPartNode[]
        {
            CssSyntaxFactory.SimpleSelector(CssSimpleSelectorKind.Class, ".dark"),
            CssSyntaxFactory.Combinator(CssCombinatorKind.Descendant),
            CssSyntaxFactory.SimpleSelector(CssSimpleSelectorKind.Class, ".foo"),
            CssSyntaxFactory.Pseudo("hover"),
        });
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            CssSyntaxFactory.QualifiedRule(selector, new[] { CssSyntaxFactory.Declaration("color", "red") }),
        });

        CssStylesheetWriter.Write(stylesheet).ShouldBe(".dark .foo:hover {\n  color: red;\n}\n");
    }

    [Fact]
    public void Write_MediaBlock_ProducesFixedText()
    {
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            CssSyntaxFactory.Media("(min-width: 768px)", new CssSyntaxNode[]
            {
                ClassRule(".foo", CssSyntaxFactory.Declaration("display", "flex")),
            }),
        });

        CssStylesheetWriter.Write(stylesheet).ShouldBe(
            "@media (min-width: 768px) {\n  .foo {\n    display: flex;\n  }\n}\n");
    }

    [Fact]
    public void Write_NestedMedia_ProducesFixedText()
    {
        // A conditional-group at-rule nested inside another — the AC's explicit nested-@media case.
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            CssSyntaxFactory.Media("screen", new CssSyntaxNode[]
            {
                CssSyntaxFactory.Media("(min-width: 768px)", new CssSyntaxNode[]
                {
                    ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red")),
                }),
            }),
        });

        CssStylesheetWriter.Write(stylesheet).ShouldBe(
            "@media screen {\n  @media (min-width: 768px) {\n    .foo {\n      color: red;\n    }\n  }\n}\n");
    }

    [Fact]
    public void Write_PreservesConstructionOrder_WithoutReordering()
    {
        // The serializer emits declarations and rules in exactly the order they were added — it never sorts.
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            ClassRule(".b",
                CssSyntaxFactory.Declaration("z-index", "1"),
                CssSyntaxFactory.Declaration("color", "red")),
            ClassRule(".a", CssSyntaxFactory.Declaration("color", "blue")),
        });

        CssStylesheetWriter.Write(stylesheet).ShouldBe(
            ".b {\n  z-index: 1;\n  color: red;\n}\n.a {\n  color: blue;\n}\n");
    }

    // ---- byte-identical determinism (the incremental-cache contract) ----

    [Fact]
    public void Write_ConstructedGraph_IsByteIdenticalToParsedGraph()
    {
        // Constructed graphs and parsed graphs converge on the same canonical text through the same serializer.
        var constructed = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red")),
        });
        var parsed = CssTestHelpers.ParseStylesheet(".foo{color:red}");

        CssStylesheetWriter.Write(constructed).ShouldBe(CssStylesheetWriter.Write(parsed));
    }

    [Fact]
    public void Write_IdenticalConstructionsFromSeparateCalls_AreByteIdentical()
    {
        CssStylesheetWriter.Write(BuildSample()).ShouldBe(CssStylesheetWriter.Write(BuildSample()));
    }

    // ---- value equality and equal hashing ----

    [Fact]
    public void Construction_EqualInputs_ProduceEqualAndEquallyHashedGraphs()
    {
        var first = BuildSample();
        var second = BuildSample();

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Construction_DifferentInputs_AreNotEqual()
    {
        var red = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[] { ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red")) });
        var blue = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[] { ClassRule(".foo", CssSyntaxFactory.Declaration("color", "blue")) });

        red.ShouldNotBe(blue);
    }

    [Fact]
    public void Declaration_EqualInputs_AreEqual_ImportantFlagDistinguishes()
    {
        CssSyntaxFactory.Declaration("color", "red").ShouldBe(CssSyntaxFactory.Declaration("color", "red"));
        CssSyntaxFactory.Declaration("color", "red").ShouldNotBe(CssSyntaxFactory.Declaration("color", "red", important: true));
    }

    // ---- the synthetic-location divergence (documented, tested) ----

    [Fact]
    public void ConstructedNodes_AreMarkedSynthetic_ThroughoutTheGraph()
    {
        AssertAllSynthetic(BuildSample());
    }

    [Fact]
    public void ParsedNodes_AreNotSynthetic()
    {
        var parsed = CssTestHelpers.ParseStylesheet("@media screen {\n  .foo { color: red; }\n}\n");

        CssSyntheticLocation.IsSynthetic(parsed).ShouldBeFalse();
        // And the parsed tree still upholds the exact-slice invariant it always has.
        CssTestHelpers.AssertExactSlice(parsed, "@media screen {\n  .foo { color: red; }\n}\n");
    }

    [Fact]
    public void Pseudo_SyntheticSource_CarriesColonPrefixedText()
    {
        CssSyntaxFactory.Pseudo("hover").Location.Source.ShouldBe(":hover");
        CssSyntaxFactory.Pseudo("before", isElement: true).Location.Source.ShouldBe("::before");
        CssSyntheticLocation.IsSynthetic(CssSyntaxFactory.Pseudo("hover")).ShouldBeTrue();
    }

    [Fact]
    public void SyntheticLocation_SentinelOffset_IsNegative()
    {
        CssSyntheticLocation.SyntheticPosition.Offset.ShouldBe(-1);
        CssSyntheticLocation.Create("x").Start.Offset.ShouldBe(-1);
        CssSyntheticLocation.Create("x").End.Offset.ShouldBe(-1);
        CssSyntheticLocation.Create("x").Source.ShouldBe("x");
    }

    // ---- the builder, and flow through the scoped rewriter ----

    [Fact]
    public void Builder_AccumulatesRules_EqualsFactoryStylesheet()
    {
        var ruleA = ClassRule(".a", CssSyntaxFactory.Declaration("color", "red"));
        var ruleB = ClassRule(".b", CssSyntaxFactory.Declaration("color", "blue"));

        var built = new CssStylesheetBuilder().Add(ruleA).Add(ruleB).Build();

        built.ShouldBe(CssSyntaxFactory.Stylesheet(new CssSyntaxNode[] { ruleA, ruleB }));
        new CssStylesheetBuilder().Add(ruleA).Count.ShouldBe(1);
    }

    [Fact]
    public void Scope_ConstructedGraph_FlowsThroughScopedRewriter()
    {
        // A constructed graph is a faithful record graph, so the scoped rewrite (which needs the parsed
        // parts to find the attribute-insertion point) handles it exactly as a parsed one.
        var stylesheet = CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red")),
        });

        CssScopedRewriter.Rewrite(stylesheet, "data-v-test")
            .ShouldBe(".foo[data-v-test] {\n  color: red;\n}\n");
    }

    // ---- argument validation (matches the serializer entry points) ----

    [Fact]
    public void Factory_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => CssSyntaxFactory.Declaration(null!, "red"));
        Should.Throw<ArgumentNullException>(() => CssSyntaxFactory.Declaration("color", null!));
        Should.Throw<ArgumentNullException>(() => CssSyntaxFactory.SimpleSelector(CssSimpleSelectorKind.Class, null!));
        Should.Throw<ArgumentNullException>(() => CssSyntaxFactory.Pseudo(null!));
        Should.Throw<ArgumentNullException>(() => CssSyntaxFactory.Stylesheet(null!));
        Should.Throw<ArgumentNullException>(() => CssSyntaxFactory.Media(null!, Array.Empty<CssSyntaxNode>()));
    }

    [Fact]
    public void Factory_NullCollectionElement_Throws()
        => Should.Throw<ArgumentException>(() => CssSyntaxFactory.Stylesheet(new CssSyntaxNode[] { null! }));

    // ---- helpers ----

    private static CssQualifiedRuleNode ClassRule(string className, params CssDeclarationNode[] declarations)
        => CssSyntaxFactory.QualifiedRule(
            CssSyntaxFactory.ComplexSelector(new CssSelectorPartNode[]
            {
                CssSyntaxFactory.SimpleSelector(CssSimpleSelectorKind.Class, className),
            }),
            declarations);

    private static CssStylesheetNode BuildSample()
        => CssSyntaxFactory.Stylesheet(new CssSyntaxNode[]
        {
            ClassRule(".foo", CssSyntaxFactory.Declaration("color", "red")),
            CssSyntaxFactory.Media("(min-width: 768px)", new CssSyntaxNode[]
            {
                ClassRule(".foo",
                    CssSyntaxFactory.Declaration("color", "blue"),
                    CssSyntaxFactory.Declaration("display", "flex", important: true)),
            }),
        });

    // The synthetic counterpart of CssTestHelpers.AssertExactSlice: every node in a constructed graph is
    // marked synthetic (the divergence), pinned recursively rather than node-by-node at each call site.
    private static void AssertAllSynthetic(CssSyntaxNode node)
    {
        CssSyntheticLocation.IsSynthetic(node).ShouldBeTrue();

        switch (node)
        {
            case CssStylesheetNode stylesheet:
                foreach (var rule in stylesheet.Rules)
                {
                    AssertAllSynthetic(rule);
                }

                break;
            case CssQualifiedRuleNode qualified:
                AssertAllSynthetic(qualified.Selectors);
                foreach (var declaration in qualified.Declarations)
                {
                    AssertAllSynthetic(declaration);
                }

                break;
            case CssAtRuleNode atRule:
                foreach (var child in atRule.Body)
                {
                    AssertAllSynthetic(child);
                }

                break;
            case CssSelectorListNode list:
                foreach (var complex in list.Selectors)
                {
                    AssertAllSynthetic(complex);
                }

                break;
            case CssComplexSelectorNode complex:
                foreach (var part in complex.Parts)
                {
                    AssertAllSynthetic(part);
                }

                break;
        }
    }
}
