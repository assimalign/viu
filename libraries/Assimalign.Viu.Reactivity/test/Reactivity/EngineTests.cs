using Shouldly;
using Xunit;

namespace Assimalign.Viu.Reactivity.Tests;

public sealed class EngineTests
{
    [Fact]
    public void Effect_ReReadingOneDependency_ReusesThePublicLink()
    {
        var count = Reactive.Reference(1);
        var effect = new ReactiveEffect(() =>
        {
            _ = count.Value;
            _ = count.Value;
        });
        effect.Run();

        var originalLink = effect.FirstDependency;
        originalLink.ShouldNotBeNull();
        originalLink!.NextDependency.ShouldBeNull();

        count.Value = 2;

        effect.FirstDependency.ShouldBeSameAs(originalLink);
        effect.FirstDependency!.NextDependency.ShouldBeNull();
    }

    [Fact]
    public void Dependency_Trigger_AdvancesTheObservedLinkVersionAndReruns()
    {
        var dependency = new Dependency();
        var runs = 0;
        var effect = new ReactiveEffect(() =>
        {
            runs++;
            dependency.Track();
        });
        effect.Run();
        var link = effect.FirstDependency;
        var originalVersion = link!.Version;

        dependency.Trigger();

        runs.ShouldBe(2);
        effect.FirstDependency.ShouldBeSameAs(link);
        link.Version.ShouldBeGreaterThan(originalVersion);
    }

    [Fact]
    public void Effect_BranchChange_RemovesTheStalePublicDependencyEdge()
    {
        var flag = Reactive.Reference(true);
        var first = Reactive.Reference(1);
        var second = Reactive.Reference(10);
        var effect = Reactive.Effect(() => _ = flag.Value ? first.Value : second.Value);

        DependsOn(effect, first.Dependency).ShouldBeTrue();
        DependsOn(effect, second.Dependency).ShouldBeFalse();

        flag.Value = false;

        DependsOn(effect, first.Dependency).ShouldBeFalse();
        DependsOn(effect, second.Dependency).ShouldBeTrue();
    }

    [Fact]
    public void Computed_AfterOwningEffectStops_RemainsFreshWithoutScopeOwnership()
    {
        var count = Reactive.Reference(1);
        var getterRuns = 0;
        var doubled = Reactive.Computed(() =>
        {
            getterRuns++;
            return count.Value * 2;
        });
        var effect = Reactive.Effect(() => _ = doubled.Value);
        getterRuns.ShouldBe(1);

        effect.Stop();
        count.Value = 2;
        getterRuns.ShouldBe(1);

        doubled.Value.ShouldBe(4);
        getterRuns.ShouldBe(2);

        var seen = 0;
        Reactive.Effect(() => seen = doubled.Value);
        seen.ShouldBe(4);

        count.Value = 3;
        seen.ShouldBe(6);
        getterRuns.ShouldBe(3);
    }

    private static bool DependsOn(Subscriber subscriber, Dependency dependency)
    {
        for (var link = subscriber.FirstDependency; link is not null; link = link.NextDependency)
        {
            if (ReferenceEquals(link.Dependency, dependency))
            {
                return true;
            }
        }
        return false;
    }
}
