using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentLifecycleTests
{
    [Fact]
    public void InvokeEveryOrdinaryPhase_SynchronousCallbacksRunInPhaseOrder()
    {
        // The named phase surface and per-phase registration order are fixed by [CMP-20].
        using ComponentLifecycle lifecycle = new();
        List<string> order = [];
        lifecycle.OnBeforeMount(() => order.Add("before-mount"));
        lifecycle.OnMounted(() => order.Add("mounted"));
        lifecycle.OnBeforeUpdate(() => order.Add("before-update"));
        lifecycle.OnUpdated(() => order.Add("updated"));
        lifecycle.OnBeforeUnmount(() => order.Add("before-unmount"));
        lifecycle.OnUnmounted(() => order.Add("unmounted"));
        lifecycle.OnActivated(() => order.Add("activated"));
        lifecycle.OnDeactivated(() => order.Add("deactivated"));

        lifecycle.InvokeBeforeMount();
        lifecycle.InvokeMounted();
        lifecycle.InvokeBeforeUpdate();
        lifecycle.InvokeUpdated();
        lifecycle.InvokeBeforeUnmount();
        lifecycle.InvokeUnmounted();
        lifecycle.InvokeActivated();
        lifecycle.InvokeDeactivated();

        order.ShouldBe(
        [
            "before-mount",
            "mounted",
            "before-update",
            "updated",
            "before-unmount",
            "unmounted",
            "activated",
            "deactivated",
        ]);
    }

    [Fact]
    public async Task InvokeMounted_MixedCallbackShapes_StartsInRegistrationOrderWithoutAwaiting()
    {
        using ComponentLifecycle lifecycle = new();
        List<string> order = [];
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lifecycle.OnMounted(() => order.Add("sync"));
        lifecycle.OnMounted(async () =>
        {
            order.Add("async-start");
            await completion.Task;
            order.Add("async-end");
        });
        lifecycle.OnMounted(cancellationToken =>
        {
            cancellationToken.ShouldBe(lifecycle.CancellationToken);
            order.Add("token");
            return Task.CompletedTask;
        });

        lifecycle.InvokeMounted();

        order.ShouldBe(["sync", "async-start", "token"]);
        completion.SetResult();
        await lifecycle.DrainAsync();
        order.ShouldBe(["sync", "async-start", "token", "async-end"]);
    }

    [Fact]
    public async Task InvokeUpdated_AsynchronousFailures_RoutesEachFaultExactlyOnce()
    {
        using ComponentLifecycle lifecycle = new();
        List<string> failures = [];
        lifecycle.SetObservedTaskFaultHandler(
            (exception, diagnosticInformation) =>
                failures.Add($"{exception.Message}:{diagnosticInformation}"));
        lifecycle.OnUpdated((Func<Task>)(() => throw new InvalidOperationException("factory")));
        lifecycle.OnUpdated(() => (Task)null!);
        lifecycle.OnUpdated(() => Task.FromException(new InvalidOperationException("task")));
        lifecycle.OnUpdated((Action)(() => throw new InvalidOperationException("sync")));

        lifecycle.InvokeUpdated();
        await lifecycle.DrainAsync();
        await lifecycle.DrainAsync();

        failures.Count.ShouldBe(4);
        failures.ShouldContain(entry => entry.StartsWith("factory:", StringComparison.Ordinal));
        failures.ShouldContain(entry => entry.StartsWith(
            "An asynchronous lifecycle callback returned a null task.:",
            StringComparison.Ordinal));
        failures.ShouldContain(entry => entry.StartsWith("task:", StringComparison.Ordinal));
        failures.ShouldContain(entry => entry.StartsWith("sync:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancel_CancellableCallbackStopsNormallyWithoutFaultRouting()
    {
        using ComponentLifecycle lifecycle = new();
        int failures = 0;
        bool receivedLifetimeToken = false;
        lifecycle.SetObservedTaskFaultHandler((_, _) => failures++);
        lifecycle.OnMounted(async cancellationToken =>
        {
            receivedLifetimeToken = cancellationToken == lifecycle.CancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });

        lifecycle.InvokeMounted();
        lifecycle.Cancel();
        await lifecycle.DrainAsync();

        receivedLifetimeToken.ShouldBeTrue();
        lifecycle.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        failures.ShouldBe(0);
    }

    [Fact]
    public async Task InvokeServerPrefetchAsync_Callbacks_AwaitsSequentiallyInRegistrationOrder()
    {
        using ComponentLifecycle lifecycle = new();
        List<string> order = [];
        TaskCompletionSource firstCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lifecycle.OnServerPrefetch(async () =>
        {
            order.Add("first-start");
            await firstCompletion.Task;
            order.Add("first-end");
        });
        lifecycle.OnServerPrefetch(() =>
        {
            order.Add("second");
            return Task.CompletedTask;
        });

        Task invocation = lifecycle.InvokeServerPrefetchAsync();
        order.ShouldBe(["first-start"]);
        invocation.IsCompleted.ShouldBeFalse();

        firstCompletion.SetResult();
        await invocation;

        order.ShouldBe(["first-start", "first-end", "second"]);
    }

    [Fact]
    public void InvokeErrorCaptured_FalseResult_StopsLaterCallbacks()
    {
        using ComponentLifecycle lifecycle = new();
        int calls = 0;
        lifecycle.OnErrorCaptured((_, _, _) =>
        {
            calls++;
            return false;
        });
        lifecycle.OnErrorCaptured((_, _, _) =>
        {
            calls++;
            return true;
        });

        bool shouldContinue = lifecycle.InvokeErrorCaptured(
            new InvalidOperationException("failure"),
            null,
            "render");

        shouldContinue.ShouldBeFalse();
        calls.ShouldBe(1);
    }

    [Fact]
    public void Dispose_CancelsLifetimeAndRejectsLaterRegistration()
    {
        ComponentLifecycle lifecycle = new();

        lifecycle.Dispose();
        lifecycle.Dispose();

        lifecycle.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => lifecycle.OnMounted(() => { }));
    }

    [Fact]
    public async Task Dispose_PendingOrdinaryTask_RetainsItsInstalledFaultRouteUntilCompletion()
    {
        ComponentLifecycle lifecycle = new();
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> failures = [];
        lifecycle.SetObservedTaskFaultHandler(
            (exception, _) => failures.Add(exception.Message));
        lifecycle.OnMounted(async () =>
        {
            await completion.Task;
            throw new InvalidOperationException("late failure");
        });
        lifecycle.InvokeMounted();

        lifecycle.Dispose();
        completion.SetResult();
        await lifecycle.DrainAsync();

        failures.ShouldBe(["late failure"]);
    }
}
