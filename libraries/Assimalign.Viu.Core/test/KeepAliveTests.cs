using System;
using System.Collections.Generic;
using System.Linq;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the KeepAlive built-in against the in-memory renderer, DOM-free — mirroring upstream
// runtime-core/__tests__/components/KeepAlive.spec.ts. KeepAlive IS a component (unlike Teleport):
// switching a child out moves its subtree to a hidden storage container (deactivate) rather than
// unmounting, and switching back moves it home (activate), so Setup runs once and state is preserved.
// Contract: packages/runtime-core/src/components/KeepAlive.ts and
// https://vuejs.org/guide/built-ins/keep-alive.html. [V01.01.03.18]
public sealed class KeepAliveTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;
    private readonly List<string> _events = [];
    private readonly Dictionary<string, int> _setupCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReference<int>> _states = new(StringComparer.Ordinal);

    public KeepAliveTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    private string Serialize() => TestNodeSerializer.Serialize(_container);

    private IReadOnlyList<string> EventsFor(params string[] names)
        => _events.Where(e => names.Any(n => e.StartsWith(n + ":", StringComparison.Ordinal))).ToList();

    // A stateful component that logs its lifecycle, counts its Setup runs, exposes its reactive state
    // ref, and renders "<div>{name}{state}</div>".
    private TestComponent Stateful(string name) => new()
    {
        Name = name,
        SetupFunction = (_, _) =>
        {
            _setupCounts[name] = _setupCounts.GetValueOrDefault(name) + 1;
            var state = Reactive.Reference(0);
            _states[name] = state;
            Lifecycle.OnMounted(() => _events.Add($"{name}:mounted"));
            Lifecycle.OnUnmounted(() => _events.Add($"{name}:unmounted"));
            Lifecycle.OnActivated(() => _events.Add($"{name}:activated"));
            Lifecycle.OnDeactivated(() => _events.Add($"{name}:deactivated"));
            return () => VirtualNodeFactory.Element("div", $"{name}{state.Value}");
        },
    };

    // Builds a root-mounted KeepAlive whose single child is chosen by childSelector each render.
    private void MountKeepAlive(Func<VirtualNode?> childSelector, VirtualNodeProperties? properties = null)
    {
        var slots = new ComponentSlots();
        slots["default"] = _ => new[] { childSelector() };
        _renderer.Render(VirtualNodeFactory.Component(KeepAlive.Instance, properties, slots), _container);
    }

    private static VirtualNode Child(TestComponent component) => VirtualNodeFactory.Component(component);

    // --- state preservation ----------------------------------------------------------------------

    [Fact]
    public void SwitchingAway_DeactivatesInsteadOfUnmounting_PreservesStateAndSetupRunsOnce()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        MountKeepAlive(() => Child(view.Value == "a" ? componentA : componentB));

        // Mutate A's internal state while it is active, then switch away and back.
        _states["a"].Value = 5;
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>a5</div></root>");

        view.Value = "b";
        _pump.RunUntilIdle();
        Serialize().ShouldBe("<root><div>b0</div></root>");

        view.Value = "a";
        _pump.RunUntilIdle();

        // Reactivated with state intact and no Setup re-run (upstream: cached subtree moved back, not
        // remounted).
        Serialize().ShouldBe("<root><div>a5</div></root>");
        _setupCounts["a"].ShouldBe(1);
        _setupCounts["b"].ShouldBe(1);
        _events.ShouldNotContain("a:unmounted");
        _events.ShouldNotContain("b:unmounted");
    }

    [Fact]
    public void ActivatedDeactivated_FireOnInitialMountAndEachSwitchCycle_WithPinnedCounts()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        MountKeepAlive(() => Child(view.Value == "a" ? componentA : componentB));

        view.Value = "b";
        _pump.RunUntilIdle();
        view.Value = "a";
        _pump.RunUntilIdle();

        // activated fires on initial mount inside KeepAlive AND on every reactivation; deactivated on
        // each deactivation; mounted exactly once; unmounted never (upstream KeepAlive lifecycle).
        _events.ShouldBe(
        [
            "a:mounted", "a:activated",                 // initial mount inside KeepAlive
            "a:deactivated", "b:mounted", "b:activated", // switch to B
            "b:deactivated", "a:activated",              // switch back to A
        ]);
        _events.Count(e => e == "a:mounted").ShouldBe(1);
        _events.Count(e => e == "a:activated").ShouldBe(2);
        _events.Count(e => e == "a:deactivated").ShouldBe(1);
        _events.ShouldNotContain("a:unmounted");
    }

    [Fact]
    public void NonKeptAliveComponent_NeverFiresActivatedOrDeactivated()
    {
        // A component with activated/deactivated hooks but no KeepAlive ancestor: the hooks register but
        // never fire (upstream: a no-op without a KeepAlive parent).
        var component = Stateful("solo");
        _renderer.Render(Child(component), _container);
        _renderer.Render(null, _container);

        _events.ShouldBe(["solo:mounted", "solo:unmounted"]);
    }

    // --- include / exclude matching --------------------------------------------------------------

    [Fact]
    public void Include_OnlyCachesMatchingComponents_ExcludedOnesMountAndUnmountNormally()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        // include="a": only A is cached; B mounts/unmounts normally on every switch.
        MountKeepAlive(
            () => Child(view.Value == "a" ? componentA : componentB),
            VirtualNodeFactory.Properties(("include", "a")));

        view.Value = "b";
        _pump.RunUntilIdle();
        view.Value = "a";
        _pump.RunUntilIdle();
        view.Value = "b";
        _pump.RunUntilIdle();

        // A cached across switches (Setup once, no unmount); B not cached (re-mounts, unmounts).
        _setupCounts["a"].ShouldBe(1);
        _events.ShouldNotContain("a:unmounted");
        _setupCounts["b"].ShouldBe(2);
        _events.Count(e => e == "b:unmounted").ShouldBe(1);
        _events.ShouldNotContain("b:activated"); // B is never kept alive, so it never activates
    }

    [Fact]
    public void Exclude_MatchingComponentsAreNotCached()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        // exclude="b": B is not cached; A is.
        MountKeepAlive(
            () => Child(view.Value == "a" ? componentA : componentB),
            VirtualNodeFactory.Properties(("exclude", "b")));

        view.Value = "b";
        _pump.RunUntilIdle();
        view.Value = "a";
        _pump.RunUntilIdle();

        _setupCounts["a"].ShouldBe(1);          // A cached
        _events.ShouldNotContain("a:unmounted");
        _events.Count(e => e == "b:unmounted").ShouldBe(1); // B unmounted on switch away
    }

    [Fact]
    public void Include_AcceptsCommaSeparatedStringAndPredicate()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var componentC = Stateful("c");
        var view = Reactive.Reference("a");

        // Comma-separated string: "a,b" caches both A and B, not C (upstream: pattern.split(',')).
        MountKeepAlive(
            () => Child(view.Value switch { "a" => componentA, "b" => componentB, _ => componentC }),
            VirtualNodeFactory.Properties(("include", "a,b")));
        view.Value = "b";
        _pump.RunUntilIdle();
        view.Value = "c";
        _pump.RunUntilIdle();
        view.Value = "a";
        _pump.RunUntilIdle();
        view.Value = "b";
        _pump.RunUntilIdle();

        _setupCounts["a"].ShouldBe(1); // cached (matched)
        _setupCounts["b"].ShouldBe(1); // cached (matched)
        _setupCounts["c"].ShouldBe(1); // never revisited; would be 2 if it had been cached-then-evicted

        // Predicate include (the C# analogue of upstream's RegExp arm): a fresh KeepAlive caching only
        // names ending in "1".
        _events.Clear();
        _setupCounts.Clear();
        var one = Stateful("one1");
        var two = Stateful("two2");
        var predicateView = Reactive.Reference("one1");
        MountKeepAlive(
            () => Child(predicateView.Value == "one1" ? one : two),
            VirtualNodeFactory.Properties(("include", (Func<string, bool>)(name => name.EndsWith('1')))));
        predicateView.Value = "two2";
        _pump.RunUntilIdle();
        predicateView.Value = "one1";
        _pump.RunUntilIdle();

        _setupCounts["one1"].ShouldBe(1);                       // matched the predicate → cached
        _events.Count(e => e == "two2:unmounted").ShouldBe(1);  // not matched → unmounted on switch away
    }

    [Fact]
    public void Include_AcceptsAStringList()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        // A string list caches any listed name (upstream: the array arm of matches).
        MountKeepAlive(
            () => Child(view.Value == "a" ? componentA : componentB),
            VirtualNodeFactory.Properties(("include", new List<string> { "a", "b" })));

        view.Value = "b";
        _pump.RunUntilIdle();
        view.Value = "a";
        _pump.RunUntilIdle();

        // Both A and B are in the list → both cached (Setup once each, neither unmounted).
        _setupCounts["a"].ShouldBe(1);
        _setupCounts["b"].ShouldBe(1);
        _events.ShouldNotContain("a:unmounted");
        _events.ShouldNotContain("b:unmounted");
    }

    // --- max / LRU -------------------------------------------------------------------------------

    [Fact]
    public void Max_EnforcesLruEviction_FullyUnmountsLeastRecentlyUsed()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var componentC = Stateful("c");
        var view = Reactive.Reference("a");
        // max=2: caching A then B then C evicts the least-recently-used (A), fully unmounting it.
        MountKeepAlive(
            () => Child(view.Value switch { "a" => componentA, "b" => componentB, _ => componentC }),
            VirtualNodeFactory.Properties(("max", 2)));

        view.Value = "b";
        _pump.RunUntilIdle(); // A cached + deactivated, B active
        view.Value = "c";
        _pump.RunUntilIdle(); // cache would hold A, B, C > max 2 → evict LRU (A)

        // A was the least-recently-used cache entry: overflowing max fully unmounts it (a real teardown
        // of the already-deactivated instance, distinct from the deactivation it had on the A→B switch).
        _events.Count(e => e == "a:unmounted").ShouldBe(1);
        _events.Count(e => e == "a:deactivated").ShouldBe(1); // once, on the A→B switch (while cached)

        // B was still cached (deactivated, not unmounted) when we moved to C.
        _events.Count(e => e == "b:unmounted").ShouldBe(0);

        // Returning to A re-mounts it fresh (its cache entry was evicted): Setup runs a second time.
        view.Value = "a";
        _pump.RunUntilIdle();
        _setupCounts["a"].ShouldBe(2);
    }

    // --- cache invalidation on include/exclude change --------------------------------------------

    [Fact]
    public void ChangingInclude_PrunesNewlyExcludedEntriesImmediately()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        var include = Reactive.Reference<object?>(null);

        // A parent renders the KeepAlive with a reactive include prop so changing it re-resolves props.
        var root = new TestComponent
        {
            SetupFunction = (_, _) => () =>
            {
                var slots = new ComponentSlots();
                slots["default"] = _ => new[] { Child(view.Value == "a" ? componentA : componentB) };
                var properties = include.Value is null
                    ? null
                    : VirtualNodeFactory.Properties(("include", include.Value));
                return VirtualNodeFactory.Component(KeepAlive.Instance, properties, slots);
            },
        };
        _renderer.Render(Child(root), _container);

        // Cache both A and B (no filter yet); B is the current active child, A sits deactivated.
        view.Value = "b";
        _pump.RunUntilIdle();
        _setupCounts["a"].ShouldBe(1);

        // Narrow include to "b": A is now excluded and must be pruned from the cache immediately
        // (upstream: watch(..., { flush: 'post' }) → pruneCache).
        include.Value = "b";
        _pump.RunUntilIdle();
        _events.Count(e => e == "a:unmounted").ShouldBe(1);

        // Confirm A's entry is gone: revisiting A re-mounts it fresh.
        view.Value = "a";
        _pump.RunUntilIdle();
        _setupCounts["a"].ShouldBe(2);
    }

    [Fact]
    public void UnmountingKeepAlive_UnmountsAllCachedInstances()
    {
        var componentA = Stateful("a");
        var componentB = Stateful("b");
        var view = Reactive.Reference("a");
        MountKeepAlive(() => Child(view.Value == "a" ? componentA : componentB));

        view.Value = "b";
        _pump.RunUntilIdle(); // A deactivated (cached), B active
        _events.Clear();

        _renderer.Render(null, _container); // unmount the whole tree

        // Both the active child and the deactivated cached child are fully unmounted (upstream:
        // onBeforeUnmount tears down every cache entry).
        _events.Count(e => e == "a:unmounted").ShouldBe(1);
        _events.Count(e => e == "b:unmounted").ShouldBe(1);
    }

    // --- nested keep-alive-aware descendants -----------------------------------------------------

    [Fact]
    public void NestedDescendants_FireActivatedAndDeactivatedChildBeforeParent()
    {
        // KeepAlive > outer > inner. Both register activated/deactivated. The KeepAlive activates and
        // deactivates its DIRECT child (outer); the nested inner's hooks are aggregated onto outer and
        // fire child-before-parent (upstream registerKeepAliveHook parent-chain injection).
        var inner = new TestComponent
        {
            Name = "inner",
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnMounted(() => _events.Add("inner:mounted"));
                Lifecycle.OnActivated(() => _events.Add("inner:activated"));
                Lifecycle.OnDeactivated(() => _events.Add("inner:deactivated"));
                return () => VirtualNodeFactory.Element("span", "inner");
            },
        };
        var outer = new TestComponent
        {
            Name = "outer",
            SetupFunction = (_, _) =>
            {
                Lifecycle.OnMounted(() => _events.Add("outer:mounted"));
                Lifecycle.OnActivated(() => _events.Add("outer:activated"));
                Lifecycle.OnDeactivated(() => _events.Add("outer:deactivated"));
                return () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(inner));
            },
        };
        var sibling = Stateful("sibling");
        var view = Reactive.Reference("outer");
        MountKeepAlive(() => Child(view.Value == "outer" ? outer : sibling));

        view.Value = "sibling";
        _pump.RunUntilIdle();
        view.Value = "outer";
        _pump.RunUntilIdle();

        EventsFor("inner", "outer").ShouldBe(
        [
            "inner:mounted", "outer:mounted",       // mounted: child before parent
            "inner:activated", "outer:activated",   // initial activate: child before parent
            "inner:deactivated", "outer:deactivated", // deactivate on switch away: child before parent
            "inner:activated", "outer:activated",   // reactivate on switch back: child before parent
        ]);
    }
}
