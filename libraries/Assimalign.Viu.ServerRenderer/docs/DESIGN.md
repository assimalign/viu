# Assimalign.Viu.ServerRenderer — design

The upstream reference is
[`@vue/server-renderer`](https://github.com/vuejs/core/tree/main/packages/server-renderer), especially
`packages/server-renderer/src/render.ts`, `renderToString.ts`, and the shared HTML escaping helpers.
Viu preserves those observable server-rendering semantics over its unified component tree.

## One component tree for client and server

ServerRenderer consumes `IComponent`; it does not maintain a second virtual-node model.
`ComponentTreeSerializer` dispatches by `ComponentKind`:

- elements serialize their immutable attributes and children;
- text is escaped;
- comments are sanitized;
- static content is written verbatim;
- fragments emit hydration anchors;
- teleports emit origin anchors and buffer target content;
- template requests activate an `IComponentTemplate` and serialize its rendered subtree.

This runtime walk is the parity baseline for a future compiler-informed string-concatenation path.
Compiler optimizations may skip portions of the walk, but must produce byte-identical HTML.

## Core owns component semantics

The server renderer does not duplicate component context, argument resolution, attribute fallthrough,
effect-scope ownership, task observation, or error propagation. Core grants ServerRenderer internal
access to `MountedComponent`, analogous to Vue runtime-core's internal `ssrUtils` export.

For each `ITemplateComponent`, `ServerComponentRenderer`:

1. activates a fresh template through `IApplicationContext.Components`;
2. creates the shared live `IComponentContext` and reactive effect scope;
3. runs synchronous `Setup`;
4. awaits every `OnServerPrefetch` callback;
5. invokes the returned `ComponentRenderer` once;
6. serializes the resulting `IComponent` subtree;
7. stops the temporary scope, cancels the component-lifetime token, and disposes the mount-owned
   template.

Client-only before-mount, mounted, update, and unmount callbacks do not run during SSR. Render
cancellation interrupts the prefetch wait and cancels the component-lifetime token during cleanup.
Errors use the same ancestor `OnErrorCaptured` chain and terminal application error handler as client
renderers.

## Application composition

`ServerApplication` is host-neutral because it does not own a `Renderer<TNode>` or host container.
It implements the non-generic `IApplication` configuration face and carries the same
`IApplicationContext` used by Browser and future WebView2 applications.

The application receives three independent, borrowed composition services:

- `IComponentFactory` activates templates;
- `IServiceProvider` resolves application services;
- `IStateStoreRegistry` optionally supplies application state.

ServerRenderer does not own or dispose any of them. It does not implement component-tree
provide/inject. Applications that need hierarchical dependency behavior can choose an appropriate
service provider or component factory at their own composition boundary.

Plugins are deduplicated by reference and awaited before rendering. Because an app may carry
request-scoped services or state, server hosts should create one app per request.

## Async and streaming model

Vue's JavaScript implementation stores nested strings, buffers, and promises and unrolls them later.
C# can express the same ordering as a single async recursion: a child template's server-prefetch tasks
are awaited inline before its subtree serializes.

`SsrWriter` is the one character sink for a render. String mode accumulates in one `StringBuilder`.
Streaming mode drains that buffer at completed-template boundaries and awaits `TextWriter.FlushAsync`,
so the destination controls backpressure.

Teleport content is the intentional exception: enabled teleport children belong to another target and
must be buffered in `SsrContext` until the full render resolves target output. Teleport buffer states
share the same application, cancellation token, and component-identifier sequence as the main tree.

## Escaping and attributes

`ServerRender.EscapeHtml` follows Vue's shared escaping table for `"`, `&`, `'`, `<`, and `>`.
`EscapeHtmlComment` repeatedly removes comment terminators. Attribute serialization:

- skips renderer metadata, event listeners, forced properties, and element child overrides;
- normalizes class and style values;
- renders HTML boolean attributes by presence;
- preserves SVG/custom-element casing;
- drops unsafe dynamic attribute names instead of attempting to escape the name.

`innerHTML` is the explicit raw-HTML path. `textContent` and a textarea's `value` are escaped and
suppress normal child serialization.

## Hydration marker contract

These exact strings are a cross-package contract:

| Component tree value | Main output |
|---|---|
| Text | escaped text |
| Comment | `<!--content-->`; empty content is `<!---->` |
| Static | raw content |
| Element | `<tag attributes>children</tag>` |
| Void element | `<tag attributes>` |
| Fragment | `<!--[-->children<!--]-->` |
| Template | rendered subtree, with no template wrapper |
| Enabled teleport | `<!--teleport start--><!--teleport end-->` |
| Disabled teleport | `<!--teleport start-->children<!--teleport end-->` |

An enabled teleport target buffer receives its children followed by
`<!--teleport anchor-->`. A disabled teleport renders children in place and contributes only the
target anchor. A missing or non-string target emits the origin anchors and skips the target content.

These markers are consumed today by Core's host-neutral `Renderer<TNode>.Hydrate` implementation
through a host-supplied `HydrationNodeReader<TNode>`. The ServerRenderer suite pins that contract
end to end by rendering real HTML, parsing it into Testing's in-memory host tree, and hydrating it
with both:

- `TestHydrationReader`, which reads the live tree; and
- `FrozenTestHydrationReader`, which captures an immutable pre-walk equivalent to Browser's batched
  DOM snapshot.

The round trips cover fragments, activated template roots followed by reactive client updates, and
Teleport origin/target ranges. Frozen-reader mismatch coverage also verifies that a server-emitted
fragment range is collected before mutation and removed exactly once.

## Deferred work

- compiler-informed server render generation;
- server-framework adapters and byte-oriented `PipeWriter` integration;
- static-site generation;
- directive-specific server properties and built-in Suspense/KeepAlive behavior.
