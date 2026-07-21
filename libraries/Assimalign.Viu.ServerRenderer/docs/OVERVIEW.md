# Assimalign.Viu.ServerRenderer — overview

Server-side rendering for Viu — the role [`@vue/server-renderer`](https://github.com/vuejs/core/tree/main/packages/server-renderer)
plays for Vue 3 ([SSR API](https://vuejs.org/api/ssr.html)). It walks a component tree to HTML on a
plain .NET host — **no DOM, no JavaScript interop** — so the same runtime assemblies that hydrate in
the browser also render on any server. This first feature ([V01.01.07.01]) is the **vnode-walking
runtime renderer** and the `ssrRender` helper library; the compiler-informed string-concatenation fast
path ([V01.01.07.02]), the client hydration walker ([V01.01.07.03]), the host-agnostic server adaptor
([V01.01.07.04]), and static prerendering ([V01.01.07.05]) are later features of the ServerRenderer
area (#63).

## What it contains

Public surface (all under namespace `Assimalign.Viu.ServerRenderer`):

- **`ServerRenderer`** (static facade): `RenderToStringAsync(app, context?)` is the C# port of
  `renderToString`; `RenderToStreamAsync(app, TextWriter, context?)` streams completed subtrees to a
  writer with `FlushAsync` backpressure. Convenience overloads take a root `IComponent`
  directly.
- **`ServerApplication`**: the host-agnostic SSR app — the port of `createSSRApp(RootComponent)`. It
  carries the root component, its props, and the app-level component/directive registries and provides
  every descendant resolves against, over the shared `ApplicationContext`. Deliberately **not** the
  DOM-bound `Application<TNode>` (which is inseparable from a `Renderer<TNode>`); see
  [DESIGN.md](DESIGN.md). It also exposes `Services` (the app's `System.IServiceProvider`, attached by
  `ServerApplicationBuilder` from `builder.Services`, [V01.01.03.24]) reachable from `Setup` during
  render, and implements `IDisposable` so a host disposes the per-request provider (and its owned
  disposable services) after rendering — build a fresh app per request.
- **`SsrContext`**: the per-render context — the port of `SSRContext`. Carries `Teleports` (out-of-tree
  content keyed by target selector, resolved when the render completes) and a free-form `State` bag for
  application handoff (e.g. serialized store state for the client to rehydrate).
- **`SsrRenderState`**: the write surface threaded through a render — the C# counterpart of upstream's
  `push` function plus the ambient `SSRContext`. It is what the runtime renderer carries and what the
  compiler-generated `ssrRender` bodies ([V01.01.07.02]) will receive, so both paths append to one
  buffer.
- **`ServerRender`** (static helper library): the by-name `ssr*` helpers the compiled SSR code calls
  into — the pure-string `EscapeHtml`, `EscapeHtmlComment`, `SsrInterpolate`, `SsrRenderAttrs`,
  `SsrRenderAttr`, `SsrRenderDynamicAttr`, `SsrRenderClass`, `SsrRenderStyle`, `SsrRenderComment`, and
  the push-based async `SsrRenderComponentAsync`, `SsrRenderSlotAsync`, `SsrRenderListAsync`,
  `SsrRenderTeleportAsync`, and `SsrRenderSuspenseAsync`.

Internal (`Internal/`, exercised through `InternalsVisibleTo` tests):

- **`VirtualNodeSerializer`**: the vnode-walking engine — the port of `renderVNode` /
  `renderElementVNode` / `renderVNodeChildren`.
- **`ServerComponentRenderer`**: the component-lifecycle driver — the port of the
  `renderComponentVNode` / `renderComponentSubTree` setup path (create instance, run `Setup`, await
  `OnServerPrefetch`, render root).
- **`SsrWriter`**: the single-`StringBuilder` sink with chunked async flush for streaming.
- **`SsrMarkers`**: the comment-marker vocabulary the output embeds for hydration (documented in
  [DESIGN.md](DESIGN.md)).

## Using it

```csharp
using Assimalign.Viu.ServerRenderer;

// Simple: render a root component to a string.
string html = await ServerRenderer.RenderToStringAsync(new RootComponent());

// With app-level provides, registered components, and a render context for teleports/state handoff.
var app = new ServerApplication(new RootComponent())
    .Component("Widget", new WidgetComponent())
    .Provide(RequestScopeKey, requestScope);

var context = new SsrContext();
string page = await ServerRenderer.RenderToStringAsync(app, context);
// Out-of-tree teleport content, ready to splice into its target element:
foreach (var (target, content) in context.Teleports) { /* place content at target */ }

// Streaming: write completed subtrees to any TextWriter as they finish.
await ServerRenderer.RenderToStreamAsync(app, Console.Out, context);
```

## Boundaries

- References **only** `Assimalign.Viu.Shared` (DOM knowledge tables, class/style normalization,
  `toDisplayString`), `Assimalign.Viu.Core` (the vnode model,
  the component lifecycle, provide/inject). It references **no** web framework and **no** DOM/interop
  assembly (`Browser`), so it runs in a plain .NET host — its own test story is pure unit tests over
  components and vnodes asserting exact HTML strings (founding decision 7).
- Trimming- and NativeAOT-safe: setup runs through the same source-generated component surface the
  client uses; there is no reflection-based component discovery or activation and no dynamic code
  generation.
- Not thread-safe (single-threaded JS event-loop model). Per-request isolation is by app instance:
  construct a fresh `ServerApplication` per request so no reactive state crosses requests.
- Design rationale, the SSR marker/hydration contract, the sync/async model, and the deliberate
  C#/WASM divergences from `@vue/server-renderer`: [DESIGN.md](DESIGN.md).
```
