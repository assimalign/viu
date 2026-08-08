# Assimalign.Viu.ServerRenderer design

## One-shot host model

ServerRenderer is a serializer over Components' existing `VirtualNode` algebra. It does not define a
parallel server node model or depend on Core's persistent mounted renderer. `ServerRenderApplication`
is immutable per-render composition and deliberately does not participate in browser application
middleware (`[SSR-2]`, `[SSR-3]`).

A component subtree is obtained only through
`ComponentHost.RenderAsync(ComponentRenderRequest)`. The returned `IComponentRenderScope` keeps the
authored instance, context, reactive scope, and parent relationship alive while serialization reads
`scope.Tree`. Nested component requests use that still-live scope as their parent. Disposal cancels
the lifetime, stops its scope, and disposes the authored instance without invoking client mount or
unmount hooks (`[SSR-4]`, `[SSR-5]`, `[SSR-10]`).

This lease is the complete runtime seam. ServerRenderer does not downcast `ComponentContext`, reach
internal mounted state, probe hidden capabilities, or require friend access (`[CMP-33]`).

## Serialization

The serializer exhaustively dispatches all ten `VirtualNodeKind` values. Elements apply the escaping,
attribute-name safety, boolean-attribute, casing, class/style, child-override, and raw-HTML rules in
`[SSR-6]`. Static HTML is emitted only from compiler-trusted `StaticNode` values; static Extensible
Markup Language payloads are rejected because they require a different serializer.

`RenderToStringAsync` accumulates one result. `RenderToStreamAsync` writes to the caller's
`TextWriter` and flushes after a completed component subtree, so the destination controls
backpressure. The renderer borrows the writer and never closes or disposes it (`[SSR-1]`).

Ordinary client lifecycle hooks do not run. Core awaits every `OnServerPrefetch` callback before the
first render, and cancellation interrupts that wait. Suspense serializes its resolved default branch;
KeepAlive and Transition serialize their lazy content without client-only behavior.

## Hydration protocol and teleports

Core's `HydrationMarkers` is the only source of fragment and teleport marker text. ServerRenderer
consumes those values directly so server output and client hydration cannot drift
(`[SSR-MARKERS-1]` through `[SSR-MARKERS-3]`).

An enabled teleport emits origin anchors and buffers its children plus the target anchor. A disabled
teleport renders children at the origin and contributes only the target anchor. `SsrContext` owns
those per-render buffers and a free-form state handoff bag; the serializer does not interpret the
state values (`[SSR-7]`).

## Composition, ownership, and AOT

The application supplies a component factory, nullable services, state registry, directives, and
diagnostics through `IApplicationContext`. ServerRenderer borrows all of them, and a host should use
one application per request when those dependencies are request-scoped (`[CMP-9]`, `[SSR-9]`). No
Viu library references a web framework; a web adapter is downstream and maps its request, response,
services, abort token, and state separately (`[SSR-8]`).

Component activation is explicit and registration-based. Serialization dispatches known node kinds
and binding forms without runtime type discovery, reflection-based serialization, emitted code, or
dynamic activation (`[EXE-4]`).

## Non-goals

ServerRenderer does not own request scopes, web responses, browser application lifetime, DOM
hydration, client directives, transition timing, persistent mounted state, or dependency disposal.
Scoped style stamping is absent while scoped CSS is deferred (`[STY-1]`).
