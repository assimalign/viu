using System;

using Assimalign.Viu;

namespace Assimalign.Viu.Browser;

/// <summary>
/// The DOM half of the by-name helper contract the render-function code generator emits against — the C#
/// port of the DOM render-helper exports of <c>@vue/runtime-dom</c> registered by <c>@vue/compiler-dom</c>'s
/// <c>runtimeHelpers.ts</c> (<c>packages/runtime-dom/src/directives/vShow.ts</c>, <c>directives/vModel.ts</c>,
/// <c>modules/events.ts</c>, and the <c>Transition</c>/<c>TransitionGroup</c> built-ins). It is the DOM sibling
/// of <see cref="RenderHelpers"/>: a DOM-targeted compiled render binds both surfaces with two file-level
/// static imports — <c>using static global::Assimalign.Viu.RenderHelpers;</c> and
/// <c>using static global::Assimalign.Viu.Browser.DomRenderHelpers;</c> — so the members here carry the
/// upstream-aliased spellings (<c>_vShow</c>, <c>_vModelText</c>, <c>_withModifiers</c>, …), the same names the
/// emitter writes (<c>_</c> + the <c>helperNameMap</c> name). The authoritative name/signature table is the
/// DOM section of <c>Assimalign.Viu.Syntax.Templates/docs/DESIGN.md</c>; this surface satisfies every DOM
/// directive/modifier call shape the emitter (<c>Internal/RenderCodeWriter.cs</c>, <c>Internal/VOnTransform.cs</c>)
/// can produce.
/// <para>
/// The <c>_</c>-prefixed lowercase member names are a deliberate, generated-code-only deviation from the
/// repository whole-word C# naming rule (<c>.claude/rules/general-rules.md</c>) — the names <b>are</b> the
/// upstream contract, and the compiler binds them literally. Deviates from the general-rules naming rule per
/// design decision: the render-helper names are the upstream <c>helperNameMap</c> contract, pinned by the
/// Templates <c>docs/DESIGN.md</c> table and <c>RenderFunctionEmitterTests</c>.
/// </para>
/// <para>
/// This class layers on <c>Assimalign.Viu.Browser</c> (the directive singletons <see cref="VShow"/>/
/// <c>VModel*</c> and the <see cref="BrowserEvents"/> modifier/key guards) plus <c>Assimalign.Viu</c>
/// (<see cref="IDirective"/>) — never any <c>Assimalign.Viu.Syntax.*</c> assembly. The contract flows one way
/// (by name) so the runtime never depends on the compiler, and these DOM helpers deliberately live here rather
/// than on the platform-agnostic <see cref="RenderHelpers"/> so a DOM directive can never mis-bind onto a
/// runtime-core marker (see the DOM-layering split in <c>RenderHelpers</c>' class docs and
/// <c>Templates/docs/DESIGN.md</c>). Not thread-safe (single-threaded JS event-loop model).
/// </para>
/// </summary>
public static class DomRenderHelpers
{
    // ==== Runtime directive values (upstream @vue/runtime-dom directive exports) ==================
    // The emitter writes each as an element of a withDirectives tuple —
    // `_withDirectives(vnode, new object?[] { new object?[] { _vShow, exp } })` — so RenderHelpers'
    // `_withDirectives` binds it through its `tuple[0] is IDirective` check; the singletons are the same
    // instances the compiled v-model / v-show transforms ([V01.01.05.03]) reference.

    /// <summary>The <c>v-show</c> directive (upstream: <c>vShow</c>), mapped to <see cref="VShow.Instance"/>.</summary>
    public static readonly IDirective _vShow = VShow.Instance;

    /// <summary>The text/<c>&lt;textarea&gt;</c> <c>v-model</c> directive (upstream: <c>vModelText</c>), mapped to <see cref="VModelText.Instance"/>.</summary>
    public static readonly IDirective _vModelText = VModelText.Instance;

    /// <summary>The checkbox <c>v-model</c> directive (upstream: <c>vModelCheckbox</c>), mapped to <see cref="VModelCheckbox.Instance"/>.</summary>
    public static readonly IDirective _vModelCheckbox = VModelCheckbox.Instance;

    /// <summary>The radio <c>v-model</c> directive (upstream: <c>vModelRadio</c>), mapped to <see cref="VModelRadio.Instance"/>.</summary>
    public static readonly IDirective _vModelRadio = VModelRadio.Instance;

    /// <summary>The <c>&lt;select&gt;</c> <c>v-model</c> directive (upstream: <c>vModelSelect</c>), mapped to <see cref="VModelSelect.Instance"/>.</summary>
    public static readonly IDirective _vModelSelect = VModelSelect.Instance;

    /// <summary>The dynamic-input-type <c>v-model</c> directive (upstream: <c>vModelDynamic</c>), mapped to <see cref="VModelDynamic.Instance"/>.</summary>
    public static readonly IDirective _vModelDynamic = VModelDynamic.Instance;

