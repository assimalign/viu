using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Css;

/// <summary>
/// Pins total dispatch for the open CSS record hierarchies. External node variants and invalid
/// combinator values fail at every public seam that interprets them instead of being silently copied,
/// dropped, or rendered as a different valid variant. Specified by <c>[SFC-DIAG-3]</c>.
/// </summary>
public sealed class CssVisitorExhaustivenessTests
{
    private static readonly SourceLocation EmptyLocation = new(
        new Position(0, 1, 1),
        new Position(0, 1, 1),
        string.Empty);

    [Fact]
    public void StylesheetWriter_UnsupportedRule_Throws()
        => ShouldThrowUnsupported(
            () => CssStylesheetWriter.Write(CreateStylesheet(new UnsupportedCssNode { Location = EmptyLocation })),
            nameof(UnsupportedCssNode));

    [Fact]
    public void BindingRewriter_UnsupportedRule_Throws()
        => ShouldThrowUnsupported(
            () => CssBindingRewriter.Rewrite(CreateStylesheet(new UnsupportedCssNode { Location = EmptyLocation }), "salt"),
            nameof(UnsupportedCssNode));

    [Fact]
    public void ModuleRewriter_UnsupportedRule_Throws()
        => ShouldThrowUnsupported(
            () => CssModuleRewriter.Rewrite(CreateStylesheet(new UnsupportedCssNode { Location = EmptyLocation }), "salt"),
            nameof(UnsupportedCssNode));

    [Fact]
    public void ScopedRewriter_UnsupportedRule_Throws()
        => ShouldThrowUnsupported(
            () => CssScopedRewriter.Rewrite(CreateStylesheet(new UnsupportedCssNode { Location = EmptyLocation }), "data-v-test"),
            nameof(UnsupportedCssNode));

    [Fact]
    public void ScopedRewriter_UnsupportedRuleNestedInKeyframes_Throws()
    {
        var unsupported = new UnsupportedCssNode { Location = EmptyLocation };
        var keyframes = new CssAtRuleNode
        {
            Name = "keyframes",
            Prelude = "spin",
            HasBlock = true,
            Body = new SyntaxList<CssSyntaxNode>([unsupported]),
            Location = EmptyLocation,
        };

        ShouldThrowUnsupported(
            () => CssScopedRewriter.Rewrite(CreateStylesheet(keyframes), "data-v-test"),
            nameof(UnsupportedCssNode));
    }

    [Fact]
    public void StylesheetWriter_UnsupportedSelectorPart_Throws()
        => ShouldThrowUnsupported(
            () => CssStylesheetWriter.Write(CreateStylesheetWithPart(new UnsupportedSelectorPartNode { Location = EmptyLocation })),
            nameof(UnsupportedSelectorPartNode));

    [Fact]
    public void ModuleRewriter_UnsupportedSelectorPart_Throws()
        => ShouldThrowUnsupported(
            () => CssModuleRewriter.Rewrite(
                CreateStylesheetWithPart(new UnsupportedSelectorPartNode { Location = EmptyLocation }),
                "salt"),
            nameof(UnsupportedSelectorPartNode));

    [Fact]
    public void ScopedRewriter_UnsupportedSelectorPart_Throws()
        => ShouldThrowUnsupported(
            () => CssScopedRewriter.Rewrite(
                CreateStylesheetWithPart(new UnsupportedSelectorPartNode { Location = EmptyLocation }),
                "data-v-test"),
            nameof(UnsupportedSelectorPartNode));

    [Fact]
    public void SyntaxFactory_UnsupportedSelectorPart_Throws()
    {
        var selector = CssSyntaxFactory.ComplexSelector(
            new CssSelectorPartNode[] { new UnsupportedSelectorPartNode { Location = EmptyLocation } });

        ShouldThrowUnsupported(
            () => CssSyntaxFactory.QualifiedRule(selector, Array.Empty<CssDeclarationNode>()),
            nameof(UnsupportedSelectorPartNode));
    }

    [Fact]
    public void StylesheetWriter_UnsupportedCombinatorKind_Throws()
        => ShouldThrowUnsupportedCombinator(
            () => CssStylesheetWriter.Write(CreateStylesheetWithPart(CreateUnsupportedCombinator())));

    [Fact]
    public void ScopedRewriter_UnsupportedCombinatorKind_Throws()
        => ShouldThrowUnsupportedCombinator(
            () => CssScopedRewriter.Rewrite(
                CreateStylesheetWithPart(CreateUnsupportedCombinator()),
                "data-v-test"));

    [Fact]
    public void SyntaxFactory_UnsupportedCombinatorKind_Throws()
    {
        var selector = CssSyntaxFactory.ComplexSelector(new CssSelectorPartNode[] { CreateUnsupportedCombinator() });

        ShouldThrowUnsupportedCombinator(
            () => CssSyntaxFactory.QualifiedRule(selector, Array.Empty<CssDeclarationNode>()));
    }

    private static CssStylesheetNode CreateStylesheet(CssSyntaxNode rule)
        => new()
        {
            Rules = new SyntaxList<CssSyntaxNode>([rule]),
            Location = EmptyLocation,
        };

    private static CssStylesheetNode CreateStylesheetWithPart(CssSelectorPartNode part)
    {
        var complex = new CssComplexSelectorNode
        {
            Parts = new SyntaxList<CssSelectorPartNode>([part]),
            Location = EmptyLocation,
        };
        var selectors = new CssSelectorListNode
        {
            Selectors = new SyntaxList<CssComplexSelectorNode>([complex]),
            Location = EmptyLocation,
        };
        var rule = new CssQualifiedRuleNode
        {
            Prelude = string.Empty,
            Selectors = selectors,
            Declarations = SyntaxList<CssDeclarationNode>.Empty,
            Location = EmptyLocation,
        };

        return CreateStylesheet(rule);
    }

    private static CssCombinatorNode CreateUnsupportedCombinator()
        => new()
        {
            Combinator = (CssCombinatorKind)int.MaxValue,
            Location = EmptyLocation,
        };

    private static void ShouldThrowUnsupported(Action action, string typeName)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(action);
        exception.Message.ShouldContain(typeName);
    }

    private static void ShouldThrowUnsupportedCombinator(Action action)
    {
        ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(action);
        exception.ParamName.ShouldBe("kind");
        exception.ActualValue.ShouldBe((CssCombinatorKind)int.MaxValue);
    }

    private sealed record UnsupportedCssNode : CssSyntaxNode
    {
        public override CssSyntaxNodeKind Kind => (CssSyntaxNodeKind)int.MaxValue;
    }

    private sealed record UnsupportedSelectorPartNode : CssSelectorPartNode
    {
        public override CssSyntaxNodeKind Kind => (CssSyntaxNodeKind)int.MaxValue;
    }
}
