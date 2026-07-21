# Assimalign.Viu.Router — overview

The client router for Viu — the role `vue-router` v4 plays for Vue 3
(https://github.com/vuejs/router). Four features have landed:

- The **route table and matcher** (`[V01.01.08.01]`): the pure, DOM-free core that, given a set of
  route records and a location (a path or a named target), resolves an immutable `RouteLocation`
  with its parent-to-child matched chain and parsed parameters. The C# port of vue-router's
  `createRouterMatcher` and its path-parser ranking model (`packages/router/src/matcher/`).
- **History integration** (`[V01.01.08.02]`): the `IRouterHistory` abstraction with three modes —
  memory, web (HTML5 History API), and hash — behind the `RouterHistory` factory. The C# port of
  vue-router's `packages/router/src/history/`.
- **`RouterView` / `RouterLink` components** (`[V01.01.08.03]`): the two built-in components plus the
  reactive `Router` facade they consume. `RouterView` renders the matched record's component
  at its nesting depth; `RouterLink` renders a navigation-intercepting anchor with active-class
  matching. The C# port of vue-router's `packages/router/src/RouterView.ts` and `RouterLink.ts`.
- **Navigation guards and async navigation flows** (`[V01.01.08.04]`): the awaitable, cancellable
  guard pipeline behind `Router.Push`/`Replace` — global `BeforeEach`/`BeforeResolve`/`AfterEach`,
  per-route `RouteRecord.BeforeEnter`, and in-component `beforeRouteLeave`/`beforeRouteUpdate`
  (`RouterGuards`) / `beforeRouteEnter` (`IRouteEnterGuard`) guards — with redirects, cancellation of
  superseded navigations, and `NavigationFailure` results. The C# port of vue-router's
  `packages/router/src/router.ts` `navigate()` and `navigationGuards.ts`.

**Lazy routes with scroll behavior** (`[V01.01.08.05]`) is the remaining Router feature (#69) and is
not part of this package yet — every route component resolves eagerly today.

## What it contains

Public surface (all under namespace `Assimalign.Viu.Router`):

- **`RouteMatcher`** (entry point): the route table and matcher. Construct it from a set of
  `RouteRecord`s (`new RouteMatcher(routes)`), then `Resolve(path)` for path resolution or
  `ResolveNamed(name, parameters)` for named resolution. `AddRoute`, `HasNamedRoute`, and
  `GetRoutes` round out the surface. The C# port of the object returned by `createRouterMatcher`.
- **`IRouteMatcher`** (`Abstraction/`): the resolve/add/query contract the later navigation pipeline
  depends on, implemented by `RouteMatcher`.
- **`RouteRecord`**: an immutable route definition — `Path`, optional `Name`, nested `Children`,
  arbitrary `Meta`, and (for the components) the `Component` the route renders plus an optional
  `PropertiesResolver`. A reference type with identity semantics (the same instance appears in every
  matched chain it participates in). The C# port of vue-router's route record; the matcher ignores
  `Component`/`PropertiesResolver`.
- **`RouteLocation`**: the immutable resolution result — `Path`, `Name`, `Parameters`, the
  parent-to-child `Matched` record chain, merged `Meta`, and `IsMatched`. Value equality so a
  navigation layer can compare/snapshot cheaply. Mirrors the object returned by the matcher's
  `resolve`.
- **`RouteParameters`**: an immutable parameter set with typed, boxing-free, reflection-free
  accessors — `GetString`/`TryGetString`, `GetInteger`/`TryGetInteger`, `GetStrings` (for
  repeatable params) — plus immutable `With`/`WithMany` builders. Mirrors vue-router's `RouteParams`.
- **`PathMatchingOptions`**: `Strict` and `Sensitive` matching toggles (vue-router's `strict` /
  `sensitive`), defaulting to non-strict and case-insensitive.
- **`RouteMatcherException`** + **`RouteMatcherError`**: the typed failure for invalid route
  definitions (bad path, unclosed/invalid custom pattern, repeatable-not-alone) and resolution
  failures (named route not found, missing required parameter, array for a non-repeatable
  parameter).

Internal (`Internal/`, exercised through `InternalsVisibleTo` tests): the path parser — `PathToken`
/ `PathTokenKind` / `PathTokenizer` (the C# port of `tokenizePath`), `PathParserFactory` (the port
of `tokensToParser`), `PathParser` (the compiled regular expression + score + keys, with
`TryParse`/`Stringify`), `PathScore` + `PathParserScoreComparer` (the ranking model),
`PathParameterKey`, `RouteParameterValue`, `RouteRecordMatcher`, and `RegularExpressionPatterns`
(the `[GeneratedRegex]` escape helper).

## History integration

The history layer (all under namespace `Assimalign.Viu.Router`) — the C# port of vue-router's
`RouterHistory` (`packages/router/src/history/`):

- **`IRouterHistory`** (`Abstraction/`): the history contract — `Base`, `Location`, `State`,
  `Push`/`Replace`/`Go`, `Listen`, `CreateHref`, `Destroy`. Locations are the base-stripped path
  the matcher resolves; the configured base is prepended on write and stripped on read.
- **`RouterHistory`** (static facade): `CreateMemory`, `CreateWeb`, `CreateWebHash`, and the
  browser-only `InitializeAsync`. The C# port of `createMemoryHistory`/`createWebHistory`/
  `createWebHashHistory`. Memory is pure and needs no initialization; web and hash drive the History
  API over interop and require `InitializeAsync` first.
- **`RouterHistoryState`**: the flat, primitives-only state carried on each entry — the adjacency
  links (`Back`/`Current`/`Forward`), the `Replaced` flag, the monotonic `Position` counter, and an
  optional `Scroll` anchor. The C# port of vue-router's `StateEntry`.
- **`NavigationType`** / **`NavigationDirection`** / **`NavigationInformation`** / **`ScrollPosition`**:
  the value types a history reports to its listeners (pop vs push, back/forward/unknown, the signed
  delta) and the saved scroll offset. Ports of the same-named vue-router types.
- **`NavigationCallback`** (`Delegates/`): the listener signature for browser-initiated navigation
  (a `popstate`, or a memory `Go`).

Internal (`Internal/`): `MemoryRouterHistory` (the pure, interop-free port of `createMemoryHistory`),
`BrowserRouterHistory` (the DOM-free web/hash **policy** — base handling, state machine, listener
bookkeeping — driving an injected `IBrowserHistoryInterop`), the pure helpers
`HistoryPathNormalization` (base normalize/strip, `createHref`, `createCurrentLocation`, hash base)
and `RouterHistoryStateBuilder` (the `buildState`/push/replace arithmetic), the batched-read
`BrowserHistorySnapshot` + `BrowserHistorySnapshotMarshaller`, and the thin browser edge —
`JavaScriptBrowserHistoryInterop` (`[JSImport]` bindings to `wwwroot/viu-history.js`) and
`BrowserHistoryInteropDispatch` (the single `[JSExport]` the `popstate` listener calls back into,
routed by subscription id).

## Components and router

The two built-in components and the reactive router facade they consume (`[V01.01.08.03]`, all under
namespace `Assimalign.Viu.Router`) — the C# port of vue-router's `RouterView`/`RouterLink` and the
minimal slice of `createRouter` those components need:

- **`Router`** (entry point): the reactive router facade — `CurrentRoute` (a reactive
  `shallowRef` over the resolved location), `Resolve`/`ResolveNamed`, `CreateHref`, the global
  `LinkActiveClass`/`LinkExactActiveClass` defaults, and the awaitable, guarded
  `Push`/`Replace`/`Go`/`Back`/`Forward` navigation surface. Global guards register through
  `BeforeEach`/`BeforeResolve`/`AfterEach`/`OnError` (each returning an unregister delegate). Built
  over an `IRouterHistory` and a matcher (or a route set), it listens to the history so browser
  back/forward drives `CurrentRoute` through the same guard pipeline. The C# stand-in for the object
  `createRouter` returns.
- **`RouterView`** (`Components/`): the route outlet. It injects the `Router` and its own nesting
  depth, renders `route.matched[depth].Component` with that record's resolved props, and provides
  `depth + 1` to any `RouterView` nested inside the rendered component. The reactive current route it
  reads re-renders it on navigation. The C# port of `RouterView.ts`.
- **`RouterLink`** (`Components/`): the navigation anchor. It renders an `<a>` whose `href` resolves
  through the `Router` (base included), applies the active / exact-active classes by matching its
  target against the current route, and intercepts an unmodified primary-button click to navigate
  client-side. Declared props: `to`, `replace`, `activeClass`, `exactActiveClass`. The C# port of
  `RouterLink.ts`.
- **`RouteComponentProperties`** (`Components/`) + **`RouteComponentPropertiesResolver`** (`Delegates/`):
  the per-route `props` option (https://router.vuejs.org/guide/essentials/passing-props.html).
  `FromParameters()` is `props: true` (params become props), `FromValues(...)` is the static-object
  form, and a hand-written resolver is the function form.
- **`RouterLinkClickEvent`** (`Components/`): the DOM-free click info `RouterLink`'s guard reads
  (button, system modifiers, `DefaultPrevented`) — a host's event bridge builds it from the native
  `MouseEvent`; tests construct it directly.
- **`RouterInjectionKeys`**: the provide/inject keys wiring the router into the tree (`Router`
  app-wide; the internal view-depth key threaded between nested views).

## Navigation guards

The awaitable, cancellable navigation pipeline (`[V01.01.08.04]`, all under namespace
`Assimalign.Viu.Router`) — the C# port of vue-router's `navigate()`/`navigationGuards.ts`:

- **`NavigationGuard`** (`Delegates/`): the guard signature `Task<NavigationGuardResult> (to, from,
  cancellationToken)`. Guards **return** a decision instead of calling `next()` (the return-value form
  vue-router v4 prefers).
- **`NavigationGuardResult`**: a guard's decision — the `Allow`/`Abort` singletons and
  `RedirectTo(path)`/`RedirectToName(name, params)` for redirects.
- **`NavigationFailure`** + **`NavigationFailureType`**: the result of a navigation that did not
  complete (`Aborted`/`Cancelled`/`Duplicated`), returned from `Push`/`Replace` and passed to every
  `AfterNavigationHook`. Ports of vue-router's `NavigationFailure`/`NavigationFailureType`.
- **`AfterNavigationHook`** / **`NavigationErrorHandler`** (`Delegates/`): the `AfterEach` and
  `OnError` signatures.
- **`RouteRecord.BeforeEnter`**: the per-route enter guard (upstream `beforeEnter`).
- **`RouterGuards`**: the `OnBeforeRouteLeave`/`OnBeforeRouteUpdate` composables, called during a route
  component's `Setup` and bound to the component's lifecycle (upstream's in-component leave/update
  guards).
- **`IRouteEnterGuard`** (`Abstraction/`): implemented by a route component to contribute a
  `beforeRouteEnter` guard (interface-based discovery, no reflection — the instance does not yet
  exist when it runs).
- **`NavigationRedirectException`**: thrown when a guard-redirect chain exceeds the safety cap
  (upstream's infinite-redirect detection).

## Using it

```csharp
using Assimalign.Viu.Router;

var matcher = new RouteMatcher(
[
    new RouteRecord("/", name: "home"),
    new RouteRecord("/users", name: "users", children:
    [
        new RouteRecord(":id", name: "user"),           // -> /users/:id
    ]),
    new RouteRecord("/:pathMatch(.*)*", name: "not-found"),
]);

RouteLocation location = matcher.Resolve("/users/42");
// location.Name == "user"
// location.Parameters.GetInteger("id") == 42
// location.Matched == [users record, user record]   (parent-to-child)

string path = matcher.ResolveNamed("user", RouteParameters.Empty.With("id", "42")).Path;
// path == "/users/42"
```

```csharp
// Memory history — pure, no browser, no initialization.
IRouterHistory history = RouterHistory.CreateMemory();
history.Push("/users/42");
// history.Location == "/users/42", history.State.Position == 1

// Web history — clean URLs over the History API (browser only).
await RouterHistory.InitializeAsync();
IRouterHistory web = RouterHistory.CreateWeb("/app/");   // base prepended on write, stripped on read
web.Listen((to, from, information) => { /* resolve `to` through the matcher */ });
```

```csharp
// Wire routes to components, build a router over a history, and provide it to the tree.
var router = new Router(RouterHistory.CreateMemory(),
[
    new RouteRecord("/users/:id", component: new UserView(),
        propertiesResolver: RouteComponentProperties.FromParameters()),   // props: true
]);
NavigationFailure? failure = await router.Push("/users/42");   // awaitable; null on success

app.Provide(RouterInjectionKeys.Router, router);   // a host provides the router app-wide
// <RouterView/> now renders UserView with { id = "42" }; <RouterLink to="/users/42"/> is exact-active,
// and a plain left-click on it calls router.Push instead of triggering a page load.
```

```csharp
// Guards run in vue-router's documented order and either allow, abort, or redirect.
Action removeAuthGuard = router.BeforeEach((to, from, cancellationToken) =>
    Task.FromResult(to.Meta.ContainsKey("requiresAuth") && !IsSignedIn
        ? NavigationGuardResult.RedirectTo("/login")
        : NavigationGuardResult.Allow));

router.AfterEach((to, from, failure) => { /* failure is null on success */ });
router.OnError((error, to, from) => { /* an unexpected guard exception */ });

// In a route component's Setup: block leaving with unsaved changes.
RouterGuards.OnBeforeRouteLeave((to, from, cancellationToken) =>
    Task.FromResult(hasUnsavedChanges ? NavigationGuardResult.Abort : NavigationGuardResult.Allow));

removeAuthGuard();   // registration handles unregister the guard
```

## Boundaries

- **Matcher and history stay framework-free; the components reference the runtime.** The matcher and
  memory history run in a plain .NET test host, using no other Viu library. `[V01.01.08.03]` adds the
  `RouterView`/`RouterLink` components, which consume the component model and reactivity, so the
  assembly now references `Assimalign.Viu.Core` (issue #72's
  boundary). It still references **no browser DOM adapter** (`Assimalign.Viu.Browser`): the
  components produce platform-agnostic `VirtualNode`s that render through the injected node-ops
  abstraction — the in-memory test renderer and the SSR renderer alike — never the DOM directly (a
  boundary the test suite asserts by reflection). `[V01.01.08.02]`'s browser history edge over the
  framework's `System.Runtime.InteropServices.JavaScript` primitive stays gated by
  `[SupportedOSPlatform("browser")]`.
- Trimming- and NativeAOT-safe: no reflection-based serialization, no dynamic code generation. Path
  patterns compile to interpreted regular expressions; the one compile-time-constant pattern uses
  the `[GeneratedRegex]` source generator. History state marshals as a flat primitives-only payload.
- Design rationale, the ranking model, and the deliberate C#/WASM divergences from vue-router:
  [DESIGN.md](DESIGN.md).
