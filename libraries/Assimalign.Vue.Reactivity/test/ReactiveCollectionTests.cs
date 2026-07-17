using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Vue.Reactivity.Tests;

// Reactive collection instrumentation, mirroring Vue 3.5's array/Map/Set handlers
// (packages/reactivity/src/collectionHandlers.ts, arrayInstrumentations.ts). Run counts are pinned:
// per-key granularity means an effect re-runs only for the entry it actually read.
public sealed class ReactiveCollectionTests
{
    [Fact]
    public void List_Indexer_TracksPerIndex_AndIgnoresOtherIndices()
    {
        var list = new ReactiveList<int>(new[] { 10, 20, 30, 40, 50, 60 });
        var runs = 0;
        var seen = 0;
        Reactive.Effect(() =>
        {
            runs++;
            seen = list[0];
        });
        runs.ShouldBe(1);
        seen.ShouldBe(10);

        // A different index changing must not re-run an effect that read list[0].
        list[5] = 99;
        runs.ShouldBe(1);

        list[0] = 11;
        runs.ShouldBe(2);
        seen.ShouldBe(11);

        // Equal value: no trigger (EqualityComparer<int>.Default).
        list[0] = 11;
        runs.ShouldBe(2);
    }

    [Fact]
    public void List_Enumeration_TracksIteration_AndReRunsOnAdd()
    {
        var list = new ReactiveList<int>(new[] { 1, 2, 3 });
        var runs = 0;
        var sum = 0;
        Reactive.Effect(() =>
        {
            runs++;
            sum = 0;
            foreach (var value in list)
            {
                sum += value;
            }
        });
        runs.ShouldBe(1);
        sum.ShouldBe(6);

        list.Add(4);
        runs.ShouldBe(2);
        sum.ShouldBe(10);
    }

    [Fact]
    public void List_SettingExistingIndex_DoesNotTriggerIteration()
    {
        var list = new ReactiveList<int>(new[] { 1, 2, 3 });
        var iterationRuns = 0;
        var indexRuns = 0;
        Reactive.Effect(() =>
        {
            iterationRuns++;
            _ = list.Count;
        });
        Reactive.Effect(() =>
        {
            indexRuns++;
            _ = list[1];
        });
        iterationRuns.ShouldBe(1);
        indexRuns.ShouldBe(1);

        // Upstream array SET: replacing an existing slot triggers that index, not length/iteration.
        list[1] = 20;
        iterationRuns.ShouldBe(1);
        indexRuns.ShouldBe(2);
    }

    [Fact]
    public void List_Count_TracksIteration_AndReRunsOnStructuralChange()
    {
        var list = new ReactiveList<int>();
        var runs = 0;
        var count = 0;
        Reactive.Effect(() =>
        {
            runs++;
            count = list.Count;
        });
        runs.ShouldBe(1);
        count.ShouldBe(0);

        list.Add(1);
        runs.ShouldBe(2);
        count.ShouldBe(1);

        list.RemoveAt(0);
        runs.ShouldBe(3);
        count.ShouldBe(0);

        list.Clear(); // already empty: no trigger
        runs.ShouldBe(3);
    }

    [Fact]
    public void List_Contains_TracksIteration()
    {
        var list = new ReactiveList<string>(new[] { "a", "b" });
        var runs = 0;
        var has = false;
        Reactive.Effect(() =>
        {
            runs++;
            has = list.Contains("c");
        });
        runs.ShouldBe(1);
        has.ShouldBeFalse();

        list.Add("c");
        runs.ShouldBe(2);
        has.ShouldBeTrue();
    }

    [Fact]
    public void Dictionary_Indexer_TracksPerKey()
    {
        var dictionary = new ReactiveDictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var runs = 0;
        var seen = 0;
        Reactive.Effect(() =>
        {
            runs++;
            seen = dictionary["a"];
        });
        runs.ShouldBe(1);
        seen.ShouldBe(1);

        // A different key: no re-run.
        dictionary["b"] = 20;
        runs.ShouldBe(1);

        dictionary["a"] = 10;
        runs.ShouldBe(2);
        seen.ShouldBe(10);

        dictionary["a"] = 10; // equal value
        runs.ShouldBe(2);
    }

    [Fact]
    public void Dictionary_AddNewKeyTriggersIteration_SetExistingDoesNot()
    {
        var dictionary = new ReactiveDictionary<string, int> { ["a"] = 1 };
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = dictionary.Count;
        });
        runs.ShouldBe(1);

        // Upstream Map SET: setting an existing key does not trigger iteration.
        dictionary["a"] = 2;
        runs.ShouldBe(1);

        // Upstream Map ADD: a new key triggers iteration.
        dictionary["c"] = 3;
        runs.ShouldBe(2);

        dictionary.Remove("c");
        runs.ShouldBe(3);
    }

    [Fact]
    public void Dictionary_ContainsKey_TracksKey_AndReRunsOnAddRemove()
    {
        var dictionary = new ReactiveDictionary<string, int>();
        var runs = 0;
        var has = false;
        Reactive.Effect(() =>
        {
            runs++;
            has = dictionary.ContainsKey("x");
        });
        runs.ShouldBe(1);
        has.ShouldBeFalse();

        dictionary["x"] = 1;
        runs.ShouldBe(2);
        has.ShouldBeTrue();

        // A different key does not disturb the tracked "x" membership.
        dictionary["y"] = 2;
        runs.ShouldBe(2);

        dictionary.Remove("x");
        runs.ShouldBe(3);
        has.ShouldBeFalse();
    }

    [Fact]
    public void Set_Contains_TracksMember_AndIgnoresOtherMembers()
    {
        var set = new ReactiveSet<int>();
        var runs = 0;
        var has = false;
        Reactive.Effect(() =>
        {
            runs++;
            has = set.Contains(5);
        });
        runs.ShouldBe(1);
        has.ShouldBeFalse();

        set.Add(5);
        runs.ShouldBe(2);
        has.ShouldBeTrue();

        // A different member changing does not re-run a Contains(5) effect.
        set.Add(6);
        runs.ShouldBe(2);

        set.Remove(5);
        runs.ShouldBe(3);
        has.ShouldBeFalse();
    }

    [Fact]
    public void Set_Count_TracksIteration()
    {
        var set = new ReactiveSet<string>();
        var runs = 0;
        var count = 0;
        Reactive.Effect(() =>
        {
            runs++;
            count = set.Count;
        });
        runs.ShouldBe(1);
        count.ShouldBe(0);

        set.Add("a");
        runs.ShouldBe(2);
        count.ShouldBe(1);

        // Duplicate add is a no-op: no trigger.
        set.Add("a");
        runs.ShouldBe(2);

        set.Add("b");
        runs.ShouldBe(3);
        count.ShouldBe(2);
    }

    [Fact]
    public void Set_UnionWith_TriggersOncePerBatchOfNewMembers()
    {
        var set = new ReactiveSet<int> { 1 };
        var runs = 0;
        var count = 0;
        Reactive.Effect(() =>
        {
            runs++;
            count = set.Count;
        });
        runs.ShouldBe(1);

        set.UnionWith(new[] { 1, 2, 3 }); // 1 already present; 2 and 3 are new
        runs.ShouldBe(2);
        count.ShouldBe(3);
    }
}
