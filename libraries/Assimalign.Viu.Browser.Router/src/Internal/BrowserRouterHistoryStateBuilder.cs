using Assimalign.Viu.Router;

namespace Assimalign.Viu.Browser.Router;

/// <summary>
/// Builds the flat history state owned by the Browser.Router implementation. These operations stay
/// local because constructing one implementation's entries is policy, not a cross-package API.
/// </summary>
internal static class BrowserRouterHistoryStateBuilder
{
    internal static RouterHistoryState BuildInitial(string current, int position) =>
        new(
            Back: null,
            Current: current,
            Forward: null,
            Replaced: true,
            Position: position,
            Scroll: null);

    internal static RouterHistoryState BuildForPush(
        RouterHistoryState current,
        string to,
        ScrollPosition? scrollSeed) =>
        new(
            Back: current.Current,
            Current: to,
            Forward: null,
            Replaced: false,
            Position: current.Position + 1,
            Scroll: scrollSeed);

    internal static RouterHistoryState BuildForReplace(
        RouterHistoryState current,
        string to,
        ScrollPosition? scrollSeed) =>
        new(
            Back: current.Back,
            Current: to,
            Forward: current.Forward,
            Replaced: true,
            Position: current.Position,
            Scroll: scrollSeed);
}
