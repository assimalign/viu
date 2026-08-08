using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Provides full-lifetime execution for persistent Viu applications.</summary>
public static class ApplicationExtensions
{
    /// <param name="application">The persistent application to execute.</param>
    extension(IApplication application)
    {
        /// <summary>
        /// Starts the application, waits for its shared stopping signal, and then waits for
        /// reverse-order cleanup.
        /// </summary>
        /// <param name="cancellationToken">Requests graceful shutdown.</param>
        /// <returns>A task spanning startup, the mounted lifetime, and cleanup.</returns>
        /// <remarks>Specified by <c>[APP-4]</c>, <c>[APP-5]</c>, and <c>[APP-7]</c>.</remarks>
        public async ValueTask RunAsync(CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(application);
            try
            {
                await application.StartAsync(cancellationToken).ConfigureAwait(false);
                await WaitForStoppingAsync(application.Context.Stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (application.Context.Stopping.IsCancellationRequested)
            {
            }
            finally
            {
                await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static async Task WaitForStoppingAsync(CancellationToken stopping)
    {
        if (stopping.IsCancellationRequested)
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = stopping.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            completion);
        await completion.Task.ConfigureAwait(false);
    }
}
