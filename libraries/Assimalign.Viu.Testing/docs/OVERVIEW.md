# Assimalign.Viu.Testing — overview

The DOM-free testing package — the C# counterpart of
[`@vue/runtime-test`](https://github.com/vuejs/core/tree/main/packages/runtime-test) (the in-memory
renderer and node-op log) plus [`@vue/test-utils`](https://test-utils.vuejs.org) (the `mount` entry
and the component wrapper). Everything runs on a plain CoreCLR test host — no browser, no WASM
toolchain, no JS interop. Area: `V01.01.11`.

## Public surface

- **`ViuTest`** — mounts either an immutable `IComponent` tree or a caller-supplied
  `IComponentTemplate`. Both paths can use the real Core `ApplicationContext`; template mounting
  activates the supplied root once, and child activation delegates to
  `ComponentMountOptions.Components`.
- **`TestRenderer`** — the ready-to-use in-memory renderer (`@vue/runtime-test`'s `render` + `nodeOps`
  pair): `Render`, `Hydrate`, `CreateContainer`, the underlying `Renderer<TestNode>`, and the
  `OperationLog` every node operation is recorded into. `RegisterQueryRoot` makes detached roots
  searchable by Core's Teleport target resolver. The constructor can select live-tree hydration or
  immutable snapshot semantics.
- **The in-memory node tree** (`Nodes/`) — `TestNode` (abstract), `TestElement`, `TestText`,
  `TestComment`; `TestNodeOperations` (the `RendererOptions<TestNode>` factory); the op log
  (`TestNodeOperationLog`, `TestNodeOperation`, `TestNodeOperationType`); `TestNodeSerializer`
  (tree-to-string for snapshot assertions); `TestServerMarkup` (server-fragment parser);
  `TestHydrationReader` and `FrozenTestHydrationReader`; and `TestEventDispatcher`.
- **Wrappers** (`Wrappers/`) — `ComponentWrapper` (`IDisposable`: query, interact, assert, inspect
  root or child templates and contexts, capture per-template events, and unmount the root on
  dispose), `ElementWrapper`, and
  `ComponentMountOptions` (arguments, slots, listeners, component factory, child stubs, service
  provider, state registry, directive resolver, and application configuration).
- **`TestSchedulerPump`** — the deterministic scheduler pump the mount installs so async update
  helpers are reproducible.

## Boundaries

- References **`Assimalign.Viu.Core`** and consumes its Components, Reactivity, and State contracts
  transitively. Ships as a
  net10.0 library with `IsAotCompatible=true`.
- It is a **platform package** in the same sense as `Assimalign.Viu.Browser` — it supplies a
  `RendererOptions<TNode>` (here `TNode = TestNode`) to the shared renderer — but its platform is an
  in-memory tree instead of the browser DOM. Teleports, scoped-style identifiers, and runtime
  directives use the same Core paths as another host.
- It does not recreate component-tree provide/inject. Components resolve application dependencies
  through `IComponentContext.Services`, and tests supply that `IServiceProvider` explicitly.
- Core grants Testing friend-only read access to its mounted template graph. `FindComponent` and
  `GetComponent` can therefore return wrappers scoped to a child's own host range without making
  mounted renderer state public to applications.
- Core's internal application event observer identifies the emitting `IComponentContext`.
  `Emitted` therefore captures declared and undeclared events during setup or later and keeps
  parent and child histories separate without replacing parent listeners.
- Hydration adopts the same unified `IComponent` tree used by mounting. Matching elements,
  fragments, template subtrees, listeners, and Teleport targets retain their server nodes; Core
  recovers mismatches at the smallest host range and falls back to a full mount for an empty
  container.
- Design rationale and the determinism model: [DESIGN.md](DESIGN.md).
