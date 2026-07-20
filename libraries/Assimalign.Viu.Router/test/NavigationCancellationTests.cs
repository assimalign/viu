using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// Pins navigation supersession/cancellation and the popstate pipeline ([V01.01.08.04]) against
// vue-router (packages/router/src/router.ts: the pendingLocation cancel check and the popstate
// listener's compensating history.go;
// https://router.vuejs.org/guide/advanced/navigation-failures.html). A gated guard creates a genuine
// in-flight navigation so a later one can supersede it deterministically; all DOM-free with memory
// history.
public class NavigationCancellationTests
{
    private static IReadOnlyList<RouteRecord> Routes() =>
    [
        new RouteRecord("/", name: "home"),
        new RouteRecord("/a", name: "a"),
        new RouteRecord("/b", name: "b"),
    ];

    [Fact]
    public async Task Push_SupersededByLaterNavigation_IsCancelled_WhileTheLaterOneConfirms()
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

        var first = router.Push("/a");   // suspends in beforeEach awaiting the gate
        var second = router.Push("/b");  // supersedes the in-flight navigation, confirms synchronously
        gate.SetResult();                // releases the first navigation to resume and observe cancellation

        var firstFailure = await first;
        var secondFailure = await second;

        firstFailure.ShouldNotBeNull();
        firstFailure.Type.ShouldBe(NavigationFailureType.Cancelled);
        secondFailure.ShouldBeNull();
        router.CurrentRoute.Value.Path.ShouldBe("/b");
    }

    [Fact]
    public async Task SupersededNavigation_DoesNotRunItsRemainingGuards()
    {
        // The cancelled chain must not run further guards after supersession (vue-router's cancel
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

        var first = router.Push("/a");
        var second = router.Push("/b");
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
        await router.Push("/a");
        await router.Push("/b");
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
        // vue-router restores the URL after an aborted popstate with a compensating history.go(-delta).
        var history = RouterHistory.CreateMemory();
        var router = new Router(history, Routes());
        _ = router.Push("/a");
        _ = router.Push("/b");
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
        await router.Push("/a");
        await router.Push("/b");
        router.BeforeEach((to, _, _) => Task.FromResult(
            to.Path == "/a" ? NavigationGuardResult.RedirectTo("/") : NavigationGuardResult.Allow));

        router.Go(-1);

        // The pop to /a is redirected: the popped URL is restored, then a fresh push lands on /.
        router.CurrentRoute.Value.Path.ShouldBe("/");
        history.Location.ShouldBe("/");
    }
}
