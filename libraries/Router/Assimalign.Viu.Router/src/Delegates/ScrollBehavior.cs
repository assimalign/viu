using System.Threading.Tasks;

namespace Assimalign.Viu.Router;

/// <summary>
/// Selects an optional scroll target after a navigation is confirmed and its render flush settles.
/// </summary>
/// <param name="to">The confirmed destination.</param>
/// <param name="from">The location left by the navigation.</param>
/// <param name="savedPosition">
/// The offset saved for the arriving history-position counter during back or forward navigation;
/// <see langword="null"/> for application pushes and replacements.
/// </param>
/// <returns>
/// A task producing the scroll target, or <see langword="null"/> to preserve the current offset.
/// The task may delay settlement until application-specific asynchronous rendering is ready.
/// </returns>
/// <remarks>
/// The destination carries its raw fragment. For a simple CSS identifier, an application may return
/// <c>new ScrollTarget("#" + to.Fragment)</c> when it is non-empty; other text needs application-chosen
/// decoding and CSS escaping. No fragment scroll is implicit. Specified by <c>[RTR-9]</c> and <c>[RTR-12]</c>.
/// </remarks>
public delegate Task<ScrollTarget?> ScrollBehavior(
    RouteLocation to,
    RouteLocation from,
    ScrollPosition? savedPosition);
