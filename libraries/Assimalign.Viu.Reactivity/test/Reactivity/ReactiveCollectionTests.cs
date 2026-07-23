using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

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
    public void List_SettingExistingIndex_TriggersIterationAndIndex_NotLength()
    {
        var list = new ReactiveList<int>(new[] { 1, 2, 3 });
        var lengthRuns = 0;
        var indexRuns = 0;
        var enumerationRuns = 0;
        var sum = 0;
        Reactive.Effect(() =>
        {
            lengthRuns++;
            _ = list.Count;
        });
        Reactive.Effect(() =>
        {
            indexRuns++;
            _ = list[1];
        });
        Reactive.Effect(() =>
        {
            enumerationRuns++;
            sum = 0;
            foreach (var value in list)
            {
                sum += value;
            }
        });
        lengthRuns.ShouldBe(1);
        indexRuns.ShouldBe(1);
        enumerationRuns.ShouldBe(1);

        // Upstream dep.ts trigger(): a numeric SET runs the index dep and ARRAY_ITERATE_KEY (so
        // enumerating effects observe the replacement) but not the length dep.
        list[1] = 20;
        lengthRuns.ShouldBe(1);
        indexRuns.ShouldBe(2);
        enumerationRuns.ShouldBe(2);
        sum.ShouldBe(24);
    }

    [Fact]
    public void List_Count_TracksLength_AndReRunsOnStructuralChange()
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
    public void Dictionary_SetExistingAndStructuralChanges_TriggerEntryIteration()
    {
        var dictionary = new ReactiveDictionary<string, int> { ["a"] = 1 };
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = dictionary.Count;
        });
        runs.ShouldBe(1);

        // Upstream dep.ts trigger(): a Map SET runs ITERATE_KEY (values()/entries() observe
        // values, and size shares that dep), so an entry-iteration effect re-runs.
        dictionary["a"] = 2;
        runs.ShouldBe(2);

        // Equal value: no trigger (EqualityComparer<int>.Default).
        dictionary["a"] = 2;
        runs.ShouldBe(2);

        // Upstream Map ADD / DELETE trigger iteration as well.
        dictionary["c"] = 3;
        runs.ShouldBe(3);

        dictionary.Remove("c");
        runs.ShouldBe(4);
    }

    [Fact]
    public void Dictionary_Keys_TracksKeyIteration_AndIgnoresValueReplacement()
    {
        var dictionary = new ReactiveDictionary<string, int> { ["a"] = 1 };
        var runs = 0;
        var keyCount = 0;
        Reactive.Effect(() =>
        {
            runs++;
            keyCount = dictionary.Keys.Count;
        });
        runs.ShouldBe(1);
        keyCount.ShouldBe(1);

        // Upstream MAP_KEY_ITERATE_KEY: keys() re-runs only on ADD/DELETE — a value replacement
        // leaves a keys-only effect untouched.
        dictionary["a"] = 99;
        runs.ShouldBe(1);

        dictionary.Add("b", 2);
        runs.ShouldBe(2);
        keyCount.ShouldBe(2);

        dictionary.Remove("a");
        runs.ShouldBe(3);
        keyCount.ShouldBe(1);
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

