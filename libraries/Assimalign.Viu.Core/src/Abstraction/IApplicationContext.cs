using System;

namespace Assimalign.Viu;

public interface IApplicationContext
{
    /// <summary>
    /// 
    /// </summary>
    IComponent RootComponent { get; }

    /// <summary>
    /// The props passed to the root component, or null.
    /// </summary>
    VirtualNodeProperties? RootProperties { get; }

    /// <summary>
    /// 
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
    public Action<Exception, ComponentInstance?, string>? ErrorHandler { get; set; }

    /// <summary>
    /// The app-level warning handler (upstream: <c>app.config.warnHandler</c>,
    /// https://vuejs.org/api/application.html#app-config-warnhandler). Intercepts dev warnings
    /// while the app is mounted; when null, warnings take their default route (a debug trace).
    /// Upstream also passes the instance and a component trace — Viu threads the message only,
    /// since the warning seam (<c>RuntimeWarnings.Sink</c>) is message-based.
    /// </summary>
    public Action<string>? WarnHandler { get; set; }

    /// <summary>
    /// Whether to enable performance instrumentation (upstream: <c>app.config.performance</c>,
    /// https://vuejs.org/api/application.html#app-config-performance). Held for the instrumentation
    /// that lands with the devtools/performance work; toggling it has no effect yet.
    /// </summary>
    public bool Performance { get; set; }
}
