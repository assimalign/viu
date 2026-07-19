namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// A snapshotted element position for the <c>&lt;TransitionGroup&gt;</c> FLIP move — the two
/// coordinates of upstream's <c>getBoundingClientRect()</c> the move delta is computed from
/// (<c>packages/runtime-dom/src/components/TransitionGroup.ts</c>). Only the top-left corner matters:
/// the move transform is <c>translate(oldLeft - newLeft, oldTop - newTop)</c>.
/// </summary>
/// <param name="Left">The element's left offset (CSS pixels).</param>
/// <param name="Top">The element's top offset (CSS pixels).</param>
internal readonly record struct TransitionRectangle(double Left, double Top);
