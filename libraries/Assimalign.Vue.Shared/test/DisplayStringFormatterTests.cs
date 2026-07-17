using System.Collections.Generic;
using System.Globalization;
using System.Threading;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.Shared.Tests;

// Pins the text-interpolation contract of @vue/shared's toDisplayString.ts —
// https://vuejs.org/guide/essentials/template-syntax.html. Invariant culture is mandatory so
// SSR output and client hydration agree byte-for-byte.
public class DisplayStringFormatterTests
{
    [Fact]
    public void ToDisplayString_NullRendersEmpty_AndStringsPassThrough()
    {
        DisplayStringFormatter.ToDisplayString(null).ShouldBe(string.Empty);
        DisplayStringFormatter.ToDisplayString("hello").ShouldBe("hello");
    }

    [Fact]
    public void ToDisplayString_ScalarsUseInvariantCulture_UnderAnyCurrentCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            // de-DE writes 1,5 — invariant output must stay 1.5 regardless.
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            DisplayStringFormatter.ToDisplayString(1.5).ShouldBe("1.5");
            DisplayStringFormatter.ToDisplayString(1234567.25m).ShouldBe("1234567.25");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ToDisplayString_BooleansRenderJavaScriptStyle()
    {
        DisplayStringFormatter.ToDisplayString(true).ShouldBe("true");
        DisplayStringFormatter.ToDisplayString(false).ShouldBe("false");
    }

    [Fact]
    public void ToDisplayString_ArraysRenderAsTwoSpaceIndentedJson()
    {
        DisplayStringFormatter.ToDisplayString(new object?[] { 1, "two", null })
            .ShouldBe("[\n  1,\n  \"two\",\n  null\n]");
    }

    [Fact]
    public void ToDisplayString_StringKeyedDictionariesRenderAsJsonObjects()
    {
        var value = new Dictionary<string, object?> { ["a"] = 1, ["b"] = "text" };

        DisplayStringFormatter.ToDisplayString(value)
            .ShouldBe("{\n  \"a\": 1,\n  \"b\": \"text\"\n}");
    }

    [Fact]
    public void ToDisplayString_NestedShapesIndentPerLevel()
    {
        var value = new Dictionary<string, object?>
        {
            ["items"] = new object?[] { 1 },
        };

        DisplayStringFormatter.ToDisplayString(value)
            .ShouldBe("{\n  \"items\": [\n    1\n  ]\n}");
    }

    [Fact]
    public void ToDisplayString_NonStringKeyedDictionaries_UseTheMapConvention()
    {
        // Upstream replacer: Map -> { "Map(n)": { "key =>": value } }.
        var value = new Dictionary<int, object?> { [1] = "one" };

        DisplayStringFormatter.ToDisplayString(value)
            .ShouldBe("{\n  \"Map(1)\": {\n    \"1 =>\": \"one\"\n  }\n}");
    }

    [Fact]
    public void ToDisplayString_Sets_UseTheSetConvention()
    {
        // Upstream replacer: Set -> { "Set(n)": [ ... ] }.
        var value = new HashSet<int> { 7 };

        DisplayStringFormatter.ToDisplayString(value)
            .ShouldBe("{\n  \"Set(1)\": [\n    7\n  ]\n}");
    }

    [Fact]
    public void ToDisplayString_EscapesJsonStrings()
    {
        DisplayStringFormatter.ToDisplayString(new object?[] { "quote\" and \\ and\nnewline" })
            .ShouldBe("[\n  \"quote\\\" and \\\\ and\\nnewline\"\n]");
    }

    [Fact]
    public void FormatScalar_InvariantForFormattables()
    {
        DisplayStringFormatter.FormatScalar(42).ShouldBe("42");
        DisplayStringFormatter.FormatScalar(true).ShouldBe("true");
        DisplayStringFormatter.FormatScalar("x").ShouldBe("x");
    }
}
