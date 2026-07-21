# Assimalign.Viu.HackerNews

The Wave-4 **exit demo** ([V01.01.13.06], #103): a HackerNews client that composes the whole W04
surface together — the Viu counterpart of [vuejs/vue-hackernews-2.0](https://github.com/vuejs/vue-hackernews-2.0).
It exercises, in one app:

- **Routing** — `Assimalign.Viu.Router` with **web history**, plus the `Assimalign.Viu.Router.Browser`
  **click bridge** installed at bootstrap so `RouterLink` navigations are client-side.
- **Store** — a Pinia-style `Assimalign.Viu.Store` (`Store<TState>` member model over `[Reactive]`
  state); *all* fetched state flows through stores, never through component fields.
- **Async components** — the item/user route views are `AsyncComponents.DefineAsyncComponent`
  definitions with loading/error components.
- **Transitions** — story lists render under `TransitionGroup` (keyed rows; FLIP move animation).
- **Networked data** — `HttpClient` + **source-generated** `System.Text.Json` against the public
  HackerNews API (reflection-free; trimming/AOT-safe).
- **Styling** — `.viu` `@style` bundles compiled into the fingerprinted, **auto-injected** CSS
  `<link>` (no manual stylesheet tag).

## Run it

```sh
dotnet run --project examples/Assimalign.Viu.HackerNews
```

(Requires the `wasm-tools` workload: `dotnet workload install wasm-tools`.) The app fetches live data
from `https://hacker-news.firebaseio.com/v0/`, which sends permissive CORS headers.

## Routes

| Path | Name | View | Notes |
| --- | --- | --- | --- |
| `/:feed/:page(\d+)?` | `feed` | `StoriesView` | Feed ∈ top/new/show/ask/jobs; optional numeric page. Paged, 20/page. |
| `/item/:id(\d+)` | `item` | `ItemView` (async) | Story header + a bounded recursive comment tree. |
| `/user/:id` | `user` | `UserView` (async) | User profile. |
| `/:pathMatch(.*)*` | `not-found` | `NotFoundView` | Catch-all 404. |

A `beforeEach` guard redirects `/` to `/top`. Bootstrap awaits `router.ReadyAsync()` before mounting,
so this guard runs the initial navigation through the full pipeline — the redirect fires both for a
page loaded directly at `/` and for in-session navigations to `/` (e.g. clicking the logo).

## Architecture

```
Data/     StoryFeed(+StoryFeeds mapping) · HackerNewsItem/HackerNewsUser/CommentNode (domain records)
          IHackerNewsClient (the injectable seam) · HackerNewsClient (live: HttpClient + source-gen JSON)
          Internal/ ItemPayload, UserPayload (DTOs) + HackerNewsJsonSerializerContext (source generator)
Stores/   StoriesStore/ItemStore/UserStore (+ their [Reactive] *State) · HackerNewsStores (definitions container)
Views/    AppShell · StoriesView · ItemView · UserView · NotFoundView · StoryItem · CommentView (recursive)
          LoadingView · ErrorView · Ui (render helpers + shared RouterLink/RouterView singletons) · HtmlText
Routing/  AppRoutes (route table + root-redirect guard)
Styles/   app.viu · stories.viu · item.viu  (@style bundles)
Program.cs  browser bootstrap (the only [SupportedOSPlatform("browser")] type)
```

**Dependency injection (`System.IServiceProvider`).** Bootstrap composes the app with bring-your-own DI
([V01.01.03.24]): `builder.AddStore(registry)` and `builder.AddRouter(router)` register the store
registry and router as application services (each also keeps the plugin/provide parity, so `UseStore()`
and `RouterView` resolve either way), and `builder.Services.AddSingleton(stores)` registers the
`HackerNewsStores` container as a plain app-level singleton. This is the flagship of the reshape's
app-level singleton wiring migrated from component-tree provide/inject to `IServiceProvider` — the
default provider is Core's AOT-safe factory-delegate registry (no reflection, no MS.Ext.DI dependency).
Component-tree `Provide`/`Inject` stays available for Vue-semantic wiring.

**Data flow.** Views resolve the `HackerNewsStores` container with
`DependencyInjection.GetRequiredService<HackerNewsStores>()`, inject the router, resolve their store
with `UseStore()`, and drive it from a route-watching effect (`Reactive.Watch(..., Immediate)`), so the
initial mount and every navigation fetch. Loading and error are explicit `[Reactive]` state; a
superseded load is cancelled so navigation never leaves a stale page. Story lists render as **keyed**
rows (stable `Id`), with no per-item interop — a list-virtualization strategy can be added later
without touching the views. Nothing touches the DOM or interop during setup/render (SSR-ready): the
data client is the only I/O boundary, behind the `IHackerNewsClient` seam.

**Injectable data client.** `IHackerNewsClient` is the swap point the acceptance criteria call for:
`Program` binds the live `HackerNewsClient`; the tests bind a `FakeHackerNewsClient`. A prerenderer
would bind its own.

## The source-generated JSON pattern (first networked Viu sample)

Reflection-based `System.Text.Json` is **forbidden** by the repo's AOT rules — the reflection
`Deserialize<T>(string, JsonSerializerOptions)` overloads are `[RequiresUnreferencedCode]`/
`[RequiresDynamicCode]` and fail the trimmed publish under `-warnaserror`. This sample establishes the
sanctioned pattern:

1. Wire DTOs (`ItemPayload`, `UserPayload`) into a `JsonSerializerContext` with
   `[JsonSerializable(...)]` (`Data/Internal/HackerNewsJsonSerializerContext.cs`).
2. Deserialize through the generated `JsonTypeInfo<T>` — `JsonSerializer.Deserialize(json,
   HackerNewsJsonSerializerContext.Default.ItemPayload)` — which carries no reflection/codegen
   requirement.
3. The feed endpoints return a bare JSON array of ids, read with reflection-free `JsonDocument.Parse`.

The trimmed publish is clean under `-warnaserror` and stays within the size budget
(`scripts/budgets/PublishBudgets.json`).

## Tests

`../Assimalign.Viu.HackerNews.Tests` drives the plain-Viu logic through the `Assimalign.Viu.Testing`
renderer and a **memory-history** router (no network, no browser):

- **Route table** resolution and the root-redirect guard (`RouteTableTests`).
- **Store behavior** with the fake client: loading/error modeling, filtering, pagination, the feed
  cache, comment-tree assembly, and superseded-load cancellation (`StoreTests`).
- **View rendering**: the story list from store state, and the loading/error/unknown-feed states
  (`ViewTests`).

```sh
dotnet test examples/Assimalign.Viu.HackerNews.Tests
```

## Why `.viu` here is `@style`-only

The logic-bearing components are hand-written C# `IComponent` render functions (like the
`WebApp` sample's `StopwatchApplication`), and the `.viu` files carry only `@style` blocks. This is now a
migration the sample has not yet made, not a framework limitation: as of [V01.01.06.07] (#216) a
`@template`-bearing `.viu` compiles to a **mountable component** — the generator emits the
`IComponent`/`Setup` bridge — so these views could be authored as full `.viu` components. The
`.viu` `@style` blocks here are global (unscoped) selectors matched by class name; converting a view to a
scoped `@template` `.viu` component is future cleanup with no change to the app's shape.

## Deep-link hosting note

Web history produces clean URLs (`/item/8863`). A production static host must serve `index.html` for
unknown routes (SPA fallback); the `dotnet run` dev server already does.
