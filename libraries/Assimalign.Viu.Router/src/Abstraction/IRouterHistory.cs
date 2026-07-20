using System;

namespace Assimalign.Viu.Router;

/// <summary>
/// The history integration behind the router: it owns the current location and its state, applies
/// application-initiated navigations (<see cref="Push"/>/<see cref="Replace"/>), relays
/// browser-initiated ones to listeners, and normalizes a configured base path in and out. The C#
/// port of vue-router's <c>RouterHistory</c> interface
/// (<c>packages/router/src/history/common.ts</c>), implemented by the memory, web (HTML5 History
/// API), and hash modes created through <see cref="RouterHistory"/>.
/// </summary>
/// <remarks>
/// Locations are the base-stripped path portion the matcher ([V01.01.08.01]) resolves — a leading
/// <c>/</c> path plus any query and fragment. The configured <see cref="Base"/> is prepended when
/// writing to the environment and stripped when reading back, so consumers never see it. Not
/// thread-safe: the router targets the single-threaded JS event loop.
/// </remarks>
public interface IRouterHistory
{
    /// <summary>
    /// The normalized base path prepended to every location written to the environment and stripped
    /// from every location read back. Mirrors <c>RouterHistory.base</c> — leading-slash-forced and
    /// trailing-slash-trimmed (an empty string means "no base").
    /// </summary>
    string Base { get; }

    /// <summary>
    /// The current base-stripped location. Mirrors <c>RouterHistory.location</c>. Updated
    /// synchronously by <see cref="Push"/>/<see cref="Replace"/> and by browser back/forward before
    /// listeners fire.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// The state of the current entry (position counter, adjacency, scroll anchor). Mirrors
    /// <c>RouterHistory.state</c>.
    /// </summary>
    RouterHistoryState State { get; }

    /// <summary>
    /// Pushes a new entry for <paramref name="location"/>, making it current and advancing the
    /// position counter. Mirrors <c>RouterHistory.push</c> (<c>history.pushState</c> in web mode).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <param name="data">Optional extra state to merge onto the new entry, or <see langword="null"/>.</param>
    void Push(string location, RouterHistoryState? data = null);

    /// <summary>
    /// Replaces the current entry with one for <paramref name="location"/>, preserving the position
    /// counter. Mirrors <c>RouterHistory.replace</c> (<c>history.replaceState</c> in web mode).
    /// </summary>
    /// <param name="location">The base-stripped location to navigate to.</param>
    /// <param name="data">Optional extra state to merge onto the entry, or <see langword="null"/>.</param>
    void Replace(string location, RouterHistoryState? data = null);

    /// <summary>
    /// Moves through history by <paramref name="delta"/> entries (negative is backward). Mirrors
    /// <c>RouterHistory.go</c> (<c>history.go</c> in web mode). When
    /// <paramref name="triggerListeners"/> is <see langword="false"/>, the resulting navigation does
    /// not notify listeners — used by the router to reposition history silently.
    /// </summary>
    /// <param name="delta">The signed number of entries to move.</param>
    /// <param name="triggerListeners">Whether the resulting navigation notifies listeners.</param>
    void Go(int delta, bool triggerListeners = true);

    /// <summary>
    /// Registers a listener for browser-initiated navigations (back/forward, memory <c>go</c>).
    /// Mirrors <c>RouterHistory.listen</c> and returns an idempotent unsubscribe delegate.
    /// </summary>
    /// <param name="callback">The listener to register.</param>
    /// <returns>A delegate that removes <paramref name="callback"/> when invoked.</returns>
    Action Listen(NavigationCallback callback);

    /// <summary>
    /// Builds a value suitable for an anchor's <c>href</c> for <paramref name="location"/>, prefixing
    /// the base (and reducing it to the leading <c>#</c> fragment in hash mode). Mirrors
    /// <c>RouterHistory.createHref</c>.
    /// </summary>
    /// <param name="location">The base-stripped location.</param>
    string CreateHref(string location);

    /// <summary>
    /// Tears the history down: removes every registered listener and, in web/hash mode, unsubscribes
    /// the underlying <c>popstate</c> handler so no interop listener leaks. Mirrors
    /// <c>RouterHistory.destroy</c>.
    /// </summary>
    void Destroy();
}
