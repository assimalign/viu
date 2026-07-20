# Assimalign.Viu.Router.RuntimeDom — design

Why this is a separate package and how the click bridge works. What it is: see
[OVERVIEW.md](OVERVIEW.md). Upstream counterpart: vue-router's `guardEvent`
(`packages/router/src/RouterLink.ts`, https://github.com/vuejs/router). Work item:
[V01.01.08.03.01] (issue #191), discovered while implementing the DOM-free RouterLink components
([V01.01.08.03], #72).

## Why a third package, not a reference either way

`RouterLink`'s guard reads a `RouterLinkClickEvent` — a DOM-free stand-in for the `MouseEvent`
upstream's `guardEvent` inspects — so the Router components run against the in-memory Testing renderer
and SSR, and the assembly references no DOM adapter (pinned by
`RouterAssembly_DoesNotReferenceTheBrowserDomAdapter`). The mapping from a real browser event to that
contract therefore cannot live in `Assimalign.Viu.Router`.

It cannot live in `Assimalign.Viu.RuntimeDom` either: `RuntimeDom` is a framework member shipped in
every Viu app (`@(ViuFrameworkAssembly)`), while `Router` is an opt-in package. A `RuntimeDom → Router`
reference would drag the whole Router into every non-router app's framework closure — the generic DOM
runtime taking on a specific feature library, the reverse of upstream's `vue-router → @vue/runtime-dom`
direction. So the DOM runtime stays Router-agnostic behind a generic seam, and the concrete mapping
lives here, in the one package that depends on both. Nothing references this package back, so the
coupling is a leaf.

## The generic seam it plugs into

`RuntimeDom` exposes an ambient `BrowserObjectEvents.Invoker` (`BrowserObjectEventInvoker`): the event
system routes any renderer-agnostic `Action<object?>` handler — the shape a component rendering
through the node-ops abstraction attaches — through the installed invoker, which owns the whole
conversion. This mirrors the Testing renderer's `Action<object?>` dispatch (`TestEventDispatcher`):
same handler shape, host-specific payload. `RouterLinkDomBridge.Install()` sets that invoker; with
none installed the DOM runtime surfaces a `NotSupportedException` to its error sink rather than
dropping the click. The seam names no Router type, so it stays a generic capability the DOM runtime
owns — the Router bridge is merely its first consumer.

## Mapping and prevent-default plumbing

`Invoke` builds a `RouterLinkClickEvent` from the `BrowserEvent`'s `Button` and its
Control/Shift/Alt/Meta `Modifiers`, runs the handler (the link's guard), and then decides whether to
suppress the browser default:

- **Already-prevented events fall through.** The dispatch payload carries the live event's
  arrival-time `event.defaultPrevented` (a `RuntimeDom` `[JSExport]` field added by this work item).
  When set, the bridge seeds `RouterLinkClickEvent.PreventDefault()` before the guard runs, so the
  guard bails exactly as upstream `guardEvent` bails on `e.defaultPrevented` — and the bridge does not
  re-signal, because the browser already suppressed the default.
- **A new prevent re-crosses the boundary once.** When the guard intercepts an unmodified
  primary-button click it calls `PreventDefault()` on the `RouterLinkClickEvent`; the bridge mirrors
  that to `BrowserEvent.PreventDefault()`, whose response flag rides the single synchronous dispatch
  return back to JS (no extra interop crossing per click). `BrowserEvent` keeps the arrival-time and
  handler-requested prevents apart, so only the newly requested one re-crosses.
- **`target="_blank"` and non-primary/modified clicks** are the guard's own concern inside
  `RouterLink` (it reads its anchor's `target` attribute and the button/modifier state the bridge
  supplied); the bridge only carries the click metadata and honors the resulting decision.

## AOT / trimming

No reflection, no dynamic code generation: the mapping is direct field reads and one allocation per
intercepted click (the `RouterLinkClickEvent`). `IsAotCompatible=true`. The bridge uses only the
non-`[SupportedOSPlatform("browser")]` surface of `RuntimeDom` (`BrowserEvent`, `BrowserEventModifiers`,
`BrowserObjectEvents`), so it carries no platform gate of its own.

## Non-goals

- Real-browser click coverage — the end-to-end harness ([V01.01.11.03], #87). The tests here pin the
  mapping and plumbing DOM-free through the Testing renderer.
- A general host-event framework. The seam is deliberately minimal; `RouterLink` is its only consumer
  today. Other renderer-agnostic components would install their own invoker (or a composed one) the
  same way.
