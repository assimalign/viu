using System;

namespace Assimalign.Viu;

/// <summary>Configures one application composition and builds its platform-selected host.</summary>
/// <remarks>
/// The builder exposes composition only; runtime behavior is registered on the built application.
/// Specified by <c>[APP-2]</c>.
/// </remarks>
public interface IApplicationBuilder
{
    /// <summary>Mutates the options that the next build snapshots.</summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This builder.</returns>
    IApplicationBuilder ConfigureApplication(Action<ApplicationOptions> configure);

    /// <summary>Builds the platform application from the current options snapshot.</summary>
    /// <returns>The built persistent application.</returns>
    IApplication Build();
}
