# Assimalign.Viu.RuntimeCore — overview

The platform-agnostic runtime — the C# port of
[`@vue/runtime-core`](https://github.com/vuejs/core/tree/main/packages/runtime-core): the virtual
DOM model, the renderer factory, the scheduler, the component model, provide/inject, the runtime
directive system, and the built-in component scaffolding. It performs no DOM or JS interop itself —
a platform package supplies the node-ops (the browser's `Assimalign.Viu.RuntimeDom`, tests'
`Assimalign.Viu.Testing`). Area: `V01.01.03`.

## Public surface

- **Virtual DOM model** (`src/` root) — `VirtualNode` (the unified vnode: element / component /
  text / comment / static / fragment via `VirtualNodeType` + a shape-flag bitmask),
  `VirtualNodeFactory`, `VirtualNodeProperties`.
- **Renderer** (`Rendering/`) — `RendererFactory.CreateRenderer(options)` (Vue's `createRenderer`),
  `Renderer<TNode>` (the mount/patch/unmount pipeline), `RendererOptions<TNode>` (the injected
  platform node-ops), `RenderEffect<TNode>` (reactive re-render integration), and `BlockToken`.
- **Scheduler** (`Scheduling/`) — `Scheduler` (batched flush phases and `NextTick`) and
  `SchedulerJob`.
- **Component model** (`Components/`) — `ComponentInstance`, `ComponentSetupContext`,
  `ComponentProperties` / `ComponentPropertyDefinition` (props declaration and validation),
  `ComponentAttributes` (attrs fallthrough), `ComponentEmitDefinition` (emits), `ComponentSlots`,
  `Lifecycle` (the hook registration facade), `DynamicComponents`, and the transition scaffolding
  (`BaseTransition`, `BaseTransitionProperties`, `TransitionState`).
- **Application / plugins** — `Application<TNode>` (Vue's `createApp` shell: one root mounted into
  one container), `ApplicationConfiguration` (error/warn handlers, performance flag),
  `IComponentDefinition`, `IPlugin<TNode>`.
- **Provide / inject** (`DependencyInjection/`) — `DependencyInjection` and the typed
  `InjectionKey<T>`.
- **Directives** (`Directives/`) — `IDirective`, `Directive`, `Directives`, `DirectiveBinding`,
  `DirectiveArgument`, plus the `DirectiveHook` delegate.
- **Watch** (`Watch/`) — `ViuWatch`, the runtime-scheduler-integrated `watch`/`watchEffect` over the
  `Assimalign.Viu.Reactivity` primitives.
- **`RenderHelpers`** — the static helper surface (`_openBlock`, `_createElementBlock`,
  `_toDisplayString`, `_renderList`, …) that compiler-generated render methods bind to **by name**.

## Boundaries

- References **`Assimalign.Viu.Shared`** (the flag vocabulary) and **`Assimalign.Viu.Reactivity`**
  only; it never references a platform/DOM package. Ships as a net10.0 runtime library with
  `IsAotCompatible=true`.
- **Composition-only** component model — no Options API, no mixins, no `app.config.globalProperties`
  (see [ADR-0004](../../../docs/adr/0004-composition-only-component-model.md)).
- Design rationale, the renderer seam, and the render-helper contract: [DESIGN.md](DESIGN.md).
