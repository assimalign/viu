# Assimalign.Viu.RuntimeCore — overview

The platform-agnostic runtime — the C# port of
[`@vue/runtime-core`](https://github.com/vuejs/core/tree/main/packages/runtime-core): the virtual
DOM model, the renderer factory, the scheduler, the component model, provide/inject, the runtime
directive system, and the built-in component scaffolding. It performs no DOM or JS interop itself —
a platform package supplies the node-ops (the browser's `Assimalign.Viu.RuntimeDom`, tests'
`Assimalign.Viu.Testing`). Area: `V01.01.03`.

## Public surface

- **Virtual DOM model** (`src/` root) — `VirtualNode` (the unified vnode: element / component /
  text / comment / static / fragment / teleport via `VirtualNodeType` + a shape-flag bitmask),
  `VirtualNodeFactory` (including `Teleport`/`TeleportBlock`), `VirtualNodeProperties`.
- **Renderer** (`Rendering/`) — `RendererFactory.CreateRenderer(options)` (Vue's `createRenderer`),
  `Renderer<TNode>` (the mount/patch/unmount pipeline, including the `Teleport` built-in as a special
  vnode type in the patch/move/unmount paths — [V01.01.03.17], and the `KeepAlive` activate/deactivate
  operations its `processComponent`/`unmount` shape-flag branches short-circuit into — [V01.01.03.18]),
  `RendererOptions<TNode>` (the injected platform node-ops, whose optional `QuerySelector` resolves a
  Teleport string target), `RenderEffect<TNode>` (reactive re-render integration), and `BlockToken`.
- **Scheduler** (`Scheduling/`) — `Scheduler` (batched flush phases and `NextTick`) and
  `SchedulerJob`.
- **Component model** (`Components/`) — `ComponentInstance`, `ComponentSetupContext`,
  `ComponentProperties` / `ComponentPropertyDefinition` (props declaration and validation),
  `ComponentAttributes` (attrs fallthrough), `ComponentEmitDefinition` (emits), `ComponentSlots`,
  `Lifecycle` (the hook registration facade, including `OnActivated`/`OnDeactivated`),
  `DynamicComponents`, the `KeepAlive` caching built-in ([V01.01.03.18]), `AsyncComponents`
  (`DefineAsyncComponent`) with `AsyncComponentOptions` and the `AsyncComponentLoader` /
  `AsyncComponentErrorHandler` delegates ([V01.01.03.16]), and the transition scaffolding
  (`BaseTransition`, `BaseTransitionProperties`, `TransitionState`).
- **Application / plugins** — `Application<TNode>` (Vue's `createApp` shell: one root mounted into
  one container), `ApplicationConfiguration` (error/warn handlers, performance flag),
  `IComponentDefinition`, `IPlugin<TNode>`, and `ISuspenseBoundary` (the async-component / Suspense
  registration seam completed by [V01.01.03.20]).
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
