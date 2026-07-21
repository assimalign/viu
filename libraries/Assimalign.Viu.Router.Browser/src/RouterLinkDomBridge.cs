using System;

using Assimalign.Viu.Browser;

namespace Assimalign.Viu.Router.Browser;

/// <summary>
/// The browser integration layer that lets <see cref="RouterLink"/> navigate on a real click — it
/// adapts the DOM adapter's dispatched <see cref="BrowserEvent"/> into the DOM-free
/// <see cref="RouterLinkClickEvent"/> the link's guard reads, then mirrors the guard's
/// <see cref="RouterLinkClickEvent.PreventDefault"/> decision back onto the live event. This is the
/// C# home for the coupling vue-router's <c>guardEvent</c> keeps inline (it reads the DOM
/// <c>MouseEvent</c> and calls <c>e.preventDefault()</c> directly,
/// <c>packages/router/src/RouterLink.ts</c>, https://github.com/vuejs/router); Viu keeps
/// <see cref="RouterLink"/> renderer-agnostic (it never references the
/// <c>Assimalign.Viu.Browser</c> DOM adapter), so the mapping lives here instead — a package that
/// depends on both the Router and the DOM adapter, kept out of every non-router app's framework
/// closure ([V01.01.08.03.01], issue #191).
/// <para>
/// Install once at app bootstrap (after <c>BrowserRuntime.InitializeAsync</c>, before mounting).
/// Not thread-safe (browser main thread only); the installed bridge is ambient process-global state
/// on <see cref="BrowserObjectEvents"/>.
/// </para>
/// </summary>
public static class RouterLinkDomBridge
{
    // A single delegate instance so Install/Uninstall can compare by reference.
    private static readonly BrowserObjectEventInvoker Invoker = Invoke;

    /// <summary>
    /// Installs the bridge so the DOM event system dispatches <see cref="RouterLink"/>'s (and any
    /// other renderer-agnostic component's) click handlers — sets
    /// <see cref="BrowserObjectEvents.Invoker"/>. Call once at bootstrap.
    /// </summary>
    public static void Install() => BrowserObjectEvents.Invoker = Invoker;

    /// <summary>
    /// Removes this bridge if it is the installed one, restoring no-bridge dispatch (leaves any other
    /// installed invoker untouched). Idempotent.
    /// </summary>
    public static void Uninstall()
    {
        if (ReferenceEquals(BrowserObjectEvents.Invoker, Invoker))
        {
            BrowserObjectEvents.Invoker = null;
        }
    }

    /// <summary>
    /// Adapts <paramref name="browserEvent"/> to a <see cref="RouterLinkClickEvent"/>, runs the
    /// component's <paramref name="handler"/> against it, and — if the guard chose to intercept —
    /// prevents the live event's default so no full page load occurs. The
    /// <see cref="BrowserObjectEventInvoker"/> the DOM event system calls for an
    /// <see cref="Action{T}"/> of <see cref="object"/> handler.
    /// </summary>
    /// <param name="handler">The link's <c>onClick</c> handler (a renderer-agnostic object-payload handler).</param>
    /// <param name="browserEvent">The dispatched click carrying the native button/modifier/prevented state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> or <paramref name="browserEvent"/> is null.</exception>
    public static void Invoke(Action<object?> handler, BrowserEvent browserEvent)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(browserEvent);

        // The live event's arrival-time prevented state, captured before the guard runs: an event
        // that arrived prevented was already suppressed by the browser, so the guard bails and this
        // bridge must not re-signal (upstream guardEvent bails on e.defaultPrevented).
        var arrivedPrevented = browserEvent.DefaultPrevented;
        var click = CreateClickEvent(browserEvent, arrivedPrevented);
        handler(click);

        // The guard intercepted an unmodified primary-button click -> suppress the native navigation.
        if (click.DefaultPrevented && !arrivedPrevented)
        {
            browserEvent.PreventDefault();
        }
    }

    // Maps the browser click metadata onto RouterLink's DOM-free event: the mouse button and the
    // four system modifiers vue-router's guardEvent inspects, plus the already-prevented state seeded
    // so the guard sees the same e.defaultPrevented the DOM would.
    private static RouterLinkClickEvent CreateClickEvent(BrowserEvent browserEvent, bool arrivedPrevented)
    {
        var modifiers = browserEvent.Modifiers;
        var click = new RouterLinkClickEvent(
            browserEvent.Button,
            (modifiers & BrowserEventModifiers.Control) != 0,
            (modifiers & BrowserEventModifiers.Shift) != 0,
            (modifiers & BrowserEventModifiers.Alt) != 0,
            (modifiers & BrowserEventModifiers.Meta) != 0);
        if (arrivedPrevented)
        {
            click.PreventDefault();
        }
        return click;
    }
}
