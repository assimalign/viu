using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

// Pins the tier-one display-string normalization ABI specified by [SFC-CG-2] and [V01.01.15.02].
public sealed class DisplayStringFormatterTests
{
    [Fact]
    public void ToDisplayString_NullStringsAndBooleans_UseTemplateTextSpellings()
    {
        DisplayStringFormatter.ToDisplayString(null).ShouldBe(string.Empty);
        DisplayStringFormatter.ToDisplayString("hello").ShouldBe("hello");
        DisplayStringFormatter.ToDisplayString(true).ShouldBe("true");
        DisplayStringFormatter.ToDisplayString(false).ShouldBe("false");
    }

    [Fact]
    public void ToDisplayString_NumberUnderNonInvariantCulture_RemainsInvariant()
    {
        CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            DisplayStringFormatter.ToDisplayString(1234567.25m).ShouldBe("1234567.25");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void ToDisplayString_NestedCollection_UsesIndentedJsonShape()
    {
        Dictionary<string, object?> value = new()
        {
            ["items"] = new object?[] { 1, "two", null },
        };

        DisplayStringFormatter.ToDisplayString(value).ShouldBe(
            "{\n  \"items\": [\n    1,\n    \"two\",\n    null\n  ]\n}");
    }

    [Fact]
    public void ToDisplayString_NonStringDictionaryAndSet_UseNamedConventions()
    {
        Dictionary<int, object?> map = new() { [1] = "one" };
        HashSet<int> set = new() { 7 };

        DisplayStringFormatter.ToDisplayString(map).ShouldBe(
            "{\n  \"Map(1)\": {\n    \"1 =>\": \"one\"\n  }\n}");
        DisplayStringFormatter.ToDisplayString(set).ShouldBe(
            "{\n  \"Set(1)\": [\n    7\n  ]\n}");
    }

    [Fact]
    public void ToDisplayString_ObjectErasedSet_UsesEnumerableArrayConvention()
    {
        object value = new HashSet<int> { 7 };

        DisplayStringFormatter.ToDisplayString(value).ShouldBe("[\n  7\n]");
    }

    [Fact]
    public void ToDisplayString_JsonStringCharacters_AreEscaped()
    {
        DisplayStringFormatter.ToDisplayString(
                new object?[] { "quote\" and \\ and\nnewline" })
            .ShouldBe("[\n  \"quote\\\" and \\\\ and\\nnewline\"\n]");
    }
}
