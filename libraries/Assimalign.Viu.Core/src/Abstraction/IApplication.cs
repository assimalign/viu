using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// Represents the platform-neutral lifetime of a persistent Viu host.
/// </summary>
/// <remarks>
/// Runtime middleware surrounds the complete mounted lifetime, while composition remains frozen in
/// <see cref="Context"/>. Mount targets remain on <see cref="IApplication{TNode}"/> so this contract
/// does not acquire a Browser or future WebView dependency. Not thread-safe. Specified by
/// <c>[APP-1]</c> through <c>[APP-7]</c> and delivered by <c>[V01.01.14.07]</c>.
/// </remarks>
public interface IApplication : IAsyncDisposable
{
    /// <summary>Gets the immutable application composition context.</summary>
    IApplicationContext Context { get; }

    /// <summary>
    /// Gets whether the single application execution is in its Running state, before graceful
    /// stopping has begun.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>Appends middleware around the complete application execution.</summary>
    /// <param name="middleware">The runtime middleware.</param>
    /// <returns>This application.</returns>
    /// <exception cref="InvalidOperationException">
    /// The application has already begun executing.
    /// </exception>
    IApplication Use(ApplicationMiddleware middleware);

    /// <summary>Runs the application once and completes after its mounted lifetime ends.</summary>
    /// <param name="cancellationToken">Requests graceful application shutdown.</param>
    /// <returns>A task that spans startup, the mounted lifetime, and cleanup.</returns>
    /// <exception cref="InvalidOperationException">The application has already executed.</exception>
    ValueTask RunAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests graceful shutdown and waits for application cleanup.</summary>
    /// <param name="cancellationToken">
    /// Cancels only this caller's wait; application cleanup continues.
    /// </param>
    /// <returns>A task that completes after application cleanup.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
