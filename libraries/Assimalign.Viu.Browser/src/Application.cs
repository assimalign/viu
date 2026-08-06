using System.Runtime.Versioning;

namespace Assimalign.Viu.Browser;

/// <summary>
/// Selects the browser as the top-level Viu host without introducing a Browser dependency into
/// host-neutral Core.
/// </summary>
/// <remarks>
/// The returned builder targets <c>#app</c>. Custom selectors use the lower-level
/// <see cref="BrowserApplication.MountAsync(string, System.Threading.CancellationToken)"/> embedding
/// API, which bypasses top-level lifetime middleware. Specified by <c>[APP-7]</c>.
/// </remarks>
[SupportedOSPlatform("browser")]
public static class Application
{
    /// <summary>Creates a browser application builder targeting <c>#app</c>.</summary>
    /// <returns>A browser builder whose fluent composition methods preserve its concrete type.</returns>
    public static BrowserApplicationBuilder CreateBuilder()
    {
        return BrowserApplication.CreateBuilder();
    }
}
