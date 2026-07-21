using System;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

/// <summary>
/// Drives the captured scheduler flush deterministically for a mounted wrapper — the bridge
/// between <see cref="TestSchedulerPump"/> (which captures scheduled flushes) and the awaitable
/// <c>NextTick</c> semantics (https://vuejs.org/api/general.html#nexttick). <see cref="RunAsync"/>
/// captures the pending flush's completion, pumps every captured flush to completion, then awaits —
/// so a caller awaiting <c>Trigger</c>/<c>SetValue</c>/<c>NextTickAsync</c> observes post-update
/// state without a wall-clock delay and without an ambient <c>SynchronizationContext</c>.
/// </summary>
internal sealed class ScheduledFlush : IDisposable
{
    private readonly TestSchedulerPump _pump;
    private bool _disposed;

    public ScheduledFlush(TestSchedulerPump pump)
    {
        _pump = pump;
    }

    public async Task RunAsync()
    {
        // Capture the pending flush's completion AFTER the caller queued its jobs, then drive every
        // captured flush (including ones queued while draining) to completion.
        var tick = Scheduler.NextTick();
        _pump.RunUntilIdle();
        await tick;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _pump.Dispose();
    }
}
