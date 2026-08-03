namespace Assimalign.Viu.Router;

/// <summary>
/// The state carried on a single history entry — a flat, primitives-only payload with no nested
/// object graph so it round-trips cheaply across the JS-interop boundary and through the browser
/// History API's structured-clone serialization. This is the payload written with
/// <c>history.pushState</c>/<c>history.replaceState</c>, so it must stay primitives-only: a nested
/// object graph would be both slower to clone and lossy across a reload. Specified by
/// <c>[RTR-3]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The linked-list fields (<see cref="Back"/>/<see cref="Current"/>/<see cref="Forward"/>) let the
/// history reconstruct adjacency after a <c>popstate</c> that the application never observed, and
/// <see cref="Position"/> is a monotonically assigned counter: comparing the arriving entry's
/// position to the leaving entry's yields the signed navigation distance
/// (<see cref="NavigationInformation.Delta"/>) that drives back/forward detection. Value equality
/// (a record) so a navigation pipeline can compare and snapshot state cheaply.
/// </para>
/// </remarks>
/// <param name="Back">The location of the previous entry, or <see langword="null"/> at the start of history.</param>
/// <param name="Current">The location of this entry.</param>
/// <param name="Forward">The location of the next entry, or <see langword="null"/> at the tip of history.</param>
/// <param name="Replaced">Whether this entry replaced its predecessor rather than being pushed onto it.</param>
/// <param name="Position">
/// The entry's monotonic position counter (the basis for back/forward distance detection and scroll
/// restoration). Seeded from <c>window.history.length</c> in the browser and from zero in memory,
/// then assigned by Viu — never re-read from the environment — so the arithmetic is identical in
/// both modes.
/// </param>
/// <param name="Scroll">
/// The saved scroll anchor for this entry, or <see langword="null"/> when none was captured (a fresh
/// push captures the leaving entry's scroll and seeds the arriving entry's as <see langword="null"/>).
/// </param>
public sealed record RouterHistoryState(
    string? Back,
    string Current,
    string? Forward,
    bool Replaced,
    int Position,
    ScrollPosition? Scroll);
