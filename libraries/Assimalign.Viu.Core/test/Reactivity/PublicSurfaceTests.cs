using System.Reflection;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

/// <summary>
/// Pins the R1 public reactivity surface — <see cref="SubscriberLink"/>,
/// <see cref="Subscriber.FirstDependency"/>, and <see cref="ITrackedReference"/>: the dependency
/// graph is publicly <b>readable</b> but never publicly <b>mutable</b>. No public member can
/// construct a link, splice a link list, or move a version counter. Upstream shape parity:
/// vuejs/core <c>packages/reactivity/src/dep.ts</c> (the <c>Link</c> class).
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
    public void ITrackedReference_IsAPublicInterface()
    {
        typeof(ITrackedReference).IsInterface.ShouldBeTrue();
        typeof(ITrackedReference).IsPublic.ShouldBeTrue();
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
        first.Dependency.ShouldBeSameAs(((ITrackedReference)count).Dependency);
        first.PreviousDependency.ShouldBeNull();

        var second = first.NextDependency;
        second.ShouldNotBeNull();
        second!.Dependency.ShouldBeSameAs(((ITrackedReference)label).Dependency);
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

        var dependency = ((ITrackedReference)shared).Dependency;
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
