using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router.Tests;

// Pins [RTR-9] and [RTR-10]: browser scroll effects wait for post-render work, cross interop once,
// preserve null, support asynchronous decisions, and receive saved offsets from pop state.
public sealed class BrowserRouterScrollTests : IDisposable
{
    public BrowserRouterScrollTests()
    {
        Scheduler.Reset();
    }

    public void Dispose()
    {
        Scheduler.Reset();
    }

    [Fact]
    public async Task ApplyAsync_PendingRenderFlush_ThenSelectorTargetUsesOneScrollCall()
    {
        List<string> order = [];
        RecordingBrowserHistoryInterop interop = new(order);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);
        Scheduler.QueueJob(new SchedulerJob(() => order.Add("render")));
        ScrollTarget expected = new(
            "#details",
            new ScrollPosition(4, 12),
            smooth: true);

        await history.ApplyAsync(
            Location("/to"),
            Location("/from"),
            savedPosition: null,
            (_, _, _) =>
            {
                order.Add("behavior");
                return Task.FromResult<ScrollTarget?>(expected);
            },
            isInitialNavigation: false,
            CancellationToken.None);

        order.ShouldBe(["render", "behavior", "scroll"]);
        interop.ScrollCount.ShouldBe(1);
        interop.LastScrollTarget.ShouldBeSameAs(expected);
        expected.Selector.ShouldBe("#details");
        expected.Offset.ShouldBe(new ScrollPosition(4, 12));
        expected.Smooth.ShouldBeTrue();
    }

    [Fact]
    public async Task ApplyAsync_InitialNavigation_DefersUntilMountSignal()
    {
        List<string> order = [];
        RecordingBrowserHistoryInterop interop = new(order);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);

        await history.ApplyAsync(
            Location("/to"),
            RouteLocation.Start,
            savedPosition: null,
            (_, _, _) =>
            {
                order.Add("behavior");
                return Task.FromResult<ScrollTarget?>(
                    new ScrollTarget(new ScrollPosition(0, 10)));
            },
            isInitialNavigation: true,
            CancellationToken.None);

        order.ShouldBeEmpty();
        interop.ScrollCount.ShouldBe(0);

        await history.CompleteInitialScrollAsync();

        order.ShouldBe(["behavior", "scroll"]);
        interop.ScrollCount.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyAsync_NewerPreMountNavigation_InvalidatesDeferredInitialScroll()
    {
        List<string> order = [];
        RecordingBrowserHistoryInterop interop = new(order);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);

        await history.ApplyAsync(
            Location("/initial"),
            RouteLocation.Start,
            savedPosition: null,
            (_, _, _) => Task.FromResult<ScrollTarget?>(
                new ScrollTarget(new ScrollPosition(0, 10))),
            isInitialNavigation: true,
            CancellationToken.None);
        await history.ApplyAsync(
            Location("/latest"),
            Location("/initial"),
            savedPosition: null,
            (_, _, _) => Task.FromResult<ScrollTarget?>(
                new ScrollTarget(new ScrollPosition(0, 20))),
            isInitialNavigation: false,
            CancellationToken.None);

        await history.CompleteInitialScrollAsync();

        interop.ScrollCount.ShouldBe(1);
        interop.LastScrollTarget.ShouldNotBeNull()
            .Position.ShouldBe(new ScrollPosition(0, 20));
    }

    [Fact]
    public async Task ApplyAsync_NewerNavigationWithoutBehavior_InvalidatesDeferredInitialScroll()
    {
        RecordingBrowserHistoryInterop interop = new([]);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);

        await history.ApplyAsync(
            Location("/initial"),
            RouteLocation.Start,
            savedPosition: null,
            (_, _, _) => Task.FromResult<ScrollTarget?>(
                new ScrollTarget(new ScrollPosition(0, 10))),
            isInitialNavigation: true,
            CancellationToken.None);
        await history.ApplyAsync(
            Location("/latest"),
            Location("/initial"),
            savedPosition: null,
            behavior: null,
            isInitialNavigation: false,
            CancellationToken.None);

        await history.CompleteInitialScrollAsync();

        interop.ScrollCount.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyAsync_NullTarget_LeavesScrollUntouched()
    {
        RecordingBrowserHistoryInterop interop = new([]);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);

        await history.ApplyAsync(
            Location("/to"),
            Location("/from"),
            savedPosition: null,
            static (_, _, _) => Task.FromResult<ScrollTarget?>(null),
            isInitialNavigation: false,
            CancellationToken.None);

        interop.ScrollCount.ShouldBe(0);
    }

    [Fact]
    public void Push_LeavingOffsetAndHistoryTransitionUseOneInteropCall()
    {
        RecordingBrowserHistoryInterop interop = new([], subscriptionIdentifier: 17);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);

        history.Push("/next");

        interop.PushCount.ShouldBe(1);
        interop.ReadSnapshotCount.ShouldBe(1);
        interop.LastAmendedState.ShouldNotBeNull().Position.ShouldBe(1);
        interop.LastNewState.ShouldNotBeNull().Position.ShouldBe(2);
        interop.LastPushSubscriptionIdentifier.ShouldBe(17);
        // The single JS push call fills this deliberately absent value from live scrollX/scrollY.
        interop.LastAmendedState.Scroll.ShouldBeNull();
    }

    [Fact]
    public void Replace_CoexistingHistories_TargetOnlyTheirOwnSubscriptions()
    {
        RecordingBrowserHistoryInterop firstInterop = new([], subscriptionIdentifier: 23);
        RecordingBrowserHistoryInterop secondInterop = new([], subscriptionIdentifier: 41);
        using BrowserRouterHistoryImplementation firstHistory = CreateHistory(firstInterop);
        using BrowserRouterHistoryImplementation secondHistory = CreateHistory(secondInterop);

        firstHistory.Replace("/first");
        secondHistory.Replace("/second");

        firstInterop.ReplaceCount.ShouldBe(1);
        firstInterop.LastReplaceSubscriptionIdentifier.ShouldBe(23);
        secondInterop.ReplaceCount.ShouldBe(1);
        secondInterop.LastReplaceSubscriptionIdentifier.ShouldBe(41);
    }

    [Fact]
    public async Task PopNavigation_SavedPosition_IsPassedAndRestoredOnceAfterAsyncDecision()
    {
        RecordingBrowserHistoryInterop interop = new([]);
        using BrowserRouterHistoryImplementation history = CreateHistory(interop);
        using global::Assimalign.Viu.Router.Router router = new(
            history,
            [new RouteRecord("/current"), new RouteRecord("/previous")]);
        (await router.ReadyAsync()).ShouldBeNull();
        TaskCompletionSource<ScrollTarget?> decision = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ScrollPosition? receivedSavedPosition = null;
        router.ScrollBehavior = (_, _, savedPosition) =>
        {
            receivedSavedPosition = savedPosition;
            return decision.Task;
        };

        interop.RaisePop(
            "/previous",
            new RouterHistoryState(
                Back: null,
                Current: "/previous",
                Forward: "/current",
                Replaced: false,
                Position: 0,
                Scroll: new ScrollPosition(18, 42)));
        await WaitUntilAsync(() => receivedSavedPosition.HasValue);

        receivedSavedPosition.ShouldBe(new ScrollPosition(18, 42));
        interop.ScrollCount.ShouldBe(0);

        decision.SetResult(new ScrollTarget(receivedSavedPosition.GetValueOrDefault()));
        await interop.ScrollCompletion.Task;

        interop.ScrollCount.ShouldBe(1);
        interop.LastScrollTarget.ShouldNotBeNull()
            .Position.ShouldBe(new ScrollPosition(18, 42));
    }

    private static BrowserRouterHistoryImplementation CreateHistory(
        RecordingBrowserHistoryInterop interop)
    {
        interop.Snapshot = new BrowserHistorySnapshot(
            "/current",
            string.Empty,
            string.Empty,
            "example.test",
            HistoryLength: 2,
            new RouterHistoryState(
                Back: "/previous",
                Current: "/current",
                Forward: null,
                Replaced: false,
                Position: 1,
                Scroll: null));
        return new BrowserRouterHistoryImplementation(interop, string.Empty);
    }

    private static RouteLocation Location(string path)
    {
        return new RouteMatcher([new RouteRecord(path)]).Resolve(path);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        condition().ShouldBeTrue();
    }

    private sealed class RecordingBrowserHistoryInterop : IBrowserHistoryInterop
    {
        private readonly List<string> _order;
        private readonly int _subscriptionIdentifier;
        private Action<BrowserHistorySnapshot>? _onPopState;

        internal RecordingBrowserHistoryInterop(
            List<string> order,
            int subscriptionIdentifier = 1)
        {
            _order = order;
            _subscriptionIdentifier = subscriptionIdentifier;
        }

        internal BrowserHistorySnapshot Snapshot { get; set; }

        internal int ReadSnapshotCount { get; private set; }

        internal int PushCount { get; private set; }

        internal int ReplaceCount { get; private set; }

        internal int ScrollCount { get; private set; }

        internal int LastPushSubscriptionIdentifier { get; private set; }

        internal int LastReplaceSubscriptionIdentifier { get; private set; }

        internal RouterHistoryState? LastAmendedState { get; private set; }

        internal RouterHistoryState? LastNewState { get; private set; }

        internal ScrollTarget? LastScrollTarget { get; private set; }

        internal TaskCompletionSource<object?> ScrollCompletion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public BrowserHistorySnapshot ReadSnapshot()
        {
            ReadSnapshotCount++;
            return Snapshot;
        }

        public string? ReadBaseHref() => null;

        public void Push(
            int subscriptionIdentifier,
            string currentUrl,
            RouterHistoryState amendedCurrentState,
            string toUrl,
            RouterHistoryState newState)
        {
            PushCount++;
            LastPushSubscriptionIdentifier = subscriptionIdentifier;
            LastAmendedState = amendedCurrentState;
            LastNewState = newState;
        }

        public void Replace(
            int subscriptionIdentifier,
            string toUrl,
            RouterHistoryState newState)
        {
            ReplaceCount++;
            LastReplaceSubscriptionIdentifier = subscriptionIdentifier;
        }

        public void Go(int delta)
        {
        }

        public void Scroll(ScrollTarget target)
        {
            ScrollCount++;
            LastScrollTarget = target;
            _order.Add("scroll");
            ScrollCompletion.TrySetResult(null);
        }

        public int Subscribe(Action<BrowserHistorySnapshot> onPopState)
        {
            _onPopState = onPopState;
            return _subscriptionIdentifier;
        }

        public void Unsubscribe(int subscriptionIdentifier)
        {
            _onPopState = null;
        }

        internal void RaisePop(string pathname, RouterHistoryState state)
        {
            Snapshot = new BrowserHistorySnapshot(
                pathname,
                string.Empty,
                string.Empty,
                "example.test",
                HistoryLength: 2,
                state);
            _onPopState.ShouldNotBeNull()(Snapshot);
        }
    }
}
