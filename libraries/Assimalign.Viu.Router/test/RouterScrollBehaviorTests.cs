using System;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins the host-free half of [RTR-9]: a memory history has no scroll controller, so navigation
// succeeds without invoking even a configured scroll decision.
public sealed class RouterScrollBehaviorTests
{
    [Fact]
    public async Task PushAsync_MemoryHistory_DoesNotInvokeScrollBehavior()
    {
        bool invoked = false;
        Router router = new(
            RouterHistory.CreateMemory(),
            [new RouteRecord("/target")])
        {
            ScrollBehavior = (_, _, _) =>
            {
                invoked = true;
                return Task.FromResult<ScrollTarget?>(
                    new ScrollTarget(new ScrollPosition(0, 0)));
            },
        };

        NavigationFailure? failure = await router.PushAsync("/target");

        failure.ShouldBeNull();
        invoked.ShouldBeFalse();
        router.CurrentRoute.Value.Path.ShouldBe("/target");
    }

    [Fact]
    public async Task PushAsync_AbortedNavigation_DoesNotAskHostToScroll()
    {
        using RecordingScrollHistory history = new();
        using Router router = new(
            history,
            [new RouteRecord("/target")])
        {
            ScrollBehavior = static (_, _, _) =>
                Task.FromResult<ScrollTarget?>(new ScrollTarget(new ScrollPosition(0, 0))),
        };
        router.BeforeEach(static (_, _, _) =>
            Task.FromResult(NavigationGuardResult.Abort));

        NavigationFailure? failure = await router.PushAsync("/target");

        failure.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Aborted);
        history.ApplyCount.ShouldBe(0);
        history.Location.ShouldBe("/");
    }

    [Fact]
    public async Task PushAsync_CancelledNavigation_DoesNotAskHostToScroll()
    {
        using RecordingScrollHistory history = new();
        using Router router = new(
            history,
            [new RouteRecord("/target")])
        {
            ScrollBehavior = static (_, _, _) =>
                Task.FromResult<ScrollTarget?>(new ScrollTarget(new ScrollPosition(0, 0))),
        };
        TaskCompletionSource<object?> guardEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        router.BeforeEach(async (_, _, cancellationToken) =>
        {
            guardEntered.SetResult(null);
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return NavigationGuardResult.Allow;
        });
        using CancellationTokenSource cancellation = new();

        Task<NavigationFailure?> navigation = router.PushAsync(
            "/target",
            cancellation.Token);
        await guardEntered.Task;
        cancellation.Cancel();
        NavigationFailure? failure = await navigation;

        failure.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Cancelled);
        history.ApplyCount.ShouldBe(0);
        history.Location.ShouldBe("/");
    }

    [Fact]
    public async Task PushAsync_NoCurrentBehavior_StillReportsConfirmedNavigationToScrollHost()
    {
        using RecordingScrollHistory history = new();
        using Router router = new(
            history,
            [new RouteRecord("/target")]);

        NavigationFailure? failure = await router.PushAsync("/target");

        failure.ShouldBeNull();
        history.ApplyCount.ShouldBe(1);
        history.LastBehavior.ShouldBeNull();
    }

    private sealed class RecordingScrollHistory : IRouterHistory, IRouterScrollController
    {
        private readonly IRouterHistory _history = RouterHistory.CreateMemory();

        internal int ApplyCount { get; private set; }

        internal ScrollBehavior? LastBehavior { get; private set; }

        public string Base => _history.Base;

        public string Location => _history.Location;

        public RouterHistoryState State => _history.State;

        public void Push(string location, RouterHistoryEntryOptions options = default) =>
            _history.Push(location, options);

        public void Replace(string location, RouterHistoryEntryOptions options = default) =>
            _history.Replace(location, options);

        public void Go(
            int delta,
            RouterHistoryNavigationOptions options = RouterHistoryNavigationOptions.None) =>
            _history.Go(delta, options);

        public Action Listen(NavigationCallback callback) => _history.Listen(callback);

        public string CreateHref(string location) => _history.CreateHref(location);

        public void Dispose() => _history.Dispose();

        public Task ApplyAsync(
            RouteLocation to,
            RouteLocation from,
            ScrollPosition? savedPosition,
            ScrollBehavior? behavior,
            bool isInitialNavigation,
            CancellationToken cancellationToken)
        {
            ApplyCount++;
            LastBehavior = behavior;
            return Task.CompletedTask;
        }

        public Task CompleteInitialScrollAsync() => Task.CompletedTask;
    }
}
