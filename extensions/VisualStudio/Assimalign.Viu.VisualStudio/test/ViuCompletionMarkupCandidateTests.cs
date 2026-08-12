using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Pins which completion items are drawn as markup elements. The Language Server Protocol has no
/// completion kind for an element, so an element arrives under the nearest kind it does carry; this
/// rule is what lets the presentation be corrected to the angle-bracket glyph and its own filter
/// button instead of the property ones.
/// </summary>
public class ViuCompletionMarkupCandidateTests
{
    [Theory]
    [InlineData("<div")]
    [InlineData("<template")]
    [InlineData("<a")]
    public void IsElement_OpenTagOfANativeOrFrameworkElement_IsAnElement(string insertText) =>
        ViuCompletionMarkupCandidate.IsElement(insertText).ShouldBeTrue();

    [Theory]
    // A component is a type and keeps the type glyph: casing is what separates the two, exactly as
    // name resolution separates them ([CMP-6]).
    [InlineData("<Transition")]
    [InlineData("<StatusPill")]
    // Everything else in a template list: attributes, bindings, handlers, directives, expressions.
    [InlineData("title=\"$1\"")]
    [InlineData(":class=\"$1\"")]
    [InlineData("@click=\"$1\"")]
    [InlineData("v-if=\"$1\"")]
    [InlineData("CurrentPath")]
    // Degenerate inputs a protocol payload can carry.
    [InlineData("<")]
    [InlineData("")]
    [InlineData(null)]
    public void IsElement_EverythingElse_IsNotAnElement(string? insertText) =>
        ViuCompletionMarkupCandidate.IsElement(insertText).ShouldBeFalse();
}
