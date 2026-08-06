using System;
using System.Threading;

namespace Assimalign.Viu;

/// <summary>Describes one execution of a persistent application host.</summary>
/// <remarks>
/// The stopping token is shared by middleware and host startup so cancellation and
/// <see cref="IApplication.StopAsync(CancellationToken)"/> request the same graceful cleanup path.
/// Specified by <c>[APP-5]</c>.
/// </remarks>
public sealed class ApplicationExecutionContext
{
    internal ApplicationExecutionContext(
        IApplication application,
        CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(application);
        Application = application;
        Stopping = stopping;
    }

    /// <summary>Gets the application whose lifetime is executing.</summary>
    public IApplication Application { get; }

    /// <summary>Gets the token that signals graceful application shutdown.</summary>
    public CancellationToken Stopping { get; }
}
