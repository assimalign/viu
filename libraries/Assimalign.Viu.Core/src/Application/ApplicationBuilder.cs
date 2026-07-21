using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// The default <see cref="IApplicationBuilder"/> — records the root component/props and an ordered
/// list of configuration actions, then <see cref="ApplyConfiguration"/> replays them onto a
/// platform application. Platform packages derive a concrete builder that overrides
/// <see cref="Build"/> to construct their application type (e.g. the browser's
/// <c>BrowserApplicationBuilder</c>), call <see cref="ApplyConfiguration"/>, and return it.
/// <para>
/// Recording actions and replaying them in call order preserves exact upstream semantics: plugins
/// install in <c>Use</c> order, and a plugin's install may itself call
/// <see cref="IApplication.Provide{T}(InjectionKey{T}, T)"/>/<see cref="IApplication.Component"/> —
/// all interleaved exactly as if configured directly on the application. The builder performs no
/// interop and holds no renderer, so it is trimming- and WASM/NativeAOT-safe.
/// </para>
/// <para>
/// <b>Reserved services seam (R5).</b> See <see cref="IApplicationBuilder"/>: bring-your-own DI over
/// <c>System.IServiceProvider</c> attaches to this builder in a later unit; R4 leaves the extension
/// point without implementing it.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public abstract class ApplicationBuilder : IApplicationBuilder
{
    // The configuration recorded before Build, replayed onto the application in call order so plugin
    // installs and provides interleave exactly as upstream (createApp(root).use(...).provide(...)).
    private readonly List<Action<IApplication>> _configuration = [];

    /// <summary>
    /// Initializes the builder for <paramref name="rootComponent"/> with optional root props.
    /// </summary>
    /// <param name="rootComponent">The root component the built application mounts.</param>
    /// <param name="rootProperties">The props passed to the root component, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    protected ApplicationBuilder(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        RootComponent = rootComponent;
        RootProperties = rootProperties;
    }

    /// <inheritdoc/>
    public IComponentDefinition RootComponent { get; }

    /// <inheritdoc/>
    public VirtualNodeProperties? RootProperties { get; }

    /// <inheritdoc/>
    public IApplicationBuilder Use(IPlugin plugin, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _configuration.Add(application => application.Use(plugin, options));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder Provide<T>(InjectionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _configuration.Add(application => application.Provide(key, value));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder Provide(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _configuration.Add(application => application.Provide(key, value));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder Component(string name, IComponentDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(definition);
        _configuration.Add(application => application.Component(name, definition));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder Directive(string name, IDirective directive)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(directive);
        _configuration.Add(application => application.Directive(name, directive));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder ConfigureApplication(Action<ApplicationConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configuration.Add(application => configure(application.Config));
        return this;
    }

    /// <inheritdoc/>
    public abstract IApplication Build();

    /// <summary>
    /// Applies every recorded configuration action to <paramref name="application"/> in the order it
    /// was recorded. A concrete <see cref="Build"/> calls this after constructing its application.
    /// </summary>
    /// <param name="application">The freshly constructed application to configure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="application"/> is null.</exception>
    protected void ApplyConfiguration(IApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        foreach (var configure in _configuration)
        {
            configure(application);
        }
    }
}
