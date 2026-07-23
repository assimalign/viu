using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>Performs application initialization before its first host render.</summary>
/// <remarks>
/// Plugins receive the already-composed application. They may initialize resources exposed by its
/// factory, service provider, state registry, or other application-owned collaborators, but Core
/// does not require those collaborators to expose mutable registration APIs. Component and
/// directive registrations for the built-in immutable resolvers are composed before the
/// application is built.
/// </remarks>
public interface IApplicationPlugin
{
    /// <summary>Installs the plugin once for the supplied application.</summary>
    /// <param name="application">The application being configured.</param>
    /// <param name="cancellationToken">Cancels asynchronous plugin initialization.</param>
    /// <returns>A task that completes when the plugin is installed.</returns>
    ValueTask InstallAsync(
        IApplication application,
        CancellationToken cancellationToken = default);
}
