using Assimalign.Viu;

namespace Assimalign.Viu.Router;

/// <summary>
/// Resolves the active <see cref="Router"/> for a router component's <c>Setup</c> —
/// <b>service-first-then-provide</b> ([V01.01.03.24]). A router registered through the application
/// service provider (via <see cref="RouterServiceBuilderExtensions.AddRouter"/> /
/// <c>Services.AddSingleton(router)</c>) is returned first; otherwise the app-wide provide under
/// <see cref="RouterInjectionKeys.Router"/> is used (the original path). A provide-only app therefore
/// resolves exactly as before — the service probe simply misses and falls through — so
/// <see cref="RouterView"/>/<see cref="RouterLink"/>/<see cref="RouterGuards"/> behavior and tests are
/// unchanged, while an app configured through services resolves the same router without an app-wide
/// provide. Internal.
/// </summary>
internal static class RouterResolution
{
    /// <summary>
    /// Returns the router for the current component context, or null when none is registered by either
    /// path. When the service probe misses, this falls through to
    /// <see cref="DependencyInjection.Inject{T}(InjectionKey{T})"/>, whose own dev "injection not found"
    /// warning stands in on a true miss (upstream parity with the previous inject-only resolution).
    /// </summary>
    /// <returns>The active router, or null.</returns>
    internal static Router? Resolve()
    {
        var fromServices = ComponentInstance.Current?.Services?.GetService<Router>();
        return fromServices ?? DependencyInjection.Inject(RouterInjectionKeys.Router);
    }
}
