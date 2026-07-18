using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Compiler;

// Ported from vuejs/core packages/compiler-core/__tests__/parse.spec.ts, describe('Errors'): each case
// asserts the complete reported error set — code AND exact offset/line/column — matching upstream's
// expected arrays verbatim. Codes upstream leaves commented out (not emitted by the 3.4+ state-machine
// parser, e.g. ABRUPT_CLOSING_OF_EMPTY_COMMENT, MISSING_WHITESPACE_BETWEEN_ATTRIBUTES) have no cases here.
public class ErrorTests
{
    public static IEnumerable<object[]> ErrorCases()
    {
        // CDATA_IN_HTML_CONTENT
        yield return Case("<template><![CDATA[cdata]]></template>",
            (CompilerErrorCode.CdataInHtmlContent, 10, 1, 11));
        yield return Case("<template><svg><![CDATA[cdata]]></svg></template>");

        // DUPLICATE_ATTRIBUTE
        yield return Case("<template><div id=\"\" id=\"\"></div></template>",
            (CompilerErrorCode.DuplicateAttribute, 21, 1, 22));

        // EOF_BEFORE_TAG_NAME
        yield return Case("<template><",
            (CompilerErrorCode.EofBeforeTagName, 11, 1, 12),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));
        yield return Case("<template></",
            (CompilerErrorCode.EofBeforeTagName, 12, 1, 13),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));

        // EOF_IN_CDATA
        yield return Case("<template><svg><![CDATA[cdata",
            (CompilerErrorCode.EofInCdata, 29, 1, 30),
            (CompilerErrorCode.XMissingEndTag, 10, 1, 11),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));

        // EOF_IN_COMMENT
        yield return Case("<template><!--comment",
            (CompilerErrorCode.EofInComment, 21, 1, 22),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));

        // EOF_IN_TAG
        yield return Case("<template><div",
            (CompilerErrorCode.EofInTag, 14, 1, 15),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));
        yield return Case("<template><div ",
            (CompilerErrorCode.EofInTag, 15, 1, 16),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));
        yield return Case("<template><div id",
            (CompilerErrorCode.EofInTag, 17, 1, 18),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));
        yield return Case("<template><div id=\"abc",
            (CompilerErrorCode.EofInTag, 22, 1, 23),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));

        // MISSING_ATTRIBUTE_VALUE
        yield return Case("<template><div id=></div></template>",
            (CompilerErrorCode.MissingAttributeValue, 18, 1, 19));
        yield return Case("<template><div id= ></div></template>",
            (CompilerErrorCode.MissingAttributeValue, 19, 1, 20));
        yield return Case("<template><div id= /></div></template>");

        // MISSING_END_TAG_NAME
        yield return Case("<template></></template>",
            (CompilerErrorCode.MissingEndTagName, 12, 1, 13));

        // UNEXPECTED_CHARACTER_IN_ATTRIBUTE_NAME
        yield return Case("<template><div a\"bc=''></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInAttributeName, 16, 1, 17));
        yield return Case("<template><div a'bc=''></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInAttributeName, 16, 1, 17));
        yield return Case("<template><div a<bc=''></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInAttributeName, 16, 1, 17));

        // UNEXPECTED_CHARACTER_IN_UNQUOTED_ATTRIBUTE_VALUE
        yield return Case("<template><div foo=bar\"></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue, 22, 1, 23));
        yield return Case("<template><div foo=bar'></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue, 22, 1, 23));
        yield return Case("<template><div foo=bar<div></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue, 22, 1, 23));
        yield return Case("<template><div foo=bar=baz></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue, 22, 1, 23));
        yield return Case("<template><div foo=bar`></div></template>",
            (CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue, 22, 1, 23));

        // UNEXPECTED_EQUALS_SIGN_BEFORE_ATTRIBUTE_NAME
        yield return Case("<template><div =foo=bar></div></template>",
            (CompilerErrorCode.UnexpectedEqualsSignBeforeAttributeName, 15, 1, 16));

        // UNEXPECTED_QUESTION_MARK_INSTEAD_OF_TAG_NAME
        yield return Case("<template><?xml?></template>",
            (CompilerErrorCode.UnexpectedQuestionMarkInsteadOfTagName, 11, 1, 12));

        // UNEXPECTED_SOLIDUS_IN_TAG
        yield return Case("<template><div a/b></div></template>",
            (CompilerErrorCode.UnexpectedSolidusInTag, 16, 1, 17));

        // X_INVALID_END_TAG
        yield return Case("<template></div></template>",
            (CompilerErrorCode.XInvalidEndTag, 10, 1, 11));
        yield return Case("<template></div></div></template>",
            (CompilerErrorCode.XInvalidEndTag, 10, 1, 11),
            (CompilerErrorCode.XInvalidEndTag, 16, 1, 17));

        // X_MISSING_END_TAG
        yield return Case("<template><div></template>",
            (CompilerErrorCode.XMissingEndTag, 10, 1, 11));
        yield return Case("<template><div>",
            (CompilerErrorCode.XMissingEndTag, 10, 1, 11),
            (CompilerErrorCode.XMissingEndTag, 0, 1, 1));

        // X_MISSING_INTERPOLATION_END
        yield return Case("{{ foo",
            (CompilerErrorCode.XMissingInterpolationEnd, 0, 1, 1));
        yield return Case("{{",
            (CompilerErrorCode.XMissingInterpolationEnd, 0, 1, 1));
        yield return Case("{{}}");

        // X_MISSING_DYNAMIC_DIRECTIVE_ARGUMENT_END
        yield return Case("<div v-foo:[sef fsef] />",
            (CompilerErrorCode.XMissingDynamicDirectiveArgumentEnd, 15, 1, 16));

        // X_MISSING_DIRECTIVE_NAME
        yield return Case("<div v-></div>",
            (CompilerErrorCode.XMissingDirectiveName, 5, 1, 6));
        yield return Case("<div v-:arg></div>",
            (CompilerErrorCode.XMissingDirectiveName, 5, 1, 6));
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public void Parse_MalformedInput_ReportsUpstreamErrorSet(
        string source,
        (CompilerErrorCode Code, int Offset, int Line, int Column)[] expected)
    {
        // Mirrors the upstream harness: parseMode 'html' with getNamespace mapping only <svg> to SVG.
        var options = new ParserOptions
        {
            Mode = TemplateParseMode.Html,
            GetNamespace = (tag, _, _) => tag == "svg" ? ElementNamespace.Svg : ElementNamespace.Html,
        };
        var errors = TestHelpers.Errors(source, options);

        errors.Select(e => (e.Code, e.Location.Start.Offset, e.Location.Start.Line, e.Location.Start.Column))
            .ShouldBe(expected.Select(e => (e.Code, e.Offset, e.Line, e.Column)));
    }

    [Fact]
    public void Parse_ErrorLocations_AreZeroWidthWithEmptySource()
    {
        var errors = TestHelpers.Errors("<template></div></template>");

        var error = errors.ShouldHaveSingleItem();
        error.Location.Start.ShouldBe(error.Location.End);
        error.Location.Source.ShouldBe("");
        error.Message.ShouldBe("Invalid end tag.");
    }

    [Fact]
    public void Parse_WithoutOnError_DoesNotThrowOnMalformedInput()
    {
        // Recoverable parsing: with no OnError installed, malformed input still yields a tree.
        var root = TemplateParser.Parse("<div><span></div>");

        root.Children.ShouldHaveSingleItem().ShouldBeOfType<ElementNode>().Tag.ShouldBe("div");
    }

    [Fact]
    public void ErrorCatalog_NumericValues_MatchUpstreamErrorCodes()
    {
        // Pins the numeric parity contract with @vue/compiler-core errors.ts (feeds [V01.01.05.08]).
        ((int)CompilerErrorCode.AbruptClosingOfEmptyComment).ShouldBe(0);
        ((int)CompilerErrorCode.UnexpectedSolidusInTag).ShouldBe(22);
        ((int)CompilerErrorCode.XInvalidEndTag).ShouldBe(23);
        ((int)CompilerErrorCode.XMissingDynamicDirectiveArgumentEnd).ShouldBe(27);
        ((int)CompilerErrorCode.XVIfNoExpression).ShouldBe(28);
        ((int)CompilerErrorCode.XKeepAliveInvalidChildren).ShouldBe(46);
        ((int)CompilerErrorCode.XVnodeHooks).ShouldBe(51);
        ((int)CompilerErrorCode.XVBindInvalidSameNameArgument).ShouldBe(52);
        ((int)CompilerErrorCode.ExtendPoint).ShouldBe(53);
    }

    private static object[] Case(string source, params (CompilerErrorCode, int, int, int)[] expected)
        => new object[] { source, expected };
}
