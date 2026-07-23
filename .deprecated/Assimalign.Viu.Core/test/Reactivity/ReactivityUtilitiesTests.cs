using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

/// <summary>
/// Pins the reactivity utility surface and escape hatches — the C# port of
/// <c>@vue/reactivity</c>'s <c>isRef</c>/<c>isReactive</c>/<c>isReadonly</c>/<c>unref</c>/<c>toRef</c>
/// (https://vuejs.org/api/reactivity-utilities.html) and <c>toRaw</c>/<c>markRaw</c>
/// (https://vuejs.org/api/reactivity-advanced.html). Track/trigger claims are pinned with run counts.
/// The generated-object shapes (<see cref="ReactivePerson"/>, <see cref="ReactiveOrder"/>) are declared
/// in <c>GeneratedReactiveObjectTests</c>.
/// </summary>
public sealed class ReactivityUtilitiesTests
{
    [Fact]
    public void IsRef_TrueForEveryRefKind_FalseForReactiveObjectsAndValues()
    {
        Reactive.IsRef(Reactive.Reference(1)).ShouldBeTrue();
        Reactive.IsRef(Reactive.ShallowReference(1)).ShouldBeTrue();
        Reactive.IsRef(Reactive.Computed(() => 1)).ShouldBeTrue();
        Reactive.IsRef(Reactive.CustomReference<int>((track, trigger) => (() => 0, _ => { }))).ShouldBeTrue();
        Reactive.IsRef(Reactive.ToRef(() => 1)).ShouldBeTrue();

        Reactive.IsRef(new ReactivePerson { Name = "A" }).ShouldBeFalse();
        Reactive.IsRef(new ReactiveList<int>()).ShouldBeFalse();
        Reactive.IsRef(42).ShouldBeFalse();
        Reactive.IsRef("x").ShouldBeFalse();
        Reactive.IsRef(null).ShouldBeFalse();
    }

    [Fact]
    public void IsReactive_TrueForGeneratedObjectsAndCollections_FalseForRefsAndValues()
    {
        Reactive.IsReactive(new ReactivePerson { Name = "A" }).ShouldBeTrue();
        Reactive.IsReactive(new ReactiveList<int>()).ShouldBeTrue();
        Reactive.IsReactive(new ReactiveDictionary<string, int>()).ShouldBeTrue();
        Reactive.IsReactive(new ReactiveSet<int>()).ShouldBeTrue();
        Reactive.IsReactive(new ShallowBox { Version = 1 }).ShouldBeTrue(); // shallowReactive is reactive

        Reactive.IsReactive(Reactive.Reference(1)).ShouldBeFalse();
        Reactive.IsReactive(Reactive.Computed(() => 1)).ShouldBeFalse();
        Reactive.IsReactive(42).ShouldBeFalse();
        Reactive.IsReactive(null).ShouldBeFalse();
    }

    [Fact]
    public void IsReadonly_TrueForGetterOnlyComputed_FalseForWritableComputedAndRefs()
    {
        Reactive.IsReadonly(Reactive.Computed(() => 1)).ShouldBeTrue(); // no setter -> readonly

        Reactive.IsReadonly(Reactive.Computed(() => 1, _ => { })).ShouldBeFalse(); // writable
        Reactive.IsReadonly(Reactive.Reference(1)).ShouldBeFalse();
        Reactive.IsReadonly(new ReactivePerson { Name = "A" }).ShouldBeFalse(); // mutable reactive object
        Reactive.IsReadonly(null).ShouldBeFalse();
    }

    [Fact]
    public void Unref_UnwrapsRefsWithoutBoxing_AndPassesValuesThrough()
    {
        // Concrete ref types must unwrap (overload resolution picks the concrete-ref overload, not the
        // value passthrough) — the guard against Unref(someRef) returning the ref itself.
        Reactive.Unref(Reactive.Reference(7)).ShouldBe(7);
        Reactive.Unref(Reactive.ShallowReference(8)).ShouldBe(8);
        Reactive.Unref(Reactive.Computed(() => 9)).ShouldBe(9);

        // Non-refs pass through unchanged (struct value never boxed by the generic passthrough).
        Reactive.Unref(5).ShouldBe(5);
        Reactive.Unref("hello").ShouldBe("hello");
    }

    [Fact]
    public void Unref_OnReactiveValueHandle_IsATrackedRead()
    {
        var count = Reactive.Reference(1);
        ReactiveValue<int> handle = count;
        var runs = 0;
        var seen = 0;
        Reactive.Effect(() =>
        {
            runs++;
            seen = Reactive.Unref(handle);
        });
        runs.ShouldBe(1);
        seen.ShouldBe(1);

        count.Value = 5;
        runs.ShouldBe(2);
        seen.ShouldBe(5);
    }

