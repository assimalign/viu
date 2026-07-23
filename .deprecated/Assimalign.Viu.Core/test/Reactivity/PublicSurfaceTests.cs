using System;
using System.Reflection;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

/// <summary>
/// Pins the public reactivity surface — <see cref="SubscriberLink"/>,
/// <see cref="Subscriber.FirstDependency"/>, and the <see cref="ReactiveValue"/> class hierarchy
/// (R6, [V01.01.03.25]): the dependency graph is publicly <b>readable</b> but never publicly
/// <b>mutable</b>. No public member can construct a link, splice a link list, move a version counter,
/// or swap out a reactive value's <see cref="ReactiveValue.Dependency"/>. Upstream shape parity:
/// vuejs/core <c>packages/reactivity/src/dep.ts</c> (the <c>Link</c> class) and
/// <c>packages/reactivity/src/ref.ts</c> (the ref's <c>dep</c>).
/// </summary>
public sealed class PublicSurfaceTests
{
    [Fact]
    public void SubscriberLink_IsPublicSealedButNotPubliclyConstructible()
    {
        typeof(SubscriberLink).IsPublic.ShouldBeTrue();
        typeof(SubscriberLink).IsSealed.ShouldBeTrue();

        // Construction is a state-mutating capability (it injects an edge into the graph): internal only.
        typeof(SubscriberLink)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .ShouldBeEmpty();
    }

    [Fact]
    public void SubscriberLink_EveryPublicProperty_IsReadableButNotPubliclySettable()
    {
        var properties = typeof(SubscriberLink).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        properties.ShouldNotBeEmpty();

        foreach (var property in properties)
        {
            property.GetGetMethod(nonPublic: false)
                .ShouldNotBeNull($"{property.Name} should be publicly readable for graph inspection.");
            property.GetSetMethod(nonPublic: false)
                .ShouldBeNull($"{property.Name} must not be publicly settable — mutation stays internal.");
        }
    }

    [Fact]
    public void Subscriber_FirstDependency_IsReadOnly()
    {
        var property = typeof(Subscriber).GetProperty(nameof(Subscriber.FirstDependency));
        property.ShouldNotBeNull();
        property!.GetGetMethod(nonPublic: false).ShouldNotBeNull();
        property.GetSetMethod(nonPublic: false).ShouldBeNull();
    }

    [Fact]
    public void ReactiveValue_IsAPublicAbstractClassClosedToExternalSubclassing()
    {
        // The reactive value abstraction is now a class, not an interface (R6). It is public and
        // abstract, and its constructor is private protected so it cannot be subclassed outside the
        // assembly — the reactive value set stays closed.
        typeof(ReactiveValue).IsClass.ShouldBeTrue();
        typeof(ReactiveValue).IsAbstract.ShouldBeTrue();
        typeof(ReactiveValue).IsPublic.ShouldBeTrue();
        typeof(ReactiveValue)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .ShouldBeEmpty();
    }

    [Fact]
    public void EveryRefKind_IsAReactiveValue()
    {
        // The is-ref check is now a class type-test (replacing the old is-IReference interface check).
        (Reactive.Reference(1) is ReactiveValue).ShouldBeTrue();
        (Reactive.ShallowReference(1) is ReactiveValue).ShouldBeTrue();
        (Reactive.Computed(() => 1) is ReactiveValue).ShouldBeTrue();
        (Reactive.CustomReference<int>((track, trigger) => (() => { track(); return 1; }, _ => trigger())) is ReactiveValue)
            .ShouldBeTrue();
        Reactive.IsRef(Reactive.Reference(1)).ShouldBeTrue();
        Reactive.IsRef(42).ShouldBeFalse();
    }

    [Fact]
    public void ReactiveValue_Dependency_IsPubliclyReadableButNotSettable()
    {
        // The dependency wiring is exposed for graph inspection but cannot be swapped through the
        // public surface — no public setter on Dependency, and the cell's own mutating members
        // (Version, subscriber list, Trigger bookkeeping) stay non-public.
        var property = typeof(ReactiveValue).GetProperty(nameof(ReactiveValue.Dependency));
        property.ShouldNotBeNull();
        property!.GetGetMethod(nonPublic: false).ShouldNotBeNull();
        property.GetSetMethod(nonPublic: false).ShouldBeNull();

        // Reading Dependency never tracks (it is a pure inspection handle).
        var count = Reactive.Reference(1);
        var runs = 0;
        var effect = new ReactiveEffect(() =>
        {
            runs++;
            _ = count.Dependency;
        });
        effect.Run();
        count.Value = 2;
        runs.ShouldBe(1);
    }

    [Fact]
    public void TriggerReference_NullArgument_Throws()
    {
        // The new contract (R6): the argument is a non-null ReactiveValue; null is rejected.
        Should.Throw<ArgumentNullException>(() => Reactive.TriggerReference(null!));
    }

