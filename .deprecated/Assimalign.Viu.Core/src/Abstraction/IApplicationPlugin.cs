using System.Threading.Tasks;

namespace Assimalign.Viu;

/// <summary>
/// A Viu plugin — the C# port of upstream's object <c>Plugin</c> form
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>, https://vuejs.org/guide/reusability/plugins.html).
/// Recorded through <see cref="IApplication.Use(IApplicationPlugin)"/>, which installs it exactly once
/// per plugin instance (a repeat <c>Use</c> of the same instance is deduplicated with a dev warning,
/// upstream parity). A plugin registers components, directives, and provides through the
/// <see cref="IApplication"/> it receives; upstream's <c>options</c> argument is carried by the plugin's
/// own constructor state instead.
/// <para>
/// Installation is <b>asynchronous</b> — <see cref="InstallAsync"/> is awaited inside the mount path
/// (upstream: plugins install synchronously during <c>createApp</c>, but Viu defers install into
/// <c>MountAsync</c> so a plugin may await work — module imports, a first data load — before the
/// initial render). The documented mount order is: services frozen → plugins install → platform
/// initialization → render.
/// </para>
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
    /// Installs the plugin into <paramref name="application"/> (upstream: <c>plugin.install(app)</c>),
    /// awaited during the mount path before platform initialization and the first render.
    /// </summary>
    /// <param name="application">The application to extend.</param>
    /// <returns>A task that completes when the plugin has finished installing.</returns>
    ValueTask InstallAsync(IApplication application);
}
