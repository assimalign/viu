using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

// The generated ToReferences() bundle and the IReadonlyReactive marker are emitted by the
// Assimalign.Viu.Core.Generators source generator (wired into this test project). These tests
// consume the real generated output. Upstream parity: https://vuejs.org/api/reactivity-utilities.html
// (toRefs, isReadonly).

/// <summary>A read-only reactive object (the port of <c>readonly(reactive())</c>).</summary>
[Reactive(Readonly = true)]
public partial class ReadonlyProfile
{
    /// <summary>The profile handle (read-only after construction).</summary>
    public partial string Handle { get; set; }
}

public sealed class GeneratedReferencesTests
{
    [Fact]
    public void ToReferences_RefsAreWriteThroughLinkedToTheObject()
    {
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var references = person.ToReferences();

        Reactive.IsRef(references.Name).ShouldBeTrue();
        references.Name.Value.ShouldBe("Ada");
        references.Age.Value.ShouldBe(30);

        // Object -> ref: a reader of the ref re-runs when the object property changes.
        var runs = 0;
        string? seen = null;
        Reactive.Effect(() =>
        {
            runs++;
            seen = references.Name.Value;
        });
        runs.ShouldBe(1);
        seen.ShouldBe("Ada");

        person.Name = "Grace";
        runs.ShouldBe(2);
        seen.ShouldBe("Grace");

        // Ref -> object: writing the ref mutates the object and triggers its dependency.
        references.Name.Value = "Hopper";
        person.Name.ShouldBe("Hopper");
        runs.ShouldBe(3);
        seen.ShouldBe("Hopper");

        // Equal-value write flows through the property's EqualityComparer cutoff: no trigger.
        references.Name.Value = "Hopper";
        runs.ShouldBe(3);
    }

    [Fact]
    public void ToReferences_DistinctPropertiesAreIndependentlyTracked()
    {
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var references = person.ToReferences();
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = references.Name.Value;
        });
        runs.ShouldBe(1);

        // A different property changing does not re-run a reader of Name.
        references.Age.Value = 31;
        person.Age.ShouldBe(31);
        runs.ShouldBe(1);

        references.Name.Value = "Grace";
        runs.ShouldBe(2);
    }

    [Fact]
    public void ReadonlyReactiveObject_IsReadonly_AndStillReactive()
    {
        var profile = new ReadonlyProfile();

        // readonly(reactive()) is both readonly and reactive (Vue parity).
        Reactive.IsReadonly(profile).ShouldBeTrue();
        Reactive.IsReactive(profile).ShouldBeTrue();

        // A mutable [Reactive] object is reactive but not readonly.
        Reactive.IsReadonly(new ReactivePerson { Name = "A" }).ShouldBeFalse();
    }

    [Fact]
    public void ToRawValues_Reads_DoNotTrack()
    {
        // Upstream toRaw contract (https://vuejs.org/api/reactivity-advanced.html#toraw): reads
        // through the raw view do not establish dependencies, so the effect never re-runs.
        var person = new ReactivePerson { Name = "Ada", Age = 30 };
        var raw = person.ToRawValues();
        var runs = 0;
        string? seen = null;
        Reactive.Effect(() =>
        {
            runs++;
            seen = raw.Name;
        });
        runs.ShouldBe(1);
        seen.ShouldBe("Ada");

        person.Name = "Grace";
        runs.ShouldBe(1); // untracked read: no re-run

        // The raw view still observes the tracked write's value (same backing field).
        raw.Name.ShouldBe("Grace");
    }

    [Fact]
    public void ToRawValues_Writes_DoNotTrigger_ButLandInTheSharedState()
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

        // A raw write mutates the shared backing field without triggering (upstream: writes to the
        // raw target bypass the proxy's trigger).
        var raw = person.ToRawValues();
        raw.Name = "Grace";
        runs.ShouldBe(1);
        person.ToRawValues().Name.ShouldBe("Grace");

        // The instrumented path is intact: a tracked write still triggers, and its equality guard
        // compares against the raw-updated value (writing the same value is a no-op).
        person.Name = "Grace";
        runs.ShouldBe(1);
        person.Name = "Hopper";
        runs.ShouldBe(2);
        seen.ShouldBe("Hopper");
    }
}