    // ==== Event-handler modifier / key guards (upstream @vue/runtime-dom modules/events.ts) =======
    // The emitter wraps a handler expression in `_withModifiers(handler, ["stop", ...])` /
    // `_withKeys(handler, ["enter", ...])` (VOnTransform), never inside a `_withHandler` — these
    // signatures type the inner lambda themselves. The handler shapes the emitter can produce are an
    // inline value expression (`__event => (expr)`), an inline statement block (`__event => { ... }`),
    // or a member/method-group reference (`_ctx.save`); the overloads below target-type each and adapt it
    // onto the single `Action<BrowserEvent>` guard <see cref="BrowserEvents"/> exposes. The result is the
    // dispatchable `Action<BrowserEvent>` the event invoker registry ([V01.01.04.03]) understands, and it
    // stays the stored prop value (not re-wrapped). Both a `.stop`/`.prevent`/`.enter` stack nest —
    // `_withKeys(_withModifiers(h, [...]), [...])` — because each overload set includes the
    // `Action<BrowserEvent>` arm the inner call returns.

    /// <summary>
    /// Wraps a value-returning inline handler (<c>__event =&gt; (expr)</c>) with Vue's event modifiers
    /// (upstream: <c>withModifiers</c>).
    /// </summary>
    /// <param name="handler">The inline handler; its result is evaluated for its side effect and discarded.</param>
    /// <param name="modifiers">The modifier names, unprefixed (e.g. <c>"stop"</c>, <c>"prevent"</c>, <c>"ctrl"</c>).</param>
    /// <returns>The guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withModifiers(Func<BrowserEvent, object?> handler, params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(browserEvent => handler(browserEvent), modifiers);
    }

    /// <summary>
    /// Wraps a void inline block (<c>__event =&gt; { … }</c>) or an event-taking method-group handler with
    /// Vue's event modifiers (upstream: <c>withModifiers</c>). This is also the arm a nested
    /// <c>_withKeys(_withModifiers(…), …)</c> stack resolves through.
    /// </summary>
    /// <param name="handler">The handler taking the browser event.</param>
    /// <param name="modifiers">The modifier names, unprefixed.</param>
    /// <returns>The guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withModifiers(Action<BrowserEvent> handler, params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(handler, modifiers);
    }

    /// <summary>Wraps a value-returning parameterless method-group handler with Vue's event modifiers (upstream: <c>withModifiers</c>).</summary>
    /// <param name="handler">The parameterless handler; its result is discarded.</param>
    /// <param name="modifiers">The modifier names, unprefixed.</param>
    /// <returns>The guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withModifiers(Func<object?> handler, params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(_ => handler(), modifiers);
    }

    /// <summary>Wraps a void parameterless method-group handler with Vue's event modifiers (upstream: <c>withModifiers</c>).</summary>
    /// <param name="handler">The parameterless handler.</param>
    /// <param name="modifiers">The modifier names, unprefixed.</param>
    /// <returns>The guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withModifiers(Action handler, params string[] modifiers)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithModifiers(_ => handler(), modifiers);
    }

    /// <summary>Wraps a value-returning inline handler (<c>__event =&gt; (expr)</c>) with Vue's key guards (upstream: <c>withKeys</c>).</summary>
    /// <param name="handler">The inline handler; its result is discarded.</param>
    /// <param name="keys">The key names (e.g. <c>"enter"</c>, <c>"esc"</c>).</param>
    /// <returns>The key-guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withKeys(Func<BrowserEvent, object?> handler, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(browserEvent => handler(browserEvent), keys);
    }

    /// <summary>
    /// Wraps a void inline block (<c>__event =&gt; { … }</c>) or an event-taking method-group handler with
    /// Vue's key guards (upstream: <c>withKeys</c>). This is the arm a
    /// <c>_withKeys(_withModifiers(…), …)</c> stack resolves through.
    /// </summary>
    /// <param name="handler">The handler taking the browser event.</param>
    /// <param name="keys">The key names.</param>
    /// <returns>The key-guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withKeys(Action<BrowserEvent> handler, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(handler, keys);
    }

    /// <summary>Wraps a value-returning parameterless method-group handler with Vue's key guards (upstream: <c>withKeys</c>).</summary>
    /// <param name="handler">The parameterless handler; its result is discarded.</param>
    /// <param name="keys">The key names.</param>
    /// <returns>The key-guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withKeys(Func<object?> handler, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(_ => handler(), keys);
    }

    /// <summary>Wraps a void parameterless method-group handler with Vue's key guards (upstream: <c>withKeys</c>).</summary>
    /// <param name="handler">The parameterless handler.</param>
    /// <param name="keys">The key names.</param>
    /// <returns>The key-guarded handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
    public static Action<BrowserEvent> _withKeys(Action handler, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return BrowserEvents.WithKeys(_ => handler(), keys);
    }

    // ==== DOM built-in components (upstream Transition / TransitionGroup) ==========================

    /// <summary>
    /// The <c>&lt;Transition&gt;</c> DOM built-in (upstream: <c>Transition</c>), resolved to the real
    /// <see cref="Browser.Transition"/> component ([V01.01.04.07]). The compiled render passes it as a
    /// vnode <c>tag</c>; the vnode factory's component-definition arm mounts it, and it resolves the
    /// CSS-class enter/leave hooks over <see cref="BaseTransition"/>.
    /// </summary>
    public static readonly object _Transition = Transition.Instance;

    /// <summary>
    /// The <c>&lt;TransitionGroup&gt;</c> DOM built-in (upstream: <c>TransitionGroup</c>), resolved to the
    /// real <see cref="Browser.TransitionGroup"/> component ([V01.01.04.07]) — a keyed list with
    /// FLIP-based <c>v-move</c> reordering.
    /// </summary>
    public static readonly object _TransitionGroup = TransitionGroup.Instance;
}
