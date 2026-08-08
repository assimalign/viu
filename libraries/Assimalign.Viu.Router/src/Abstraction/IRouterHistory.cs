using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The history integration behind the router: it owns the current location and its state, applies
/// application-initiated navigations (<see cref="Push"/>/<see cref="Replace"/>), relays
/// browser-initiated ones to listeners, and normalizes a configured base path in and out.
/// Implemented by the memory, web (HTML5 History API), and hash modes created through
/// <see cref="RouterHistory"/>. Specified by <c>[RTR-3]</c>.
/// </summary>
/// <remarks>
/// Locations are the base-stripped path portion the matcher ([V01.01.08.01]) resolves — a leading
/// <c>/</c> path plus any query and fragment. The configured <see cref="Base"/> is prepended when
/// writing to the environment and stripped when reading back, so consumers never see it. Not
/// thread-safe: the router targets the single-threaded JS event loop.
/// <para>
/// Web and hash histories initialize their browser bridge through <see cref="Router.ReadyAsync"/>.
/// <see cref="Listen"/> and <see cref="IDisposable.Dispose"/> are valid before readiness so a router can attach
/// and detach safely; every other synchronous member throws <see cref="InvalidOperationException"/>
/// until readiness completes.
/// </para>
/// <para>
/// Disposal is terminal and idempotent. After disposal every member except another
/// <see cref="IDisposable.Dispose"/> throws <see cref="ObjectDisposedException"/>; owners can
/// therefore prove that no listener or environment operation survives the history lifetime.
/// </para>
/// </remarks>
public interface IRouterHistory : IDisposable
{
    /// <summary>
    /// The normalized base path prepended to every location written to the environment and stripped
    /// from every location read back — leading-slash-forced and trailing-slash-trimmed (an empty
    /// string means "no base").
    /// </summary>
    string Base { get; }

    /// <summary>
    /// The current base-stripped location. Updated synchronously by
    /// <see cref="Push"/>/<see cref="Replace"/> and by browser back/forward before listeners fire.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// The state of the current entry (position counter, adjacency, scroll anchor).
    /// </summary>
    RouterHistoryState State { get; }

    /// <summary>
    /// Pushes a new entry for <paramref name="location"/>, making it current and advancing the
    /// position counter (<c>history.pushState</c> in web mode).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <param name="data">Optional extra state to merge onto the new entry, or <see langword="null"/>.</param>
    void Push(string location, RouterHistoryState? data = null);

    /// <summary>
    /// Replaces the current entry with one for <paramref name="location"/>, preserving the position
    /// counter (<c>history.replaceState</c> in web mode).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <param name="data">Optional extra state to merge onto the entry, or <see langword="null"/>.</param>
    void Replace(string location, RouterHistoryState? data = null);

    /// <summary>
    /// Moves through history by <paramref name="delta"/> entries (negative is backward;
    /// <c>history.go</c> in web mode). When <paramref name="triggerListeners"/> is
    /// <see langword="false"/>, the resulting navigation does not notify listeners — used by the
    /// router to reposition history silently.
    /// </summary>
    /// <param name="delta">The signed number of entries to move.</param>
    /// <param name="triggerListeners">Whether the resulting navigation notifies listeners.</param>
    void Go(int delta, bool triggerListeners = true);

    /// <summary>
    /// Registers a listener for browser-initiated navigations (back/forward, memory
    /// <see cref="Go"/>) and returns an idempotent unsubscribe delegate.
    /// </summary>
    /// <param name="callback">The listener to register.</param>
    /// <returns>A delegate that removes <paramref name="callback"/> when invoked.</returns>
    Action Listen(NavigationCallback callback);

    /// <summary>
    /// Builds a value suitable for an anchor's <c>href</c> for <paramref name="location"/>, prefixing
    /// the base (and reducing it to the leading <c>#</c> fragment in hash mode).
    /// </summary>
    /// <param name="location">The base-stripped location.</param>
    string CreateHref(string location);

    /// <summary>
    /// Releases the history: removes every registered listener and, in web/hash mode, unsubscribes
    /// the underlying <c>popstate</c> handler so no interop listener leaks. Repeated disposal is safe.
    /// </summary>
    new void Dispose();
}
