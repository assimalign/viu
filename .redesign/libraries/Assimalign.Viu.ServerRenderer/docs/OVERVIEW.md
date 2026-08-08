# Assimalign.Viu.ServerRenderer

ServerRenderer is Viu's host-neutral WHATWG HTML serialization host. Its public entry points accept
either an immutable `ServerRenderApplication` or a primitive `VirtualNode` tree and render to a
string or a caller-owned `TextWriter`. Streaming flushes at completed component-subtree boundaries,
so the destination controls backpressure without imposing a web-framework dependency.

The serializer dispatches all ten `VirtualNodeKind` values. Component nodes execute only through
`ComponentHost.RenderAsync`; it holds the returned `IComponentRenderScope` while consuming
`scope.Tree`, passes that active scope as the parent of nested requests, and disposes the scope after
the subtree and its streaming boundary complete. This is the complete one-shot host seam: there is
no friend access, mounted-engine access, context downcast, or capability probe.

Serialization owns the HTML-specific rules required by `[SSR-6]`: the five-character escape set,
repeated comment-terminator removal, void and boolean elements, safe dynamic attribute names,
class/style normalization, property/event exclusion, child overrides, and SVG/custom-element casing.
Qualified names remain explicit. Static Extensible Markup Language payloads are rejected because
they require a different host serializer.

`HydrationMarkers` is the sole marker vocabulary. Fragments emit its range markers; enabled
teleports leave origin anchors and buffer children plus a target anchor; disabled teleports render
children in place and contribute only their target anchor. `SsrContext` exposes the resolved target
buffers and a renderer-uninterpreted state handoff bag. Suspense serializes only its resolved default
branch; KeepAlive and Transition serialize their lazy default slots without client-only behavior.

Scoped CSS remains deferred for this wave, so ServerRenderer performs no scope-identifier attribute
pass. Application services, factories, directives, state registries, and diagnostics are borrowed
from `IApplicationContext` and are never disposed by this package.
