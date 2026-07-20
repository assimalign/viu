namespace Assimalign.Viu.Router;

/// <summary>
/// The metadata a history reports to its listeners for a single navigation: the initiating
/// <see cref="Type"/>, the <see cref="Direction"/> relative to the current entry, and the signed
/// <see cref="Delta"/> (the distance between the leaving and arriving history positions). The C#
/// port of vue-router's <c>NavigationInformation</c>
/// (<c>packages/router/src/history/common.ts</c>).
/// </summary>
/// <param name="Type">Whether the navigation was a browser pop or an application push.</param>
/// <param name="Direction">The direction relative to the current entry.</param>
/// <param name="Delta">
/// The signed distance between positions (negative for backward, positive for forward, zero when
/// unknown) — the basis for back/forward detection and scroll restoration.
/// </param>
public readonly record struct NavigationInformation(
    NavigationType Type,
    NavigationDirection Direction,
    int Delta);
