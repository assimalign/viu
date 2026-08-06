using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// Represents the platform-neutral lifetime of a persistent Viu host.
/// </summary>
/// <remarks>
/// Runtime middleware surrounds the complete mounted lifetime, while composition remains frozen in
/// <see cref="Context"/>. Host-specific mount operations belong to their platform assemblies so this
/// contract does not acquire a Browser or future WebView dependency. Not thread-safe. Specified by
/// <c>[APP-1]</c> through <c>[APP-7]</c> and delivered by <c>[V01.01.14.08]</c>.
/// </remarks>
public interface IApplication : IAsyncDisposable
{
    /// <summary>Gets the immutable composition and observable runtime state for this application.</summary>
    IApplicationContext Context { get; }

    /// <summary>Appends middleware around the complete application execution.</summary>
    /// <param name="middleware">The runtime middleware.</param>
    /// <returns>This application.</returns>
    /// <exception cref="InvalidOperationException">
    /// The application has already begun executing.
    /// </exception>
    IApplication Use(ApplicationMiddleware middleware);

    /// <summary>
    /// Starts the application middleware pipeline once and returns after the host terminal has
    /// mounted and entered its Running state.
    /// </summary>
    /// <param name="cancellationToken">Requests graceful shutdown during or after startup.</param>
    /// <returns>A task that completes when startup succeeds or the pipeline ends before mounting.</returns>
    /// <exception cref="InvalidOperationException">The application has already started.</exception>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests graceful shutdown and waits for application cleanup.</summary>
    /// <param name="cancellationToken">
    /// Cancels only this caller's wait; application cleanup continues.
    /// </param>
    /// <returns>A task that completes after application cleanup.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
