namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// A Viu plugin — the C# port of upstream's object <c>Plugin</c> form
/// (<c>packages/runtime-core/src/apiCreateApp.ts</c>, https://vuejs.org/guide/reusability/plugins.html).
/// Installed through <see cref="Application{TNode}.Use(IPlugin{TNode}, object?)"/>, which
/// invokes <see cref="Install"/> exactly once per plugin instance (a repeat <c>Use</c> of the same
/// instance is deduplicated with a dev warning, upstream parity). A plugin registers components,
/// directives, and provides through the app it receives. Plugins are explicit objects, never
/// discovered by reflection or assembly scanning (AOT/trimming contract).
/// </summary>
/// <typeparam name="TNode">The platform node type of the application being extended.</typeparam>
public interface IPlugin<TNode>
    where TNode : notnull
{
    /// <summary>
    /// Installs the plugin into <paramref name="application"/> (upstream: <c>plugin.install(app, options)</c>).
    /// </summary>
    /// <param name="application">The application to extend.</param>
    /// <param name="options">The options passed to <c>Use</c>, or null.</param>
    void Install(Application<TNode> application, object? options);
}
