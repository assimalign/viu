# Assimalign.Viu.Components

Components is **the component model**: the closed, immutable, host-neutral virtual-tree algebra
*and* the authored-component contract. It references only Reactivity — change tracking is
intrinsic to the model (a component is a reactive render function; the tree is its output).

The tree currency types are `VirtualNode`, `VirtualNodeKind`, `QualifiedName`, and `RenderPlan`
(carrying the frozen `PatchFlags` semantics). Sealed variants describe ordinary structure,
component invocations, teleports, and the built-in control nodes — `KeepAliveNode`,
`SuspenseNode`, and `TransitionNode` each carry a `ComponentInvocation` whose slots stay lazy at
description time. External renderer-unknown virtual-node subclasses are deliberately unsupported.

The authored contract is `IComponent { ComponentRenderer Setup(ComponentContext) }` with the
public abstract `ComponentContext` (Bindings, Services, Lifecycle, Scope, WatchScheduler, Parent,
Emit, Expose, Warn, concrete Watch), `ComponentBase`, `ComponentBindings` and its pure static
`Resolve`, `ComponentLifecycle`, `ComponentRegistration (Reference, Contract, Activator)`, and
`IComponentFactory`/`ComponentFactory`. The runtime provides the single context implementation;
conventions such as State attach only through `Services` and the ambient reactive scope and never
earn a context member. Deliberately absent: any style-scope identity — scoped CSS is deferred.

A `ComponentRenderer` receives its mount's `ComponentRenderFrame` — the per-mount render cache
and block assembly — so there is no ambient render-helper state and no public static helper
class; compiled output binds through the frame parameter, never through statics imported by name.
Code-first components are `ComponentRegistration.Define(name, contract, setup)` wrapping a
`ComponentSetup` delegate (composition-only per
[ADR-0004](../../../../docs/adr/0004-composition-only-component-model.md); no options-object
form); hand-built subtrees carry `RenderPlan.None` and patch by full diff unless plans are
supplied.

Adopted disposition: [`../../../../REDESIGN-REVIEW.md`](../../../../REDESIGN-REVIEW.md) §2/§2a.
