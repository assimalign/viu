using Assimalign.Viu.Components;

namespace Assimalign.Viu;

/// <summary>Invokes one runtime directive lifecycle phase.</summary>
/// <param name="element">The boxed host element.</param>
/// <param name="binding">The resolved directive use and its current and previous values.</param>
/// <param name="value">The current immutable element node.</param>
/// <param name="previousValue">The previous element node on update, or null.</param>
/// <remarks>Specified by <c>[CMP-7]</c> and <c>[HYD-4]</c>.</remarks>
public delegate void DirectiveHook(
    object element,
    DirectiveBinding binding,
    ElementNode value,
    ElementNode? previousValue);
