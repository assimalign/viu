using System;

using BenchmarkDotNet.Attributes;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Testing.Benchmarks;

/// <summary>
/// BenchmarkDotNet timings for the reactivity hot paths the architecture bets on — dependency
/// track/trigger, computed invalidation/recompute, effect trigger fan-out, batched-write coalescing, and
/// scheduler queue+flush (the version-counter/linked-list design of <c>@vue/reactivity</c>). Pure CoreCLR
/// with no browser or interop; wall-clock numbers are environment-relative, so they are reported as
/// artifacts, never gated. BenchmarkDotNet isolates each case in its own process, so the ambient static
/// reactivity/scheduler state does not leak between cases.
/// </summary>
[MemoryDiagnoser]
public class ReactivityBenchmarks
{
    private const int FanOutEffectCount = 100;
    private const int BatchedWriteCount = 50;
    private const int SchedulerJobCount = 100;

    private Reference<int> _reference = null!;
    private Reference<int> _computedChainHead = null!;
    private Computed<int> _computedTail = null!;
    private Reference<int> _fanOutReference = null!;
    private Reference<int> _batchReference = null!;
    private SchedulerJob[] _jobs = [];
    private TestSchedulerPump _pump = null!;
    private int _counter;
    private int _sink;
    private int _jobRuns;

    /// <summary>Builds the reactive graphs once per benchmark process.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // Single ref observed by one effect: writing it triggers exactly one synchronous re-run.
        _reference = Reactive.Reference(0);
        Reactive.Effect(() => _sink = _reference.Value);

        // A three-deep computed chain over a ref: a write invalidates lazily; reading the tail recomputes.
        var head = Reactive.Reference(0);
        var first = Reactive.Computed(() => head.Value + 1);
        var second = Reactive.Computed(() => first.Value + 1);
        _computedTail = Reactive.Computed(() => second.Value + 1);
        _computedChainHead = head;

        // One ref observed by many effects: a write fans the trigger out to every subscriber.
        _fanOutReference = Reactive.Reference(0);
        for (var index = 0; index < FanOutEffectCount; index++)
        {
            Reactive.Effect(() => _sink = _fanOutReference.Value);
        }

        // One ref observed by one effect, written many times inside a batch: the effect runs once.
        _batchReference = Reactive.Reference(0);
        Reactive.Effect(() => _sink = _batchReference.Value);

        // Reusable scheduler jobs and a pump that drains the queue deterministically off the JS event loop.
        _pump = TestSchedulerPump.Install();
        _jobs = new SchedulerJob[SchedulerJobCount];
        for (var index = 0; index < SchedulerJobCount; index++)
        {
            _jobs[index] = new SchedulerJob(() => _jobRuns++) { Identifier = index };
        }
    }

    /// <summary>Tears down the installed scheduler pump.</summary>
    [GlobalCleanup]
    public void Cleanup() => _pump.Dispose();

    /// <summary>The rawest path: write a ref, trigger its one effect, re-run it synchronously.</summary>
    /// <returns>The effect's captured value (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int DependencyTrackTrigger()
    {
        _reference.Value = _counter++;
        return _sink;
    }

    /// <summary>Invalidate a three-deep computed chain with a write, then recompute it with a read.</summary>
    /// <returns>The recomputed tail value.</returns>
    [Benchmark]
    public int ComputedChainRecompute()
    {
        _computedChainHead.Value = _counter++;
        return _computedTail.Value;
    }

    /// <summary>Write one ref observed by many effects, fanning the trigger out to all of them.</summary>
    /// <returns>The last effect's captured value.</returns>
    [Benchmark]
    public int EffectTriggerFanOut()
    {
        _fanOutReference.Value = _counter++;
        return _sink;
    }

    /// <summary>Coalesce many writes to one ref inside a batch into a single effect run.</summary>
    /// <returns>The effect's captured value.</returns>
    [Benchmark]
    public int BatchedWriteCoalescing()
    {
        Reactive.StartBatch();
        for (var index = 0; index < BatchedWriteCount; index++)
        {
            _batchReference.Value = _counter++;
        }
        Reactive.EndBatch();
        return _sink;
    }

    /// <summary>Queue many scheduler jobs and drain them in one coalesced flush.</summary>
    /// <returns>The number of flushes the pump ran.</returns>
    [Benchmark]
    public int SchedulerQueueAndFlush()
    {
        for (var index = 0; index < SchedulerJobCount; index++)
        {
            Scheduler.QueueJob(_jobs[index]);
        }
        return _pump.RunUntilIdle();
    }
}
