using Shouldly;
using Xunit;

namespace Assimalign.Vue.Reactivity.Tests;

// These [Reactive]/[ShallowReactive] partial classes are implemented by the
// Assimalign.Vue.Reactivity.Generators source generator (wired into this test project via
// VuecsAnalyzerReference). The tests below consume the generated output to pin track/trigger
// semantics end to end. Upstream parity: https://vuejs.org/api/reactivity-core.html#reactive

/// <summary>A deep reactive object used by the generated-object behavioral tests.</summary>
[Reactive]
public partial class ReactivePerson
{
    /// <summary>The person's name.</summary>
    public partial string Name { get; set; }

    /// <summary>The person's age.</summary>
    public partial int Age { get; set; }
}

/// <summary>A deep reactive object that composes a nested reactive object.</summary>
[Reactive]
public partial class ReactiveOrder
{
    /// <summary>The nested reactive customer.</summary>
    public partial ReactivePerson Customer { get; set; }

    /// <summary>The order total.</summary>
    public partial int Total { get; set; }
}

/// <summary>A shallow reactive object: only root-level property replacement is deep-traversed.</summary>
[ShallowReactive]
public partial class ShallowBox
{
    /// <summary>The nested reactive content (tracked as a slot, not recursed by deep watch).</summary>
    public partial ReactivePerson Content { get; set; }

    /// <summary>The box version.</summary>
    public partial int Version { get; set; }
}

public sealed class GeneratedReactiveObjectTests
{
    [Fact]
    public void ReadingProperty_TracksAndReRunsOnlyThatProperty()
    {
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var runs = 0;
        string? seen = null;
        Reactive.Effect(() =>
        {
            runs++;
            seen = person.Name;
        });
        runs.ShouldBe(1);
        seen.ShouldBe("Ada");

        // A different property: the effect read only Name, so it must not re-run.
        person.Age = 99;
        runs.ShouldBe(1);

        person.Name = "Grace";
        runs.ShouldBe(2);
        seen.ShouldBe("Grace");

        // Equal value (EqualityComparer<string>.Default): no trigger.
        person.Name = "Grace";
        runs.ShouldBe(2);
    }

    [Fact]
    public void ReactiveObject_ExposesRawAndPerPropertyDependency()
    {
        var person = new ReactivePerson { Name = "Ada" };
        var reactive = (IReactiveObject)person;

        // No identity-swapping wrapper: the instance is its own raw (documented divergence from reactive()).
        reactive.ToRaw().ShouldBeSameAs(person);
        reactive.GetDependency("Name").ShouldNotBeNull();
        reactive.GetDependency("Age").ShouldNotBeNull();
        reactive.GetDependency("DoesNotExist").ShouldBeNull();
    }

    [Fact]
    public void DeepWatch_FiresForRootAndNestedReactiveMembers()
    {
        var order = new ReactiveOrder { Customer = new ReactivePerson { Name = "A" }, Total = 10 };
        var runs = 0;
        Reactive.Watch(order, (_, _, _) => runs++);
        runs.ShouldBe(0);

        // Root property.
        order.Total = 20;
        runs.ShouldBe(1);

        // Nested reactive member: deep traversal subscribed to it.
        order.Customer.Name = "B";
        runs.ShouldBe(2);

        // Nested equal-value write: no trigger.
        order.Customer.Name = "B";
        runs.ShouldBe(2);
    }

    [Fact]
    public void ShallowReactive_DeepWatchTracksRootOnly()
    {
        var box = new ShallowBox { Content = new ReactivePerson { Name = "A" }, Version = 1 };
        var runs = 0;
        Reactive.Watch(box, (_, _, _) => runs++);
        runs.ShouldBe(0);

        // Root property replacement is tracked.
        box.Version = 2;
        runs.ShouldBe(1);

        // Nested member of a shallow object: traversal stopped at the root, so no re-run.
        box.Content.Name = "B";
        runs.ShouldBe(1);

        // Replacing the whole nested slot triggers (the slot itself is a tracked root property).
        box.Content = new ReactivePerson { Name = "C" };
        runs.ShouldBe(2);
    }

    [Fact]
    public void Computed_OverReactiveProperty_CachesUntilThatPropertyChanges()
    {
        var person = new ReactivePerson { Name = "ada" };
        var getterRuns = 0;
        var upper = Reactive.Computed(() =>
        {
            getterRuns++;
            return person.Name.ToUpperInvariant();
        });

        upper.Value.ShouldBe("ADA");
        upper.Value.ShouldBe("ADA");
        getterRuns.ShouldBe(1); // cached

        person.Age = 5; // unrelated property
        upper.Value.ShouldBe("ADA");
        getterRuns.ShouldBe(1); // still cached

        person.Name = "grace";
        upper.Value.ShouldBe("GRACE");
        getterRuns.ShouldBe(2);
    }

    [Fact]
    public void NumericDeepDepth_BoundsTraversalOfGeneratedObject()
    {
        var order = new ReactiveOrder { Customer = new ReactivePerson { Name = "A" }, Total = 1 };
        var runs = 0;
        Reactive.Watch(order, (_, _, _) => runs++, new WatchOptions { DeepDepth = 1 });

        order.Total = 2; // root property is within depth 1
        runs.ShouldBe(1);

        order.Customer.Name = "B"; // one level deeper than the depth-1 ceiling
        runs.ShouldBe(1);
    }

    [Fact]
    public void DeepWatchOfReactiveList_ReRunsWhenAReactiveElementChanges()
    {
        var first = new ReactivePerson { Name = "A" };
        var list = new ReactiveList<ReactivePerson> { first };
        var runs = 0;
        Reactive.Watch(() => list, (_, _, _) => runs++, new WatchOptions { Deep = true });
        runs.ShouldBe(0);

        // A nested reactive element mutating: deep traversal subscribed to it (list -> element -> Name).
        first.Name = "B";
        runs.ShouldBe(1);

        // Structural change: the list's iteration dependency.
        list.Add(new ReactivePerson { Name = "C" });
        runs.ShouldBe(2);
    }
}
