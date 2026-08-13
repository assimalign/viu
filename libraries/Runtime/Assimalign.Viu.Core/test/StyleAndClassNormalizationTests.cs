using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

// Pins the tier-one class and style normalization ABI specified by [SFC-CG-2] and [V01.01.15.02].
public sealed class StyleAndClassNormalizationTests
{
    [Fact]
    public void NormalizeClass_NestedShapes_JoinTruthyTokensInOrder()
    {
        object?[] value =
        [
            "base ",
            new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["active"] = true,
                    ["disabled"] = false,
                    ["counted"] = 1,
                    ["hidden"] = 0,
                },
                "nested",
            },
        ];

        StyleAndClassNormalization.NormalizeClass(value)
            .ShouldBe("base active counted nested");
    }

    [Fact]
    public void NormalizeStyle_EnumerableEntries_MergeWithLaterValuesWinning()
    {
        object? normalized = StyleAndClassNormalization.NormalizeStyle(
            new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["color"] = "red",
                    ["width"] = "10px",
                },
                "color: blue; height: 2px",
            });

        IReadOnlyDictionary<string, object?> merged =
            normalized.ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        merged["color"].ShouldBe("blue");
        merged["width"].ShouldBe("10px");
        merged["height"].ShouldBe("2px");
    }

    [Fact]
    public void NormalizeStyle_StringDictionaryAndNull_PassThrough()
    {
        Dictionary<string, object?> map = new() { ["color"] = "red" };

        StyleAndClassNormalization.NormalizeStyle("color:red").ShouldBe("color:red");
        StyleAndClassNormalization.NormalizeStyle(map).ShouldBeSameAs(map);
        StyleAndClassNormalization.NormalizeStyle(null).ShouldBeNull();
    }

    [Fact]
    public void ParseStringStyle_CommentsAndParenthesizedSemicolons_PreserveDeclarations()
    {
        Dictionary<string, object?> parsed = StyleAndClassNormalization.ParseStringStyle(
            "/* ignore; this */ background-image:url(data:image/png;base64,x); color:red");

        parsed.Count.ShouldBe(2);
        parsed["background-image"].ShouldBe("url(data:image/png;base64,x)");
        parsed["color"].ShouldBe("red");
    }

    [Fact]
    public void StringifyStyle_CamelCaseAndCustomProperties_UseCssNames()
    {
        Dictionary<string, object?> style = new()
        {
            ["backgroundColor"] = "red",
            ["--brand-color"] = "#123",
            ["ignored"] = null,
        };

        StyleAndClassNormalization.StringifyStyle(style)
            .ShouldBe("background-color:red;--brand-color:#123;");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(0, false)]
    [InlineData("", false)]
    [InlineData(true, true)]
    [InlineData(1, true)]
    [InlineData("false", true)]
    public void IsTruthy_HostCoercionValues_ReturnExpectedResult(object? value, bool expected)
    {
        StyleAndClassNormalization.IsTruthy(value).ShouldBe(expected);
    }
}
