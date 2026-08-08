using System;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// Composes a host-neutral server-render application through options frozen at build time.
/// </summary>
/// <remarks>
/// The builder borrows every configured dependency and never disposes one. It creates a plain
/// per-render composition rather than a persistent application lifetime. Specified by
/// <c>[APP-2]</c>, <c>[APP-6]</c>, and <c>[SSR-2]</c>.
/// </remarks>
public sealed class ServerApplicationBuilder
{
    private readonly ApplicationOptions _options = new();

    /// <summary>Applies application composition and diagnostics immediately.</summary>
    /// <param name="configure">The options mutation to snapshot during <see cref="Build"/>.</param>
    /// <returns>This builder for continued composition.</returns>
    /// <remarks>Composition remains separate from rendering under <c>[APP-2]</c>.</remarks>
    public ServerApplicationBuilder ConfigureApplication(Action<ApplicationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options);
        return this;
    }

    /// <summary>Freezes the current options into a server-render application.</summary>
    /// <returns>An immutable per-render composition object.</returns>
    /// <exception cref="InvalidOperationException">No root virtual node was configured.</exception>
    /// <remarks>Specified by <c>[APP-2]</c> and <c>[SSR-2]</c>.</remarks>
    public ServerRenderApplication Build() =>
        new(new ApplicationContext(_options));
}
