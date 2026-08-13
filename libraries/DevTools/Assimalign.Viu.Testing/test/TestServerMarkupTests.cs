using System;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Tests;

public sealed class TestServerMarkupTests
{
    [Fact]
    public void Parse_HydrationMarkers_UsesCoreWireVocabulary()
    {
        string markup = string.Concat(
            HydrationMarkers.FragmentStart,
            HydrationMarkers.EmptyComment,
            HydrationMarkers.FragmentEnd,
            HydrationMarkers.TeleportStart,
            HydrationMarkers.TeleportEnd,
            HydrationMarkers.TeleportAnchor);

        TestElement container = TestServerMarkup.Parse(markup);

        container.Children.Count.ShouldBe(6);
        ((TestComment)container.Children[0]).Text.ShouldBe("[");
        ((TestComment)container.Children[1]).Text.ShouldBe(string.Empty);
        ((TestComment)container.Children[2]).Text.ShouldBe("]");
        ((TestComment)container.Children[3]).Text.ShouldBe("teleport start");
        ((TestComment)container.Children[4]).Text.ShouldBe("teleport end");
        ((TestComment)container.Children[5]).Text.ShouldBe("teleport anchor");
    }

    [Fact]
    public void Parse_EncodedTextAndQuotedAttribute_DecodesBrowserVisibleValues()
    {
        TestElement container = TestServerMarkup.Parse(
            "<p title=\"1 > 0 &amp; 2\">&lt;safe&gt;&amp;</p>");

        TestElement paragraph = (TestElement)container.Children.ShouldHaveSingleItem();
        paragraph.Properties["title"].ShouldBe("1 > 0 & 2");
        ((TestText)paragraph.Children.ShouldHaveSingleItem()).Text.ShouldBe("<safe>&");
    }

    [Fact]
    public void Parse_VoidAndBooleanAttributes_PreservesFollowingSibling()
    {
        TestElement container = TestServerMarkup.Parse(
            "<input disabled><span>after</span>");

        container.Children.Count.ShouldBe(2);
        TestElement input = (TestElement)container.Children[0];
        input.Properties["disabled"].ShouldBe(string.Empty);
        ((TestElement)container.Children[1]).Tag.ShouldBe("span");
    }

    [Fact]
    public void Parse_MismatchedCloseTag_ThrowsPreciseFormatFailure()
    {
        FormatException exception = Should.Throw<FormatException>(
            () => TestServerMarkup.Parse("<div></span>"));

        exception.Message.ShouldContain("expected </div>");
    }
}
