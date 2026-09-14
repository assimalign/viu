using System;
using System.Threading;

namespace Assimalign.Viu;

/// <summary>Posts a clock deadline onto the owning scheduler without mutating a renderer on a timer thread.</summary>
internal sealed class SchedulerDelay : IDisposable
{
    private readonly ITimer _timer;
    private readonly SchedulerJob _job;

    internal SchedulerDelay(TimeProvider clock, int milliseconds, Action callback)
    {
        _job = new SchedulerJob(callback) { Name = "suspense timeout" };
        SynchronizationContext? context = SynchronizationContext.Current;
        ExecutionContext? execution = ExecutionContext.Capture();
        _timer = clock.CreateTimer(
            _ =>
            {
                void Queue() => Scheduler.QueueJob(_job);
                void Dispatch()
                {
                    if (context is null || ReferenceEquals(SynchronizationContext.Current, context))
                    {
                        Queue();
                    }
                    else
                    {
                        context.Post(_ => Queue(), null);
                    }
                }

                if (execution is null)
                {
                    Dispatch();
                }
                else
                {
                    ExecutionContext.Run(execution, _ => Dispatch(), null);
                }
            },
            null,
            TimeSpan.FromMilliseconds(milliseconds),
            Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _job.IsDisposed = true;
        _timer.Dispose();
    }
}
