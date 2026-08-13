using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Represents the platform-neutral lifetime of one persistent Viu host.</summary>
/// <remarks>
/// Runtime middleware surrounds the complete mounted lifetime. Host packages supply the terminal
/// mount operation without adding a host-node type to this contract. The lifetime is single-use and
/// single-threaded. Specified by <c>[APP-1]</c> through <c>[APP-7]</c>.
/// </remarks>
public interface IApplication : IAsyncDisposable
{
    /// <summary>Gets the frozen application composition and observable lifetime state.</summary>
    IApplicationContext Context { get; }

    /// <summary>Appends middleware around the complete application execution.</summary>
    /// <param name="middleware">The middleware to append without deduplication.</param>
    /// <returns>This application.</returns>
    /// <exception cref="InvalidOperationException">Execution has already begun.</exception>
    IApplication Use(ApplicationMiddleware middleware);

    /// <summary>
    /// Starts the application exactly once and returns after the host terminal signals that its
    /// root has mounted. An already-cancelled token is observed after the single-use claim.
    /// </summary>
    /// <param name="cancellationToken">Requests graceful shutdown.</param>
    /// <returns>A task representing startup through the running signal.</returns>
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Requests graceful shutdown and waits for lifetime cleanup.</summary>
    /// <param name="cancellationToken">Cancels this caller's wait, never shared cleanup.</param>
    /// <returns>A task representing cleanup completion.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
