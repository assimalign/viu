# Assimalign.Vue.RuntimeDom — design

Why the browser package is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md).
Upstream counterpart: `@vue/runtime-dom`
(https://github.com/vuejs/core/tree/main/packages/runtime-dom).

## The boundary is the budget

Every node-op is (potentially) a marshaled interop call, so the package is organized around
spending as few as possible and keeping each one cheap:

- **Decision logic lives in .NET; JS stays a dumb applier.** The patchProp decision tree
  (`BrowserPropertyPatcher`) resolves class/style/prop-vs-attribute entirely in C# and lands at
  most one leaf call per resolution. This keeps every leaf expressible as an opcode for the
  planned command buffer ([V01.01.04.05]) and makes the tree unit-testable with no DOM
  (recorded leaf delegates).
- **Int handles, not `JSObject` proxies**, identify nodes across the boundary — measured
  decision, see [ADR-0001](ADR-0001-interop-marshaling.md).
- **Value writes are compare-and-set in JS** (`setValueGuarded`): one interop call that skips
  the DOM write when unchanged, so caret and IME state survive re-renders without a read call.
- **The renderer passes the element tag into `patchProp`** (a `PatchPropertyDelegate` parameter)
  because upstream reads `el.tagName` inside patchProp — which would be an interop round-trip
  per patch for a handle-based platform.

## Handle lifecycle (deterministic, two-sided)

The JS registry (`Map<handle, Node>` + per-handle listener maps) is released *deterministically*,
never swept:

- `remove(handle)` detaches the node, walks the removed subtree, releases every registered
  descendant handle and its DOM listeners, and **returns the released handles**; the C# side
  purges its listener delegates for those handles in the same call.
- `setElementText` does the same for replaced children before writing.
- The `?diagnostics=1` mode of the example app runs the mount/unmount stress cycle behind the
  [V01.01.04.01] criterion; 100 cycles of a listener-carrying tree return the registries to
  baseline exactly (verified live 2026-07-17).

## Failure semantics

Bridge failures surface as typed `BrowserDomException`s carrying the operation name and handle.
The JS side throws structured `vuecs-dom|operation|handle|message` errors for its own guard
failures (unknown handle, bad anchor, unparsable tag); the C# wrapper layer translates those and
any other `JSException` at the op that raised it.

## Knowledge sets instead of `key in el`

Upstream's `shouldSetAsProp` probes the live element (`key in el`). A handle platform cannot
without paying a round-trip, so the patcher uses curated sets for the criteria-named cases
(boolean IDL properties, boolean attributes whose IDL name differs, enumerated attributes) and
falls back to the attribute path for unknown keys — upstream's own fallback. The full per-tag
knowledge tables are [V01.01.01.03] (#5); style-value normalization (camelCase keys, arrays)
is [V01.01.01.02] (#4). Until then style map keys are CSS property names (kebab-case or
`--custom`).

## Shipping the JS half (WasmAppHost constraint)

The bridge module (`src/wwwroot/vuecs-dom.js`) is owned by this package and loaded by
`BrowserRuntime.InitializeAsync` from `/_content/Assimalign.Vue.RuntimeDom/vuecs-dom.js`. The
canonical RCL (Razor SDK) static-web-asset flow is **not usable here**: measured on .NET SDK
10.0.10, the non-Blazor WebAssembly dev host (WasmAppHost) stops serving the app entirely when
it references a Razor-SDK class library, and its dev serving pattern only maps the app's source
`wwwroot/`. The library therefore stays on the plain SDK and the central build
(`build/Targets/Build.StaticWebAssets.targets`) copies referenced libraries' `wwwroot/**` into a
consuming WASM app's source `wwwroot/_content/<AssemblyName>/` (gitignored) — same URL shape as
the RCL convention, so call sites won't change when NuGet packaging ships the files as real
static web assets later.

## Events: the invoker pattern ([V01.01.04.03])

One JS listener is attached per (element, event, capture); a re-rendered handler is a .NET
delegate swap on the invoker — zero `addEventListener`/`removeEventListener` interop between
renders. Prop-name suffixes (`onClickOnce`/`Capture`/`Passive`, combined) map to listener
options at attach time. The listener applies Vue's attach-timestamp guard JS-side
(`e.timeStamp < attached` ignores events that fired before their patch attached the listener —
zero interop for guarded events) and forwards the complete typed payload as primitives through
the single `[JSExport]` dispatch entry (`BrowserEventDispatch.DispatchBrowserEvent`), which
returns stop/prevent flags the listener applies to the live event. `BrowserEvents.WithModifiers`
and `.WithKeys` port `withModifiers`/`withKeys` (guards run .NET-side over the payload).
Handler exceptions route to the registry's error sink — a debug trace until the app
error-handling pipeline ([V01.01.03.12]) replaces it — and never escape into the JS listener.

## Non-goals (sequenced work)

- Interop command-buffer batching — [V01.01.04.05] (#43).
- App bootstrap (`CreateApp`-equivalent, container clearing) — [V01.01.04.04] (#42).
- `v-model` runtime — [V01.01.04.06].
