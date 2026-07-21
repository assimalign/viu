using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// A Viu plugin — the C# port of upstream's object <c>Plugin</c> form
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>, https://vuejs.org/guide/reusability/plugins.html).
/// Installed through <see cref="IApplication.Use(IApplicationPlugin, object?)"/>, which invokes
/// <see cref="Install"/> exactly once per plugin instance (a repeat <c>Use</c> of the same instance
/// is deduplicated with a dev warning, upstream parity). A plugin registers components, directives,
/// and provides through the <see cref="IApplication"/> it receives.
/// <para>
/// The contract is platform-neutral — a plugin extends the app through the node-type-agnostic
/// <see cref="IApplication"/> surface, so the same plugin installs on a browser app, a server app, or
/// any custom-renderer app. Plugins are explicit objects, never discovered by reflection or assembly
/// scanning (AOT/trimming contract).
/// </para>
/// </summary>
public interface IApplicationPlugin
{
    /// <summary>
    /// Installs the plugin into <paramref name="application"/> (upstream: <c>plugin.install(app, options)</c>).
    /// </summary>
    /// <param name="application">The application to extend.</param>
    ValueTask InstallAsync(IApplication application);
}