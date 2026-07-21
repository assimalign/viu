namespace Assimalign.Viu.Browser;

/// <summary>
/// The opcode for one buffered DOM node-op in the interop command buffer ([V01.01.04.05]). Every
/// <em>write</em> op of <see cref="BrowserDomBridge"/> (the ones that return no data) has an opcode;
/// read ops (<c>querySelector</c>, <c>parentNode</c>, <c>nextSibling</c>, <c>insertStaticContent</c>,
/// <c>getRegistrySizes</c>) are not buffered — they force the buffer to apply and then call the
/// bridge directly (see <see cref="BufferedBrowserNodeOperations"/>).
/// <para>
/// These values are a <b>wire contract</b> shared byte-for-byte with the JS applier in
/// <c>viu-dom.js</c> (<c>applyCommandBuffer</c>): the frame carries a version byte so the two sides
/// fail loudly rather than drift. Never renumber or reuse a value without bumping
/// <see cref="DomCommandBuffer.Version"/> and the JS applier's accepted version. 0 is reserved
/// (an all-zero buffer must decode as "no valid op", not a real op).
/// </para>
/// </summary>
internal enum DomCommandOpcode : byte
{
    /// <summary>Create an element AS a pre-allocated handle: <c>[handle][tag][namespace?]</c>.</summary>
    CreateElement = 1,

    /// <summary>Create a text node AS a pre-allocated handle: <c>[handle][text]</c>.</summary>
    CreateText = 2,

    /// <summary>Create a comment node AS a pre-allocated handle: <c>[handle][text]</c>.</summary>
    CreateComment = 3,

    /// <summary>Set a text/comment node's content: <c>[handle][text]</c>.</summary>
    SetText = 4,

    /// <summary>Replace an element's content with text: <c>[handle][text]</c> (releases replaced children).</summary>
    SetElementText = 5,

    /// <summary>Insert child before anchor (0 anchor appends): <c>[parent][child][anchor]</c>.</summary>
    Insert = 6,

    /// <summary>Remove a node and release its subtree: <c>[handle]</c>.</summary>
    Remove = 7,

    /// <summary>Set an attribute: <c>[handle][name][value]</c>.</summary>
    SetAttribute = 8,

    /// <summary>Remove an attribute: <c>[handle][name]</c>.</summary>
    RemoveAttribute = 9,

    /// <summary>Set an <c>xlink:</c> attribute: <c>[handle][name][value]</c>.</summary>
    SetXlinkAttribute = 10,

    /// <summary>Remove an <c>xlink:</c> attribute: <c>[handle][name]</c>.</summary>
    RemoveXlinkAttribute = 11,

    /// <summary>Set <c>className</c>: <c>[handle][value]</c>.</summary>
    SetClassName = 12,

    /// <summary>Set a string DOM property: <c>[handle][name][value]</c>.</summary>
    SetStringProperty = 13,

    /// <summary>Set a boolean DOM property: <c>[handle][name][value:1]</c>.</summary>
    SetBooleanProperty = 14,

    /// <summary>Compare-and-set <c>value</c>: <c>[handle][value]</c>.</summary>
    SetValueGuarded = 15,

    /// <summary>Replace inline <c>style.cssText</c>: <c>[handle][cssText]</c>.</summary>
    SetStyleText = 16,

    /// <summary>Set one style property: <c>[handle][name][value][important:1]</c>.</summary>
    SetStyleProperty = 17,

    /// <summary>Remove one style property: <c>[handle][name]</c>.</summary>
    RemoveStyleProperty = 18,

    /// <summary>Attach a listener: <c>[handle][event][once:1][capture:1][passive:1]</c>.</summary>
    AddEventListener = 19,

    /// <summary>Detach a listener: <c>[handle][event][capture:1]</c>.</summary>
    RemoveEventListener = 20,

    /// <summary>
    /// Add one transition CSS class: <c>[handle][class]</c> (upstream <c>addTransitionClass</c> —
    /// <c>packages/runtime-dom/src/components/Transition.ts</c>). Buffered so the enter/leave class
    /// choreography is ordered with the node create/insert ops in the same frame ([V01.01.04.07.02]).
    /// </summary>
    AddTransitionClass = 21,

    /// <summary>Remove one transition CSS class: <c>[handle][class]</c> (upstream <c>removeTransitionClass</c>).</summary>
    RemoveTransitionClass = 22,

    /// <summary>
    /// The reflow barrier — no operands. When the applier reaches it while draining a frame it performs
    /// a real synchronous reflow (upstream <c>forceReflow</c>'s <c>document.body.offsetHeight</c> read),
    /// committing every class write <em>before</em> it in the frame to its own style recalc before the
    /// writes after it. This is what keeps a buffered leave's <c>*-leave-from</c> class from coalescing
    /// with <c>*-leave-active</c> (upstream #2593) without splitting the flush — the frame still crosses
    /// the interop boundary exactly once ([V01.01.04.07.02]).
    /// </summary>
    ForceReflow = 23,

    /// <summary>
    /// Apply a FLIP inverting transform: <c>[handle][deltaX:float64][deltaY:float64]</c> (upstream
    /// <c>applyTranslation</c>'s <c>style.transform = translate(dx,dy); style.transitionDuration = '0s'</c>
    /// — <c>packages/runtime-dom/src/components/TransitionGroup.ts</c>). Buffered so the whole FLIP write
    /// pass — every element's transform, the <see cref="ForceReflow"/> barrier, then the move class and
    /// the transform clear — rides one frame in upstream order, one interop crossing ([V01.01.04.07.03]).
    /// The <c>float64</c> deltas are the first non-int32/bool operands in the wire format.
    /// </summary>
    SetMoveTransform = 24,

    /// <summary>
    /// Clear the FLIP transform and zero transition-duration so the move class animates the element back
    /// to its settled spot: <c>[handle]</c> (upstream <c>style.transform = style.transitionDuration = ''</c>).
    /// Buffered after the reflow barrier and the move class in the same FLIP write frame ([V01.01.04.07.03]).
    /// </summary>
    ClearMoveStyles = 25,
}
