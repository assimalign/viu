using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins vue-router's tokensToParser parse/stringify (packages/router/src/matcher/pathParserRanker.ts).
public class PathParserTests
{
    private static PathParser Compile(string path, PathMatchingOptions? options = null)
        => PathParserFactory.Compile(PathTokenizer.Tokenize(path), options ?? PathMatchingOptions.Default);

    private static RouteParameters Parse(string path, string candidate)
    {
        Compile(path).TryParse(candidate, out var parameters).ShouldBeTrue();
        return parameters;
    }

    [Fact]
    public void TryParse_StaticPath_MatchesExactly()
    {
        var parser = Compile("/users/list");

        parser.TryParse("/users/list", out _).ShouldBeTrue();
        parser.TryParse("/users/other", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_NonStrict_ToleratesTrailingSlash()
    {
        var parser = Compile("/users");

        parser.TryParse("/users", out _).ShouldBeTrue();
        parser.TryParse("/users/", out _).ShouldBeTrue();
    }

    [Fact]
    public void TryParse_Strict_RejectsTrailingSlash()
    {
        var parser = Compile("/users", new PathMatchingOptions { Strict = true });

        parser.TryParse("/users", out _).ShouldBeTrue();
        parser.TryParse("/users/", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_CaseInsensitiveByDefault_CaseSensitiveWhenRequested()
    {
        Compile("/Users").TryParse("/users", out _).ShouldBeTrue();
        Compile("/Users", new PathMatchingOptions { Sensitive = true }).TryParse("/users", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_DynamicParameter_CapturesValue()
    {
        var parameters = Parse("/users/:id", "/users/42");

        parameters.GetString("id").ShouldBe("42");
    }

    [Fact]
    public void TryParse_MissingRequiredParameter_DoesNotMatch()
    {
        Compile("/users/:id").TryParse("/users", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_OptionalParameter_MatchesWithAndWithout()
    {
        var parser = Compile("/users/:id?");

        parser.TryParse("/users", out var without).ShouldBeTrue();
        without.GetString("id").ShouldBe(string.Empty);

        parser.TryParse("/users/42", out var with).ShouldBeTrue();
        with.GetString("id").ShouldBe("42");
    }

    [Fact]
    public void TryParse_CustomPattern_ConstrainsTheValue()
    {
        var parser = Compile(@"/:id(\d+)");

        parser.TryParse("/42", out var parameters).ShouldBeTrue();
        parameters.GetString("id").ShouldBe("42");
        parser.TryParse("/abc", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_RepeatableParameter_SplitsOnSlash()
    {
        var parser = Compile("/:chapters+");

        parser.TryParse("/a/b/c", out var many).ShouldBeTrue();
        many.GetStrings("chapters").ShouldBe(new[] { "a", "b", "c" });

        parser.TryParse("/a", out var one).ShouldBeTrue();
        one.GetStrings("chapters").ShouldBe(new[] { "a" });

        // '+' requires at least one occurrence.
        parser.TryParse("/", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_OptionalRepeatableParameter_MatchesEmpty()
    {
        var parser = Compile("/:chapters*");

        parser.TryParse("/", out var empty).ShouldBeTrue();
        empty.GetStrings("chapters").ShouldBeEmpty();

        parser.TryParse("/a/b", out var many).ShouldBeTrue();
        many.GetStrings("chapters").ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void TryParse_CatchAllWildcard_MatchesAnyPath()
    {
        var parser = Compile("/:pathMatch(.*)*");

        parser.TryParse("/anything/at/all", out var parameters).ShouldBeTrue();
        parameters.GetStrings("pathMatch").ShouldBe(new[] { "anything", "at", "all" });
    }

    [Fact]
    public void Stringify_InterpolatesRequiredParameter()
    {
        Compile("/users/:id").Stringify(RouteParameters.Empty.With("id", "42")).ShouldBe("/users/42");
    }

    [Fact]
    public void Stringify_OmitsOptionalParameterWhenAbsent()
    {
        Compile("/users/:id?").Stringify(RouteParameters.Empty).ShouldBe("/users");
    }

    [Fact]
    public void Stringify_JoinsRepeatableValues()
    {
        Compile("/:chapters+")
            .Stringify(RouteParameters.Empty.WithMany("chapters", "a", "b", "c"))
            .ShouldBe("/a/b/c");
    }

    [Fact]
    public void Stringify_MissingRequiredParameter_Throws()
    {
        var exception = Should.Throw<RouteMatcherException>(
            () => Compile("/users/:id").Stringify(RouteParameters.Empty));

        exception.Error.ShouldBe(RouteMatcherError.MissingRequiredParameter);
    }

    [Fact]
    public void Stringify_ArrayForNonRepeatableParameter_Throws()
    {
        var exception = Should.Throw<RouteMatcherException>(
            () => Compile("/users/:id").Stringify(RouteParameters.Empty.WithMany("id", "a", "b")));

        exception.Error.ShouldBe(RouteMatcherError.ParameterNotRepeatable);
    }

    [Fact]
    public void Compile_InvalidCustomPattern_Throws()
    {
        var exception = Should.Throw<RouteMatcherException>(() => Compile("/:id([)"));

        exception.Error.ShouldBe(RouteMatcherError.InvalidCustomPattern);
    }
}
