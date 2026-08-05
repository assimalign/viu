using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins every auto-closing decision a <c>.viu</c> document makes ([V01.01.12.07.08]).
/// </summary>
/// <remarks>
/// <para>
/// The caret is written into the test text as a <c>|</c>, which the helpers strip before handing the
/// document to the decision logic. Assertions on a completion re-render the affected line with the
/// insertion applied and the caret marked, so a single expected string pins the inserted text and
/// the caret offset together and a wrong offset cannot pass.
/// </para>
/// <para>
/// Only the decisions are covered here, because only the decisions are testable outside
/// <c>devenv.exe</c>: the MEF exports, the brace-completion session, and the buffer edit are
/// runtime-verified.
/// </para>
/// </remarks>
public class ViuAutoClosingLogicTests
{
    // ---- Character-pair gating: the brackets ------------------------------------------------

    [Fact]
    public void AllowsBracketPair_AllThreeBracketsInATemplate_Pair()
    {
        // The matrix these four tests pin is [V01.01.12.07.08]'s: the brackets pair in every
        // section. [V01.01.12.07.09] moved them onto a brace-completion context provider so a '{'
        // block can expand on Return, and moving them is exactly what makes the decision a question
        // somebody has to answer rather than pure MEF metadata.
        //
        // The '{' sample is a first brace. An earlier revision wrote it as "<p>{|", which is the
        // second-brace position the interpolation scaffold now owns - the sample, not the rule,
        // changed. That position is pinned by its own test below.
        AllowsBracket('{', "<template>", "    <p>|", "</template>").ShouldBeTrue();
        AllowsBracket('(', "<template>", "    <p>{{ Format(|", "</template>").ShouldBeTrue();
        AllowsBracket('[', "<template>", "    <p>{{ Items[|", "</template>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBracketPair_AllThreeBracketsInAScriptSection_Pair()
    {
        AllowsBracket('{', "@script {", "    partial void OnSetup() |", "}").ShouldBeTrue();
        AllowsBracket('(', "@script {", "    partial void OnSetup|", "}").ShouldBeTrue();
        AllowsBracket('[', "@script {", "    var first = Items|", "}").ShouldBeTrue();

        AllowsBracket('{', "<script>", "    void M() |", "</script>").ShouldBeTrue();
        AllowsBracket('(', "<script>", "    void M|", "</script>").ShouldBeTrue();
        AllowsBracket('[', "<script>", "    var first = Items|", "</script>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBracketPair_AllThreeBracketsInAStyleSection_Pair()
    {
        AllowsBracket('{', "<style>", "    .card |", "</style>").ShouldBeTrue();
        AllowsBracket('(', "<style>", "    .card { width: calc|", "</style>").ShouldBeTrue();
        AllowsBracket('[', "<style>", "    a|", "</style>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBracketPair_AllThreeBracketsBetweenContainers_Pair()
    {
        AllowsBracket('{', "<template></template>", "|", "@script {", "}").ShouldBeTrue();
        AllowsBracket('(', "<template></template>", "|", "@script {", "}").ShouldBeTrue();
        AllowsBracket('[', "<template></template>", "|", "@script {", "}").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBracketPair_ACharacterThatIsNotABracket_DoesNotPair()
    {
        // The quotes are gated by AllowsQuotePair and answered by their own provider; a character
        // this provider is not registered for is never a pair.
        AllowsBracket('"', "@script {", "    var text = |", "}").ShouldBeFalse();
        AllowsBracket('<', "<template>", "    |", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsBracketPair_APositionOutsideTheDocument_DoesNotPair()
    {
        ViuAutoClosingLogic.AllowsBracketPair(["@script {", "}"], 5, 0, '{').ShouldBeFalse();
        ViuAutoClosingLogic.AllowsBracketPair(["@script {", "}"], 0, 99, '{').ShouldBeFalse();
    }

    [Fact]
    public void AllowsBracketPair_TheSecondBraceOfATemplateInterpolation_DoesNotPair()
    {
        // The scaffold owns that keystroke ([V01.01.12.07.09]). Declining here is what keeps the
        // editor from holding a pending session that would insert a third brace after the scaffold
        // has already written a balanced pair.
        AllowsBracket('{', "<template>", "    <p>{|", "</template>").ShouldBeFalse();
        AllowsBracket('{', "<template>", "    <p>{|}", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsBracketPair_ASecondBraceOutsideATemplate_StillPairs()
    {
        // In C# the second brace is a nested block or an interpolated-string brace and must pair on
        // its own; in CSS it is a nested at-rule block.
        AllowsBracket('{', "@script {", "    if (Visible) {|", "}").ShouldBeTrue();
        AllowsBracket('{', "<script>", "    if (Visible) {|", "</script>").ShouldBeTrue();
        AllowsBracket('{', "<style>", "    @media print {|", "</style>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBracketPair_ABraceAfterSomethingElse_StillPairs()
    {
        // Only a brace immediately after a brace is the interpolation position.
        AllowsBracket('{', "<template>", "    <p>|", "</template>").ShouldBeTrue();
        AllowsBracket('{', "<template>", "    <p>{{ a }}|", "</template>").ShouldBeTrue();
    }

    // ---- The template interpolation scaffold --------------------------------------------------

    [Fact]
    public void GetTypedCharacterCompletion_SecondBraceWhereTheFirstOneNeverPaired_WritesBothClosers()
    {
        // The ordinary case: '<' is a registered opening brace, so the editor declined to pair the
        // first '{' inside an element and there is no closer to reuse.
        Complete('{', "<template>", "    <p>{|</p>", "</template>")
            .ShouldBe("    <p>{{|}}</p>");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SecondBraceWhereTheFirstOnePaired_ReusesTheExistingCloser()
    {
        // Same result from the other caret state, so the user cannot tell which path they were on.
        Complete('{', "<template>", "    <p>{|}", "</template>")
            .ShouldBe("    <p>{{|}}");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SecondBraceAtTheEndOfALine_WritesBothClosers()
    {
        Complete('{', "<template>", "    {|", "</template>").ShouldBe("    {{|}}");
    }

    [Fact]
    public void GetTypedCharacterCompletion_ABraceThatIsNotTheSecondOne_ScaffoldsNothing()
    {
        Completion('{', "<template>", "    <p>|</p>", "</template>").ShouldBeNull();
        Completion('{', "<template>", "    <p>a|</p>", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_ABraceAtTheStartOfALine_ScaffoldsNothing()
    {
        // '{{' is one token and never spans a line break, so a brace ending the line above opens
        // nothing this could continue.
        Completion('{', "<template>", "    {", "|", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_ASecondBraceOutsideATemplate_ScaffoldsNothing()
    {
        Completion('{', "@script {", "    if (Visible) {|", "}").ShouldBeNull();
        Completion('{', "<script>", "    if (Visible) {|", "</script>").ShouldBeNull();
        Completion('{', "<style>", "    @media print {|", "</style>").ShouldBeNull();
        Completion('{', "<template></template>", "{|", "@script {", "}").ShouldBeNull();
    }

    // ---- Walking over a closing brace ---------------------------------------------------------

    [Fact]
    public void AllowsClosingBraceWalkover_WithAClosingBraceAtTheCaretInATemplate_Advances()
    {
        // Nothing tracks the hand-written scaffold, so the editor's own type-through never applies:
        // without this, closing '{{}}' by typing both braces would leave '{{}|}}'.
        AllowsWalkover("<template>", "    <p>{{ Count |}}</p>", "</template>").ShouldBeTrue();
        AllowsWalkover("<template>", "    <p>{{ Count }|}</p>", "</template>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsClosingBraceWalkover_WithoutAClosingBraceAtTheCaret_Inserts()
    {
        AllowsWalkover("<template>", "    <p>{{ Count |</p>", "</template>").ShouldBeFalse();
        AllowsWalkover("<template>", "    <p>|</p>", "</template>").ShouldBeFalse();
        AllowsWalkover("<template>", "    <p>{{ Count }}|", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsClosingBraceWalkover_OutsideATemplate_Inserts()
    {
        // Script and style keep the platform's behavior: there a live session owns '}', and a
        // walk-over firing outside one would break ordinary C# and CSS authoring.
        AllowsWalkover("@script {", "    if (Visible) {|}", "}").ShouldBeFalse();
        AllowsWalkover("<script>", "    if (Visible) {|}", "</script>").ShouldBeFalse();
        AllowsWalkover("<style>", "    .card { color: red; |}", "</style>").ShouldBeFalse();
        AllowsWalkover("<template></template>", "|}", "@script {", "}").ShouldBeFalse();
    }

    // ---- Return between paired braces --------------------------------------------------------

    [Fact]
    public void AllowsBlockExpansionOnReturn_ABraceOpenedInAScriptSection_Expands()
    {
        // The C#-parity requirement of [V01.01.12.07.09]: a '{' in @script is a C# block, so Return
        // between its halves produces the indented block Visual Studio's C# editor produces.
        AllowsBlockExpansion("@script {", "    partial void OnSetup() |", "}").ShouldBeTrue();
        AllowsBlockExpansion("<script>", "    void M() |", "</script>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBlockExpansionOnReturn_ABraceOpenedInATemplate_DoesNotExpand()
    {
        // Template braces are most often an interpolation - '{{ Count }}' is authored by typing '{'
        // twice - and pushing its closer onto a line of its own would break the expression.
        AllowsBlockExpansion("<template>", "    <p>|", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsBlockExpansionOnReturn_ABraceOpenedInAStyleSection_DoesNotExpand()
    {
        // Recorded decision ([V01.01.12.07.09]): a CSS rule really is a block and expanding it would
        // be defensible, but style sections keep the pairing they shipped with so this change carries
        // exactly the behavior the work item specifies.
        AllowsBlockExpansion("<style>", "    .card |", "</style>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsBlockExpansionOnReturn_ABraceOpenedBetweenContainers_DoesNotExpand()
    {
        AllowsBlockExpansion("<template></template>", "|", "@script {", "}").ShouldBeFalse();
    }

    [Fact]
    public void AllowsBlockExpansionOnReturn_TheScriptHeaderLineItself_Expands()
    {
        // '@script {' is attributed to the section it opens, so the outermost block of the container
        // expands like any block inside it.
        AllowsBlockExpansion("|@script {", "}").ShouldBeTrue();
    }

    [Fact]
    public void AllowsBlockExpansionOnReturn_ALineOutsideTheDocument_DoesNotExpand()
    {
        ViuAutoClosingLogic.AllowsBlockExpansionOnReturn(["@script {", "}"], -1).ShouldBeFalse();
        ViuAutoClosingLogic.AllowsBlockExpansionOnReturn(["@script {", "}"], 7).ShouldBeFalse();
    }

    // ---- Character-pair gating: the quotes ---------------------------------------------------

    [Fact]
    public void AllowsQuotePair_DoubleQuoteInTemplateAttributeValuePosition_Pairs()
    {
        AllowsQuote('"', "<template>", "    <div class=|", "</template>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsQuotePair_DoubleQuoteInDirectiveValuePosition_Pairs()
    {
        AllowsQuote('"', "<template>", "    <button @click=|", "</template>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsQuotePair_DoubleQuoteInAttributeValuePositionOnAContinuationLine_Pairs()
    {
        // A tag header may span lines, so the attribute-value test walks back through them.
        AllowsQuote(
            '"',
            "<template>",
            "    <div",
            "        class=|",
            "</template>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsQuotePair_DoubleQuoteInTemplateTextContent_DoesNotPair()
    {
        // Pairing here would interrupt ordinary prose: the quote is punctuation, not a delimiter.
        AllowsQuote('"', "<template>", "    <p>He said |", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsQuotePair_DoubleQuoteInsideAnOpenAttributeValue_DoesNotPair()
    {
        AllowsQuote('"', "<template>", "    <div class=\"card |", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsQuotePair_DoubleQuoteInTagHeaderWithoutAnEquals_DoesNotPair()
    {
        AllowsQuote('"', "<template>", "    <div |", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsQuotePair_SingleQuoteInTemplate_NeverPairs()
    {
        // The apostrophe is the reason the single quote is script-only: pairing it in template prose
        // would corrupt ordinary text, and the attribute-value position is already served by the
        // double quote Viu's own lexer recognizes.
        AllowsQuote('\'', "<template>", "    <div class=|", "</template>").ShouldBeFalse();
        AllowsQuote('\'', "<template>", "    <p>it|", "</template>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsQuotePair_BothQuotesInTheScriptBlock_Pair()
    {
        AllowsQuote('"', "@script {", "    var text = |", "}").ShouldBeTrue();
        AllowsQuote('\'', "@script {", "    var letter = |", "}").ShouldBeTrue();
    }

    [Fact]
    public void AllowsQuotePair_BothQuotesInATopLevelScriptTag_Pair()
    {
        AllowsQuote('"', "<script>", "    var text = |", "</script>").ShouldBeTrue();
        AllowsQuote('\'', "<script>", "    var letter = |", "</script>").ShouldBeTrue();
    }

    [Fact]
    public void AllowsQuotePair_NeitherQuoteInAStyleSection_Pairs()
    {
        AllowsQuote('"', "<style>", "    div { content: |", "</style>").ShouldBeFalse();
        AllowsQuote('\'', "<style>", "    div { content: |", "</style>").ShouldBeFalse();
    }

    [Fact]
    public void AllowsQuotePair_NeitherQuoteBetweenContainers_Pairs()
    {
        AllowsQuote('"', "<template></template>", "|", "@script {", "}").ShouldBeFalse();
        AllowsQuote('\'', "<template></template>", "|", "@script {", "}").ShouldBeFalse();
    }

    [Fact]
    public void AllowsQuotePair_ACharacterThatIsNotAQuote_DoesNotPair()
    {
        AllowsQuote('`', "@script {", "    var text = |", "}").ShouldBeFalse();
    }

    // ---- Element auto-close: '>' finishing an open tag ------------------------------------

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterAnElementTag_InsertsTheEndTag()
    {
        Complete('>', "<template>", "    <div|", "</template>")
            .ShouldBe("    <div>|</div>");
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterATagWithAttributes_InsertsTheEndTag()
    {
        Complete('>', "<template>", "    <div class=\"card\" @click=\"Save\"|", "</template>")
            .ShouldBe("    <div class=\"card\" @click=\"Save\">|</div>");
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterAComponentTag_InsertsTheEndTag()
    {
        // Casing is the only component signal a lexical decision has, and it needs none: the end tag
        // repeats the authored spelling ordinally, which is how Viu resolves names ([CMP-6]).
        Complete('>', "<template>", "    <TodoItem|", "</template>")
            .ShouldBe("    <TodoItem>|</TodoItem>");
        Complete('>', "<template>", "    <Layout.Header|", "</template>")
            .ShouldBe("    <Layout.Header>|</Layout.Header>");
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterAFrameworkTag_InsertsTheEndTag()
    {
        // The framework tags auto-close like any other element: <template> is what a new component
        // starts with, and <slot> is what a layout component is built from.
        Complete('>', "<template|").ShouldBe("<template>|</template>");
        Complete('>', "<template>", "    <slot|", "</template>")
            .ShouldBe("    <slot>|</slot>");
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterATagHeaderSpanningLines_InsertsTheEndTag()
    {
        Complete(
            '>',
            "<template>",
            "    <div",
            "        class=\"card\"|",
            "</template>").ShouldBe("        class=\"card\">|</div>");
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterAVoidElement_InsertsNothing()
    {
        // The WHATWG void elements take no end tag, and the list comes from the repository's shared
        // DOM knowledge rather than from a table written for the editor.
        Completion('>', "<template>", "    <br|", "</template>").ShouldBeNull();
        Completion('>', "<template>", "    <input type=\"text\"|", "</template>").ShouldBeNull();
        Completion('>', "<template>", "    <img src=\"a.png\"|", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterAVoidElementInAnyCasing_InsertsNothing()
    {
        // Void elements are matched case-insensitively, the same way the template compiler and the
        // runtime resolve them.
        Completion('>', "<template>", "    <BR|", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterASelfClosedTag_InsertsNothing()
    {
        Completion('>', "<template>", "    <div /|", "</template>").ShouldBeNull();
        Completion('>', "<template>", "    <TodoItem :item=\"Item\"/|", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanBeforeAnExistingEndTag_InsertsNothing()
    {
        // Re-typing the '>' of a tag that already has its end tag must not add a second one.
        Completion('>', "<template>", "    <div|</div>", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanInsideAnAttributeValue_InsertsNothing()
    {
        // The '>' is content inside the value, not the end of the tag header.
        Completion('>', "<template>", "    <div :title=\"a |", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanInTemplateTextContent_InsertsNothing()
    {
        Completion('>', "<template>", "    <p>a |", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanInsideAnInterpolation_InsertsNothing()
    {
        Completion('>', "<template>", "    <p>{{ Left |", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanAfterAnEndTagName_InsertsNothing()
    {
        Completion('>', "<template>", "    <div>text</div|", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanInTheScriptBlock_InsertsNothing()
    {
        // A '>' in C# closes a generic argument list or a lambda arrow. This is the whole reason the
        // decision is section-aware.
        Completion('>', "@script {", "    var value = Context.Get<string|", "}").ShouldBeNull();
        Completion('>', "@script {", "    Items.Select(item =|", "}").ShouldBeNull();
        Completion('>', "@script {", "    if (Count |", "}").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanInATopLevelScriptTag_InsertsNothing()
    {
        Completion('>', "<script>", "    var value = Read<string|", "</script>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanInAStyleSection_InsertsNothing()
    {
        // '>' is the CSS child combinator.
        Completion('>', "<style>", "    div |", "</style>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_GreaterThanBetweenContainers_InsertsNothing()
    {
        Completion('>', "<template></template>", "<div|").ShouldBeNull();
    }

    // ---- Element auto-close: '/' completing the nearest unclosed element -------------------

    [Fact]
    public void GetTypedCharacterCompletion_SolidusAfterAngleBracket_CompletesTheNearestUnclosedElement()
    {
        Complete('/', "<template>", "    <div>", "        <|", "</template>")
            .ShouldBe("        </div>|");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusAfterAngleBracket_CompletesTheInnermostOfNestedElements()
    {
        Complete(
            '/',
            "<template>",
            "    <div>",
            "        <section>",
            "            <|",
            "</template>").ShouldBe("            </section>|");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusAfterAngleBracket_SkipsElementsAlreadyClosed()
    {
        Complete(
            '/',
            "<template>",
            "    <div>",
            "        <section>Done</section>",
            "        <|",
            "</template>").ShouldBe("        </div>|");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusAfterAngleBracket_SkipsVoidAndSelfClosedChildren()
    {
        Complete(
            '/',
            "<template>",
            "    <div>",
            "        <br>",
            "        <TodoItem :item=\"Item\" />",
            "        <|",
            "</template>").ShouldBe("        </div>|");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusAfterAngleBracket_IgnoresTagsInsideCommentsAndAttributeValues()
    {
        Complete(
            '/',
            "<template>",
            "    <div title=\"<b>\">",
            "        <!-- <section> -->",
            "        <|",
            "</template>").ShouldBe("        </div>|");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusAtTheTopOfATagDelimitedTemplate_CompletesTheContainer()
    {
        // The container tag is an unclosed element like any other, so this is how a hand-authored
        // <template> gets its closer.
        Complete('/', "<template>", "    <|").ShouldBe("    </template>|");
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusWithNothingUnclosed_InsertsNothing()
    {
        Completion('/', "@template {", "    <|", "}").ShouldBeNull();
        Completion('/', "@template {", "    <p>Done</p>", "    <|", "}").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusNotImmediatelyAfterAngleBracket_InsertsNothing()
    {
        Completion('/', "<template>", "    <div>text |", "</template>").ShouldBeNull();
        Completion('/', "<template>", "    <div |", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusInTheScriptBlock_InsertsNothing()
    {
        // Division, comments, and a generic type argument all put a '/' next to a '<' in C#.
        Completion('/', "@script {", "    var value = Read<|", "}").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_SolidusInAStyleSection_InsertsNothing()
    {
        Completion('/', "<style>", "    div { width: calc(100% <|", "</style>").ShouldBeNull();
    }

    // ---- Comment auto-close ---------------------------------------------------------------

    [Fact]
    public void GetTypedCharacterCompletion_HyphenCompletingACommentOpener_InsertsTheTerminator()
    {
        // Recorded shape: the caret lands between two spaces, so the comment reads "<!-- text -->"
        // as soon as the user types - the spaced form comments are conventionally written in.
        Complete('-', "<template>", "    <!-|", "</template>")
            .ShouldBe("    <!-- | -->");
    }

    [Fact]
    public void GetTypedCharacterCompletion_HyphenWithATrailingHyphenAlreadyPresent_InsertsNothing()
    {
        Completion('-', "<template>", "    <!-|->", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_HyphenNotCompletingACommentOpener_InsertsNothing()
    {
        Completion('-', "<template>", "    <!|", "</template>").ShouldBeNull();
        Completion('-', "<template>", "    <p>a |", "</template>").ShouldBeNull();
        Completion('-', "<template>", "    <div class=\"top|", "</template>").ShouldBeNull();
    }

    [Fact]
    public void GetTypedCharacterCompletion_HyphenOutsideATemplate_InsertsNothing()
    {
        Completion('-', "@script {", "    var value = Count -|", "}").ShouldBeNull();
        Completion('-', "<style>", "    <!-|", "</style>").ShouldBeNull();
    }

    // ---- Trigger characters and bounds ------------------------------------------------------

    [Theory]
    [InlineData('>', true)]
    [InlineData('/', true)]
    [InlineData('-', true)]
    [InlineData('<', false)]
    [InlineData('"', false)]
    [InlineData('a', false)]
    public void IsCompletionTriggerCharacter_ReportsOnlyTheThreeActingCharacters(
        char typedCharacter,
        bool expected)
    {
        ViuAutoClosingLogic.IsCompletionTriggerCharacter(typedCharacter).ShouldBe(expected);
    }

    [Fact]
    public void GetTypedCharacterCompletion_APositionOutsideTheDocument_InsertsNothing()
    {
        string[] lines = ["<template>", "    <div", "</template>"];

        ViuAutoClosingLogic.GetTypedCharacterCompletion(lines, -1, 0, '>').ShouldBeNull();
        ViuAutoClosingLogic.GetTypedCharacterCompletion(lines, 3, 0, '>').ShouldBeNull();
        ViuAutoClosingLogic.GetTypedCharacterCompletion(lines, 1, 99, '>').ShouldBeNull();
        ViuAutoClosingLogic.AllowsQuotePair(lines, 9, 0, '"').ShouldBeFalse();
    }

    [Fact]
    public void GetTypedCharacterCompletion_ANonTriggerCharacter_InsertsNothing()
    {
        Completion('<', "<template>", "    <div|", "</template>").ShouldBeNull();
    }

    // ---- Helpers ----------------------------------------------------------------------------

    // Applies the completion the given keystroke produces and re-renders the caret line with the
    // caret marked, so one expected string pins the inserted text and the caret offset together.
    private static string Complete(char typedCharacter, params string[] markedLines)
    {
        (IReadOnlyList<string> lines, int lineNumber, int characterIndex) = ReadCaret(markedLines);
        ViuAutoClosingEdit? completion = ViuAutoClosingLogic.GetTypedCharacterCompletion(
            lines,
            lineNumber,
            characterIndex,
            typedCharacter);

        completion.ShouldNotBeNull();
        ViuAutoClosingEdit autoClosingEdit = completion.Value;
        autoClosingEdit.CaretOffset.ShouldBeInRange(0, autoClosingEdit.InsertedText.Length);
        autoClosingEdit.InsertedText[0].ShouldBe(typedCharacter);

        return lines[lineNumber]
            .Insert(characterIndex, autoClosingEdit.InsertedText)
            .Insert(characterIndex + autoClosingEdit.CaretOffset, "|");
    }

    private static ViuAutoClosingEdit? Completion(char typedCharacter, params string[] markedLines)
    {
        (IReadOnlyList<string> lines, int lineNumber, int characterIndex) = ReadCaret(markedLines);
        return ViuAutoClosingLogic.GetTypedCharacterCompletion(
            lines,
            lineNumber,
            characterIndex,
            typedCharacter);
    }

    private static bool AllowsBracket(char openingCharacter, params string[] markedLines)
    {
        (IReadOnlyList<string> lines, int lineNumber, int characterIndex) = ReadCaret(markedLines);
        return ViuAutoClosingLogic.AllowsBracketPair(
            lines,
            lineNumber,
            characterIndex,
            openingCharacter);
    }

    private static bool AllowsWalkover(params string[] markedLines)
    {
        (IReadOnlyList<string> lines, int lineNumber, int characterIndex) = ReadCaret(markedLines);
        return ViuAutoClosingLogic.AllowsClosingBraceWalkover(lines, lineNumber, characterIndex);
    }

    // The caret marks the line the opening brace sits on, which is the line the expansion decision
    // is made from - by the time the editor asks, the caret itself has already moved to a new line.
    private static bool AllowsBlockExpansion(params string[] markedLines)
    {
        (IReadOnlyList<string> lines, int lineNumber, _) = ReadCaret(markedLines);
        return ViuAutoClosingLogic.AllowsBlockExpansionOnReturn(lines, lineNumber);
    }

    private static bool AllowsQuote(char quoteCharacter, params string[] markedLines)
    {
        (IReadOnlyList<string> lines, int lineNumber, int characterIndex) = ReadCaret(markedLines);
        return ViuAutoClosingLogic.AllowsQuotePair(
            lines,
            lineNumber,
            characterIndex,
            quoteCharacter);
    }

    // Strips the '|' caret marker out of the document and reports where it was.
    private static (IReadOnlyList<string> Lines, int LineNumber, int CharacterIndex) ReadCaret(
        string[] markedLines)
    {
        List<string> lines = new(markedLines.Length);
        int caretLineNumber = -1;
        int caretCharacterIndex = -1;

        for (int lineNumber = 0; lineNumber < markedLines.Length; lineNumber++)
        {
            int markerIndex = markedLines[lineNumber].IndexOf('|');
            if (markerIndex < 0)
            {
                lines.Add(markedLines[lineNumber]);
                continue;
            }

            caretLineNumber = lineNumber;
            caretCharacterIndex = markerIndex;
            lines.Add(markedLines[lineNumber].Remove(markerIndex, 1));
        }

        caretLineNumber.ShouldBeGreaterThanOrEqualTo(0, "the test document needs one '|' caret");
        return (lines, caretLineNumber, caretCharacterIndex);
    }
}
