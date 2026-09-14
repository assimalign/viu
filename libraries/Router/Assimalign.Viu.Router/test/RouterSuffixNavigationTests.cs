using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// [RTR-3], [RTR-6], [RTR-9], [RTR-12], [V01.01.08.09]: complete locations survive navigation,
// while matching and record reuse continue to depend on the path. All hosts here are DOM-free.
public sealed class RouterSuffixNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Navigate_StartSentinelIsNotAResolvedDestination_RejectsWithoutConsumingInitialNavigation(bool replace)
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a")]);
        List<RouteLocation> observedFrom = [];
        router.BeforeEach((_, from, _) =>
        {
            observedFrom.Add(from);
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(
            () => replace ? router.ReplaceAsync(RouteLocation.Start) : router.PushAsync(RouteLocation.Start));

        exception.ParamName.ShouldBe("location");
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);
        history.Location.ShouldBe("/");
        history.State.Position.ShouldBe(0);
        observedFrom.ShouldBeEmpty();

        (await router.PushAsync("/a?item=one#first")).ShouldBeNull();

        observedFrom.ShouldHaveSingleItem().ShouldBeSameAs(RouteLocation.Start);
        router.CurrentRoute.Value.FullPath.ShouldBe("/a?item=one#first");
        router.CurrentRoute.Value.IsMatched.ShouldBeTrue();
        history.State.Current.ShouldBe(router.CurrentRoute.Value.FullPath);
        history.State.Position.ShouldBe(0);
        history.State.Replaced.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadyAsync_SuffixedInitialEntry_ConfirmsAllLocationPartsWithoutAddingHistory()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        history.Replace("/guide/quick-start?mode=full#reactivity");
        using Router router = new(history, [new RouteRecord("/guide/quick-start", name: "guide")]);

        (await router.ReadyAsync()).ShouldBeNull();

        router.CurrentRoute.Value.Name.ShouldBe("guide");
        router.CurrentRoute.Value.Path.ShouldBe("/guide/quick-start");
        router.CurrentRoute.Value.Query.GetStrings("mode").ShouldBe(["full"]);
        router.CurrentRoute.Value.Fragment.ShouldBe("reactivity");
        history.Location.ShouldBe("/guide/quick-start?mode=full#reactivity");
        history.State.Current.ShouldBe(history.Location);
        history.State.Position.ShouldBe(0);
        history.State.Replaced.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Navigate_MemoryPushReplaceBackAndForward_PreserveCompleteLocationsAndState(bool useResolvedLocation)
    {
        using IRouterHistory history = RouterHistory.CreateMemory("/app");
        using Router router = new(history, [new RouteRecord("/a", name: "page")]);
        (await router.PushAsync("/a?item=one#first")).ShouldBeNull();
        RouteLocation pushed = router.ResolveNamed(
            "page",
            RouteParameters.Empty,
            RouteQuery.Parse("item=two&item=three"),
            "second");

        (await (useResolvedLocation
            ? router.PushAsync(pushed)
            : router.PushAsync(pushed.FullPath))).ShouldBeNull();

        history.Location.ShouldBe("/a?item=two&item=three#second");
        history.State.Current.ShouldBe(history.Location);
        history.State.Back.ShouldBe("/a?item=one#first");
        history.State.Position.ShouldBe(1);
        history.State.Replaced.ShouldBeFalse();
        router.CurrentRoute.Value.ShouldBe(pushed);
        router.CreateHref(router.CurrentRoute.Value).ShouldBe("/app/a?item=two&item=three#second");

        RouteLocation replacement = router.Resolve("/a?item=four#third");
        (await (useResolvedLocation
            ? router.ReplaceAsync(replacement)
            : router.ReplaceAsync(replacement.FullPath))).ShouldBeNull();

        history.Location.ShouldBe("/a?item=four#third");
        history.State.Current.ShouldBe(history.Location);
        history.State.Back.ShouldBe("/a?item=one#first");
        history.State.Position.ShouldBe(1);
        history.State.Replaced.ShouldBeTrue();
        router.CurrentRoute.Value.ShouldBe(replacement);

        await MoveHistoryAsync(router, -1);

        history.Location.ShouldBe("/a?item=one#first");
        history.State.Current.ShouldBe(history.Location);
        history.State.Position.ShouldBe(0);
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        router.CurrentRoute.Value.Query.GetStrings("item").ShouldBe(["one"]);
        router.CurrentRoute.Value.Fragment.ShouldBe("first");

        await MoveHistoryAsync(router, 1);

        history.Location.ShouldBe("/a?item=four#third");
        history.State.Current.ShouldBe(history.Location);
        history.State.Position.ShouldBe(1);
        router.CurrentRoute.Value.ShouldBe(replacement);
    }

    [Theory]
    [InlineData("/a?item=one#first", "/a?item=two#first")]
    [InlineData("/a?item=one#first", "/a?item=one#second")]
    [InlineData("/a", "/a?")]
    [InlineData("/a", "/a#")]
    [InlineData("/a?item=+", "/a?item=%20")]
    public async Task PushAsync_SuffixDifference_RunsGuardsWhileAnIdenticalFullLocationIsDuplicated(
        string initial,
        string destination)
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a")]);
        (await router.PushAsync(initial)).ShouldBeNull();
        int beforeEachRuns = 0;
        int beforeResolveRuns = 0;
        List<(string To, string From, NavigationFailureType? Failure)> observed = [];
        router.BeforeEach((_, _, _) =>
        {
            beforeEachRuns++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.BeforeResolve((_, _, _) =>
        {
            beforeResolveRuns++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.AfterEach((to, from, failure) => observed.Add((to.FullPath, from.FullPath, failure?.Type)));

        (await router.PushAsync(destination)).ShouldBeNull();
        NavigationFailure? duplicate = await router.PushAsync(destination);

        duplicate.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Duplicated);
        beforeEachRuns.ShouldBe(1);
        beforeResolveRuns.ShouldBe(1);
        observed.ShouldBe(
        [
            (destination, initial, null),
            (destination, destination, NavigationFailureType.Duplicated),
        ]);
        router.CurrentRoute.Value.FullPath.ShouldBe(destination);
        history.Location.ShouldBe(destination);
        history.State.Position.ShouldBe(1);
    }

    [Fact]
    public async Task PushAsync_IdenticalUnmatchedFullLocationAfterConfirmation_IsDuplicated()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a")]);
        int beforeEachRuns = 0;
        router.BeforeEach((_, _, _) =>
        {
            beforeEachRuns++;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        (await router.PushAsync("/missing?item=one#first")).ShouldBeNull();
        NavigationFailure? duplicate = await router.PushAsync("/missing?item=one#first");
        (await router.PushAsync("/missing?item=one#second")).ShouldBeNull();

        duplicate.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Duplicated);
        beforeEachRuns.ShouldBe(2);
        history.State.Position.ShouldBe(1);
        history.Location.ShouldBe("/missing?item=one#second");
        router.CurrentRoute.Value.IsMatched.ShouldBeFalse();
        router.CurrentRoute.Value.FullPath.ShouldBe(history.Location);
    }

    [Theory]
    [InlineData("/a?item=one#first", "/a?item=two#first")]
    [InlineData("/a?item=one#first", "/a?item=one#second")]
    public async Task PushAsync_SuffixOnlyChange_ReusesMountedRecordAndRunsUpdateButNoEnterOrLeaveGuards(
        string initial,
        string destination)
    {
        List<string> order = [];
        RecordingEnterGuard enterGuard = new();
        int recordEnterRuns = 0;
        TrackingComponent view = new(
            "suffix-page",
            _ => Element("main", children: [Text("page")]),
            setup: context =>
            {
                RouterGuards.OnBeforeRouteLeave(context, (_, _, _) =>
                {
                    order.Add("leave");
                    return Task.FromResult(NavigationGuardResult.Allow);
                });
                RouterGuards.OnBeforeRouteUpdate(context, (to, from, _) =>
                {
                    order.Add("update:" + from.FullPath + "->" + to.FullPath);
                    return Task.FromResult(NavigationGuardResult.Allow);
                });
            });
        RouteRecord record = new(
            "/a",
            component: view.Request,
            beforeEnter: (_, _, _) =>
            {
                recordEnterRuns++;
                return Task.FromResult(NavigationGuardResult.Allow);
            },
            routeEnterGuard: enterGuard);
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [record]);
        (await router.PushAsync(initial)).ShouldBeNull();
        using var wrapper = MountView(router, view);
        var originalContext = view.Context;
        router.BeforeEach((_, _, _) =>
        {
            order.Add("beforeEach");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.BeforeResolve((_, _, _) =>
        {
            order.Add("beforeResolve");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.AfterEach((_, _, _) => order.Add("afterEach"));

        (await router.PushAsync(destination)).ShouldBeNull();
        await wrapper.NextTickAsync();

        order.ShouldBe(["beforeEach", "update:" + initial + "->" + destination, "beforeResolve", "afterEach"]);
        recordEnterRuns.ShouldBe(1);
        enterGuard.RunCount.ShouldBe(1);
        router.CurrentRoute.Value.Matched.ShouldBe([record]);
        view.SetupCount.ShouldBe(1);
        view.Context.ShouldBeSameAs(originalContext);
        view.IsUnmounted.ShouldBeFalse();
    }

    [Fact]
    public async Task PushAsync_AbortedFragmentChange_PreservesCurrentRouteAndReportsFullDestinations()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a")]);
        (await router.PushAsync("/a?item=one#first")).ShouldBeNull();
        RouteLocation original = router.CurrentRoute.Value;
        List<(string To, string From, NavigationFailureType? Failure)> observed = [];
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Fragment == "blocked" ? NavigationGuardResult.Abort : NavigationGuardResult.Allow));
        router.AfterEach((to, from, failure) => observed.Add((to.FullPath, from.FullPath, failure?.Type)));

        NavigationFailure? failure = await router.PushAsync("/a?item=one#blocked");

        failure.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Aborted);
        observed.ShouldBe([("/a?item=one#blocked", "/a?item=one#first", NavigationFailureType.Aborted)]);
        router.CurrentRoute.Value.ShouldBeSameAs(original);
        history.Location.ShouldBe(original.FullPath);
    }

    [Fact]
    public async Task Go_AbortedSuffixOnlyPop_RestoresTheFullLocation()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a")]);
        (await router.PushAsync("/a?item=one#first")).ShouldBeNull();
        (await router.PushAsync("/a?item=two#second")).ShouldBeNull();
        router.BeforeEach(static (_, _, _) => Task.FromResult(NavigationGuardResult.Abort));
        TaskCompletionSource<NavigationFailure?> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        router.AfterEach((_, _, failure) => completed.TrySetResult(failure));

        router.Back();
        NavigationFailure? failure = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        failure.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Aborted);
        history.Location.ShouldBe("/a?item=two#second");
        history.State.Current.ShouldBe(history.Location);
        router.CurrentRoute.Value.FullPath.ShouldBe(history.Location);
    }

    [Theory]
    [InlineData("/a?item=one#first", "/a?item=one#first", "router-link-active router-link-exact-active")]
    [InlineData("/a?item=one#first", "/a?item=two#first", "router-link-active")]
    [InlineData("/a?item=one#first", "/a?item=one#second", "router-link-active")]
    [InlineData("/a?item=one#first", "/a", "router-link-active")]
    [InlineData("/a?item=+", "/a?item=%20", "router-link-active")]
    [InlineData("/A?item=one#first", "/a?item=one#first", null)]
    [InlineData("/a?", "/a", "router-link-active")]
    [InlineData("/a#", "/a", "router-link-active")]
    [InlineData("/a/child?item=one#first", "/a?item=two#second", "router-link-active")]
    [InlineData("/a/child?item=one#first", "/a/child?item=one#first", "router-link-active router-link-exact-active")]
    [InlineData("/a/child?item=one#first", "/ab?item=one#first", null)]
    public async Task RouterLink_ActiveUsesThePathAndExactActiveAlsoRequiresIdenticalFullLocation(
        string current,
        string target,
        string? expectedClass)
    {
        using IRouterHistory history = RouterHistory.CreateMemory("/app");
        using Router router = new(
            history,
            [new RouteRecord("/a", children: [new RouteRecord("child")]), new RouteRecord("/ab")]);
        (await router.PushAsync(current)).ShouldBeNull();

        using var wrapper = MountLink(router, Arguments(("to", target)), TextSlot("Page"));

        (wrapper.Get("a").Attribute("class") as string).ShouldBe(expectedClass);
        wrapper.Get("a").Attribute("href").ShouldBe("/app" + target);
    }

    [Fact]
    public async Task RouterLink_FragmentNavigation_UpdatesExactActiveReactivelyAndPreservesTheSuffixOnClick()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Router router = new(history, [new RouteRecord("/a")]);
        (await router.PushAsync("/a?item=one#first")).ShouldBeNull();
        using var wrapper = MountLink(router, Arguments(("to", "/a?item=one#second")), TextSlot("Second"));
        (wrapper.Get("a").Attribute("class") as string).ShouldBe("router-link-active");
        RouterLinkClickEvent click = new();

        await wrapper.TriggerAsync("click", click);
        await wrapper.NextTickAsync();

        click.DefaultPrevented.ShouldBeTrue();
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        router.CurrentRoute.Value.Fragment.ShouldBe("second");
        history.Location.ShouldBe("/a?item=one#second");
        (wrapper.Get("a").Attribute("class") as string).ShouldBe("router-link-active router-link-exact-active");
    }

    [Fact]
    public async Task ScrollBehavior_FragmentChange_ReceivesCompleteLocationsAndCanChooseASelector()
    {
        using RecordingScrollHistory history = new();
        using Router router = new(history, [new RouteRecord("/a")]);
        (await router.PushAsync("/a?item=one#first")).ShouldBeNull();
        List<(string To, string From, ScrollPosition? Saved)> observed = [];
        router.ScrollBehavior = (to, from, saved) =>
        {
            observed.Add((to.FullPath, from.FullPath, saved));
            return Task.FromResult<ScrollTarget?>(new ScrollTarget("#" + to.Fragment));
        };

        (await router.PushAsync("/a?item=one#second")).ShouldBeNull();
        NavigationFailure? duplicate = await router.PushAsync("/a?item=one#second");

        observed.ShouldBe([("/a?item=one#second", "/a?item=one#first", null)]);
        history.LastTarget.ShouldNotBeNull().Selector.ShouldBe("#second");
        duplicate.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Duplicated);
    }

    private static async Task MoveHistoryAsync(Router router, int delta)
    {
        TaskCompletionSource<NavigationFailure?> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Action remove = router.AfterEach((_, _, failure) => completed.TrySetResult(failure));
        try
        {
            router.Go(delta);
            (await completed.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeNull();
        }
        finally
        {
            remove();
        }
    }

    private sealed class RecordingEnterGuard : IRouteEnterGuard
    {
        internal int RunCount { get; private set; }

        public Task<NavigationGuardResult> BeforeRouteEnterAsync(
            RouteLocation to,
            RouteLocation from,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(NavigationGuardResult.Allow);
        }
    }

    private sealed class RecordingScrollHistory : IRouterHistory, IRouterScrollController
    {
        private readonly IRouterHistory _history = RouterHistory.CreateMemory();

        internal ScrollTarget? LastTarget { get; private set; }

        public string Base => _history.Base;

        public string Location => _history.Location;

        public RouterHistoryState State => _history.State;

        public void Push(string location, RouterHistoryEntryOptions options = default) => _history.Push(location, options);

        public void Replace(string location, RouterHistoryEntryOptions options = default) => _history.Replace(location, options);

        public void Go(int delta, RouterHistoryNavigationOptions options = RouterHistoryNavigationOptions.None) => _history.Go(delta, options);

        public Action Listen(NavigationCallback callback) => _history.Listen(callback);

        public string CreateHref(string location) => _history.CreateHref(location);

        public void Dispose() => _history.Dispose();

        public async Task ApplyAsync(
            RouteLocation to,
            RouteLocation from,
            ScrollPosition? savedPosition,
            ScrollBehavior? behavior,
            bool isInitialNavigation,
            CancellationToken cancellationToken)
        {
            if (behavior is not null)
            {
                LastTarget = await behavior(to, from, savedPosition);
            }
        }

        public Task CompleteInitialScrollAsync() => Task.CompletedTask;
    }
}
