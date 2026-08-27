# Assimalign.Viu.ServerRenderer design

## One-shot host model

ServerRenderer is a serializer over Components' existing `VirtualNode` algebra. It does not define a
parallel server node model or depend on Core's persistent mounted renderer. `ServerRenderApplication`
is immutable per-render composition and deliberately does not participate in browser application
middleware (`[SSR-2]`, `[SSR-3]`).

A traversal component subtree is obtained through
`ComponentHost.RenderAsync(ComponentRenderRequest)`. A generated server registration instead uses
the public `ComponentHost.ExecuteAsync` operation seam. Core performs the same resolution, setup,
server-prefetch, cancellation, error routing, and teardown without calling the client renderer; the
callback receives only `IComponent`, `ComponentRenderFrame`, and `IComponentRenderScope` while the
lease is live. Its named outcome tells ServerRenderer whether to commit the buffered body or emit
the handled-failure placeholder (`[SSR-4]`, `[SSR-5]`, `[SSR-10]`, `[SSR-TARGET-3]`).

These two public ComponentHost operations are the complete runtime seam. ServerRenderer does not
downcast `ComponentContext`, reach internal activation or mounted state, probe hidden capabilities,
or require friend access to Core (`[CMP-33]`, `[SSR-TARGET-3]`).

## Serialization

The serializer exhaustively dispatches all ten `VirtualNodeKind` values. Elements apply the escaping,
attribute-name safety, boolean-attribute, casing, class/style, child-override, and raw-HTML rules in
`[SSR-6]`. Both HTML and Extensible Markup Language static payloads are emitted verbatim only from
compiler-trusted `StaticNode` values. The server does not reparse either format, which preserves
traversal/direct-body byte equality for SVG and MathML chunks selected by `[SFC-OPT-4]`; the closed
`StaticNode` contract rejects every unsupported format before serialization.

`RenderToStringAsync` accumulates one result. `RenderToStreamAsync` writes to the caller's
`TextWriter` and flushes after a completed component subtree, so the destination controls
backpressure. The renderer borrows the writer and never closes or disposes it (`[SSR-1]`).

The compiler's `ServerMarkup` target lowers a proven static/native region to direct
`SsrRenderState.Push` calls. Dynamic values use ServerRenderer's public escaping and normalization
helpers; components and unsupported binding shapes build only their local fallback subtree. The
public compiled-render entry point owns exactly the same cancellation, component-scope, teleport,
state, streaming, and flush protocol as the tree path. Executed differential fixtures, rather than
source snapshots alone, pin ordinal byte equality (`[SSR-COMPILE-1]` through `[SSR-COMPILE-4]`).

The SDK declaration `ViuServerRendering=true` selects this profile for the whole project and brings
the exact ServerRenderer package. Generated component registrations are collected into
`GeneratedViuServerRenders`; the host calls that catalog explicitly and supplies the resulting
registry to `ServerRenderAdaptor<TContext>`. Client-only generation has no server branch, while a
dual-target project emits both profiles from the same parsed template in deterministic order
(`[SSR-TARGET-1]` through `[SSR-TARGET-4]`).

`ServerRenderRegistry` deliberately separates composition from concurrent serving. Registration
and lookup are synchronized while the registry is mutable, preserving existing single-threaded
populate-and-render use. `Freeze` is an explicit, idempotent publication boundary: it creates one
immutable registration snapshot, rejects every later registration with `InvalidOperationException`,
and makes resolution against the safely published snapshot lock-free. A host must cross that
boundary before sharing one registry between concurrent requests (`[SSR-TARGET-4]`).

A registry-selected direct body is one atomic output transaction. It writes into a buffered
`SsrRenderState` with a child `SsrContext`; successful completion commits context contributions and
replays each requested flush boundary in source order, awaiting the destination after every chunk.
Failure discards the buffered markup and context before ordinary component error routing runs. This
delays the first destination write until the direct body succeeds without collapsing its requested
chunk ordering or backpressure boundaries (`[SSR-TARGET-3]`).

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

## Streaming document composition

`RenderDocumentAsync` composes a host-owned shell around the ordinary progressive root render. It
does not introduce a document parser or a second serializer. After scope creation, identity checks,
and root validation, `IServerRenderDocumentShell.WritePrefixAsync` writes through the same
`IServerRenderOutput` as the body. An internal tracker flushes only non-empty content that the shell
has not already flushed, so an empty prefix does not force transport commitment. The main render
then retains every component-subtree flush and destination-backpressure boundary from `[SSR-1]`.

After the body succeeds and `SsrContext` has resolved every teleport, `WriteSuffixAsync` receives a
stable read-only target-to-payload map rather than the whole render context. That narrow contract
keeps state handoff outside document composition. The shell owns target order and markup, looks up
each host-defined marker explicitly, and emits its matching payload verbatim; Viu's payload already
contains the hydration anchor required by `[SSR-MARKERS-2]` and `[HYD-6]`. Pending suffix content is
flushed before scope teardown (`[SSR-14]`).

The adaptor borrows both shell and output and never disposes either. A request-specific shell may
capture host document data; an instance reused concurrently must synchronize its own mutable state.
Prefix, body, output, or cancellation failure skips the suffix, while suffix failure follows the
ordinary adaptor failure path. The output's host-authoritative `ResponseCommitted` value remains the
only fallback-policy signal throughout all three phases; the tracker's pending-write bookkeeping is
used only to decide whether a shell phase needs a flush (`[SSR-12]`, `[SSR-14]`).

This seam intentionally supports only post-render teleport points in the suffix. Once prefix bytes
have streamed, neither Viu nor the shell can insert a payload into them, including into an already
closed `<head>`. An earlier target requires the host to buffer that document region or perform a
render prepass; arbitrary backpatching and a speculative multi-channel renderer remain outside this
design (`[SSR-14]`).

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
`IServerRenderOutput`, and calls `DisposeAsync(CancellationToken)` with the original request token on
success, failure, cancellation, and partial output. A token-aware override may interrupt cancellable
teardown work but must still release every resource it owns. The default interface member delegates
to parameterless `IAsyncDisposable.DisposeAsync`, preserving existing implementations that have no
abort-aware cleanup. Consuming both identities matters because a rejected scope is disposed and
neither object may safely reappear. Weak identity tracking prevents reuse without retaining
completed requests (`[SSR-11]`, `[SSR-13]`, `[V01.01.07.04]`).

Both renderer entry paths call `RuntimeExecution.EnterExecutionFlow` before user component code.
That public lease composes the public Core, Reactivity, and State flow boundaries, supplies
independent component-current, scheduler, reactive tracking/batching/scope, and State
setup/active-registry bookkeeping across asynchronous continuations, then restores the caller's
logical state. This closes ambient cross-request races without making a request graph itself
thread-safe (`[EXE-1]`, `[SSR-9]`).

`ServerRenderResult` separates render execution from host policy. It reports the exception and
snapshots `IServerRenderOutput.ResponseCommitted` after scope teardown. That monotonic value belongs
to the downstream transport: an attempted write or an accepted flush never establishes commitment
by inference, and false guarantees that accepted content can still be discarded for a clean
replacement response. Request cancellation remains an `OperationCanceledException`, because it
belongs to the host's abort path rather than its failure-response path. The output's `WriteAsync`
and `FlushAsync` are both awaited; component-subtree flush boundaries therefore preserve
progressive delivery and the host's backpressure (`[SSR-12]`).

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
(`[SSR-COMPILE-3]`). The document-shell seam does not parse templates, buffer the completed body,
or support random-access insertion into a prefix that has already streamed (`[SSR-14]`).
