using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.Shared.Tests;

// Pins the class/style binding normalization contract of @vue/shared's normalizeProp.ts
// (test vectors ported from vuejs/core's normalizeProp.spec.ts) —
// https://vuejs.org/guide/essentials/class-and-style.html.
public class StyleAndClassNormalizationTests
{
    [Fact]
    public void NormalizeClass_StringsPassThroughTrimmed()
    {
        StyleAndClassNormalization.NormalizeClass("foo").ShouldBe("foo");
        StyleAndClassNormalization.NormalizeClass("foo bar ").ShouldBe("foo bar");
        StyleAndClassNormalization.NormalizeClass(null).ShouldBe(string.Empty);
    }

    [Fact]
    public void NormalizeClass_EnumerablesJoinAndRecurse()
    {
        StyleAndClassNormalization.NormalizeClass(new object?[] { "foo", "bar" }).ShouldBe("foo bar");
        StyleAndClassNormalization.NormalizeClass(new object?[] { "foo", null, "bar" }).ShouldBe("foo bar");
        StyleAndClassNormalization.NormalizeClass(
            new object?[] { "a", new object?[] { "b", new object?[] { "c" } } }).ShouldBe("a b c");
    }

    [Fact]
    public void NormalizeClass_DictionariesContributeTruthyKeysInOrder()
    {
        var binding = new Dictionary<string, object?>
        {
            ["active"] = true,
            ["disabled"] = false,
            ["highlighted"] = 1,
            ["hidden"] = 0,
            ["empty"] = "",
            ["named"] = "anything",
        };

        StyleAndClassNormalization.NormalizeClass(binding).ShouldBe("active highlighted named");
    }

    [Fact]
    public void NormalizeClass_MixedArrayAndDictionaryForms()
    {
        StyleAndClassNormalization.NormalizeClass(
            new object?[]
            {
                "base",
                new Dictionary<string, object?> { ["active"] = true, ["off"] = null },
            }).ShouldBe("base active");
    }

    [Fact]
    public void NormalizeStyle_ArraysMergeWithLaterEntriesWinning()
    {
        var merged = StyleAndClassNormalization.NormalizeStyle(
            new object?[]
            {
                new Dictionary<string, object?> { ["color"] = "red", ["width"] = "10px" },
                "color: blue; height: 2px",
            }).ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>();

        merged!["color"].ShouldBe("blue");
        merged["width"].ShouldBe("10px");
        merged["height"].ShouldBe("2px");
    }

    [Fact]
    public void NormalizeStyle_StringsAndDictionariesPassThrough()
    {
        StyleAndClassNormalization.NormalizeStyle("color:red").ShouldBe("color:red");
        var map = new Dictionary<string, object?> { ["color"] = "red" };
        StyleAndClassNormalization.NormalizeStyle(map).ShouldBeSameAs(map);
        StyleAndClassNormalization.NormalizeStyle(null).ShouldBeNull();
    }

    [Fact]
    public void ParseStringStyle_SplitsDeclarationsOnFirstColon()
    {
        var parsed = StyleAndClassNormalization.ParseStringStyle("color: red; margin: 0 10px");

        parsed["color"].ShouldBe("red");
        parsed["margin"].ShouldBe("0 10px");
    }

    [Fact]
    public void ParseStringStyle_KeepsSemicolonsInsideParentheses()
    {
        // Upstream listDelimiterRE: /;(?![^(]*\))/ — the url(data:...;base64,...) survives.
        var parsed = StyleAndClassNormalization.ParseStringStyle(
            "background-image: url(data:image/png;base64,abc123); color: red");

        parsed["background-image"].ShouldBe("url(data:image/png;base64,abc123)");
        parsed["color"].ShouldBe("red");
    }

    [Fact]
    public void ParseStringStyle_StripsCssComments()
    {
        var parsed = StyleAndClassNormalization.ParseStringStyle(
            "/* comment; with : symbols */ color: red; /* another */ width: 1px");

        parsed.Count.ShouldBe(2);
        parsed["color"].ShouldBe("red");
        parsed["width"].ShouldBe("1px");
    }

    [Fact]
    public void StringifyStyle_HyphenatesCamelCaseAndPreservesCustomProperties()
    {
        var text = StyleAndClassNormalization.StringifyStyle(new Dictionary<string, object?>
        {
            ["backgroundColor"] = "red",
            ["--brand-color"] = "#123",
            ["width"] = "1px",
        });

        text.ShouldBe("background-color:red;--brand-color:#123;width:1px;");
    }

    [Fact]
    public void StringifyStyle_StringsAndNullPassThrough()
    {
        StyleAndClassNormalization.StringifyStyle("color:red;").ShouldBe("color:red;");
        StyleAndClassNormalization.StringifyStyle(null).ShouldBe(string.Empty);
    }

    [Fact]
    public void RoundTrip_ParseThenStringify_IsStable()
    {
        // Runtime and SSR consume the same helpers; deterministic ordering means a parse →
        // stringify → parse cycle is lossless.
        const string css = "color:red;background-image:url(data:image/png;base64,x);width:1px;";
        var parsed = StyleAndClassNormalization.ParseStringStyle(css);
        var stringified = StyleAndClassNormalization.StringifyStyle(parsed);
        var reparsed = StyleAndClassNormalization.ParseStringStyle(stringified);

        reparsed.ShouldBe(parsed);
    }

    [Fact]
    public void Hyphenate_ConvertsCamelCase()
    {
        StyleAndClassNormalization.Hyphenate("backgroundColor").ShouldBe("background-color");
        StyleAndClassNormalization.Hyphenate("color").ShouldBe("color");
        // Upstream \B([A-Z]): no hyphen before a leading capital.
        StyleAndClassNormalization.Hyphenate("WebkitTransition").ShouldBe("webkit-transition");
        StyleAndClassNormalization.Hyphenate("ArrowUp").ShouldBe("arrow-up");
    }

    [Fact]
    public void IsTruthy_FollowsJavaScriptSemantics()
    {
        StyleAndClassNormalization.IsTruthy(null).ShouldBeFalse();
        StyleAndClassNormalization.IsTruthy(false).ShouldBeFalse();
        StyleAndClassNormalization.IsTruthy(0).ShouldBeFalse();
        StyleAndClassNormalization.IsTruthy(0.0).ShouldBeFalse();
        StyleAndClassNormalization.IsTruthy(double.NaN).ShouldBeFalse();
        StyleAndClassNormalization.IsTruthy(string.Empty).ShouldBeFalse();
        StyleAndClassNormalization.IsTruthy(true).ShouldBeTrue();
        StyleAndClassNormalization.IsTruthy(1).ShouldBeTrue();
        StyleAndClassNormalization.IsTruthy("false").ShouldBeTrue(); // non-empty string is truthy
        StyleAndClassNormalization.IsTruthy(new object()).ShouldBeTrue();
    }
}
