using System;
using System.Collections.Generic;

namespace Assimalign.Viu.RuntimeDom;

/// <summary>
/// The DOM/timing primitives the CSS-class transition choreography drives — the injectable seam that
/// keeps the managed <c>&lt;Transition&gt;</c>/<c>&lt;TransitionGroup&gt;</c> logic browser-free and
/// deterministically testable. It abstracts exactly the operations upstream reaches to the DOM for
/// (<c>packages/runtime-dom/src/components/Transition.ts</c>/<c>TransitionGroup.ts</c>):
/// <c>classList.add</c>/<c>remove</c>, the double-<c>requestAnimationFrame</c> next-frame schedule,
/// the forced reflow, <c>transitionend</c>/<c>animationend</c> end-detection (with the
/// <c>getComputedStyle</c> duration/count fallbacks), and the FLIP <c>getBoundingClientRect</c> reads
/// plus <c>transform</c> writes.
/// <para>
/// Resolved ambiently (<see cref="Current"/>) exactly like <see cref="BrowserDirectiveOperations"/>:
/// <see cref="BrowserNodeOperations"/> installs a bridge-backed instance for a real app, and tests
/// install a recording instance that advances frames and fires end events on demand. The
/// <c>transitionend</c>/<c>animationend</c> listeners and <c>requestAnimationFrame</c> scheduling live
/// JS-side with a single callback into .NET per completion (never a per-property managed listener or a
/// poll); the FLIP read/write pass is the one unavoidable synchronous layout seam and is documented as
/// such on the bridge-backed implementation. Single-threaded ambient state (browser main thread only) —
/// NOT thread-safe.
/// </para>
/// </summary>
internal sealed class DomTransitionOperations
{
    /// <summary>
    /// The ambient instance the transition components resolve. Installed by
    /// <see cref="BrowserNodeOperations"/> (bridge-backed) or a test harness (recording); never
    /// resolved off the main thread.
    /// </summary>
    public static DomTransitionOperations? Current { get; set; }

    /// <summary>Adds one whitespace-free transition class to an element (upstream: <c>addTransitionClass</c>/<c>classList.add</c>).</summary>
    public required Action<int, string> AddTransitionClass { get; init; }

    /// <summary>Removes one transition class from an element (upstream: <c>removeTransitionClass</c>/<c>classList.remove</c>).</summary>
    public required Action<int, string> RemoveTransitionClass { get; init; }

    /// <summary>Schedules a callback for the second animation frame from now (upstream: <c>nextFrame</c>'s double <c>requestAnimationFrame</c>).</summary>
    public required Action<Action> NextFrame { get; init; }

    /// <summary>Forces a synchronous layout/reflow so a just-applied class takes effect before the next class swap (upstream: <c>forceReflow</c>).</summary>
    public required Action ForceReflow { get; init; }

    /// <summary>
    /// Resolves <c>resolve</c> when the element's transition/animation ends (upstream:
    /// <c>whenTransitionEnds</c>). <c>expectedType</c> is <c>"transition"</c>, <c>"animation"</c>, or
    /// null (detect the longer of the two); <c>explicitTimeout</c> is a duration in milliseconds, or a
    /// negative value to detect the timeout from <c>getComputedStyle</c>. Exactly one <c>resolve</c>
    /// fires per call — via the end event or a timeout fallback — so a managed leave/enter always settles.
    /// </summary>
    public required Action<int, string?, int, Action> WhenTransitionEnds { get; init; }

    /// <summary>Reads an element's top-left position for a FLIP snapshot (upstream: <c>getBoundingClientRect</c>).</summary>
    public required Func<int, TransitionRectangle> MeasurePosition { get; init; }

    /// <summary>
    /// Applies a FLIP inverting transform and a zero transition-duration to an element (upstream:
    /// <c>applyTranslation</c>'s <c>style.transform = translate(dx,dy); style.transitionDuration = '0s'</c>).
    /// </summary>
    public required Action<int, double, double> SetMoveTransform { get; init; }

    /// <summary>Clears the FLIP transform and transition-duration inline styles so the move class animates back (upstream: <c>style.transform = style.transitionDuration = ''</c>).</summary>
    public required Action<int> ClearMoveStyles { get; init; }

    /// <summary>
    /// Whether an element gains a CSS transform transition when the move class is applied (upstream:
    /// <c>hasCSSTransform</c> — clone the element without its live transition classes, add the move
    /// class, and read <c>getTransitionInfo().hasTransform</c>). <c>root</c> is the group container.
    /// </summary>
    public required Func<int, int, string, bool> HasCssTransform { get; init; }

    /// <summary>
    /// Resolves <c>resolve</c> when the element's FLIP transform transition ends (upstream: the
    /// <c>_moveCb</c> <c>transitionend</c> listener that only fires for a <c>transform</c> property).
    /// </summary>
    public required Action<int, Action> WhenMoveEnds { get; init; }

    /// <summary>
    /// The per-element "is currently leaving" flags (upstream: <c>el._isLeaving</c>) — pure managed
    /// choreography state a cancelling enter clears so the deferred leave-to swap is skipped. Keyed by
    /// element handle; default comparer keys correctly for unique handles.
    /// </summary>
    public Dictionary<int, bool> LeavingFlags { get; } = new();

    /// <summary>
    /// The per-element "the enter was cancelled" flags (upstream: <c>el._enterCancelled</c>) — chooses
    /// the leave's reflow ordering when a leave immediately follows a cancelled enter.
    /// </summary>
    public Dictionary<int, bool> EnterCancelledFlags { get; } = new();

    /// <summary>The ambient instance, or a thrown <see cref="InvalidOperationException"/> when none is installed.</summary>
    /// <exception cref="InvalidOperationException">No operations are installed.</exception>
    public static DomTransitionOperations Require()
        => Current ?? throw new InvalidOperationException(
            "No DomTransitionOperations installed. BrowserRuntime installs the browser-backed transition "
            + "operations; a <Transition>/<TransitionGroup> rendered before it, or outside a test harness.");
}