    [Fact]
    public void TriggerReference_OnAnyReactiveValue_ForceNotifies_NoSilentNoOp()
    {
        // The old IReference-not-IDependencyReference silent no-op branch is gone: every ReactiveValue
        // owns a dependency, so a plain ref force-notifies (upstream triggerRef parity).
        var count = Reactive.Reference(0);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = count.Value;
        });
        runs.ShouldBe(1);

        Reactive.TriggerReference(count);
        runs.ShouldBe(2);
    }

    [Fact]
    public void BoxedValue_BoxesTheTypedValueAndTracks()
    {
        // BoxedValue exposes the typed Value as object (boxing value types) and reading it tracks.
        ReactiveValue reference = Reactive.Reference(7);
        reference.BoxedValue.ShouldBe(7);

        var count = Reactive.Reference(1);
        var runs = 0;
        Reactive.Effect(() =>
        {
            runs++;
            _ = ((ReactiveValue)count).BoxedValue;
        });
        runs.ShouldBe(1);
        count.Value = 2;
        runs.ShouldBe(2); // a BoxedValue read established the dependency
    }

    [Fact]
    public void ReadonlyComputed_SetterWarns_ThroughTheWarningsSink_AndDoesNotThrow()
    {
        // Vue 3.5 parity (packages/reactivity/src/computed.ts): writing a getter-only computed warns
        // in dev and is a no-op — it never throws. The warning routes through the runtime sink.
        var captured = new System.Collections.Generic.List<string>();
        var previousSink = RuntimeWarnings.Sink;
        RuntimeWarnings.Sink = captured.Add;
        try
        {
            var readonlyComputed = Reactive.Computed(() => 41);
            readonlyComputed.IsReadOnly.ShouldBeTrue();

            Should.NotThrow(() => readonlyComputed.Value = 99);

            readonlyComputed.Value.ShouldBe(41); // unchanged
            captured.ShouldContain(message => message.Contains("readonly"));
        }
        finally
        {
            RuntimeWarnings.Sink = previousSink;
        }
    }

    [Fact]
    public void FirstDependency_ExposesTheDependencyChainThroughThePublicSurface()
    {
        var count = Reactive.Reference(1);
        var label = Reactive.Reference("a");
        var effect = new ReactiveEffect(() =>
        {
            _ = count.Value;
            _ = label.Value; // second dependency, tracked after count
        });
        effect.Run();

        // Walk the subscriber's dependency list purely through the public surface.
        var first = effect.FirstDependency;
        first.ShouldNotBeNull();
        first!.Subscriber.ShouldBeSameAs(effect);
        first.Dependency.ShouldBeSameAs(count.Dependency);
        first.PreviousDependency.ShouldBeNull();

        var second = first.NextDependency;
        second.ShouldNotBeNull();
        second!.Dependency.ShouldBeSameAs(label.Dependency);
        second.PreviousDependency.ShouldBeSameAs(first);
        second.NextDependency.ShouldBeNull();
    }

    [Fact]
    public void SubscriberList_IsNavigableThroughThePublicSurface()
    {
        var shared = Reactive.Reference(0);
        var firstEffect = new ReactiveEffect(() => _ = shared.Value);
        var secondEffect = new ReactiveEffect(() => _ = shared.Value);
        firstEffect.Run();
        secondEffect.Run();

        var dependency = shared.Dependency;
        var firstLink = firstEffect.FirstDependency!;
        var secondLink = secondEffect.FirstDependency!;
        firstLink.Dependency.ShouldBeSameAs(dependency);
        secondLink.Dependency.ShouldBeSameAs(dependency);

        // Both links are threaded into one subscriber list reachable through public navigation
        // (the later subscriber is inserted ahead of the earlier one — notification is newest-first).
        secondLink.PreviousSubscriber.ShouldBeSameAs(firstLink);
        firstLink.NextSubscriber.ShouldBeSameAs(secondLink);
    }

    [Fact]
    public void LinkVersion_IsPubliclyReadableAndAdvancesWithTheDependency()
    {
        var count = Reactive.Reference(1);
        var effect = new ReactiveEffect(() => _ = count.Value);
        effect.Run();

        var versionAfterFirstRun = effect.FirstDependency!.Version;
        versionAfterFirstRun.ShouldBeGreaterThanOrEqualTo(0);

        count.Value = 2; // bumps the dependency version; the effect re-runs and re-observes it
        effect.FirstDependency!.Version.ShouldBeGreaterThan(versionAfterFirstRun);
    }

    [Fact]
    public void PublicGraphReads_DoNotTrackAsDependencies()
    {
        // Inspecting another subscriber's graph from inside an effect must not create dependencies.
        var count = Reactive.Reference(1);
        var probe = new ReactiveEffect(() => _ = count.Value);
        probe.Run();

        var runs = 0;
        var inspector = new ReactiveEffect(() =>
        {
            runs++;
            var link = probe.FirstDependency; // pure reads over the public surface — no tracking
            _ = link?.Version;
            _ = link?.NextDependency;
            _ = link?.Subscriber;
            _ = link?.Dependency;
        });
        inspector.Run();
        runs.ShouldBe(1);

        // Mutating count re-runs `probe` (which tracked it) but never `inspector`.
        count.Value = 2;
        runs.ShouldBe(1);
    }
}
