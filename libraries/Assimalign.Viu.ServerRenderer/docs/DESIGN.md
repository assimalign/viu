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

The compiler's `ServerMarkup` target lowers a proven static/native region to direct
`SsrRenderState.Push` calls. Dynamic values use ServerRenderer's public escaping and normalization
helpers; components and unsupported binding shapes build only their local fallback subtree. The
public compiled-render entry point owns exactly the same cancellation, component-scope, teleport,
state, streaming, and flush protocol as the tree path. Executed differential fixtures, rather than
source snapshots alone, pin ordinal byte equality (`[SSR-COMPILE-1]` through `[SSR-COMPILE-4]`).

Ordinary client lifecycle hooks do not run. Core awaits every `OnServerPrefetch` callback before the
first render, and cancellation interrupts that wait. Suspense serializes its resolved default branch;
KeepAlive and Transition serialize their lazy content without client-only behavior.

## Hydration protocol and teleports

Core's `HydrationMarkers` is the only source of fragment and teleport marker text. ServerRenderer
consumes those values directly so server output and client hydration cannot drift
(`[SSR-MARKERS-1]` through `[SSR-MARKERS-3]`).

An enabled teleport emits origin anchors and buffers its children plus the target anchor. A disabled
teleport renders children at the origin and contributes only the target anchor. `SsrContext` owns
those per-render buffers and a `StateStorePayload` whose fixed schema is
`{"version":1,"stores":{"store-key":state}}`. After traversal, ServerRenderer captures only the
request registry's materialized stores and appends an inert `script[data-viu-state]` island. Store
definitions, not ServerRenderer, own the source-generated serializers (`[SSR-7]`,
`[V01.01.09.03]`, `[EXE-4]`).

A component invocation with a non-immediate hydration strategy receives one fixed opening marker
and `LazyHydrationEnd` after its fully rendered subtree. Marker text carries only the strategy kind;
strategy parameters remain invocation metadata used by the client host. An asynchronous definition
owns the outer marker and strips the strategy from its resolved target, preventing nested duplicate
boundaries (`[SSR-MARKERS-1]`, `[HYD-LAZY-1]`, `[HYD-LAZY-2]`).

## Composition, ownership, and AOT

The application supplies a component factory, nullable services, state registry, directives, and
diagnostics through `IApplicationContext`. ServerRenderer borrows all of them, and a host should use
one application per request when those dependencies are request-scoped (`[CMP-9]`, `[SSR-9]`). No
Viu library references a web framework; a web adapter is downstream and maps its request, response,
services, abort token, and state separately (`[SSR-8]`).

`ServerRenderAdaptor<TContext>` makes that downstream boundary executable without naming an HTTP
stack. A typed `ServerRenderRequest<TContext>` carries the root plus the host's own request context;
`IServerRenderRequestScopeFactory<TContext>` must return an async-disposable scope containing a fresh
`ServerRenderApplication` and `SsrContext`. The adaptor invokes the factory once, consumes both weak
identities before validating that the application uses the requested root, streams through
`IServerRenderOutput`, and disposes the scope on success, failure, or cancellation. Consuming both
identities matters because a rejected scope is disposed and neither object may safely reappear.
Weak identity tracking prevents reuse without retaining completed requests (`[V01.01.07.04]`).

Both renderer entry paths enter `CoreExecutionIsolation` before user component code. That internal
lease supplies independent component-current, scheduler, reactive tracking/batching/scope, and State
setup/active-registry bookkeeping across asynchronous continuations, then restores the caller's
logical state. This closes ambient cross-request races without making a request graph itself
thread-safe (`[EXE-1]`, `[SSR-9]`).

`ServerRenderResult` separates render execution from host policy. It reports the exception and
whether content started, allowing a downstream server to choose its own status and error page.
Request cancellation remains an `OperationCanceledException`, because it belongs to the host's
abort path rather than its failure-response path. The output's `WriteAsync` and `FlushAsync` are both
awaited; component-subtree flush boundaries therefore preserve progressive delivery and the host's
backpressure.

The generic hydration handoff is `SsrStateIsland`. It accepts only explicitly serialized JSON,
normalizes it through reflection-free System.Text.Json metadata, and emits
`<script type="application/json" data-viu-state>...</script>`. Client hosts locate
`SsrStateIsland.Selector` and deserialize through a caller-supplied `JsonTypeInfo<T>` before
hydration. Store schema, capture, and restoration belong to State; the island transport has no store
dependency (`[EXE-4]`, `[V01.01.07.04]`).

Component activation is explicit and registration-based. Serialization dispatches known node kinds
and binding forms without runtime type discovery, reflection-based serialization, emitted code, or
dynamic activation. State payload capture requires an `IStateStorePayloadRegistry`; a custom
registry that materializes stores without that contract fails instead of emitting an incomplete
payload (`[EXE-4]`).

## Non-goals

The low-level render entry points do not own request scopes. The host adaptor owns only the
factory-returned scope for one invocation; it never owns the output or prescribes an HTTP response.
ServerRenderer does not own browser application lifetime, DOM hydration, client directives,
transition timing, or persistent mounted state. The runtime-tree entry point has no compiled
scope-identifier input; scoped identifiers are emitted only by the server compiler profile
(`[SSR-COMPILE-3]`).
