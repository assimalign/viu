# Assimalign.Viu.RuntimeDom — overview

The browser package of Viu — the role `@vue/runtime-dom` plays for Vue 3
(https://github.com/vuejs/core/tree/main/packages/runtime-dom): it supplies the real-DOM
node-ops and prop patching that the platform-agnostic renderer
(`Assimalign.Viu.RuntimeCore`) drives.

## What it contains

- **`BrowserRuntime`** (public entry): `InitializeAsync()` loads the package's JS bridge module;
  `CreateApp(rootComponent).Mount("#app")` is a whole app bootstrap ([V01.01.04.04]);
  `CreateApp(root, props, useCommandBuffer: true)` runs the renderer over the batched interop command
  buffer ([V01.01.04.05], behaviorally identical to direct); `CreateRenderer()` returns a
  `Renderer<int>` over the browser node-ops; `QuerySelector()` resolves mount containers;
  `GetRegistryDiagnostics()` exposes handle-registry sizes for leak checks.
- **`BrowserApplication`** (public): the mounted app — selector or handle mounting with
  clear-before-mount, and `Unmount()` returning the bridge registry to its pre-mount baseline.
- **`BrowserDomException`** (public): typed interop failure carrying the operation name and the
  node handle.
- **`viu-dom.js`** (`src/wwwroot/`, shipped with the package): the JS half — the handle
  registry, the `nodeOps` leaves (create/insert/remove/text/static-content, SVG and MathML
  namespaces), the property/style/attribute leaf appliers, the per-(element, event)
  listeners feeding the single `[JSExport]` event dispatch, and `applyCommandBuffer` — the batched
  applier that replays one command-buffer frame per flush ([V01.01.04.05]).
- **`BrowserEvent` / `BrowserEventModifiers` / `BrowserEvents`** (public): the typed event
  payload with `StopPropagation()`/`PreventDefault()`, and the `WithModifiers`/`WithKeys`
  guard helpers ([V01.01.04.03]).
- **Internal:** `BrowserDomBridge` (`[JSImport]` bindings + typed-error wrappers),
  `BrowserPropertyPatcher` + `BrowserPropertyLeafOperations` (the `patchProp` decision tree over
  injected leaves), `BrowserNodeOperations` (the direct `RendererOptions<int>` factory and event
  dispatch), and the command-buffer trio `DomCommandOpcode` / `DomCommandBuffer` (the versioned
  opcode encoder) / `BufferedBrowserNodeOperations` (the buffered `RendererOptions<int>` that batches
  every write and applies once per flush).

## Using it

```csharp
await BrowserRuntime.InitializeAsync();
BrowserRuntime.CreateApp(new RootComponent()).Mount("#app");
```

The example app (`examples/Assimalign.Viu.WebApp`) is the living usage sample; its
`?diagnostics=1` mode runs the handle-lifecycle stress check and the marshaling benchmark
behind [ADR-0001](ADR-0001-interop-marshaling.md).

## Boundaries

- May reference `Assimalign.Viu.RuntimeCore` (and transitively Shared/Reactivity) only.
- All interop stubs come from the `[JSImport]` source generator — no reflection, AOT/trimming
  safe, browser main thread only.
- Design rationale and sequenced non-goals: [DESIGN.md](DESIGN.md).
