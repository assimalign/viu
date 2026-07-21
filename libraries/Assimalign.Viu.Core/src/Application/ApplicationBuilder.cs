using System;
using System.Collections.Generic;

namespace Assimalign.Viu;

/// <summary>
/// The default <see cref="IApplicationBuilder"/> — records the root component/props and an ordered
/// list of configuration actions, then <see cref="ApplyConfiguration"/> replays them onto a
/// platform application. Platform packages derive a concrete builder that overrides
/// <see cref="Build"/> to construct their application type (e.g. the browser's
/// <c>BrowserApplicationBuilder</c>), attach the service provider, call <see cref="ApplyConfiguration"/>,
/// and return it.
/// <para>
/// Recording actions and replaying them in call order preserves exact upstream semantics: provides,
/// registrations, and config callbacks apply in order, and recorded plugins are queued on the
/// application (their asynchronous <see cref="IApplicationPlugin.InstallAsync"/> runs later, during the
/// mount path — <see cref="Application{TNode}.Use(IApplicationPlugin)"/>). The builder performs no
/// interop and holds no renderer, so it is trimming- and WASM/NativeAOT-safe.
/// </para>
/// <para>
/// <b>Services (bring-your-own DI, R5, [V01.01.03.24]).</b> The builder holds an
/// <see cref="IServiceContainer"/> (default: Core's AOT-safe <see cref="ServiceContainer"/>) exposed as
/// <see cref="Services"/>; a concrete <see cref="Build"/> calls <see cref="BuildServiceProvider"/> once
/// and attaches the result to the application's <see cref="IApplicationContext.ServicesProvider"/>
/// before <see cref="ApplyConfiguration"/> runs. Replace the default with a container adapter via
/// <see cref="UseServiceContainer"/>. Component-tree provide/inject stays untouched.
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public abstract class ApplicationBuilder : IApplicationBuilder
{
    // The configuration recorded before Build, replayed onto the application in call order so provides,
    // registrations, config callbacks, and queued plugins interleave exactly as upstream.
    private readonly List<Action<IApplication>> _configuration = [];
    // The bring-your-own DI registration surface (default: the Core factory-delegate container). Built
    // and attached to the application by BuildServiceProvider at Build time; replaceable via
    // UseServiceContainer before Build.
    private IServiceContainer _services = new ServiceContainer();

    /// <summary>
    /// Initializes the builder for <paramref name="rootComponent"/> with optional root props.
    /// </summary>
    /// <param name="rootComponent">The root component the built application mounts.</param>
    /// <param name="rootProperties">The props passed to the root component, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    protected ApplicationBuilder(IComponent rootComponent, VirtualNodeProperties? rootProperties)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        RootComponent = rootComponent;
        RootProperties = rootProperties;
    }

    /// <summary>The root component the built application mounts.</summary>
    public IComponent RootComponent { get; }

    /// <summary>The props passed to the root component, or null.</summary>
    public VirtualNodeProperties? RootProperties { get; }

    /// <inheritdoc/>
    public IServiceContainer Services => _services;

    /// <inheritdoc/>
    public IApplicationBuilder Use(IApplicationPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        _configuration.Add(application => application.Use(plugin));
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
    public IApplicationBuilder Component(string name, IComponent definition)
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
    public IApplicationBuilder ConfigureApplication(Action<IApplicationContext> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configuration.Add(application => configure(application.Context));
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder UseServiceContainer(IServiceContainer services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        return this;
    }

    /// <inheritdoc/>
    public IApplicationBuilder ConfigureServices(Action<IServiceContainer> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_services);
        return this;
    }

    /// <inheritdoc/>
    public abstract IApplication Build();

    /// <summary>
    /// Applies every recorded configuration action to <paramref name="application"/> in the order it
    /// was recorded. A concrete <see cref="Build"/> calls this after constructing its application and
    /// attaching the service provider.
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
    /// Builds the <see cref="IServiceProvider"/> from the active <see cref="Services"/> container
    /// exactly once ([V01.01.03.24]) — the container freezes, so a later
    /// <see cref="IServiceContainer.Add"/> throws. A concrete <see cref="Build"/> calls this once and
    /// attaches the result to its application's <see cref="IApplicationContext.ServicesProvider"/>
    /// <b>before</b> <see cref="ApplyConfiguration"/>, so a plugin install can resolve from the
    /// provider. The application owns and disposes the returned provider.
    /// </summary>
    /// <returns>The application's service provider.</returns>
    protected IServiceProvider BuildServiceProvider() => _services.Build();
}
