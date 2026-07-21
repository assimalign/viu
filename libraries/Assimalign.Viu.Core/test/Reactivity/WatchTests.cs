using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tests;

// Watch/WatchEffect semantics, mirroring Vue 3.5's apiWatch/baseWatch
// (https://vuejs.org/api/reactivity-core.html#watch and .../watchers.html). Run counts are pinned.
public sealed class WatchTests
{
    [Fact]
    public void Watch_Ref_FiresWithNewAndOldValue_NotImmediately()
    {
        var source = Reactive.Reference(1);
        var runs = 0;
        var lastNew = 0;
        var lastOld = 0;
        Reactive.Watch(source, (newValue, oldValue, _) =>
        {
            runs++;
            lastNew = newValue;
            lastOld = oldValue;
        });
        runs.ShouldBe(0); // not immediate

        source.Value = 2;
        runs.ShouldBe(1);
        lastNew.ShouldBe(2);
        lastOld.ShouldBe(1);

        // Equal value never reaches the watcher (the ref itself does not trigger).
        source.Value = 2;
        runs.ShouldBe(1);
    }

    [Fact]
    public void Watch_Getter_FiresWhenAnyReadValueChanges()
    {
        var first = Reactive.Reference(1);
        var second = Reactive.Reference(1);
        var runs = 0;
        Reactive.Watch(() => first.Value + second.Value, (_, _, _) => runs++);

        first.Value = 2;
        runs.ShouldBe(1);
        second.Value = 5;
        runs.ShouldBe(2);
    }

    [Fact]
    public void Watch_Immediate_FiresAtCreationWithDefaultOldValue()
    {
        var source = Reactive.Reference(5);
        var news = new List<int>();
        var olds = new List<int>();
        Reactive.Watch(
            source,
            (newValue, oldValue, _) =>
            {
                news.Add(newValue);
                olds.Add(oldValue);
            },
            new WatchOptions { Immediate = true });

        news.ShouldBe(new[] { 5 });
        olds.ShouldBe(new[] { 0 }); // default(int) is the documented unset old value

        source.Value = 6;
        news.ShouldBe(new[] { 5, 6 });
        olds.ShouldBe(new[] { 0, 5 });
    }

    [Fact]
    public void Watch_Once_StopsAfterFirstCallback()
    {
        var source = Reactive.Reference(1);
        var runs = 0;
        var handle = Reactive.Watch(source, (_, _, _) => runs++, new WatchOptions { Once = true });

        source.Value = 2;
        runs.ShouldBe(1);
        handle.IsActive.ShouldBeFalse();

        source.Value = 3;
        runs.ShouldBe(1);
    }

