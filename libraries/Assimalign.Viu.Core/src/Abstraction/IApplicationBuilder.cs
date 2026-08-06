using System;

namespace Assimalign.Viu;

/// <summary>
/// Configures one application composition and builds its platform-selected persistent host.
/// </summary>
/// <remarks>
/// <see cref="ApplicationOptions"/> is the single composition surface. A build snapshots the
/// options into an immutable <see cref="IApplicationContext"/>. Specified by <c>[APP-2]</c>.
/// </remarks>
public interface IApplicationBuilder
{
    /// <summary>Configures the composition and diagnostics to snapshot at build time.</summary>
    /// <param name="configure">The options configuration action.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder ConfigureApplication(Action<ApplicationOptions> configure);

    /// <summary>Builds an application over the configured options snapshot.</summary>
    /// <returns>The built persistent application host.</returns>
    IApplication Build();
}
