# Assimalign.Viu.Components

`Assimalign.Viu.Components` owns Viu's platform-neutral component model: the closed immutable
virtual-node vocabulary, authored component behavior, static contracts and registrations,
parent-created invocations, mounted bindings, lifecycle authoring, and compiler-produced render
plans. The adopted surface is specified by `[CMP-1]`–`[CMP-34]`, `[RND-1]`–`[RND-BLOCK-7]`, and
`[BLT-1]`–`[BLT-15]` in
[`docs/SPECIFICATION.md`](../../../docs/SPECIFICATION.md).

The `[V01.01.15]` documentation describes the standard being implemented on
`feature/V01.01.15-component-model`; the source migration is an atomic swap. The exact scaffold is
under `.redesign/libraries/Assimalign.Viu.Components/src` until that swap completes.

## Four lifetimes

The package names four roles separately:

| Role | Main types | Lifetime |
| --- | --- | --- |
| Immutable render description | `VirtualNode` and sealed variants | One render |
| Static identity, contract, and activation | `ComponentReference`, `ComponentContract`, `ComponentRegistration` | One registration |
| Activated authored behavior | `IComponent` | One mounted invocation |
| Resolved mounted capabilities | Components-owned `ComponentContext` contract; Core-owned runtime implementation and bookkeeping | Mount through unmount |

`IComponent` has one member: synchronous
`ComponentRenderer Setup(ComponentContext context)`. The returned renderer receives that mount's
`ComponentRenderFrame` and produces a fresh `VirtualNode?` description. `ComponentBase` is optional
authoring storage for the mounted context; it deliberately does not implement `IComponent`.

## Closed virtual-node vocabulary

`VirtualNode` has a `private protected` constructor, and each concrete node variant is sealed with a
fixed `VirtualNodeKind`. The ten kinds are `Element`, `Text`, `Comment`, `Static`, `Fragment`,
`Component`, `Teleport`, `KeepAlive`, `Suspense`, and `Transition`; their concrete types are
`ElementNode`, `TextNode`, `CommentNode`, `StaticNode`, `FragmentNode`, `ComponentNode`,
`TeleportNode`, `KeepAliveNode`, `SuspenseNode`, and `TransitionNode`. Kind and runtime type
therefore cannot disagree.

`ElementNode` carries a `QualifiedName`, immutable `ElementBinding` values, directives, and
children. `ComponentNode` is a non-activating request carrying a `ComponentReference` and raw
`ComponentInvocation`. The structural KeepAlive, Suspense, and Transition nodes carry lazy
invocations; their executors remain internal to Core. `TeleportNode` retains target identifier,
disabled, and deferred-target semantics.

## Registration, invocation, and bindings

`ComponentRegistration` is exactly a reference, a static contract, and an explicit activator.
`IComponentFactory.Resolve` and `TryResolve` map a `ComponentReference` to that registration; the
factory performs no constructor discovery or activation. Activators receive the nullable borrowed
application service provider, keeping construction explicit and AOT-safe.

`ComponentInvocation` snapshots raw parent arguments, slots, listeners, and directives.
`ComponentBindings.Resolve(contract, invocation, diagnostics)` produces the mounted view of
declared parameters, effective slots, and undeclared fallthrough bindings. These types deliberately
share no interface because they belong to different lifetimes. A contract is readable before the
activator runs.

`ComponentRegistration.Define(name, contract, setup)` is the small code-first path. It accepts the
same composition closure shape as `IComponent.Setup`; there is no configuration-object form.
Hand-built virtual trees use `RenderPlan.None` and the correct full diff unless the author supplies
compiler-equivalent plans.

## Mounted authoring surface

Core supplies the runtime implementation of abstract `ComponentContext`. It exposes resolved
`Bindings`, nullable `Services`, `Lifecycle`, the component-owned reactive `Scope`, nullable
`WatchScheduler`, `Parent`, `Emit`, `Expose`, `Warn`, and scoped `Watch`. The context has no
convention-specific member and no capability-by-cast protocol. State, Router, and future
conventions attach through `Services` plus the ambient reactive scope.

`ComponentContract` declares parameters and events before activation. `ComponentEventListener`
receives one immutable ordered argument list. Attribute-authored parameters and events are generator
inputs that produce the same static contract and per-render property rebinding; runtime reflection
is never involved. `ComponentLifecycle` owns named registration methods and the mounted lifetime's
cancellation token.

## Compiler and runtime contract

`RenderPlan` carries `PatchFlags`, dynamic binding indices, and the three-state dynamic-child list:
null means no block, an empty list means a static block, and a non-empty list is the direct patch
list. `PatchFlags`, `ShapeFlags`, and `SlotFlags` are Components-owned frozen compiler/runtime value
contracts. Generated statement-form code assembles blocks through
`ComponentRenderFrame.OpenBlock`, `Track`, and `CloseBlock`; its cache and handler identity are
per-mount frame state.

Generated `.viu` and compatible `.vue` components implement `IComponent`, emit a static contract on
their registration, and use the frame-based renderer without static helper imports or reserved
underscore names. Development builds register hot-reload metadata through the hidden
`ComponentHotReload` ABI. Scoped-CSS identity is deferred until after the component-model arc and is
not part of the adopted context, contract, or virtual-node surface.

## Boundary

Components depends only on Reactivity. It does not depend on State, Core, a renderer, or a host.
Core owns activation and mounted bookkeeping; hosts depend on Core; conventions depend on
Components and attach through the designed seams. The complete layer charter and migration trains
are in the
[`COMPONENT-MODEL-PLAN.md`](../../../docs/COMPONENT-MODEL-PLAN.md) plan of record.
