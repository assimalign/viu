using System;

namespace Assimalign.Viu;

/// <summary>
/// The shared per-application context — the platform-neutral consolidation of everything an
/// application carries beside its runtime state: the root component and its props, the
/// bring-your-own dependency-injection provider, and the app-level configuration handlers that were
/// formerly a separate <c>ApplicationConfiguration</c> bag. It is the C# port of upstream's
/// <c>AppContext</c> + <c>app.config</c> (<c>packages/runtime-core/src/apiCreateApp.ts</c>,
/// https://vuejs.org/api/application.html#app-config), reduced to the surface a plugin or hosting
/// code reads through <see cref="IApplication.Context"/>.
/// <para>
/// Runtime state — <see cref="IApplication.IsMounted"/> and <see cref="IApplication.RootInstance"/> —
/// deliberately stays on <see cref="IApplication"/>, not here: the context is the app's
/// <i>configuration</i>, the application object is its <i>lifecycle</i>. Component-tree
/// <c>Provide</c>/<c>Inject</c> (the Vue-semantic feature) is independent of this context's
/// <see cref="ServicesProvider"/>. Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public interface IApplicationContext
{
    /// <summary>
    /// The root component the application mounts (upstream: the argument to <c>createApp</c>). Fixed
    /// at builder creation and read by the mount path to build the root virtual node.
    /// </summary>
    IComponent RootComponent { get; }

    /// <summary>The props passed to the root component, or null.</summary>
    VirtualNodeProperties? RootProperties { get; }

    /// <summary>
    /// The application's bring-your-own dependency-injection provider ([V01.01.03.24]) — the
    /// <see cref="System.IServiceProvider"/> the <see cref="IApplicationBuilder"/> built from its
    /// <see cref="IApplicationBuilder.Services"/> registrations and attached at
    /// <see cref="IApplicationBuilder.Build"/>, reachable from component <c>Setup</c> through
    /// <see cref="ComponentInstance.Services"/> and the <see cref="DependencyInjection.GetService{T}()"/>
    /// composition functions. Null when the application was created without a builder (a raw
    /// renderer-created app). The application owns this provider and disposes it (if
    /// <see cref="System.IDisposable"/>) when it disposes.
    /// <para>
    /// This is app-level DI over <see cref="System.IServiceProvider"/>, layered <b>beside</b> — never
    /// replacing — the Vue-semantic component-tree provide/inject chain (<see cref="DependencyInjection"/>).
    /// </para>
    /// </summary>
    IServiceProvider? ServicesProvider { get; }

    /// <summary>
    /// The app-level error handler (upstream: <c>app.config.errorHandler</c>,
    /// https://vuejs.org/api/application.html#app-config-errorhandler). Receives
    /// <c>(exception, instance, info)</c> for errors from render, lifecycle hooks, and event
    /// handlers that no ancestor's <c>OnErrorCaptured</c> hook stopped. When set it is the
    /// terminal sink — the error is delivered here instead of rethrown; when null, an unhandled
    /// error rethrows to the host (crash loudly, unchanged behavior). The <c>info</c> string names
    /// the source (e.g. <c>"render function"</c>, <c>"Mounted hook"</c>).
    /// </summary>
    Action<Exception, ComponentInstance?, string>? ErrorHandler { get; set; }

    /// <summary>
    /// The app-level warning handler (upstream: <c>app.config.warnHandler</c>,
    /// https://vuejs.org/api/application.html#app-config-warnhandler). Intercepts dev warnings
    /// while the app is mounted; when null, warnings take their default route (a debug trace).
    /// Upstream also passes the instance and a component trace — Viu threads the message only,
    /// since the warning seam (<c>RuntimeWarnings.Sink</c>) is message-based.
    /// </summary>
    Action<string>? WarnHandler { get; set; }

    /// <summary>
    /// Whether to enable performance instrumentation (upstream: <c>app.config.performance</c>,
    /// https://vuejs.org/api/application.html#app-config-performance). Held for the instrumentation
    /// that lands with the devtools/performance work; toggling it has no effect yet.
    /// </summary>
    bool Performance { get; set; }
}
