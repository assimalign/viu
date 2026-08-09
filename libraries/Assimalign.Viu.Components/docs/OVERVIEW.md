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
earn a context member. The model deliberately carries no style-scope identity; compiled trees stamp
their static scope id as an ordinary element attribute (`[STY-1]`).

`ComponentBindings.Resolve` performs the full pure resolution step: exact, camelized, and
hyphenated parameter aliases; declared-listener and node-lifecycle filtering; fallthrough splitting;
and diagnostics for alias collisions, failed validators, and missing required inputs. It deliberately
does not evaluate defaults or gate warning delivery. Those are mounted-instance responsibilities, so
the runtime can cache a default once and warn at the correct lifecycle boundary ([CMP-12]).

`ComponentLifecycle` carries the complete named hook surface ([CMP-20] through [CMP-22]). Ordinary
asynchronous hooks are observed without delaying progression, server prefetch is awaited, and hidden
runtime operations drive error routing, cancellation, observer draining, and disposal. The public
surface never exposes an enum-keyed callback registry.

A `ComponentRenderer` receives its mount's `ComponentRenderFrame` — the per-mount render cache
and block assembly — so there is no ambient render-helper state and no public static helper
class; compiled output binds through the frame parameter, never through statics imported by name.
The frame supports nested/disabled block tracking, compiler-sized cache slots, stable handler caching, and
memo dependency snapshots. Block snapshots preserve ordered occurrences, including repeated node
references; cached static subtrees retain description identity for one mount while every render
position keeps independent mounted state ([RND-2], [RND-4], [SFC-OPT-1]).
Code-first components are `ComponentRegistration.Define(name, contract, setup)` wrapping a
`ComponentSetup` delegate (composition-only per
[ADR-0004](../../../../docs/adr/0004-composition-only-component-model.md); no options-object
form); hand-built subtrees carry `RenderPlan.None` and patch by full diff unless plans are
supplied.

`ComponentInvocation.HydrationStrategy` is immutable, host-neutral metadata for adopting a
server-rendered component now and activating it later. Immediate is the default; idle, visible,
media-query, and interaction strategies carry only timing or matching data. Components never owns a
browser observer, event listener, or scheduler callback (`[HYD-LAZY-1]`).

Adopted disposition:
[`../../../../docs/COMPONENT-MODEL-PLAN.md`](../../../../docs/COMPONENT-MODEL-PLAN.md) §2/§2a.
