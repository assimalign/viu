namespace Assimalign.Viu.Browser;

/// <summary>
/// A snapshotted element position and rendered scale for a <c>&lt;TransitionGroup&gt;</c> move.
/// </summary>
/// <param name="Left">The element's left offset (CSS pixels).</param>
/// <param name="Top">The element's top offset (CSS pixels).</param>
/// <param name="HorizontalScale">
/// The rendered width divided by the layout width, normalized to one when unavailable.
/// </param>
/// <param name="VerticalScale">
/// The rendered height divided by the layout height, normalized to one when unavailable.
/// </param>
internal readonly record struct TransitionRectangle(
    double Left,
    double Top,
    double HorizontalScale = 1,
    double VerticalScale = 1);
