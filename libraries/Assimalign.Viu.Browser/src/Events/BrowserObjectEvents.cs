namespace Assimalign.Viu.Browser;

/// <summary>
/// The install point for the host's <see cref="BrowserObjectEventInvoker"/> — how the DOM event
/// system dispatches a renderer-agnostic <see cref="System.Action{T}"/> of
/// <see cref="object"/> handler (the shape components rendering through the node-ops abstraction
/// attach, e.g. the Router's <c>RouterLink</c>). The DOM runtime never constructs a concrete
/// payload itself; a browser integration layer that knows the payload type (the Router's DOM
/// bridge) installs the invoker here before mounting. When no invoker is installed, dispatching
/// such a handler routes a <see cref="System.NotSupportedException"/> to the event system's error
/// sink rather than silently dropping the event.
/// <para>
/// Ambient process-global state, mirroring the DOM runtime's other single-renderer seams (a browser
/// app has one active renderer on the single JS event-loop thread). Not thread-safe.
/// </para>
/// </summary>
public static class BrowserObjectEvents
{
    /// <summary>
    /// The installed invoker, or null when none is installed. Set by the browser integration layer
    /// (the Router's DOM bridge) at bootstrap; read by the event dispatch when a handler is an
    /// <see cref="System.Action{T}"/> of <see cref="object"/>.
    /// </summary>
    public static BrowserObjectEventInvoker? Invoker { get; set; }
}