    [Fact]
    public void ToRef_GetterSetter_IsWriteThroughLinkedToTheSource()
    {
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var nameRef = Reactive.ToRef(() => person.Name, value => person.Name = value);
        Reactive.IsRef(nameRef).ShouldBeTrue();
        nameRef.Value.ShouldBe("Ada");

        // Source -> ref: mutating the object triggers a reader of the ref.
        var runs = 0;
        string? seen = null;
        Reactive.Effect(() =>
        {
            runs++;
            seen = nameRef.Value;
        });
        runs.ShouldBe(1);
        seen.ShouldBe("Ada");

        person.Name = "Grace";
        runs.ShouldBe(2);
        seen.ShouldBe("Grace");

        // Ref -> source: writing the ref mutates the object and triggers its dependency.
        nameRef.Value = "Hopper";
        person.Name.ShouldBe("Hopper");
        runs.ShouldBe(3);
        seen.ShouldBe("Hopper");
    }

    [Fact]
    public void ToRef_GetterOnly_IsReadonly()
    {
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var ageRef = Reactive.ToRef(() => person.Age);
        ageRef.Value.ShouldBe(30);

        // No setter: the write is a warned no-op, the source is unchanged.
        ageRef.Value = 99;
        person.Age.ShouldBe(30);
    }

    [Fact]
    public void ToRaw_OfGeneratedObject_ReturnsTheSameInstance()
    {
        var person = new ReactivePerson { Name = "Ada" };
        Reactive.ToRaw(person).ShouldBeSameAs(person);
    }

    [Fact]
    public void ToRaw_OfList_WritesDoNotTrigger_ReadsDoNotTrack()
    {
        var list = new ReactiveList<int> { 1, 2 };
        var runs = 0;
        var seenCount = 0;
        Reactive.Effect(() =>
        {
            runs++;
            seenCount = list.Count;
        });
        runs.ShouldBe(1);
        seenCount.ShouldBe(2);

        // Writing through raw mutates the same storage but does not trigger.
        var raw = Reactive.ToRaw(list);
        raw.Add(3);
        runs.ShouldBe(1);
        raw.Count.ShouldBe(3);

        // A reactive mutation still triggers.
        list.Add(4);
        runs.ShouldBe(2);
    }

    [Fact]
    public void ToRaw_OfList_ReadThroughRawDoesNotTrack()
    {
        var list = new ReactiveList<int> { 10 };
        var raw = Reactive.ToRaw(list);
        var runs = 0;
        var seen = 0;
        Reactive.Effect(() =>
        {
            runs++;
            seen = raw[0]; // read off the raw storage: no dependency established
        });
        runs.ShouldBe(1);
        seen.ShouldBe(10);

        list[0] = 20; // reactive write triggers the index dep, but the effect never subscribed
        runs.ShouldBe(1);
    }

    [Fact]
    public void ToRaw_OfDictionary_WritesDoNotTrigger()
    {
        var dictionary = new ReactiveDictionary<string, int> { ["a"] = 1 };
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = dictionary.Count;
        });
        runs.ShouldBe(1);

        var raw = Reactive.ToRaw(dictionary);
        raw["b"] = 2;
        runs.ShouldBe(1);
        raw.Count.ShouldBe(2);

        dictionary["c"] = 3;
        runs.ShouldBe(2);
    }

    [Fact]
    public void ToRaw_OfSet_WritesDoNotTrigger()
    {
        var set = new ReactiveSet<int> { 1 };
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = set.Count;
        });
        runs.ShouldBe(1);

        var raw = Reactive.ToRaw(set);
        raw.Add(2);
        runs.ShouldBe(1);
        raw.Count.ShouldBe(2);

        set.Add(3);
        runs.ShouldBe(2);
    }

    [Fact]
    public void MarkRaw_ReturnsSameInstance_AndReportsNotReactive()
    {
        var person = new ReactivePerson { Name = "A" };
        Reactive.MarkRaw(person).ShouldBeSameAs(person);
        Reactive.IsReactive(person).ShouldBeFalse();
    }

    [Fact]
    public void MarkRaw_ExcludesObjectFromDeepWatchTraversal()
    {
        // The nested customer is marked raw: deep traversal skips it, so a change inside it never
        // re-runs the watcher, but a root-property change still does.
        var order = new ReactiveOrder
        {
            Customer = Reactive.MarkRaw(new ReactivePerson { Name = "A" }),
            Total = 10,
        };
        var runs = 0;
        Reactive.Watch(order, (_, _, _) => runs++);
        runs.ShouldBe(0);

        order.Total = 11;
        runs.ShouldBe(1);

        order.Customer.Name = "B"; // marked -> skipped by traversal
        runs.ShouldBe(1);
    }

    [Fact]
    public void MarkRaw_MemberOfReactiveList_IsSkippedByDeepWatch()
    {
        // A marked element stored in a reactive collection is stored as-is (no wrapping) and its inner
        // changes are invisible to a deep watch; structural changes to the list still fire.
        var marked = Reactive.MarkRaw(new ReactivePerson { Name = "A" });
        var list = new ReactiveList<ReactivePerson> { marked };
        var runs = 0;
        Reactive.Watch(() => list, (_, _, _) => runs++, new WatchOptions { Deep = true });
        runs.ShouldBe(0);

        marked.Name = "B"; // marked element skipped
        runs.ShouldBe(0);

        list.Add(new ReactivePerson { Name = "C" }); // structural change fires
        runs.ShouldBe(1);
    }
}
