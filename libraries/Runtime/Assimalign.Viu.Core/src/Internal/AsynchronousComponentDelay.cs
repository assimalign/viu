using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

internal static class AsynchronousComponentDelay
{
    internal static IDisposable Schedule(int milliseconds, Action callback)
    {
        DelayTimer timer = new();
        timer.Start(milliseconds, callback, Scheduler.CurrentTimeProvider);
        return timer;
    }

    private sealed class DelayTimer : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        internal void Start(
            int milliseconds,
            Action callback,
            TimeProvider timeProvider)
        {
            _ = RunAsync(milliseconds, callback, timeProvider, _cancellation.Token);
        }

        private static async Task RunAsync(
            int milliseconds,
            Action callback,
            TimeProvider timeProvider,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(milliseconds),
                    timeProvider,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                callback();
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