    [Fact]
    public void Watch_OnCleanup_RunsBeforeNextCallbackAndOnStop()
    {
        // Canonical stale-request cleanup ordering. Immediate so the first callback (id=1) registers
        // its cleanup, which the id=2 change then runs before the next callback.
        var id = Reactive.Reference(1);
        var cleaned = new List<int>();
        var handle = Reactive.Watch(
            id,
            (newId, _, onCleanup) =>
            {
                var current = newId;
                onCleanup(() => cleaned.Add(current));
            },
            new WatchOptions { Immediate = true });

        id.Value = 2; // cleanup registered for id=1 runs before the id=2 callback
        cleaned.ShouldBe(new[] { 1 });

        id.Value = 3;
        cleaned.ShouldBe(new[] { 1, 2 });

        handle.Stop(); // the final cleanup (id=3) runs on stop
        cleaned.ShouldBe(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Watch_MultipleSources_PreservesPerSourceOldValues()
    {
        var first = Reactive.Reference(1);
        var second = Reactive.Reference(10);
        object?[]? lastNew = null;
        object?[]? lastOld = null;
        Reactive.Watch(
            new IReference[] { first, second },
            (newValues, oldValues, _) =>
            {
                lastNew = newValues;
                lastOld = oldValues;
            });

        first.Value = 2;
        lastNew.ShouldBe(new object?[] { 2, 10 });
        lastOld.ShouldBe(new object?[] { 1, 10 });

        second.Value = 20;
        lastNew.ShouldBe(new object?[] { 2, 20 });
        lastOld.ShouldBe(new object?[] { 2, 10 });
    }

    [Fact]
    public void Watch_Stop_EndsTheWatcher()
    {
        var source = Reactive.Reference(1);
        var runs = 0;
        var handle = Reactive.Watch(source, (_, _, _) => runs++);

        source.Value = 2;
        runs.ShouldBe(1);

        handle.Stop();
        handle.IsActive.ShouldBeFalse();
        source.Value = 3;
        runs.ShouldBe(1);
    }

    [Fact]
    public void Watch_RegistersWithActiveScope_AndStopsWithIt()
    {
        var source = Reactive.Reference(1);
        var runs = 0;
        var scope = Reactive.EffectScope();
        scope.Run(() => Reactive.Watch(source, (_, _, _) => runs++));

        source.Value = 2;
        runs.ShouldBe(1);

        scope.Stop();
        source.Value = 3;
        runs.ShouldBe(1);
    }

    [Fact]
    public void Watch_PauseAndResume_DefersCallbackDelivery()
    {
        var source = Reactive.Reference(1);
        var runs = 0;
        var handle = Reactive.Watch(source, (_, _, _) => runs++);

        handle.Pause();
        source.Value = 2;
        runs.ShouldBe(0); // deferred while paused

        handle.Resume();
        runs.ShouldBe(1); // one trailing delivery
    }

    [Fact]
    public void Watch_PreFlush_DelegatesToScheduler_AndDoesNotRunSynchronously()
    {
        var scheduler = new RecordingWatchScheduler();
        var source = Reactive.Reference(1);
        var log = new List<int>();
        Reactive.Watch(
            source,
            (newValue, _, _) => log.Add(newValue),
            new WatchOptions { Flush = WatchFlushMode.Pre, Scheduler = scheduler });

        source.Value = 2;
        log.ShouldBeEmpty(); // pre-flush: not synchronous
        scheduler.ScheduledJobs.Count.ShouldBe(1);
        scheduler.ScheduledJobs[0].Flush.ShouldBe(WatchFlushMode.Pre);

        scheduler.FlushAll();
        log.ShouldBe(new[] { 2 });
    }

    [Fact]
    public void Watch_PostFlush_SchedulesWithPostPhase()
    {
        var scheduler = new RecordingWatchScheduler();
        var source = Reactive.Reference(1);
        var log = new List<int>();
        Reactive.Watch(
            source,
            (newValue, _, _) => log.Add(newValue),
            new WatchOptions { Flush = WatchFlushMode.Post, Scheduler = scheduler });

        source.Value = 2;
        source.Value = 3;
        // Deduplicated to a single queued job (upstream scheduler dedup).
        scheduler.ScheduledJobs.Count.ShouldBe(1);
        scheduler.ScheduledJobs[0].Flush.ShouldBe(WatchFlushMode.Post);

        scheduler.FlushAll();
        log.ShouldBe(new[] { 3 });
    }

    [Fact]
    public void WatchEffect_RunsImmediately_AndReRunsOnChange()
    {
        var source = Reactive.Reference(1);
        var runs = 0;
        var seen = 0;
        Reactive.WatchEffect(() =>
        {
            runs++;
            seen = source.Value;
        });
        runs.ShouldBe(1); // immediate
        seen.ShouldBe(1);

        source.Value = 2;
        runs.ShouldBe(2);
        seen.ShouldBe(2);
    }

    [Fact]
    public void WatchEffect_OnCleanup_RunsBeforeReRunAndOnStop()
    {
        var source = Reactive.Reference(1);
        var cleaned = new List<int>();
        var handle = Reactive.WatchEffect(onCleanup =>
        {
            var current = source.Value;
            onCleanup(() => cleaned.Add(current));
        });
        cleaned.ShouldBeEmpty();

        source.Value = 2; // cleanup for the run that read 1 fires before the re-run
        cleaned.ShouldBe(new[] { 1 });

        handle.Stop(); // cleanup for the run that read 2 fires on stop
        cleaned.ShouldBe(new[] { 1, 2 });
    }
}

/// <summary>
/// A test <see cref="IWatchScheduler"/> that records queued jobs (deduplicated, as the runtime
/// scheduler does) and runs them on demand, so pre/post flush ordering can be asserted without the
/// runtime scheduler.
/// </summary>
internal sealed class RecordingWatchScheduler : IWatchScheduler
{
    public List<WatchJob> ScheduledJobs { get; } = new();

    public void Schedule(WatchJob job)
    {
        if (!ScheduledJobs.Contains(job))
        {
            ScheduledJobs.Add(job);
        }
    }

    public void FlushAll()
    {
        var jobs = ScheduledJobs.ToArray();
        ScheduledJobs.Clear();
        foreach (var job in jobs)
        {
            job.Invoke();
        }
    }
}
