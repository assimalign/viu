using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing.Benchmarks;

namespace Assimalign.Viu.Testing.Benchmarks.Tests;

// Pins the js-framework-benchmark data helpers: globally unique monotonic keys (so a create-after-clear
// never reuses a key), reproducible labels, and pure mutation helpers.
public class BenchmarkRowSourceTests
{
    [Fact]
    public void Build_ProducesTheRequestedCount_WithUniqueMonotonicIds()
    {
        var source = new BenchmarkRowSource();

        var rows = source.Build(1_000);

        rows.Count.ShouldBe(1_000);
        rows.Select(row => row.Identifier).Distinct().Count().ShouldBe(1_000);
        rows[0].Identifier.ShouldBeLessThan(rows[^1].Identifier);
    }

    [Fact]
    public void Build_NeverReusesKeys_AcrossCalls()
    {
        var source = new BenchmarkRowSource();

        var first = source.Build(1_000);
        var second = source.Build(1_000);

        var firstKeys = first.Select(row => row.Identifier).ToHashSet();
        second.ShouldAllBe(row => !firstKeys.Contains(row.Identifier));
    }

    [Fact]
    public void Append_ContinuesTheIdSequence()
    {
        var source = new BenchmarkRowSource();
        var rows = source.Build(10);

        source.Append(rows, 5);

        rows.Count.ShouldBe(15);
        rows.Select(row => row.Identifier).Distinct().Count().ShouldBe(15);
    }

    [Fact]
    public void UpdateEvery_ChangesEveryNthLabel_AndKeepsKeys()
    {
        var original = new BenchmarkRowSource().Build(50);

        var updated = BenchmarkRowSource.UpdateEvery(original, 10);

        for (var index = 0; index < original.Count; index++)
        {
            updated[index].Identifier.ShouldBe(original[index].Identifier);
            if (index % 10 == 0)
            {
                updated[index].Label.ShouldBe(original[index].Label + " !!!");
            }
            else
            {
                updated[index].Label.ShouldBe(original[index].Label);
            }
        }
    }

    [Fact]
    public void Swap_ExchangesTheTwoRows_AndLeavesTheRestUntouched()
    {
        var original = new BenchmarkRowSource().Build(1_000);

        var swapped = BenchmarkRowSource.Swap(original, 1, 998);

        swapped[1].ShouldBe(original[998]);
        swapped[998].ShouldBe(original[1]);
        swapped[0].ShouldBe(original[0]);
        swapped[500].ShouldBe(original[500]);
    }

    [Fact]
    public void RemoveAt_DropsTheRow_AndPreservesOrder()
    {
        var original = new BenchmarkRowSource().Build(10);
        var removedIdentifier = original[3].Identifier;

        var remaining = BenchmarkRowSource.RemoveAt(original, 3);

        remaining.Count.ShouldBe(9);
        remaining.ShouldAllBe(row => row.Identifier != removedIdentifier);
        remaining[3].ShouldBe(original[4]);
    }

    [Fact]
    public void SameSeed_ProducesTheSameLabels()
    {
        var first = new BenchmarkRowSource(seed: 42).Build(100);
        var second = new BenchmarkRowSource(seed: 42).Build(100);

        first.Select(row => row.Label).ShouldBe(second.Select(row => row.Label));
    }
}
