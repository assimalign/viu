# Assimalign.Viu.ServerRenderer — design

Server rendering emits HTML for the same unified component tree the client renderers patch, on a
plain .NET host with no DOM or interop dependency. The normative statement of what it emits is
[`docs/SPECIFICATION.md` §11](../../../docs/SPECIFICATION.md#11-server-rendering-and-hydration);
this document records why the area is shaped the way it is.

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

This runtime walk is the reference behavior for a future compiler-informed string-concatenation
path. Compiler optimizations may skip portions of the walk, but must produce byte-identical HTML —
compiled server rendering is an optimization of this walk, never a second semantics.

## Core owns component semantics

The server renderer does not duplicate component context, argument resolution, attribute fallthrough,
effect-scope ownership, task observation, or error propagation. Core grants ServerRenderer internal
access to `MountedComponent` (`InternalsVisibleTo`) precisely so that one component pipeline serves
both hosts; duplicating it in the server area is what would let the two drift.

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

`ServerRenderApplication` is a plain composition object because server rendering has no persistent
mounted host lifetime. It does not own a `Renderer<TNode>` or host container, does not implement
`IApplication`, and carries the same immutable `IApplicationContext` that component execution reads.

The application receives three independent, borrowed composition services:

- `IComponentFactory` activates templates;
- `IServiceProvider` resolves application services;
- `IStateStoreRegistry` optionally supplies application state.

ServerRenderer does not own or dispose any of them. It does not implement component-tree
provide/inject. Applications that need hierarchical dependency behavior can choose an appropriate
service provider or component factory at their own composition boundary.

Top-level application middleware surrounds one live hosted application, so server rendering
deliberately bypasses it. A future interception surface, if required, would be a per-render contract
carrying the `SsrContext`, output, and cancellation token; D5 explicitly defers that separate design.
Because a server-render application may carry request-scoped services or state, server hosts should
create one composition object per request.

## Async and streaming model

Ordering is expressed as a single async recursion rather than as a tree of nested string, buffer,
and promise segments unrolled after the fact: a child template's server-prefetch tasks are awaited
inline before its subtree serializes. One recursion means the emission order *is* the tree order, so
no separate unroll pass can reorder output.

`SsrWriter` is the one character sink for a render. String mode accumulates in one `StringBuilder`.
Streaming mode drains that buffer at completed-template boundaries and awaits `TextWriter.FlushAsync`,
so the destination controls backpressure.

Teleport content is the intentional exception: enabled teleport children belong to another target and
must be buffered in `SsrContext` until the full render resolves target output. Teleport buffer states
share the same application, cancellation token, and component-identifier sequence as the main tree.

## Escaping and attributes

`ServerRender.EscapeHtml` escapes `"`, `&`, `'`, `<`, and `>` — a deliberate superset of the WHATWG
minimal set, so one routine is correct for both text and attribute values and no caller has to
choose. `EscapeHtmlComment` repeatedly removes comment terminators. Attribute serialization:

- skips renderer metadata, event listeners, forced properties, and element child overrides;
- normalizes class and style values;
- renders HTML boolean attributes by presence;
- preserves SVG/custom-element casing;
- drops unsafe dynamic attribute names instead of attempting to escape the name.

`innerHTML` is the explicit raw-HTML path. `textContent` and a textarea's `value` are escaped and
suppress normal child serialization.

## Hydration marker contract

These exact strings are a cross-package contract; changing one is a breaking change to the
hydration protocol, because markup already served by a deployed application would stop hydrating
([`[SSR-MARKERS-1]`](../../../docs/SPECIFICATION.md#112-the-hydration-marker-protocol)):

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
