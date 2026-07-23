using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins vue-router's tokenizePath (packages/router/src/matcher/pathTokenizer.ts). Route matching
// syntax reference: https://router.vuejs.org/guide/essentials/route-matching-syntax.html
public class PathTokenizerTests
{
    [Fact]
    public void Tokenize_EmptyPath_ProducesOneEmptySegment()
    {
        // Upstream: `if (!path) return [[]]`.
        var segments = PathTokenizer.Tokenize(string.Empty);

        segments.Count.ShouldBe(1);
        segments[0].ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_Root_ProducesSingleEmptyStaticToken()
    {
        // Upstream ROOT_TOKEN: "/" is one segment holding a Static token with an empty value.
        var segments = PathTokenizer.Tokenize("/");

        segments.Count.ShouldBe(1);
        segments[0].Count.ShouldBe(1);
        segments[0][0].Kind.ShouldBe(PathTokenKind.Static);
        segments[0][0].Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void Tokenize_StaticSegments_SplitOnSlash()
    {
        var segments = PathTokenizer.Tokenize("/users/list");

        segments.Count.ShouldBe(2);
        segments[0][0].Value.ShouldBe("users");
        segments[1][0].Value.ShouldBe("list");
    }

    [Fact]
    public void Tokenize_DynamicParameter_ProducesParameterToken()
    {
        var segments = PathTokenizer.Tokenize("/users/:id");

        var parameter = segments[1][0];
        parameter.Kind.ShouldBe(PathTokenKind.Parameter);
        parameter.Value.ShouldBe("id");
        parameter.Optional.ShouldBeFalse();
        parameter.Repeatable.ShouldBeFalse();
        parameter.CustomPattern.ShouldBe(string.Empty);
    }

    [Theory]
    // '?' => optional only, '+' => repeatable only, '*' => optional and repeatable.
    [InlineData("/:id?", true, false)]
    [InlineData("/:id+", false, true)]
    [InlineData("/:id*", true, true)]
    public void Tokenize_Modifiers_SetOptionalAndRepeatable(string path, bool optional, bool repeatable)
    {
        var parameter = PathTokenizer.Tokenize(path)[0][0];

        parameter.Kind.ShouldBe(PathTokenKind.Parameter);
        parameter.Optional.ShouldBe(optional);
        parameter.Repeatable.ShouldBe(repeatable);
    }

    [Fact]
    public void Tokenize_CustomPattern_CapturesInnerRegularExpression()
    {
        var parameter = PathTokenizer.Tokenize(@"/:id(\d+)")[0][0];

        parameter.Kind.ShouldBe(PathTokenKind.Parameter);
        parameter.Value.ShouldBe("id");
        parameter.CustomPattern.ShouldBe(@"\d+");
    }

    [Fact]
    public void Tokenize_SubSegment_MixesStaticAndParameterInOneSegment()
    {
        // /user-:id -> a single segment with a Static token followed by a Parameter token.
        var segment = PathTokenizer.Tokenize("/user-:id")[0];

        segment.Count.ShouldBe(2);
        segment[0].Kind.ShouldBe(PathTokenKind.Static);
        segment[0].Value.ShouldBe("user-");
        segment[1].Kind.ShouldBe(PathTokenKind.Parameter);
        segment[1].Value.ShouldBe("id");
    }

    [Fact]
    public void Tokenize_StaticPrefixBeforeRepeatable_IsAllowed()
    {
        // Upstream guard is `segment.length > 1`, so one preceding static token is fine.
        var segment = PathTokenizer.Tokenize("/user-:id+")[0];

        segment.Count.ShouldBe(2);
        segment[1].Repeatable.ShouldBeTrue();
    }

    [Fact]
    public void Tokenize_PathWithoutLeadingSlash_Throws()
    {
        var exception = Should.Throw<RouteMatcherException>(() => PathTokenizer.Tokenize("users"));

        exception.Error.ShouldBe(RouteMatcherError.InvalidRoutePath);
    }

    [Fact]
    public void Tokenize_RepeatableNotAloneInSegment_Throws()
    {
        // Two preceding tokens then a repeatable trips the "> 1" guard.
        var exception = Should.Throw<RouteMatcherException>(() => PathTokenizer.Tokenize("/:a:b:c+"));

        exception.Error.ShouldBe(RouteMatcherError.RepeatableParameterNotAlone);
    }

    [Fact]
    public void Tokenize_UnfinishedCustomPattern_Throws()
    {
        var exception = Should.Throw<RouteMatcherException>(() => PathTokenizer.Tokenize(@"/:id(\d+"));

        exception.Error.ShouldBe(RouteMatcherError.UnfinishedCustomPattern);
    }

    [Fact]
    public void Tokenize_TrailingSlash_ProducesTrailingEmptySegment()
    {
        // /users/ -> [[users], []]; the empty trailing segment is what the ranker scores as Root.
        var segments = PathTokenizer.Tokenize("/users/");

        segments.Count.ShouldBe(2);
        segments[0][0].Value.ShouldBe("users");
        segments[1].ShouldBeEmpty();
    }
}
