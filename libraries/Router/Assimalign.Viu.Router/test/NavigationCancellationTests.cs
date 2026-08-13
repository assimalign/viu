using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins navigation supersession/cancellation and the popstate pipeline ([V01.01.08.04]): a later
// navigation cancels an in-flight one, the superseded chain runs no further guards, and an aborted
// popstate navigation restores the URL with a compensating history.go. A gated guard creates a
// genuine in-flight navigation so a later one can supersede it deterministically; all DOM-free with
// memory history.
public class NavigationCancellationTests
{
    private static IReadOnlyList<RouteRecord> Routes() =>
    [
        new RouteRecord("/", name: "home"),
        new RouteRecord("/a", name: "a"),
        new RouteRecord("/b", name: "b"),
        new RouteRecord("/c", name: "c"),
    ];

    [Fact]
    public async Task PushAsync_SupersededByLaterNavigation_IsCancelled_WhileTheLaterOneConfirms()
    {
        var gate = new TaskCompletionSource();
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.BeforeEach(async (to, _, _) =>
        {
            if (to.Path == "/a")
            {
                await gate.Task;
            }
            return NavigationGuardResult.Allow;
        });

        var first = router.PushAsync("/a");   // suspends in beforeEach awaiting the gate
        var second = router.PushAsync("/b");  // supersedes the in-flight navigation, confirms synchronously
        gate.SetResult();                // releases the first navigation to resume and observe cancellation

        var firstFailure = await first;
        var secondFailure = await second;

        firstFailure.ShouldNotBeNull();
        firstFailure.Type.ShouldBe(NavigationFailureType.Cancelled);
        secondFailure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/b");
    }

