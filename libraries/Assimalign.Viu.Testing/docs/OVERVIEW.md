# Assimalign.Viu.Testing — overview

The DOM-free testing package — the C# counterpart of
[`@vue/runtime-test`](https://github.com/vuejs/core/tree/main/packages/runtime-test) (the in-memory
renderer and node-op log) plus [`@vue/test-utils`](https://test-utils.vuejs.org) (the `mount` entry
and the component wrapper). Everything runs on a plain CoreCLR test host — no browser, no WASM
toolchain, no JS interop. Area: `V01.01.11`.

## Public surface

- **`ViuTest`** (static) — `Mount<TComponent>(component, options?)` (Vue's `mount`): renders a
  component against the in-memory renderer and returns a `ComponentWrapper`. The caller supplies the
  component instance (source-generated factories, never reflection activation).
- **`TestRenderer`** — the ready-to-use in-memory renderer (`@vue/runtime-test`'s `render` + `nodeOps`
  pair): `Render`, `CreateContainer`, `RegisterQueryRoot` (makes an element findable by a
  `<Teleport>` string target — the in-memory stand-in for the DOM `querySelector`; render containers
  are auto-registered), the underlying `Renderer<TestNode>`, and the `OperationLog` every node
  operation is recorded into.
- **The in-memory node tree** (`Nodes/`) — `TestNode` (abstract), `TestElement`, `TestText`,
  `TestComment`; `TestNodeOperations` (the `RendererOptions<TestNode>` factory); the op log
  (`TestNodeOperationLog`, `TestNodeOperation`, `TestNodeOperationType`); `TestNodeSerializer`
  (tree-to-string for snapshot assertions); and `TestEventDispatcher`.
- **Wrappers** (`Wrappers/`) — `ComponentWrapper` (`IDisposable`: query, interact, assert, and
  unmount on dispose), `ElementWrapper`, and `ComponentMountOptions` (props, slots, provides,
  registered components, stubs).
- **`TestSchedulerPump`** — the deterministic scheduler pump the mount installs so async update
  helpers are reproducible.

## Boundaries

- References **`Assimalign.Viu.Core`** (and transitively Shared) only. Ships as a
  net10.0 library with `IsAotCompatible=true`.
- It is a **platform package** in the same sense as `Assimalign.Viu.RuntimeDom` — it supplies a
  `RendererOptions<TNode>` (here `TNode = TestNode`) to the shared renderer — but its platform is an
  in-memory tree instead of the browser DOM.
- Design rationale and the determinism model: [DESIGN.md](DESIGN.md).
