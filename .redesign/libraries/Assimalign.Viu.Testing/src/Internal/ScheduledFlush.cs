using System;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

internal sealed class ScheduledFlush : IDisposable
{
    private readonly TestSchedulerPump _pump;
    private bool _isDisposed;

    internal ScheduledFlush(TestSchedulerPump pump)
    {
        _pump = pump;
    }

    internal async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Task tick = Scheduler.NextTick();
        _pump.RunUntilIdle();
        await tick.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _pump.Dispose();
        _isDisposed = true;
    }
}
