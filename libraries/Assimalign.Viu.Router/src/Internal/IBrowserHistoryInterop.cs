using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The thin interop edge the web/hash history policy drives — the only surface that ever touches the
/// browser. Each method is a single interop crossing carrying flat primitives; the DOM-free policy
/// (<see cref="BrowserRouterHistory"/>) computes every URL and <see cref="RouterHistoryState"/> and
/// hands them here to apply. Modeling the edge as an injected seam (mirroring
/// <c>BrowserEventInvokerRegistry</c>'s recorded bridge delegates) lets the policy — base handling,
/// state round-trip, listener bookkeeping, interop-call counting — be unit-tested with a fake
/// implementation and no browser; the real implementation is
/// <see cref="JavaScriptBrowserHistoryInterop"/>.
/// </summary>
internal interface IBrowserHistoryInterop
{
    /// <summary>
    /// Reads the raw <c>location</c> components and the current entry's state in one interop crossing
    /// (the batched read). Ports the reads <c>createCurrentLocation</c> and the state bootstrap make.
    /// </summary>
    BrowserHistorySnapshot ReadSnapshot();

    /// <summary>
    /// Reads the document <c>&lt;base&gt;</c> element's href (origin already stripped), or
    /// <see langword="null"/> when there is none — the web-mode default when no base is configured.
    /// Called at most once, at construction. Ports the <c>&lt;base&gt;</c> read in <c>normalizeBase</c>.
    /// </summary>
    string? ReadBaseHref();

    /// <summary>
    /// Applies a push in one interop crossing: <c>replaceState(amendedCurrentState)</c> on
    /// <paramref name="currentUrl"/> (recording the leaving entry's live <c>window.scrollX/Y</c> as
    /// its scroll anchor) followed by <c>pushState(newState)</c> on <paramref name="toUrl"/>. Ports
    /// the two <c>changeLocation</c> calls of <c>useHistoryStateNavigation.push</c>.
    /// </summary>
    /// <param name="currentUrl">The absolute URL of the leaving entry (base already prepended).</param>
    /// <param name="amendedCurrentState">The leaving entry's rewritten state (its scroll is filled from the live scroll).</param>
    /// <param name="toUrl">The absolute URL of the new entry.</param>
    /// <param name="newState">The new entry's state.</param>
    void Push(string currentUrl, RouterHistoryState amendedCurrentState, string toUrl, RouterHistoryState newState);

    /// <summary>
    /// Applies a replace in one interop crossing: <c>replaceState(newState)</c> on
    /// <paramref name="toUrl"/>. Ports the single <c>changeLocation</c> of
    /// <c>useHistoryStateNavigation.replace</c>.
    /// </summary>
    /// <param name="toUrl">The absolute URL of the entry (base already prepended).</param>
    /// <param name="newState">The entry's state.</param>
    void Replace(string toUrl, RouterHistoryState newState);

    /// <summary>Moves through browser history by <paramref name="delta"/> entries (<c>history.go</c>).</summary>
    /// <param name="delta">The signed number of entries to move.</param>
    void Go(int delta);

    /// <summary>
    /// Subscribes <paramref name="onPopState"/> to the browser <c>popstate</c> event, attaching
    /// exactly one JS listener. The listener reconstructs a <see cref="BrowserHistorySnapshot"/> for
    /// the arrived entry and invokes the callback. Called once per history instance.
    /// </summary>
    /// <param name="onPopState">The policy's popstate handler.</param>
    void Subscribe(Action<BrowserHistorySnapshot> onPopState);

    /// <summary>
    /// Removes the <c>popstate</c> listener attached by <see cref="Subscribe"/>. Must be called on
    /// teardown so no interop listener leaks across history instances (test hosts create many).
    /// </summary>
    void Unsubscribe();
}