    [Fact]
    public async Task PushAsync_CallerCancellation_ReachesGuardAndReturnsCancelledFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        var entered = new TaskCompletionSource();
        router.BeforeEach(async (_, _, cancellationToken) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return NavigationGuardResult.Allow;
        });

        Task<NavigationFailure?> navigation = router.PushAsync("/a", cancellation.Token);
        await entered.Task;
        cancellation.Cancel();

        NavigationFailure? failure = await navigation;
        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Cancelled);
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);
    }

    [Fact]
    public async Task ReplaceAsync_AlreadyCancelledCaller_ReturnsCancelledFailureWithoutWritingHistory()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        IRouterHistory history = RouterHistory.CreateMemory();
        using var router = new Router(history, Routes());

        NavigationFailure? failure = await router.ReplaceAsync("/a", cancellation.Token);

        failure.ShouldNotBeNull();
        failure.Type.ShouldBe(NavigationFailureType.Cancelled);
        history.Location.ShouldBe("/");
    }

    [Fact]
    public async Task Pop_SupersededByPush_ReportsCancelledWithoutAnErrorOrCompensation()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using var router = new Router(history, Routes());
        await router.PushAsync("/a");
        await router.PushAsync("/b");
        var guardEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var popFinished = new TaskCompletionSource<NavigationFailure?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = 0;
        router.BeforeEach(async (to, _, cancellationToken) =>
        {
            if (to.Path == "/a")
            {
                guardEntered.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return NavigationGuardResult.Allow;
        });
        router.AfterEach((to, _, failure) =>
        {
            if (to.Path == "/a")
            {
                popFinished.TrySetResult(failure);
            }
        });
        router.OnError((_, _, _) => errors++);

        router.Go(-1);
        await guardEntered.Task;
        NavigationFailure? pushFailure = await router.PushAsync("/");
        NavigationFailure? popFailure = await popFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        pushFailure.ShouldBeNull();
        popFailure.ShouldNotBeNull();
        popFailure.Type.ShouldBe(NavigationFailureType.Cancelled);
        errors.ShouldBe(0);
        router.CurrentRoute.Value.Path.ShouldBe("/");
        history.Location.ShouldBe("/");
    }

    [Fact]
    public async Task OverlappingPops_OlderCancellationCannotOverwriteTheNewerPosition()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using var router = new Router(history, Routes());
        await router.PushAsync("/a");
        await router.PushAsync("/b");
        await router.PushAsync("/c");
        var firstPopEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstPopFinished = new TaskCompletionSource<NavigationFailure?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = 0;
        router.BeforeEach(async (to, _, cancellationToken) =>
        {
            if (to.Path == "/b")
            {
                firstPopEntered.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return NavigationGuardResult.Allow;
        });
        router.AfterEach((to, _, failure) =>
        {
            if (to.Path == "/b")
            {
                firstPopFinished.TrySetResult(failure);
            }
        });
        router.OnError((_, _, _) => errors++);

        router.Go(-1);
        await firstPopEntered.Task;
        router.Go(-1);
        NavigationFailure? firstFailure = await firstPopFinished.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        firstFailure.ShouldNotBeNull();
        firstFailure.Type.ShouldBe(NavigationFailureType.Cancelled);
        errors.ShouldBe(0);
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        history.Location.ShouldBe("/a");
    }

    [Fact]
    public async Task RedirectChain_PropagatesOneUncancelledNavigationToken()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using var router = new Router(history, Routes());
        CancellationToken firstToken = default;
        CancellationToken redirectedToken = default;
        router.BeforeEach((to, _, cancellationToken) =>
        {
            if (to.Path == "/a")
            {
                firstToken = cancellationToken;
                return Task.FromResult(NavigationGuardResult.RedirectTo("/b"));
            }
            redirectedToken = cancellationToken;
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        NavigationFailure? failure = await router.PushAsync("/a");

        failure.ShouldBeNull();
        firstToken.CanBeCanceled.ShouldBeTrue();
        redirectedToken.ShouldBe(firstToken);
        firstToken.IsCancellationRequested.ShouldBeFalse();
        router.CurrentRoute.Value.Path.ShouldBe("/b");
    }

    [Fact]
    public async Task RouterDispose_ReleasesItsListenerWithoutDisposingBorrowedHistory()
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using var router = new Router(history, Routes());
        await router.PushAsync("/a");
        await router.PushAsync("/b");

        router.Dispose();
        history.Go(-1);

        history.Location.ShouldBe("/a");
        router.CurrentRoute.Value.Path.ShouldBe("/b");
        Should.NotThrow(() => history.Replace("/c"));
        history.Location.ShouldBe("/c");
    }

    [Fact]
    public async Task SupersededNavigation_DoesNotRunItsRemainingGuards()
    {
        // The cancelled chain must not run further guards after supersession (the cancel
        // check short-circuits the queue).
        var gate = new TaskCompletionSource();
        var beforeResolveForA = 0;
        var router = new Router(RouterHistory.CreateMemory(), Routes());
        router.BeforeEach(async (to, _, _) =>
        {
            if (to.Path == "/a")
            {
                await gate.Task;
            }
            return NavigationGuardResult.Allow;
        });
        router.BeforeResolve((to, _, _) =>
        {
            if (to.Path == "/a")
            {
                beforeResolveForA++;
            }
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        var first = router.PushAsync("/a");
        var second = router.PushAsync("/b");
        gate.SetResult();
        await first;
        await second;

        beforeResolveForA.ShouldBe(0);
    }

    [Fact]
    public async Task Go_DrivesTheSameGuardPipelineAsPush_AndConfirmsOnAllow()
    {
        var seen = new List<string>();
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        await router.PushAsync("/a");
        await router.PushAsync("/b");
        router.BeforeEach((to, from, _) =>
        {
            seen.Add($"{from.Path}->{to.Path}");
            return Task.FromResult(NavigationGuardResult.Allow);
        });

        router.Go(-1);

        seen.ShouldBe(["/b->/a"]);
        router.CurrentRoute.Value.Path.ShouldBe("/a");
        history.Location.ShouldBe("/a");
    }

    [Fact]
    public void Go_WhenGuardAborts_RestoresTheUrl_AndLeavesTheRouteUntouched()
    {
        // An aborted popstate navigation restores the URL with a compensating history.go(-delta).
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        _ = router.PushAsync("/a");
        _ = router.PushAsync("/b");
        router.BeforeEach((_, _, _) => Task.FromResult(NavigationGuardResult.Abort));

        router.Go(-1);

        router.CurrentRoute.Value.Path.ShouldBe("/b");
        history.Location.ShouldBe("/b");
    }

    [Fact]
    public async Task Go_WhenGuardRedirects_RestoresThenNavigatesToTheRedirectTarget()
    {
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        await router.PushAsync("/a");
        await router.PushAsync("/b");
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/a" ? NavigationGuardResult.RedirectTo("/") : NavigationGuardResult.Allow));

        router.Go(-1);

        // The pop to /a is redirected: the popped URL is restored, then a fresh push lands on /.
        router.CurrentRoute.Value.Path.ShouldBe("/");
        history.Location.ShouldBe("/");
    }
}
