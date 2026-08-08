# Assimalign.Viu.Router design

## Dependency boundary

The package has two production dependencies: Components supplies `VirtualNode`, `ComponentNode`,
component registration, and authored component contexts; Reactivity supplies the shallow current
route. Core and Browser remain downstream. Consequently matching and memory history can run without
a renderer or host, and Router attaches to mounted applications only through
`ComponentContext.Services` and application composition (`[RTR-1]`, `[RTR-7]`, `[CMP-33]`).

Browser.Router exists as a separate leaf package so neither Router nor Browser acquires the other's
dependency graph. Router still owns its web and hash history adapters: their JavaScript module calls
Router's generated export dispatch, and initialization is deliberately deferred until
`Router.ReadyAsync` (`[RTR-3]`).

## Immutable route output

A route record stores `VirtualNode?`, not an activation callback. Matching therefore produces a
description and never mounts a component. A `ComponentNode` continues to carry a
`ComponentReference`, immutable `ComponentInvocation`, key, mount reference, and `RenderPlan`; Core
resolves and activates it later (`[CMP-7]`).

`RouterView` treats that node as an immutable copy boundary. Route-derived arguments override
authored arguments, while every other invocation channel is retained. The view does not mutate the
record's node or resolve its registration. Record identity participates in the effective key so
different route records cannot accidentally share mounted state; the authored key still
distinguishes intentional variants within one record.

Non-component nodes pass through unchanged. This permits redirect placeholders, static content, or
other host-neutral tree descriptions without manufacturing a component wrapper.

## Component integration

`RouterView` and `RouterLink` expose static reflection-free `ComponentRegistration` values and
implement `IComponent.Setup(ComponentContext)`. `RouterResolution` consults nullable services only;
there is no context cast, friend access, or bridge interface (`[CMP-8]`, `[CMP-33]`, `[RTR-4]`).

The view's depth is an explicit declared parameter. That preserves the component model's decision
that parent-to-child data uses parameters and slots rather than an ambient component-tree dependency
API (`[CMP-24]`). `RouterLink` similarly emits a host-neutral click value. It owns navigation
eligibility and router calls, while a host adapter owns conversion from its native event shape.

## Navigation and lifetime

Navigation follows an ordered state transition: resolve the target, classify duplication, run global
and record guards, commit history, publish `CurrentRoute`, then notify after-navigation hooks.
Redirects restart resolution with loop protection. Every non-success result is represented by the
typed failure model in `[RTR-5]` and `[RTR-6]`.

Web and hash history need asynchronous module initialization, so no constructor blocks on
JavaScript. `ReadyAsync` completes initialization and the initial navigation before an application
mounts routed content. Application lifetime ordering is supplied by Browser.Router, not by this
package.

## AOT and WASM seams

The browser history boundary uses `JSImport` and `JSExport` declarations with primitive snapshots.
`viu-history.js` and its `buildTransitive` target stay package-owned and are copied into consuming
applications. No reflection-based marshalling, runtime serialization, constructor probing, or
dynamic code generation is permitted (`[RTR-3]`, `[EXE-4]`, `[EXE-7]`).

## Non-goals

Router does not own component activation, mounted state, rendering, DOM event policy, application
shutdown, dependency disposal, lazy route-component loading, or automatic scroll restoration. The
last two are explicit current limits rather than hidden partial implementations (`[CMP-9]`,
`[RTR-8]`).
