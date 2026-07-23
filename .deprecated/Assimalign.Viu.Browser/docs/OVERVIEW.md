# Assimalign.Viu.Browser — overview

The browser package of Viu — the role `@vue/runtime-dom` plays for Vue 3
(https://github.com/vuejs/core/tree/main/packages/runtime-dom): it supplies the real-DOM
node-ops and prop patching that the platform-agnostic renderer
(`Assimalign.Viu.Core`) drives.

## What it contains

- **`BrowserApplication`** (public entry): the mounted app. Build one with
  `BrowserApplication.CreateBuilder(rootComponent)` (or `CreateSsrBuilder(...)` to hydrate
  server-rendered markup, `CreateBuilder(root, props, useCommandBuffer: true)` to run the renderer over
  the batched interop command buffer, [V01.01.04.05], behaviorally identical to direct), then
  `await app.MountAsync("#app")`. It extends the platform-agnostic `Application<int>` base and
  **overrides the initialization seam** to load the JS bridge module inside its own mount path — so
  there is no external initialization pre-call ([V01.01.03.23]) — with selector/handle mounting,
  clear-before-mount, and `Unmount()` returning the bridge registry to its pre-mount baseline.
- **`BrowserApplicationBuilder`** (public): the `IApplicationBuilder` created by
  `BrowserApplication.CreateBuilder`/`CreateSsrBuilder`; records plugins/provides and, on `Build()`,
  constructs the app over the direct or command-buffered node-ops.
- **`BrowserRuntime`** (public, low-level): `InitializeAsync()` loads the JS bridge module (advanced —
  a normal app never calls it; `MountAsync` runs the same initialization internally); `CreateRenderer()`
  returns a `Renderer<int>` over the browser node-ops; `QuerySelector()` resolves mount containers;
  `GetRegistryDiagnostics()` exposes handle-registry sizes for leak checks.
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
await BrowserApplication.CreateBuilder(new RootComponent()).Build().MountAsync("#app");
```

The example app (`examples/Assimalign.Viu.WebApp`) is the living usage sample; its
`?diagnostics=1` mode runs the handle-lifecycle stress check and the marshaling benchmark
behind [ADR-0001](ADR-0001-interop-marshaling.md).

## Boundaries

- May reference `Assimalign.Viu.Core` (and transitively Shared) only.
- All interop stubs come from the `[JSImport]` source generator — no reflection, AOT/trimming
  safe, browser main thread only.
- Design rationale and sequenced non-goals: [DESIGN.md](DESIGN.md).
