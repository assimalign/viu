using System;
using System.Threading.Tasks;

using Assimalign.Viu;

namespace Assimalign.Viu.Testing;

internal sealed class ScheduledFlush : IDisposable
{
    private readonly TestSchedulerPump _pump;
    private readonly TestRenderer _renderer;
    private bool _isDisposed;

    internal ScheduledFlush(TestSchedulerPump pump, TestRenderer renderer)
    {
        _pump = pump;
        _renderer = renderer;
    }

    internal Task RunAsync() => RunAsync(static () => Task.CompletedTask);

    internal Task RunAsync(Func<Task> action)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            _renderer.Run(action);
            Task tick = Scheduler.NextTickAsync();
            _pump.RunUntilIdle();
            _renderer.Pump(tick);
            _renderer.Drain();
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _pump.Dispose();
        }
        finally
        {
            _isDisposed = true;
            _renderer.Dispose();
        }
    }
}
