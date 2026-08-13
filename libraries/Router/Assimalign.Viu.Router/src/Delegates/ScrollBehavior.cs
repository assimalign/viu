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
/// <remarks>Specified by <c>[V01.01.08.05]</c>.</remarks>
public delegate Task<ScrollTarget?> ScrollBehavior(
    RouteLocation to,
    RouteLocation from,
    ScrollPosition? savedPosition);
