using System;
using System.Runtime.CompilerServices;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the root render-effect contract of setupRenderEffect in @vue/runtime-core's renderer.ts
// (https://vuejs.org/guide/extras/rendering-mechanism.html): tracked render, scheduler-batched
// re-render, minimal patching, and leak-free teardown. Run counts are asserted throughout —
// values alone hide dependency-tracking bugs.
public class RenderEffectTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public RenderEffectTests()
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

    [Fact]
    public void CreateRenderEffect_MountsImmediately_AndTracksDependencies()
    {
        var message = Reactive.Reference("hello");
        var renders = 0;

        using var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renders++;
                return VirtualNodeFactory.Element("div", message.Value);
            },
            _container);

        renders.ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>hello</div></root>");
    }

    [Fact]
    public void Mutation_EnqueuesAScheduledUpdate_InsteadOfReRunningSynchronously()
    {
        var message = Reactive.Reference("one");
        var renders = 0;
        using var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renders++;
                return VirtualNodeFactory.Element("div", message.Value);
            },
            _container);

        message.Value = "two";

        // Not synchronous: the update waits for the flush.
        renders.ShouldBe(1);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>one</div></root>");

        _pump.RunUntilIdle();

        renders.ShouldBe(2);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>two</div></root>");
    }

    [Fact]
    public void MultipleMutationsInOneTurn_ProduceExactlyOneReRenderPerFlush()
    {
        var counter = Reactive.Reference(0);
        var renders = 0;
        using var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renders++;
                return VirtualNodeFactory.Element("div", counter.Value.ToString());
            },
            _container);

        counter.Value = 1;
        counter.Value = 2;
        counter.Value = 3;
        _pump.RunUntilIdle();

        renders.ShouldBe(2); // one mount + one batched update
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>3</div></root>");
    }

    [Fact]
    public void UntouchedDependencies_CauseNoReRender()
    {
        var tracked = Reactive.Reference("shown");
        var untracked = Reactive.Reference("never-read");
        var renders = 0;
        using var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renders++;
                return VirtualNodeFactory.Element("div", tracked.Value);
            },
            _container);

        untracked.Value = "changed";
        _pump.RunUntilIdle();

        renders.ShouldBe(1);
    }

    [Fact]
    public void Update_AppliesMinimalNodeOps()
    {
        var message = Reactive.Reference("a");
        using var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () => VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("id", "stable")), message.Value),
            _container);
        _renderer.OperationLog.Reset();

        message.Value = "b";
        _pump.RunUntilIdle();

        // The re-render diffs old vs new and lands one targeted text write — no structure,
        // no unchanged-prop patches.
        _renderer.OperationLog.Count(TestNodeOperationType.SetElementText).ShouldBe(1);
        _renderer.OperationLog.Count(TestNodeOperationType.PatchProperty).ShouldBe(0);
        _renderer.OperationLog.StructuralOperationCount.ShouldBe(0);
    }

    [Fact]
    public void ConditionalDependencies_AbandonedBranchStopsTriggering()
    {
        var useFirst = Reactive.Reference(true);
        var first = Reactive.Reference("first");
        var second = Reactive.Reference("second");
        var renders = 0;
        using var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renders++;
                return VirtualNodeFactory.Element("div", useFirst.Value ? first.Value : second.Value);
            },
            _container);

        useFirst.Value = false;
        _pump.RunUntilIdle();
        renders.ShouldBe(2);

        // The untaken branch no longer notifies (link-version cleanup through the effect).
        first.Value = "stale";
        _pump.RunUntilIdle();
        renders.ShouldBe(2);

        second.Value = "live";
        _pump.RunUntilIdle();
        renders.ShouldBe(3);
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>live</div></root>");
    }

    [Fact]
    public void Stop_DetachesTracking_AndSubsequentMutationsTriggerNothing()
    {
        var message = Reactive.Reference("alive");
        var renders = 0;
        var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () =>
            {
                renders++;
                return VirtualNodeFactory.Element("div", message.Value);
            },
            _container);

        renderEffect.Stop();
        renderEffect.IsActive.ShouldBeFalse();

        message.Value = "after-stop";
        _pump.RunUntilIdle();

        renders.ShouldBe(1);
        // Stop leaves the tree mounted (Unmount tears it down).
        TestNodeSerializer.Serialize(_container).ShouldBe("<root><div>alive</div></root>");
    }

    [Fact]
    public void Stop_LeavesNoDependencyRetention_EffectIsCollectible()
    {
        var message = Reactive.Reference("leak-check");
        var weakEffect = CreateStopAndRelease(message);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // The ref's dependency list no longer retains the stopped effect — no leaks.
        weakEffect.IsAlive.ShouldBeFalse();
    }

    [Fact]
    public void Unmount_StopsAndRemovesTheTree()
    {
        var message = Reactive.Reference("temp");
        var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () => VirtualNodeFactory.Element("div", message.Value),
            _container);

        renderEffect.Unmount();

        renderEffect.IsActive.ShouldBeFalse();
        TestNodeSerializer.Serialize(_container).ShouldBe("<root></root>");
    }

    [Fact]
    public void AThrowingFirstRender_LeavesNoLiveSubscriptions()
    {
        var message = Reactive.Reference("boom");
        var renders = 0;

        // Statement body so Shouldly binds the Action overload and runs on this thread.
        Should.Throw<InvalidOperationException>(() =>
        {
            _renderer.Renderer.CreateRenderEffect(
                () =>
                {
                    renders++;
                    _ = message.Value;
                    throw new InvalidOperationException("mount failed");
                },
                _container);
        });

        renders.ShouldBe(1);
        message.Value = "after";
        _pump.RunUntilIdle();
        renders.ShouldBe(1); // the failed effect was stopped; no re-run
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference CreateStopAndRelease(Reference<string> message)
    {
        var renderEffect = _renderer.Renderer.CreateRenderEffect(
            () => VirtualNodeFactory.Element("div", message.Value),
            _container);
        var weakEffect = new WeakReference(renderEffect.Effect);
        renderEffect.Stop();
        return weakEffect;
    }
}
