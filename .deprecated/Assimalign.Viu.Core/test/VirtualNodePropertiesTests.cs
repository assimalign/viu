using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

// Pins the prop-bag contract: array-backed with ordinal linear scans, pre-sizable, dictionary
// fallback past the linear-scan limit, and cheaper than a Dictionary for typical prop counts —
// vnodes are created per render pass, so the bag is a hot allocation site ([V01.01.03.01]).
public class VirtualNodePropertiesTests
{
    [Fact]
    public void SetAndTryGetValue_RoundTripAndReplace()
    {
        var properties = new VirtualNodeProperties(2);

        properties.Set("id", "a");
        properties.Set("class", "c");
        properties.Set("id", "b");

        properties.Count.ShouldBe(2);
        properties["id"].ShouldBe("b");
        properties.TryGetValue("class", out var value).ShouldBeTrue();
        value.ShouldBe("c");
        properties.TryGetValue("missing", out _).ShouldBeFalse();
        properties.ContainsName("class").ShouldBeTrue();
        properties["missing"].ShouldBeNull();
    }

    [Fact]
    public void Enumeration_YieldsInsertionOrderWhileArrayBacked()
    {
        var properties = VirtualNodeFactory.Properties(("a", 1), ("b", 2), ("c", 3));

        var names = new List<string>();
        foreach (var (name, _) in properties)
        {
            names.Add(name);
        }

        names.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void GrowingPastCapacity_KeepsEntries()
    {
        var properties = new VirtualNodeProperties(1);
        for (var index = 0; index < 8; index++)
        {
            properties.Set($"name{index}", index);
        }

        properties.Count.ShouldBe(8);
        properties.IsDictionaryBacked.ShouldBeFalse();
        properties["name7"].ShouldBe(7);
    }

    [Fact]
    public void ExceedingLinearScanLimit_MigratesToDictionaryAndKeepsBehavior()
    {
        var properties = new VirtualNodeProperties();
        for (var index = 0; index <= VirtualNodeProperties.LinearScanLimit; index++)
        {
            properties.Set($"name{index}", index);
        }

        properties.IsDictionaryBacked.ShouldBeTrue();
        properties.Count.ShouldBe(VirtualNodeProperties.LinearScanLimit + 1);
        properties["name0"].ShouldBe(0);
        properties[$"name{VirtualNodeProperties.LinearScanLimit}"].ShouldBe(VirtualNodeProperties.LinearScanLimit);

        var seen = 0;
        foreach (var _ in properties)
        {
            seen++;
        }
        seen.ShouldBe(VirtualNodeProperties.LinearScanLimit + 1);
    }

    [Fact]
    public void OverLimitCapacity_StartsDictionaryBacked()
    {
        var properties = new VirtualNodeProperties(VirtualNodeProperties.LinearScanLimit + 8);
        properties.IsDictionaryBacked.ShouldBeTrue();
        properties.Set("a", 1);
        properties["a"].ShouldBe(1);
    }

    [Fact]
    public void PreSizedBag_AllocatesLessThanDictionaryBaseline()
    {
        // The acceptance micro-benchmark for [V01.01.03.01]: a pre-sized bag beats the
        // per-vnode Dictionary<string, object?> baseline on allocated bytes. Measured with the
        // per-thread allocation counter; the full BenchmarkDotNet suite lands with
        // [V01.01.11.04].
        const int iterations = 10_000;
        (string, object?)[] entries = [("class", "a"), ("id", "b"), ("title", "c"), ("onClick", null)];

        // Warm up both paths so first-call costs are excluded.
        CreateBag(entries);
        CreateDictionary(entries);

        var bagBytes = MeasureAllocatedBytes(() =>
        {
            for (var index = 0; index < iterations; index++)
            {
                CreateBag(entries);
            }
        });
        var dictionaryBytes = MeasureAllocatedBytes(() =>
        {
            for (var index = 0; index < iterations; index++)
            {
                CreateDictionary(entries);
            }
        });

        bagBytes.ShouldBeGreaterThan(0);
        bagBytes.ShouldBeLessThan(dictionaryBytes);
    }

    private static VirtualNodeProperties CreateBag((string, object?)[] entries)
    {
        var bag = new VirtualNodeProperties(entries.Length);
        foreach (var (name, value) in entries)
        {
            bag.Set(name, value);
        }
        return bag;
    }

    private static Dictionary<string, object?> CreateDictionary((string, object?)[] entries)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in entries)
        {
            dictionary[name] = value;
        }
        return dictionary;
    }

    private static long MeasureAllocatedBytes(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
