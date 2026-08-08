using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu;

namespace Assimalign.Viu.Core.Tests;

// Pins the tier-one property normalization ABI specified by [SFC-CG-2] and [V01.01.15.02].
public sealed class PropertyNormalizationTests
{
    [Fact]
    public void Merge_ClassStyleAndOrdinaryDuplicates_UseTheirDefinedPolicies()
    {
        Dictionary<string, object?> first = new()
        {
            ["class"] = "base",
            ["style"] = new Dictionary<string, object?>
            {
                ["color"] = "red",
                ["width"] = "1px",
            },
            ["title"] = "first",
        };
        Dictionary<string, object?> second = new()
        {
            ["class"] = new Dictionary<string, object?> { ["active"] = true },
            ["style"] = "color:blue;height:2px",
            ["title"] = "second",
        };

        IReadOnlyDictionary<string, object?> merged = PropertyNormalization.Merge(first, second);

        merged["class"].ShouldBe("base active");
        IReadOnlyDictionary<string, object?> style =
            merged["style"].ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        style["color"].ShouldBe("blue");
        style["width"].ShouldBe("1px");
        style["height"].ShouldBe("2px");
        merged["title"].ShouldBe("second");
    }

    [Fact]
    public void Merge_CompatibleEventDelegates_CombinesInSourceOrder()
    {
        List<string> calls = [];
        Action firstHandler = () => calls.Add("first");
        Action secondHandler = () => calls.Add("second");

        IReadOnlyDictionary<string, object?> merged = PropertyNormalization.Merge(
            new Dictionary<string, object?> { ["onClick"] = firstHandler },
            new Dictionary<string, object?> { ["onClick"] = secondHandler });
        Action handler = merged["onClick"].ShouldBeOfType<Action>();

        handler();

        calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Normalize_SupportedSource_ReturnsIndependentNormalizedSnapshot()
    {
        Dictionary<string, object?> source = new()
        {
            ["class"] = new object?[] { "base", "active" },
            ["style"] = new object?[] { "color:red", "color:blue" },
        };

        IReadOnlyDictionary<string, object?> normalized = PropertyNormalization.Normalize(source);
        source["class"] = "changed";

        normalized["class"].ShouldBe("base active");
        IReadOnlyDictionary<string, object?> style =
            normalized["style"].ShouldBeAssignableTo<IReadOnlyDictionary<string, object?>>()!;
        style["color"].ShouldBe("blue");
    }

    [Fact]
    public void Normalize_NullOrUnreadableSource_ReturnsEmptySnapshot()
    {
        PropertyNormalization.Normalize(null).ShouldBeEmpty();
        PropertyNormalization.Normalize(new object()).ShouldBeEmpty();
    }

    [Fact]
    public void Merge_NullSourceArray_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => PropertyNormalization.Merge(null!));
    }
}
