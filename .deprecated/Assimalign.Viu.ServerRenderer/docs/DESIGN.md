# Assimalign.Viu.ServerRenderer — design

Why the server renderer is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). Upstream
counterpart: [`@vue/server-renderer`](https://github.com/vuejs/core/tree/main/packages/server-renderer)
— `renderToString` and the render pipeline over vnodes and components
(`packages/server-renderer/src/render.ts`, `renderToString.ts`), plus `@vue/shared`'s `escapeHtml`
(`packages/shared/src/escapeHtml.ts`). This feature is the **runtime, vnode-walking** renderer; the
compiler-informed string-concatenation fast path is [V01.01.07.02] and shortcuts this walk without
changing its output.

## The renderer walks the same vnodes the client patches

SSR is the fallback tier: the runtime renderer runs a component's `Setup`, calls its render function to
produce a vnode subtree, and walks that tree to HTML. Because it consumes the **same** vnodes the
client renderer patches, its output is the parity baseline the compiled `ssrRender` fast path
([V01.01.07.02]) must match byte-for-byte. `VirtualNodeSerializer` is the port of upstream's
`renderVNode` / `renderElementVNode` / `renderVNodeChildren`; `ServerComponentRenderer` is the port of
the `renderComponentVNode` / `renderComponentSubTree` setup path.

## The Core seam: reusing the real component pipeline

The server renderer must create a `ComponentInstance`, run its `Setup` inside its effect scope, await
its `ServerPrefetch` hooks, and render its root — the platform-agnostic half of the component lifecycle.
Upstream exposes exactly these primitives to `@vue/server-renderer` through runtime-core's `ssrUtils`
`@internal` export (`createComponentInstance`, `setupComponent`, `renderComponentRoot`). Viu's analog is
a single `[assembly: InternalsVisibleTo("Assimalign.Viu.ServerRenderer")]` on `Assimalign.Viu.Core`
(alongside the grants it already makes to `Assimalign.Viu.Testing` and `Assimalign.Viu.Browser`).
`ServerComponentRenderer` then drives the real pipeline — the same `ComponentPropertyResolution.Resolve`,
the same setup-in-scope discipline, the same `RenderComponentRoot` normalization and single-root attrs
fallthrough the client renderer uses — instead of forking a second copy that could drift. No render
effect is created (there is no client-side reactivity server-side) and no lifecycle beyond
`Setup`/`ServerPrefetch` fires, matching upstream's server lifecycle.

## The sync/async model: inline await, not a deferred buffer tree

Upstream defers async subtrees into a nested `SSRBuffer` of `string | SSRBuffer | Promise<SSRBuffer>`
and unrolls it at the end (`hasAsync`/`nestedUnrollBuffer`), because JS cannot pause a synchronous
render to await a child's async data. C# `async`/`await` can, so the C# port is a **single async
recursion that awaits inline at each async boundary** — a child component's `ServerPrefetch`. Awaiting
the child's async dependencies before serializing the child's subtree produces the same in-order
document as unrolling a deferred buffer, and it streams naturally: each completed component subtree is
written immediately. `SsrWriter` is one `StringBuilder` threaded through the whole render (never per-node
string concatenation — SSR throughput is the reason the area exists); in streaming mode it is a bounded
chunk buffer that `FlushAsync` drains to the backing `TextWriter` at each component-subtree boundary, so
the writer's write cadence applies backpressure and the full document is never buffered.

The one thing that genuinely must be buffered is **teleport content**: it is rendered at the teleport's
tree position but belongs at a different target, so it is collected into a per-target buffer on the
`SsrContext` and resolved into `SsrContext.Teleports` when the whole tree has serialized (the port of
`context.__teleportBuffers` → `resolveTeleports`).

### Per-request isolation

Every render creates its own root vnode, its own component instances (each with freshly `Setup`-created
refs/computeds), and renders against one `ServerApplication`'s `ApplicationContext`. The renderer holds
no static mutable render state, so two renders with distinct app instances share nothing — the
per-request app-instance discipline Vue's SSR requires. The ambient Core machinery
(`ComponentInstance.Current`, the block-tree accumulator) is used **only synchronously within a render
turn** and is always balanced across the two async boundaries (`ServerPrefetch` and flush) — neither is
ever crossed with an instance on the current stack or a block open — so even two renders interleaving at
those boundaries cannot observe each other's ambient state (pinned by
`ServerRendererIsolationTests.ConcurrentRenders_InterleavingAtAsyncBoundaries_DoNotPolluteEachOther`).
The single-threaded model still holds: a render must not run on two threads at once.

## Escaping is pinned to the exact upstream tables

Escaping is security-adjacent, so it is pinned character-for-character to `@vue/shared`'s `escapeHtml`
and `escapeHtmlComment`, not re-derived:

- **`EscapeHtml`** rewrites `"` → `&quot;`, `&` → `&amp;`, `'` → `&#39;`, `<` → `&lt;`, `>` → `&gt;` —
  the same function for text nodes and attribute values, exactly as upstream. This is a **superset** of
  the WHATWG minimal set the issue names (`&`, `<`, `>`, `"`): it also escapes `'`, matching Vue's
  output (and hardening single-quoted attribute contexts). The vectorized scan returns the input
  unchanged when it contains none of the five.
- **`EscapeHtmlComment`** strips the comment-terminating sequences (`-->`, `--!>`, `<!--`, leading `>`
  runs, trailing `<!-`) repeatedly until stable, so overlapping sequences cannot reconstitute a
  terminator and break a comment out of its wrapper.
