# Assimalign.Vue.RuntimeDom — overview

The browser package of Vuecs — the role `@vue/runtime-dom` plays for Vue 3
(https://github.com/vuejs/core/tree/main/packages/runtime-dom): it supplies the real-DOM
node-ops and prop patching that the platform-agnostic renderer
(`Assimalign.Vue.RuntimeCore`) drives.

## What it contains

- **`BrowserRuntime`** (public entry): `InitializeAsync()` loads the package's JS bridge module;
  `CreateRenderer()` returns a `Renderer<int>` over the browser node-ops; `QuerySelector()`
  resolves mount containers; `GetRegistryDiagnostics()` exposes handle-registry sizes for leak
  checks.
- **`BrowserDomException`** (public): typed interop failure carrying the operation name and the
  node handle.
- **`vuecs-dom.js`** (`src/wwwroot/`, shipped with the package): the JS half — the handle
  registry, the `nodeOps` leaves (create/insert/remove/text/static-content, SVG and MathML
  namespaces), the property/style/attribute leaf appliers, and minimal event listener wiring.
- **Internal:** `BrowserDomBridge` (`[JSImport]` bindings + typed-error wrappers),
  `BrowserPropertyPatcher` + `BrowserPropertyLeafOperations` (the `patchProp` decision tree over
  injected leaves), `BrowserNodeOperations` (the `RendererOptions<int>` factory and event
  dispatch).

## Using it

```csharp
await BrowserRuntime.InitializeAsync();
var renderer = BrowserRuntime.CreateRenderer();
var container = BrowserRuntime.QuerySelector("#app");
renderer.CreateRenderEffect(BuildView, container); // reactive mount
```

The example app (`examples/Assimalign.Vue.WebApp`) is the living usage sample; its
`?diagnostics=1` mode runs the handle-lifecycle stress check and the marshaling benchmark
behind [ADR-0001](ADR-0001-interop-marshaling.md).

## Boundaries

- May reference `Assimalign.Vue.RuntimeCore` (and transitively Shared/Reactivity) only.
- All interop stubs come from the `[JSImport]` source generator — no reflection, AOT/trimming
  safe, browser main thread only.
- Design rationale and sequenced non-goals: [DESIGN.md](DESIGN.md).
