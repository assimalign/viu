using System;

using Assimalign.Viu;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>
/// A host-agnostic server-rendering application — the C# port of the app object
/// <c>createSSRApp(RootComponent)</c> produces (<c>@vue/runtime-core</c>'s <c>createAppAPI</c>,
/// https://vuejs.org/api/application.html), pared to what server rendering consumes: a root component,
/// its root props, and the app-level registries and provides every descendant resolves against.
/// <para>
/// This is deliberately <b>not</b> the DOM-bound <see cref="Application{TNode}"/>: that type is
/// inseparable from a DOM renderer (it is generic over the platform node and owns a
/// <c>Renderer&lt;TNode&gt;</c>), whereas founding decision 7 requires the server renderer to run on a
/// plain .NET host with no DOM/interop dependency. So the SSR app carries the same
/// component/directive/provide surface over the shared <see cref="ApplicationContext"/> without the
/// renderer. Per-request discipline is the caller's: construct a fresh <see cref="ServerApplication"/>
/// per request so no reactive state crosses requests (the host DI wiring is the server adaptor's
/// concern, [V01.01.07.04]).
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ServerApplication
{
    private readonly ApplicationContext _context = new();

    /// <summary>Creates a server app for <paramref name="rootComponent"/> with optional root props.</summary>
    /// <param name="rootComponent">The root component definition (upstream: <c>createSSRApp</c>'s argument).</param>
    /// <param name="rootProperties">The props passed to the root component, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rootComponent"/> is null.</exception>
    public ServerApplication(IComponentDefinition rootComponent, VirtualNodeProperties? rootProperties = null)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        RootComponent = rootComponent;
        RootProperties = rootProperties;
    }

    /// <summary>The root component definition.</summary>
    public IComponentDefinition RootComponent { get; }

    /// <summary>The root component's props, or null.</summary>
    public VirtualNodeProperties? RootProperties { get; }

    /// <summary>
    /// The app-level configuration (upstream: <c>app.config</c>) — the error and warn handlers the SSR
    /// error path consults. Configure it before rendering.
    /// </summary>
    public ApplicationConfiguration Config => _context.Config;

    /// <summary>The shared application context threaded onto the root vnode at render.</summary>
    internal ApplicationContext Context => _context;

    /// <summary>
    /// Registers a component under <paramref name="name"/> so descendants resolve it by name, including
    /// <c>&lt;component :is&gt;</c> (upstream: <c>app.component(name, definition)</c>). Returns the app for
    /// chaining.
    /// </summary>
    /// <param name="name">The component name (resolved raw/camelCase/PascalCase at render).</param>
    /// <param name="definition">The component definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public ServerApplication Component(string name, IComponentDefinition definition)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(definition);
        _context.Components[name] = definition;
        return this;
    }

    /// <summary>
    /// Registers a directive under <paramref name="name"/> (upstream: <c>app.directive(name, directive)</c>).
    /// Returns the app for chaining. (Directive server-side props via <c>getSSRProps</c> are future work;
    /// registration is provided so name resolution succeeds.)
    /// </summary>
    /// <param name="name">The directive name.</param>
    /// <param name="directive">The directive definition.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directive"/> is null.</exception>
    public ServerApplication Directive(string name, IDirective directive)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(directive);
        _context.Directives[name] = directive;
        return this;
    }

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under the typed <paramref name="key"/> (upstream:
    /// <c>app.provide(key, value)</c>) — the final fallback in the inject lookup chain. Returns the app for
    /// chaining.
    /// </summary>
    /// <typeparam name="T">The provided value type.</typeparam>
    /// <param name="key">The identity-based key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public ServerApplication Provide<T>(InjectionKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _context.Provides[key] = value;
        return this;
    }

    /// <summary>
    /// Provides <paramref name="value"/> app-wide under a string <paramref name="key"/> (upstream:
    /// <c>app.provide</c> with a string key). Returns the app for chaining.
    /// </summary>
    /// <param name="key">The string key descendants inject with.</param>
    /// <param name="value">The value to provide.</param>
    /// <returns>This application, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null or empty.</exception>
    public ServerApplication Provide(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _context.Provides[key] = value;
        return this;
    }
}
