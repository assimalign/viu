# Assimalign.Viu.ServerRenderer

ServerRenderer is Viu's host-neutral WHATWG HTML serialization host. Its public entry points accept
either an immutable `ServerRenderApplication` or a primitive `VirtualNode` tree and render to a
string or a caller-owned `TextWriter`. Streaming flushes at completed component-subtree boundaries,
so the destination controls backpressure without imposing a web-framework dependency.

`ServerRenderAdaptor<TContext>` is the request-hosting layer over those entry points. A downstream
server supplies an `IServerRenderRequestScopeFactory<TContext>` and `IServerRenderOutput`; Viu asks
for a fresh async-disposable application/context scope on every request, awaits every write and
flush, and reports completion or a host-mappable failure through `ServerRenderResult`. Reusing an
application or `SsrContext` is rejected. `IServerRenderOutput.ResponseCommitted` is the host's
monotonic, authoritative statement that accepted output can no longer be wholly replaced;
`ServerRenderResult.ResponseCommitted` snapshots that value after scope teardown and never infers
commitment from an attempted write or flush. A false value therefore leaves the host free to
discard accepted content and send a clean failure response (`[SSR-12]`).

Cancellation propagates to component prefetch, serialization, output, and scope teardown without
being converted into an application error. The adaptor passes the original request token to
`IServerRenderRequestScope.DisposeAsync(CancellationToken)` on success, failure, cancellation, and
partial output. Existing scope implementations remain compatible because the default token-aware
member delegates to `IAsyncDisposable.DisposeAsync`; an override may observe abort while still
releasing every owned resource (`[SSR-11]`, `[SSR-13]`).

Every runtime-tree and compiled render selects fresh logical Core, Reactivity, State, and scheduler
bookkeeping. Parallel request-owned graphs therefore cannot exchange ambient component/setup state,
effects, batch queues, active registries, or scheduled jobs; sharing graph objects between requests
remains unsupported [EXE-1], [SSR-9].

The serializer dispatches all ten `VirtualNodeKind` values. Component nodes always resolve and
activate through the ordinary component factory. Without a compiled registration,
`ComponentHost.RenderAsync` supplies the tree and live parent scope. With an explicitly supplied
`IServerRenderRegistry`, ServerRenderer uses public `ComponentHost.ExecuteAsync` to run the same
setup and server-prefetch path without first executing the client renderer. Its callback receives
the activated component, render frame, and public scope only while Core owns the live operation;
the named outcome controls commit versus handled-failure output. Neither path reaches the
persistent mounted engine, downcasts a public context, probes a hidden capability, or uses friend
access.

Serialization owns the HTML-specific rules required by `[SSR-6]`: the five-character escape set,
repeated comment-terminator removal, void and boolean elements, safe dynamic attribute names,
class/style normalization, property/event exclusion, child overrides, and SVG/custom-element casing.
Qualified names remain explicit. Static Extensible Markup Language payloads are rejected because
they require a different host serializer.

`HydrationMarkers` is the sole marker vocabulary. Fragments emit its range markers; enabled
teleports leave origin anchors and buffer children plus a target anchor; disabled teleports render
children in place and contribute only their target anchor. `SsrContext` exposes the resolved target
buffers and the versioned `StateStorePayload` captured from the request registry after traversal.
When state is composed, the renderer appends the island to its emitted HTML. `SsrStateIsland`
provides the store-independent transport: it validates raw JSON, emits an inert
`data-viu-state` script, and restores a typed payload only through caller-supplied source-generated
`JsonTypeInfo<T>`. Suspense serializes only its resolved default branch; KeepAlive and Transition
serialize their lazy default slots without client-only behavior.

`ServerRenderAdaptor<TContext>.RenderDocumentAsync` adds a minimal full-document streaming seam.
After the request scope and root identity are valid, the borrowed `IServerRenderDocumentShell`
writes a prefix and Viu flushes any non-empty pending prefix before component execution. The main
root then uses the ordinary progressive stream. Once it succeeds and every teleport buffer has
resolved, the suffix receives the stable target-to-payload map, writes host-owned closing markup,
and splices payloads verbatim at host-defined target markers before its final pending content is
flushed. The shell and output remain host-owned, and a shell shared across requests must provide its
own concurrency safety (`[SSR-1]`, `[SSR-14]`, `[SSR-MARKERS-2]`, `[HYD-6]`).

Prefix, render, output, and cancellation failures skip the suffix; a suffix failure is reported as
an ordinary output failure. Teleport emission points must occur in the suffix. The seam does not
parse a document template or backpatch an already streamed prefix or `<head>`; an earlier target
requires host buffering or a render prepass (`[SSR-14]`).

The Templates compiler's `ServerMarkup` profile is the build-time fast path. It writes proven native
structure and interpolations directly through `SsrRenderState`, while dynamic components and
unsupported binding shapes fall back to a local virtual tree on that same render state. The public
`CompiledServerRender` seam supplies renderer-owned cancellation, component leases, streaming,
teleports, state capture, and final flush. Differential fixtures execute both paths and require
ordinal byte equality (`[SSR-COMPILE-1]` through `[SSR-COMPILE-4]`).

Projects select that fast path once with `ViuServerRendering=true` under the base or Browser SDK.
The generator retains the client body, adds the server body, and emits
`GeneratedViuServerRenders.Register(ServerRenderRegistry)`. A host supplies the populated registry
to `ServerRenderAdaptor<TContext>`; absent entries retain traversal. Registration and lookup remain
synchronized during single-threaded composition and rendering. Before sharing the registry across
concurrent requests, the host calls the idempotent `Freeze` transition, which publishes one
immutable snapshot, rejects later registration, and makes resolution lock-free. Client-only
projects emit no server catalog or server references (`[SSR-TARGET-1]` through
`[SSR-TARGET-4]`).

A non-immediate component invocation is serialized inside Core's fixed lazy-hydration markers.
Server rendering still resolves the authored subtree; the client may then adopt the complete range
without activating it until its host trigger fires. Asynchronous definitions emit exactly one outer
boundary (`[SSR-MARKERS-1]`, `[HYD-LAZY-1]`, `[HYD-LAZY-2]`).

The runtime-tree serializer has no scope-identifier input, so it performs no scope-identifier
attribute pass. The compiler profile does emit the transform's known scope identifier on every
native element. Application services, factories, directives, state registries, and diagnostics are borrowed
from `IApplicationContext`. The low-level renderer never disposes them; the host adaptor disposes
only the request scope explicitly returned by the downstream factory. The test project includes a
BCL-only loopback HTTP smoke host, proving that neither the contract nor its execution requires
ASP.NET Core or Kestrel.
