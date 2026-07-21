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
/// <b>Services (bring-your-own DI, R5, [V01.01.03.24]).</b> The builder holds an
/// <see cref="IServiceProviderBuilder"/> (default: Core's AOT-safe <see cref="ServiceProviderBuilder"/>)
/// exposed as <see cref="Services"/>; a concrete <see cref="Build"/> calls
/// <see cref="BuildServiceProvider"/> and attaches the result to the application before
/// <see cref="ApplyConfiguration"/> runs, so a plugin install can already resolve from
/// <see cref="IApplication.Services"/>. Replace the default with a container adapter via
/// <see cref="UseServiceProviderBuilder"/>. Component-tree provide/inject stays untouched.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public abstract class ApplicationBuilder : IApplicationBuilder
{
    // The configuration recorded before Build, replayed onto the application in call order so plugin
    // installs and provides interleave exactly as upstream (createApp(root).use(...).provide(...)).
    private readonly List<Action<IApplication>> _configuration = [];
    // The bring-your-own DI registration surface (default: the Core factory-delegate builder). Built
    // and attached to the application by BuildServiceProvider at Build time; replaceable via
    // UseServiceProviderBuilder before Build.
    private IServiceProviderBuilder _services = new ServiceProviderBuilder();

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
    public IServiceProviderBuilder Services => _services;

    /// <inheritdoc/>
    public IApplicationBuilder UseServiceProviderBuilder(IServiceProviderBuilder services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder ConfigureServices(Action<IServiceProviderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_services);
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

    /// <summary>
    /// Builds the <see cref="IServiceProvider"/> from the active <see cref="Services"/> builder
    /// ([V01.01.03.24]). A concrete <see cref="Build"/> calls this once and attaches the result to its
    /// application (the internal <c>ApplicationContext.Services</c>) <b>before</b>
    /// <see cref="ApplyConfiguration"/>, so a plugin install can resolve from
    /// <see cref="IApplication.Services"/>. The application owns and disposes the returned provider.
    /// </summary>
    /// <returns>The application's service provider.</returns>
    protected IServiceProvider BuildServiceProvider() => _services.Build();
}
