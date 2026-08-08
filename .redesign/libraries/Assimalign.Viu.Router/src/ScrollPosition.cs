namespace Assimalign.Viu.Router;

/// <summary>
/// A saved scroll anchor (document scroll offset) carried on a history entry's state so a later
/// back/forward navigation can restore it. Captured from <c>window.scrollX</c>/<c>window.scrollY</c>
/// at the browser edge. Scroll <em>restoration</em> itself is a later Router feature
/// ([V01.01.08.05], see <c>[RTR-8]</c>); this history layer only round-trips the anchor.
/// </summary>
/// <param name="Left">The horizontal scroll offset in CSS pixels (<c>window.scrollX</c>).</param>
/// <param name="Top">The vertical scroll offset in CSS pixels (<c>window.scrollY</c>).</param>
public readonly record struct ScrollPosition(double Left, double Top);
