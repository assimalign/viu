using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

internal static class StateStoreTestSupport
{
    internal static IComponentFactory Components { get; } =
        new ComponentFactory(Array.Empty<ComponentRegistration>());

    internal static IServiceProvider Services { get; } =
        new EmptyServiceProvider();

    internal static StateStoreRegistry CreateRegistry(
        IReactiveWatchScheduler? scheduler = null)
        => new(
            Components,
            Services,
            new ReactiveEffectScopeFactory(),
            scheduler);

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}

internal sealed class TestReactiveWatchScheduler : IReactiveWatchScheduler
{
    private readonly Queue<WatchJob> _preFlush = new();
    private readonly Queue<WatchJob> _postFlush = new();
    private readonly HashSet<WatchJob> _queued =
        new(ReferenceEqualityComparer.Instance);

    internal int ScheduleCalls { get; private set; }

    internal int PendingCount => _queued.Count;

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

[Reactive]
internal partial class CounterState
{
    internal partial int Count { get; set; }

    internal partial int Step { get; set; }
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
