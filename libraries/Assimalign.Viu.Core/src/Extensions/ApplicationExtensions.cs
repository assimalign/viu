using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Provides full-lifetime execution operations for persistent Viu applications.</summary>
public static class ApplicationExtensions
{
    /// <param name="application">The persistent application to execute.</param>
    extension(IApplication application)
    {
        /// <summary>
        /// Starts the application, waits for its shared stopping signal, and then awaits shutdown
        /// cleanup.
        /// </summary>
        /// <param name="cancellationToken">Requests graceful application shutdown.</param>
        /// <returns>A task that spans startup, the mounted lifetime, and reverse-order cleanup.</returns>
        /// <exception cref="InvalidOperationException">The application has already started.</exception>
        public async ValueTask RunAsync(CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(application);

            var taskCompletionSource = new TaskCompletionSource();
            using (cancellationToken.Register(() => taskCompletionSource.TrySetResult()))
            {
                try
                {
                    await application.StartAsync(cancellationToken).ConfigureAwait(false);
                    await taskCompletionSource.Task;   // <-- the "forever" await
                }
                catch (OperationCanceledException) when (application.Context.Stopping.IsCancellationRequested)
                {
                    
                }
                finally
                {
                    await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}
