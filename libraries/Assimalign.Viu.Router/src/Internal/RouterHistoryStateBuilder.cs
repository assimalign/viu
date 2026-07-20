namespace Assimalign.Viu.Router;

/// <summary>
/// The pure state-machine arithmetic for a push/replace/bootstrap, shared by the memory and web
/// histories: it produces the <see cref="RouterHistoryState"/> objects a navigation writes without
/// touching the DOM or interop, so the linked-list/position semantics are unit-testable. The C# port
/// of vue-router's <c>buildState</c> and the state assembly inside <c>useHistoryStateNavigation</c>'s
/// <c>push</c>/<c>replace</c> (<c>packages/router/src/history/html5.ts</c>).
/// </summary>
/// <remarks>
/// Position is assigned here (monotonic, +1 per push, preserved across a replace) rather than read
/// from <c>window.history.length</c>, so the same arithmetic runs identically in memory and in the
/// browser. The one field this layer cannot compute is the leaving entry's live scroll anchor: a
/// browser push captures <c>window.scrollX/Y</c> at apply time (the interop edge injects it), while
/// memory has no scroll and leaves it <see langword="null"/>.
/// </remarks>
internal static class RouterHistoryStateBuilder
{
    /// <summary>
    /// Builds the initial state for a fresh entry that has no prior state — the C# port of the
    /// <c>buildState(null, current, null, replaced: true, position: history.length - 1)</c> bootstrap
    /// upstream writes when <c>history.state</c> is empty.
    /// </summary>
    /// <param name="current">The current location.</param>
    /// <param name="position">The seed position (upstream: <c>history.length - 1</c>; memory: <c>0</c>).</param>
    internal static RouterHistoryState BuildInitial(string current, int position)
        => new(Back: null, Current: current, Forward: null, Replaced: true, Position: position, Scroll: null);

    /// <summary>
    /// Rewrites the leaving entry during a push so its <see cref="RouterHistoryState.Forward"/> points
    /// at the pushed location (upstream amends the current entry with <c>forward: to</c> and the live
    /// scroll before pushing the new one). The scroll anchor stays <see langword="null"/> here; the
    /// browser interop injects <c>window.scrollX/Y</c> when it applies the replace.
    /// </summary>
    /// <param name="current">The state of the entry being left.</param>
    /// <param name="to">The location being pushed.</param>
    internal static RouterHistoryState AmendCurrentForPush(RouterHistoryState current, string to)
        => current with { Forward = to };

    /// <summary>
    /// Builds the new entry for a push: its predecessor is the leaving location, it has no successor
    /// yet, and its position is one past the leaving entry's. The C# port of
    /// <c>buildState(currentLocation, to, null)</c> with <c>position: currentState.position + 1</c>.
    /// </summary>
    /// <param name="current">The state of the entry being left.</param>
    /// <param name="to">The location being pushed.</param>
    /// <param name="scrollSeed">An optional scroll anchor to seed on the new entry (from push data).</param>
    internal static RouterHistoryState BuildForPush(RouterHistoryState current, string to, ScrollPosition? scrollSeed)
        => new(
            Back: current.Current,
            Current: to,
            Forward: null,
            Replaced: false,
            Position: current.Position + 1,
            Scroll: scrollSeed);

    /// <summary>
    /// Builds the entry for a replace: it keeps the leaving entry's neighbours and position but marks
    /// itself replaced. The C# port of <c>buildState(currentState.back, to, currentState.forward,
    /// true)</c> pinned to <c>position: currentState.position</c>.
    /// </summary>
    /// <param name="current">The state of the entry being replaced.</param>
    /// <param name="to">The replacement location.</param>
    /// <param name="scrollSeed">An optional scroll anchor to seed on the entry (from replace data).</param>
    internal static RouterHistoryState BuildForReplace(RouterHistoryState current, string to, ScrollPosition? scrollSeed)
        => new(
            Back: current.Back,
            Current: to,
            Forward: current.Forward,
            Replaced: true,
            Position: current.Position,
            Scroll: scrollSeed);
}
