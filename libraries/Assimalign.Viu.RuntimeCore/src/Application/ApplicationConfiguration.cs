using System;

namespace Assimalign.Viu.RuntimeCore;

/// <summary>
/// The app-level configuration bag — the C# port of <c>app.config</c> from
/// <c>@vue/runtime-core</c> (<c>packages/runtime-core/src/apiCreateApp.ts</c>,
/// https://vuejs.org/api/application.html#app-config). Reached through
/// <see cref="Application{TNode}.Config"/>; its handlers are consulted by the runtime as a
/// last resort once the per-component chains have run.
/// <para>
/// <b>globalProperties is deliberately excluded.</b> Upstream's
/// <c>app.config.globalProperties</c> injects members onto every component's <c>this</c> proxy;
/// Viu has no <c>this</c> proxy (there is no <c>Proxy</c> under AOT/trimming) and favors typed
/// app-level <see cref="Application{TNode}.Provide{T}"/> /
/// <see cref="DependencyInjection.Inject{T}(InjectionKey{T})"/> instead. The decision and its
/// rationale are recorded in the founding ADR ([V01.01.13.01]).
/// </para>
/// Not thread-safe (single-threaded JS event-loop model).
/// </summary>
public sealed class ApplicationConfiguration
{
    internal ApplicationConfiguration()
    {
    }

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
