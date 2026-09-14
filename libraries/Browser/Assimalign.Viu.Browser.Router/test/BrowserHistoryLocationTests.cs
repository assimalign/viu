using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router.Tests;

// Pins [RTR-3] and [RTR-12] / [V01.01.08.09]: full URL suffixes survive the browser's primitive
// boundary and every history operation while Router matches only the path. The injected snapshots
// stand in for browser reads and popstate so these tests require no DOM or JavaScript runtime.
public sealed class BrowserHistoryLocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Navigation_WebAndHash_PreserveQueryAndFragmentThroughPushReplaceBackForward(
        bool isHash)
    {
        const string first = "/guide?term=one#first";
        const string second = "/guide?term=two#second";
        const string replacement = "/guide?term=two&term=three#third";
        RouterHistoryState initialState = new(null, first, null, true, 0, null);
        RecordingBrowserHistoryInterop interop = new(
            Snapshot(isHash, "?term=one", "#first", initialState));
        using BrowserRouterHistoryImplementation history = new(
            interop,
            isHash ? "/app/index.html?host=1#" : "/app");
        RouteRecord record = new("/guide");
        using global::Assimalign.Viu.Router.Router router = new(history, [record]);
        List<(string To, string From, NavigationFailure? Failure)> outcomes = [];
        router.AfterEach((to, from, failure) =>
            outcomes.Add((to.FullPath, from.FullPath, failure)));

        (await router.ReadyAsync()).ShouldBeNull();
        AssertLocation(router.CurrentRoute.Value, first, "term=one", "first", record);
        history.Location.ShouldBe(first);
        history.State.Current.ShouldBe(first);
        router.CreateHref(router.CurrentRoute.Value).ShouldBe((isHash ? "#" : "/app") + first);

        (await router.PushAsync(second)).ShouldBeNull();
        AssertLocation(router.CurrentRoute.Value, second, "term=two", "second", record);
        interop.LastCurrentUrl.ShouldBe((isHash ? "#" : "/app") + first);
        interop.LastToUrl.ShouldBe((isHash ? "#" : "/app") + second);
        RouterHistoryState leavingState = interop.LastAmendedState.ShouldNotBeNull();
        leavingState.Current.ShouldBe(first);
        leavingState.Forward.ShouldBe(second);
        history.State.Back.ShouldBe(first);
        history.State.Current.ShouldBe(second);

        (await router.ReplaceAsync(replacement)).ShouldBeNull();
        AssertLocation(router.CurrentRoute.Value, replacement, "term=two&term=three", "third", record);
        router.CurrentRoute.Value.Query.GetStrings("term").ShouldBe(["two", "three"]);
        interop.LastToUrl.ShouldBe((isHash ? "#" : "/app") + replacement);
        RouterHistoryState replacedState = history.State;
        replacedState.Current.ShouldBe(replacement);
        replacedState.Back.ShouldBe(first);
        replacedState.Position.ShouldBe(1);
        replacedState.Replaced.ShouldBeTrue();

        interop.NextPopSnapshot = Snapshot(isHash, "?term=one", "#first", leavingState);
        await NavigatePopAsync(router, router.Back);
        AssertLocation(router.CurrentRoute.Value, first, "term=one", "first", record);
        history.State.ShouldBe(leavingState);
        history.Location.ShouldBe(first);

        interop.NextPopSnapshot = Snapshot(isHash, "?term=two&term=three", "#third", replacedState);
        await NavigatePopAsync(router, router.Forward);
        AssertLocation(router.CurrentRoute.Value, replacement, "term=two&term=three", "third", record);
        history.State.ShouldBe(replacedState);
        history.Location.ShouldBe(replacement);
        interop.GoDeltas.ShouldBe([-1, 1]);
        outcomes.ShouldBe(
        [
            (first, RouteLocation.Start.FullPath, null),
            (second, first, null),
            (replacement, second, null),
            (first, replacement, null),
            (replacement, first, null),
        ]);
    }

    [Theory]
    [InlineData(false, "?", "", "/guide?", true, false)]
    [InlineData(false, "", "#", "/guide#", false, true)]
    [InlineData(false, "?", "#", "/guide?#", true, true)]
    [InlineData(false, "", "#first?notquery", "/guide#first?notquery", false, true)]
    [InlineData(true, "?", "", "/guide?", true, false)]
    [InlineData(true, "", "#", "/guide#", false, true)]
    [InlineData(true, "?", "#", "/guide?#", true, true)]
    [InlineData(true, "", "#first?notquery", "/guide#first?notquery", false, true)]
    public async Task ReadyAsync_BrowserSnapshot_PreservesEmptyDelimitersAndFragmentQuestionMarks(
        bool isHash,
        string search,
        string fragment,
        string fullPath,
        bool hasQuery,
        bool hasFragment)
    {
        RecordingBrowserHistoryInterop interop = new(Snapshot(isHash, search, fragment, null));
        using BrowserRouterHistoryImplementation history = new(interop, isHash ? "#" : "/app");
        using global::Assimalign.Viu.Router.Router router = new(history, [new RouteRecord("/guide")]);

        (await router.ReadyAsync()).ShouldBeNull();

        RouteLocation location = router.CurrentRoute.Value;
        location.Path.ShouldBe("/guide");
        location.FullPath.ShouldBe(fullPath);
        location.HasQuery.ShouldBe(hasQuery);
        location.HasFragment.ShouldBe(hasFragment);
        location.Query.Count.ShouldBe(0);
        location.Fragment.ShouldBe(fragment.Length == 0 ? string.Empty : fragment[1..]);
        history.State.Current.ShouldBe(fullPath);
        router.CreateHref(location).ShouldBe((isHash ? "#" : "/app") + fullPath);
    }

    [Fact]
    public void SnapshotMarshaller_FlatPayload_PreservesRawSuffixesAndCompleteStateLinks()
    {
        string[] payload =
        [
            "/app/guide", "?term=a%26b&term=c+d", "#section%20one?detail", "example.test", "3",
            "1", "/guide?#", "/guide?term=a%26b&term=c+d#section%20one?detail", "/guide?next#last",
            "0", "1", "1", "4.5", "12.25",
        ];

        BrowserHistorySnapshot snapshot = BrowserHistorySnapshotMarshaller.Decode(payload);

        BrowserHistorySnapshotMarshaller.FieldCount.ShouldBe(14);
        snapshot.Search.ShouldBe("?term=a%26b&term=c+d");
        snapshot.Hash.ShouldBe("#section%20one?detail");
        snapshot.State.ShouldBe(new RouterHistoryState(
            "/guide?#",
            "/guide?term=a%26b&term=c+d#section%20one?detail",
            "/guide?next#last",
            false,
            1,
            new ScrollPosition(4.5, 12.25)));
        BrowserHistoryPathNormalization.CreateCurrentLocation(
            "/app", snapshot.Pathname, snapshot.Search, snapshot.Hash)
            .ShouldBe(snapshot.State.ShouldNotBeNull().Current);
    }

    private static void AssertLocation(
        RouteLocation location,
        string fullPath,
        string rawQuery,
        string fragment,
        RouteRecord record)
    {
        location.Path.ShouldBe("/guide");
        location.FullPath.ShouldBe(fullPath);
        location.RawQuery.ShouldBe(rawQuery);
        location.Fragment.ShouldBe(fragment);
        location.Matched.ShouldHaveSingleItem().ShouldBeSameAs(record);
    }

    private static BrowserHistorySnapshot Snapshot(
        bool isHash,
        string search,
        string fragment,
        RouterHistoryState? state) =>
        isHash
            ? new("/app/index.html", "?host=1", "#/guide" + search + fragment, "example.test", 2, state)
            : new("/app/guide", search, fragment, "example.test", 2, state);

    private static async Task NavigatePopAsync(global::Assimalign.Viu.Router.Router router, Action navigate)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Action stop = router.AfterEach((_, _, failure) =>
        {
            if (failure is null)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(new InvalidOperationException(failure.Type.ToString()));
            }
        });
        try
        {
            navigate();
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            stop();
        }
    }

    private sealed class RecordingBrowserHistoryInterop(BrowserHistorySnapshot snapshot) : IBrowserHistoryInterop
    {
        private Action<BrowserHistorySnapshot>? _onPopState;

        internal string? LastCurrentUrl { get; private set; }

        internal string? LastToUrl { get; private set; }

        internal RouterHistoryState? LastAmendedState { get; private set; }

        internal BrowserHistorySnapshot NextPopSnapshot { get; set; }

        internal List<int> GoDeltas { get; } = [];

        public BrowserHistorySnapshot ReadSnapshot() => snapshot;

        public string? ReadBaseHref() => null;

        public void Push(
            int subscriptionIdentifier,
            string currentUrl,
            RouterHistoryState amendedCurrentState,
            string toUrl,
            RouterHistoryState newState)
        {
            LastCurrentUrl = currentUrl;
            LastToUrl = toUrl;
            LastAmendedState = amendedCurrentState;
        }

        public void Replace(int subscriptionIdentifier, string toUrl, RouterHistoryState newState)
        {
            LastToUrl = toUrl;
        }

        public void Go(int delta)
        {
            GoDeltas.Add(delta);
            _onPopState.ShouldNotBeNull()(NextPopSnapshot);
        }

        public void Scroll(ScrollTarget target)
        {
        }

        public int Subscribe(Action<BrowserHistorySnapshot> onPopState)
        {
            _onPopState = onPopState;
            return 1;
        }

        public void Unsubscribe(int subscriptionIdentifier)
        {
            _onPopState = null;
        }
    }
}
