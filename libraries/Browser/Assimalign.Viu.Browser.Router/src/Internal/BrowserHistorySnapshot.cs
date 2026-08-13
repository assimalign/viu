using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router;

/// <summary>
/// One batched read of the browser environment: the raw <c>location</c> components plus the current
/// entry's state, gathered in a single interop crossing so the web history never issues chatty
/// per-property getters (the [V01.01.08.02] batched-read criterion). Also the shape the
/// <c>popstate</c> dispatch reconstructs for a browser back/forward.
/// </summary>
/// <remarks>
/// The components are carried raw: base stripping happens on the .NET side
/// (<see cref="BrowserHistoryPathNormalization.CreateCurrentLocation"/>) rather than JS-side, so the whole
/// base policy stays unit-testable without a browser and the JS edge remains a dumb reader.
/// </remarks>
/// <param name="Pathname">The raw <c>location.pathname</c>.</param>
/// <param name="Search">The raw <c>location.search</c> (with any leading <c>?</c>).</param>
/// <param name="Hash">The raw <c>location.hash</c> (with any leading <c>#</c>).</param>
/// <param name="Host">The raw <c>location.host</c> (empty for a <c>file://</c> URL) — used for hash-base defaulting.</param>
/// <param name="HistoryLength">The current <c>window.history.length</c>, used to seed the initial position.</param>
/// <param name="State">
/// The current entry's state, or <see langword="null"/> when the environment had no Viu-written state
/// (a fresh entry, or one created before the history was constructed).
/// </param>
internal readonly record struct BrowserHistorySnapshot(
    string Pathname,
    string Search,
    string Hash,
    string Host,
    int HistoryLength,
    RouterHistoryState? State);
