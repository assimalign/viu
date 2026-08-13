using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Testing;

using static Assimalign.Viu.Router.Tests.RouterComponentsTestSupport;

namespace Assimalign.Viu.Router.Tests;

// Pins [RTR-8]: route factories resolve between record before-enter and component-associated
// before-enter guards, keep the current Testing-host tree unchanged while pending, cache success,
// and leave failures retryable.
public sealed class LazyRouteComponentTests
{
    [Fact]
    public async Task PushAsync_LazyFactoryPending_AwaitsInResolveStepBeforeConfirmAndRender()
    {
        List<string> order = [];
        TrackingComponent current = LabelView("current");
        TrackingComponent lazy = LabelView("lazy");
        TaskCompletionSource<ComponentNode> resolution = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int factoryRuns = 0;
        RouteRecord lazyRecord = RouteRecord.CreateLazy(
            "/lazy",
            _ =>
            {
                factoryRuns++;
                order.Add("resolve");
                return resolution.Task;
            },
            beforeEnter: (_, _, _) =>
            {
                order.Add("beforeEnter");
                return Task.FromResult(NavigationGuardResult.Allow);
            },
            routeEnterGuard: new RecordingEnterGuard(order));
        Router router = new(
            RouterHistory.CreateMemory(),
            [
                new RouteRecord("/current", component: current.Request),
                lazyRecord,
            ]);
        (await router.PushAsync("/current")).ShouldBeNull();
        using ComponentWrapper wrapper = MountView(router, current, lazy);
        router.BeforeResolve((_, _, _) =>
        {
            order.Add("beforeResolve");
            return Task.FromResult(NavigationGuardResult.Allow);
        });
        router.AfterEach((_, _, _) => order.Add("afterEach"));

        Task<NavigationFailure?> navigation = router.PushAsync("/lazy");

        navigation.IsCompleted.ShouldBeFalse();
        router.CurrentRoute.Value.Path.ShouldBe("/current");
        wrapper.Html().ShouldBe("<div class=\"current\">current</div>");
        order.ShouldBe(["beforeEnter", "resolve"]);

        resolution.SetResult(lazy.Request);
        (await navigation).ShouldBeNull();

        order.ShouldBe(
        [
            "beforeEnter",
            "resolve",
            "beforeRouteEnter",
            "beforeResolve",
            "afterEach",
        ]);
        await wrapper.NextTickAsync();
        wrapper.Html().ShouldBe("<div class=\"lazy\">lazy</div>");

        (await router.PushAsync("/current")).ShouldBeNull();
        (await router.PushAsync("/lazy")).ShouldBeNull();
        factoryRuns.ShouldBe(1);
    }

    [Fact]
    public async Task PushAsync_LazyFactoryFails_RoutesErrorAndLaterNavigationRetries()
    {
        TrackingComponent lazy = LabelView("lazy");
        InvalidOperationException expected = new("lazy route failed");
        int factoryRuns = 0;
        RouteRecord record = RouteRecord.CreateLazy(
            "/lazy",
            _ => ++factoryRuns == 1
                ? Task.FromException<ComponentNode>(expected)
                : Task.FromResult(lazy.Request));
        Router router = new(
            RouterHistory.CreateMemory(),
            [record]);
        List<Exception> errors = [];
        router.OnError((error, _, _) => errors.Add(error));

        InvalidOperationException thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => router.PushAsync("/lazy"));

        thrown.ShouldBeSameAs(expected);
        errors.ShouldBe([expected]);
        factoryRuns.ShouldBe(1);
        record.Component.ShouldBeNull();
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);

        (await router.PushAsync("/lazy")).ShouldBeNull();

        factoryRuns.ShouldBe(2);
        record.Component.ShouldBeSameAs(lazy.Request);
        router.CurrentRoute.Value.Path.ShouldBe("/lazy");
    }

    [Fact]
    public async Task PushAsync_LazyFactoryCancelled_ReturnsCancelledAndLaterNavigationRetries()
    {
        TrackingComponent lazy = LabelView("lazy");
        TaskCompletionSource<object?> entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int factoryRuns = 0;
        RouteRecord record = RouteRecord.CreateLazy(
            "/lazy",
            async cancellationToken =>
            {
                factoryRuns++;
                if (factoryRuns == 1)
                {
                    entered.SetResult(null);
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return lazy.Request;
            });
        using Router router = new(
            RouterHistory.CreateMemory(),
            [record]);
        List<Exception> errors = [];
        router.OnError((error, _, _) => errors.Add(error));
        using CancellationTokenSource cancellation = new();

        Task<NavigationFailure?> navigation = router.PushAsync(
            "/lazy",
            cancellation.Token);
        await entered.Task;
        cancellation.Cancel();
        NavigationFailure? failure = await navigation;

        failure.ShouldNotBeNull().Type.ShouldBe(NavigationFailureType.Cancelled);
        errors.ShouldBeEmpty();
        factoryRuns.ShouldBe(1);
        record.Component.ShouldBeNull();
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);

        (await router.PushAsync("/lazy")).ShouldBeNull();

        factoryRuns.ShouldBe(2);
        record.Component.ShouldBeSameAs(lazy.Request);
        router.CurrentRoute.Value.Path.ShouldBe("/lazy");
    }

    [Fact]
    public async Task PushAsync_LazyFactoryReturnsNull_RoutesErrorAndLaterNavigationRetries()
    {
        TrackingComponent lazy = LabelView("lazy");
        int factoryRuns = 0;
        RouteRecord record = RouteRecord.CreateLazy(
            "/lazy",
            _ => ++factoryRuns == 1
                ? Task.FromResult<ComponentNode>(null!)
                : Task.FromResult(lazy.Request));
        using Router router = new(
            RouterHistory.CreateMemory(),
            [record]);
        List<Exception> errors = [];
        router.OnError((error, _, _) => errors.Add(error));

        InvalidOperationException thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => router.PushAsync("/lazy"));

        thrown.Message.ShouldContain("returned null");
        errors.ShouldBe([thrown]);
        factoryRuns.ShouldBe(1);
        record.Component.ShouldBeNull();
        router.CurrentRoute.Value.ShouldBeSameAs(RouteLocation.Start);

        (await router.PushAsync("/lazy")).ShouldBeNull();

        factoryRuns.ShouldBe(2);
        record.Component.ShouldBeSameAs(lazy.Request);
    }

    private sealed class RecordingEnterGuard : IRouteEnterGuard
    {
        private readonly List<string> _order;

        internal RecordingEnterGuard(List<string> order)
        {
            _order = order;
        }

        public Task<NavigationGuardResult> BeforeRouteEnterAsync(
            RouteLocation to,
            RouteLocation from,
            System.Threading.CancellationToken cancellationToken)
        {
            _order.Add("beforeRouteEnter");
            return Task.FromResult(NavigationGuardResult.Allow);
        }
    }
}