- **`SsrRenderAttrs` / `SsrRenderDynamicAttr`** follow upstream `ssrRenderAttrs.ts`: boolean attributes
  render by presence (`includeBooleanAttr`), an SSR-unsafe attribute name (containing `>`, `/`, `=`,
  quotes, or whitespace/control) is **skipped rather than escaped** (`isSSRSafeAttrName` — the injection
  gate), `class`/`style` route through the Shared normalizers, `className` coerces directly, and the
  reserved (`key`/`ref`/`ref_for`/`ref_key`/`innerHTML`/`textContent`), event-handler (`onX`),
  `.`-prefixed, and `<textarea>`-`value` props are excluded. Injection-shaped inputs are covered in
  tests.

## The SSR marker contract (input to the hydration walker, [V01.01.07.03])

The hydration walker stacks on this branch and matches on these exact byte sequences to align the
server DOM with the client vnode tree. They are centralized in `Internal/SsrMarkers.cs`; **changing one
is a breaking change to the hydration protocol.** The emitted contract:

| Construct | Main-tree output | Notes |
|---|---|---|
| Text vnode | `EscapeHtml(text)` | no markers |
| Comment vnode | `<!--` + `EscapeHtmlComment(content)` + `-->` | empty content ⇒ `<!---->` (the anchor) |
| Static vnode | raw markup, verbatim | no escaping |
| Element (normal) | `<tag attrs>children</tag>` | attrs via `SsrRenderAttrs` |
| Element (void) | `<tag attrs>` | no closing tag, no children (WHATWG); no trailing slash |
| Element child override | `innerHTML` raw · `textContent`/`<textarea>` value escaped | override suppresses the child walk |
| Fragment | `<!--[-->` children `<!--]-->` | a multi-root component's subtree is a fragment, so it too is wrapped |
| Component | its subtree's markup | no component-specific wrapper; a fragment/multi-root root carries fragment markers |
| Teleport (enabled) | `<!--teleport start--><!--teleport end-->` | content buffered to the target (below) |
| Teleport (disabled) | `<!--teleport start-->` children `<!--teleport end-->` | content stays in place |
| `null` child placeholder | `<!---->` | normalization usually replaces nulls with comment vnodes first |

Teleport content is collected into `SsrContext.Teleports[target]` (keyed by the `to` selector), in tree
order when several teleports share a target:

- **enabled**: the target buffer receives `children` + `<!--teleport anchor-->`.
- **disabled**: the children render in the main tree; the target buffer receives only
  `<!--teleport anchor-->`.
- **missing / non-string target**: the content is skipped (a DOM-node target is not serializable
  server-side); the main tree still gets the `start`/`end` anchor pair.

## Deliberate divergences from `@vue/server-renderer`

- **`ServerApplication`, not `renderToString(app)` over `Application<TNode>`.** Viu's DOM app
  (`Application<TNode>`) is generic over the platform node and owns a `Renderer<TNode>`, so it cannot be
  constructed without a DOM/interop backend — which founding decision 7 forbids here. The SSR entry
  therefore takes a host-agnostic `ServerApplication` carrying the same root-component / registry /
  provide surface over the shared `ApplicationContext`, without a renderer. The host DI wiring that a
  real `renderToString(app)` implies is the server adaptor's concern ([V01.01.07.04]).
- **`Setup` is synchronous; async server data is `OnServerPrefetch`.** Upstream awaits an `async setup()`
  Promise. Viu's `IComponent.Setup` is synchronous by contract — the closure it returns *is*
  the proxy-free realization of upstream's state object (see `IComponent`'s XML docs), so there
  is no setup Promise to await. The upstream requirement to "await each component's async setup" is
  therefore realized through `Lifecycle.OnServerPrefetch` (the sanctioned async server-data hook, awaited
  before the subtree serializes) and, for `<Suspense>`, its async-component dependencies. Pinned by
  `ServerRendererServerPrefetchTests`.
- **Inline await instead of the nested buffer + Promise model** (above) — same in-order output, plus true
  streaming, without materializing a deferred buffer tree.
- **`EscapeHtml` escapes `'`** — a superset of the WHATWG minimal set the issue lists, matching upstream
  exactly.
- **No `console.warn` on an unsafe attribute name.** Upstream logs a dev warning when it skips an
  SSR-unsafe attribute name; the helper skips it silently to stay self-contained. The security behavior
  (the attribute is dropped) is identical.

## Non-goals (sequenced work)

- **Compiler-informed `ssrRender` codegen** (string-concatenation transforms that shortcut this walk) —
  [V01.01.07.02]. This ticket ships the `ServerRender` helper surface those bodies will call; the
  generator that emits the bodies is separate.
- **Client hydration** (adopting this output in the browser) — [V01.01.07.03]. This ticket defines and
  emits the marker contract above; the walker that consumes it stacks on this branch.
- **The host-agnostic server adaptor** (framework hosting, per-request app factory, `PipeWriter`/byte
  streaming with an encoding choice) — [V01.01.07.04]. This ticket streams to a `TextWriter` (the natural
  char sink); byte-level `PipeWriter` streaming is a hosting concern deferred there.
- **Static prerendering (SSG)** — [V01.01.07.05].
- **Directive server-side props (`getSSRProps`) and scoped-CSS `ssrCssVars` (`:--` reset)** — not applied
  by the runtime fallback renderer; they arrive with the compiler-informed path and the scoped-CSS work.
- **Built-in components beyond plain components and Teleport** (`KeepAlive`, `<Transition>`) render as
  ordinary components server-side; their SSR-specific transparency is future work. `<Suspense>` is
  supported at the `SsrRenderSuspenseAsync` helper level (default branch, awaiting async dependencies);
  its full boundary semantics arrive with the runtime Suspense component ([V01.01.03.20]).
```
