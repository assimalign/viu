using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

internal static class StateStoreTestSupport
{
    internal static StateStoreRegistry CreateRegistry(
        IReactiveWatchScheduler? scheduler = null,
        IServiceProvider? services = null)
        => new(
            services,
            new ReactiveEffectScopeFactory(),
            scheduler);
}

internal sealed class TestReactiveWatchScheduler : IReactiveWatchScheduler
{
    private readonly Queue<WatchJob> _postFlush = new();
    private readonly Queue<WatchJob> _preFlush = new();
    private readonly HashSet<WatchJob> _queued =
        new(ReferenceEqualityComparer.Instance);

    internal int PendingCount => _queued.Count;

    internal int ScheduleCalls { get; private set; }

    public void Schedule(WatchJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ScheduleCalls++;
        if (!_queued.Add(job))
        {
            return;
        }

        Queue<WatchJob> queue =
            job.Flush == WatchFlushMode.Post
                ? _postFlush
                : _preFlush;
        queue.Enqueue(job);
    }

    internal void RunUntilIdle()
    {
        int passes = 0;
        while (_queued.Count > 0)
        {
            if (++passes > 100)
            {
                throw new InvalidOperationException(
                    "The test watch scheduler did not become idle.");
            }

            RunQueue(_preFlush);
            RunQueue(_postFlush);
        }
    }

    private void RunQueue(Queue<WatchJob> queue)
    {
        int count = queue.Count;
        for (int index = 0; index < count; index++)
        {
            WatchJob job = queue.Dequeue();
            _queued.Remove(job);
            job.Invoke();
        }
    }
}

internal sealed class CounterStore
{
    internal CounterStore()
    {
        Count = Reactive.Reference(0);
        Doubled = Reactive.Computed(() => Count.Value * 2);
        Reactive.Watch(
            () => Doubled.Value,
            (_, _, _) => WatcherRuns++);
    }

    internal Reference<int> Count { get; }

    internal Computed<int> Doubled { get; }

    internal int WatcherRuns { get; private set; }

    internal void Increment() => Count.Value++;
}

internal sealed class CounterState : IReactiveObject
{
    private readonly Dependency _countDependency = new();
    private readonly Dependency _stepDependency = new();
    private int _count;
    private int _step;

    internal int Count
    {
        get
        {
            _countDependency.Track();
            return _count;
        }
        set
        {
            if (_count == value)
            {
                return;
            }

            _count = value;
            _countDependency.Trigger();
        }
    }

    internal int Step
    {
        get
        {
            _stepDependency.Track();
            return _step;
        }
        set
        {
            if (_step == value)
            {
                return;
            }

            _step = value;
            _stepDependency.Trigger();
        }
    }

    public object ToRaw() => this;

    public Dependency? GetDependency(string propertyName)
        => propertyName switch
        {
            nameof(Count) => _countDependency,
            nameof(Step) => _stepDependency,
            _ => null,
        };

    public void Traverse(ReactiveTraversal traversal)
    {
        ArgumentNullException.ThrowIfNull(traversal);
        traversal.Visit(Count);
        traversal.Visit(Step);
    }
}

internal sealed class ModelCounterStateStore : StateStore<CounterState>
{
    internal ModelCounterStateStore()
        : base(
            "model-counter",
            static () => new CounterState
            {
                Count = 0,
                Step = 1,
            },
            static (target, source) =>
            {
                target.Count = source.Count;
                target.Step = source.Step;
            })
    {
        Doubled = Reactive.Computed(
            () =>
            {
                DoubledRuns++;
                return State.Count * 2;
            });
    }

    internal Computed<int> Doubled { get; }

    internal int DoubledRuns { get; private set; }

    internal void Increment()
        => RunAction(
            nameof(Increment),
            () => State.Count += State.Step);

    internal int IncrementBy(int amount)
        => RunAction(
            nameof(IncrementBy),
            () =>
            {
                State.Count += amount;
                return State.Count;
            });

    internal Task<int> IncrementByAsync(int amount)
        => RunActionAsync(
            nameof(IncrementByAsync),
            async () =>
            {
                await Task.Yield();
                State.Count += amount;
                return State.Count;
            });

    internal Task IncrementAsync()
        => RunActionAsync(
            nameof(IncrementAsync),
            async () =>
            {
                await Task.Yield();
                State.Count += State.Step;
            });

    internal void Explode()
        => RunAction(
            nameof(Explode),
            () => throw new InvalidOperationException("boom"));

    internal Task ExplodeAsync()
        => RunActionAsync(
            nameof(ExplodeAsync),
            async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async boom");
            });
}

internal sealed class NoResetStateStore : StateStore<CounterState>
{
    internal NoResetStateStore()
        : base(
            "no-reset",
            new CounterState
            {
                Count = 0,
                Step = 1,
            })
    {
    }
}
