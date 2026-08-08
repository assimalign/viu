using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

internal static class AsynchronousComponentDelay
{
    internal static IDisposable Schedule(int milliseconds, Action callback)
    {
        DelayTimer timer = new();
        timer.Start(milliseconds, callback);
        return timer;
    }

    private sealed class DelayTimer : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        internal void Start(int milliseconds, Action callback)
        {
            _ = RunAsync(milliseconds, callback, _cancellation.Token);
        }

        private static async Task RunAsync(
            int milliseconds,
            Action callback,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
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
