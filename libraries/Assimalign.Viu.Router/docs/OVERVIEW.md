# Assimalign.Viu.Router — overview

The client router for Viu: a route table, history integration, two built-in components, and an
awaitable guard pipeline. Specified by [§12 of the Viu Specification](../../../docs/SPECIFICATION.md#12-routing).
Four features have landed:

- The **route table and matcher** (`[V01.01.08.01]`): the pure, DOM-free core that, given a set of
  route records and a location (a path or a named target), resolves an immutable `RouteLocation`
  with its parent-to-child matched chain and parsed parameters, plus the specificity ranking model
  that makes resolution independent of route-table order.
- **History integration** (`[V01.01.08.02]`): the `IRouterHistory` abstraction with three modes —
  memory, web (HTML5 History API), and hash — behind the `RouterHistory` factory.
- **`RouterView` / `RouterLink` components** (`[V01.01.08.03]`): the two built-in components plus the
  reactive `Router` facade they consume. `RouterView` renders the matched record's component
  at its nesting depth; `RouterLink` renders a navigation-intercepting anchor with active-class
  matching.
- **Navigation guards and async navigation flows** (`[V01.01.08.04]`): the awaitable, cancellable
  guard pipeline behind `Router.Push`/`Replace` — global `BeforeEach`/`BeforeResolve`/`AfterEach`,
  per-route `RouteRecord.BeforeEnter`, and the in-component before-leave/before-update
  (`RouterGuards`) and before-enter (`IRouteEnterGuard`) guards — with redirects, cancellation of
  superseded navigations, and `NavigationFailure` results.

**Lazy routes with scroll behavior** (`[V01.01.08.05]`) is the remaining Router feature (#69) and is
not part of this package yet — every route component resolves eagerly today.

## What it contains

Public surface (all under namespace `Assimalign.Viu.Router`):

- **`RouteMatcher`** (entry point): the route table and matcher. Construct it from a set of
  `RouteRecord`s (`new RouteMatcher(routes)`), then `Resolve(path)` for path resolution or
  `ResolveNamed(name, parameters)` for named resolution. `AddRoute`, `HasNamedRoute`, and
  `GetRoutes` round out the surface.
- **`IRouteMatcher`** (`Abstraction/`): the resolve/add/query contract the later navigation pipeline
  depends on, implemented by `RouteMatcher`.
- **`RouteRecord`**: an immutable route definition — `Path`, optional `Name`, nested `Children`,
  arbitrary `Meta`, and (for the components) the `Component` the route renders plus an optional
  `ArgumentsResolver`. A reference type with identity semantics (the same instance appears in every
  matched chain it participates in). The matcher ignores `Component`/`ArgumentsResolver` — they are
  read only by the view components. The component may be any unified `IComponent`; an arguments
  resolver is valid only for an `ITemplateComponent` request.
- **`RouteLocation`**: the immutable resolution result — `Path`, `Name`, `Parameters`, the
  parent-to-child `Matched` record chain, merged `Meta`, and `IsMatched`. Value equality so a
  navigation layer can compare/snapshot cheaply.
- **`RouteParameters`**: an immutable parameter set with typed, boxing-free, reflection-free
  accessors — `GetString`/`TryGetString`, `GetInteger`/`TryGetInteger`, `GetStrings` (for
  repeatable parameters) — plus immutable `With`/`WithMany` builders.
- **`PathMatchingOptions`**: the `Strict` (trailing slash is significant) and `Sensitive`
  (case-sensitive) matching toggles, defaulting to non-strict and case-insensitive.
- **`RouteMatcherException`** + **`RouteMatcherError`**: the typed failure for invalid route
  definitions (bad path, unclosed/invalid custom pattern, repeatable-not-alone) and resolution
  failures (named route not found, missing required parameter, array for a non-repeatable
  parameter).

Internal (`Internal/`, exercised through `InternalsVisibleTo` tests): the path parser — `PathToken`
/ `PathTokenKind` / `PathTokenizer` (the character-by-character tokenizer), `PathParserFactory` (the
tokens-to-parser compiler), `PathParser` (the compiled regular expression + score + keys, with
`TryParse`/`Stringify`), `PathScore` + `PathParserScoreComparer` (the ranking model),
`PathParameterKey`, `RouteParameterValue`, `RouteRecordMatcher`, and `RegularExpressionPatterns`
(the `[GeneratedRegex]` escape helper).

## History integration

The history layer (all under namespace `Assimalign.Viu.Router`):

- **`IRouterHistory`** (`Abstraction/`): the history contract — `Base`, `Location`, `State`,
  `Push`/`Replace`/`Go`, `Listen`, `CreateHref`, `Destroy`. Locations are the base-stripped path
  the matcher resolves; the configured base is prepended on write and stripped on read.
- **`RouterHistory`** (static facade): `CreateMemory`, `CreateWeb`, `CreateWebHash`, and the
  browser-only `InitializeAsync`. Memory is pure and needs no initialization; web and hash drive the
  History API over interop and require `InitializeAsync` first.
- **`RouterHistoryState`**: the flat, primitives-only state carried on each entry — the adjacency
  links (`Back`/`Current`/`Forward`), the `Replaced` flag, the monotonic `Position` counter, and an
  optional `Scroll` anchor.
- **`NavigationType`** / **`NavigationDirection`** / **`NavigationInformation`** / **`ScrollPosition`**:
  the value types a history reports to its listeners (pop vs push, back/forward/unknown, the signed
  delta) and the saved scroll offset.
- **`NavigationCallback`** (`Delegates/`): the listener signature for browser-initiated navigation
  (a `popstate`, or a memory `Go`).

Internal (`Internal/`): `MemoryRouterHistory` (the pure, interop-free mode), `BrowserRouterHistory`
(the DOM-free web/hash **policy** — base handling, state machine, listener bookkeeping — driving an
injected `IBrowserHistoryInterop`), the pure helpers `HistoryPathNormalization` (base
normalize/strip, href building, current-location derivation, hash base) and
`RouterHistoryStateBuilder` (the push/replace/bootstrap state arithmetic), the batched-read
`BrowserHistorySnapshot` + `BrowserHistorySnapshotMarshaller`, and the thin browser edge —
`JavaScriptBrowserHistoryInterop` (`[JSImport]` bindings to `wwwroot/viu-history.js`) and
`BrowserHistoryInteropDispatch` (the single `[JSExport]` the `popstate` listener calls back into,
routed by subscription id).

## Components and router

The two built-in components and the reactive router facade they consume (`[V01.01.08.03]`, all under
namespace `Assimalign.Viu.Router`):

- **`Router`** (entry point): the reactive router facade — `CurrentRoute` (a shallow reactive
  reference over the resolved location), `Resolve`/`ResolveNamed`, `CreateHref`, the global
  `LinkActiveClass`/`LinkExactActiveClass` defaults, and the awaitable, guarded
  `Push`/`Replace`/`Go`/`Back`/`Forward` navigation surface. Global guards register through
  `BeforeEach`/`BeforeResolve`/`AfterEach`/`OnError` (each returning an unregister delegate). Built
  over an `IRouterHistory` and a matcher (or a route set), it listens to the history so browser
  back/forward drives `CurrentRoute` through the same guard pipeline.
- **`RouterView`** (`Components/`): the route outlet. It resolves `Router` from
  `IComponentContext.Services`, reads its explicit `depth` argument (default `0`), and renders
  `route.matched[depth].Component` with that record's resolved arguments. A nested layout passes the
  next depth explicitly because Viu has no hierarchical component dependency API. The reactive
  current route it reads re-renders it on navigation.
- **`RouterLink`** (`Components/`): the navigation anchor. It renders an `<a>` whose `href` resolves
  through the `Router` (base included), applies the active / exact-active classes by matching its
  target against the current route, and intercepts an unmodified primary-button click to navigate
  client-side. Declared arguments: `to`, `replace`, `activeClass`, `exactActiveClass`.
- **`RouteComponentArguments`** (`Components/`) + **`RouteComponentArgumentsResolver`** (`Delegates/`):
  the per-route argument supply. `FromParameters()` forwards the resolved route parameters as
  same-named arguments, `FromValues(...)` supplies a fixed argument set, and a hand-written resolver
  reads the whole `RouteLocation`.
- **`RouterLinkClickEvent`** (`Components/`): the DOM-free click info `RouterLink`'s guard reads
  (button, system modifiers, `DefaultPrevented`) — a host's event bridge builds it from the native
  `MouseEvent`; tests construct it directly.

## Navigation guards

The awaitable, cancellable navigation pipeline (`[V01.01.08.04]`, all under namespace
`Assimalign.Viu.Router`):

- **`NavigationGuard`** (`Delegates/`): the guard signature `Task<NavigationGuardResult> (to, from,
  cancellationToken)`. A guard **returns** its decision rather than invoking a continuation, so an
  exhaustive result type makes the compiler check that every path decides and lets the pipeline
  guarantee a guard decides exactly once.
- **`NavigationGuardResult`**: a guard's decision — the `Allow`/`Abort` singletons and
  `RedirectTo(path)`/`RedirectToName(name, params)` for redirects.
- **`NavigationFailure`** + **`NavigationFailureType`**: the result of a navigation that did not
  complete (`Aborted`/`Cancelled`/`Duplicated`), returned from `Push`/`Replace` and passed to every
  `AfterNavigationHook`.
- **`AfterNavigationHook`** / **`NavigationErrorHandler`** (`Delegates/`): the `AfterEach` and
  `OnError` signatures.
- **`RouteRecord.BeforeEnter`**: the per-route enter guard, run only for a newly matched record.
- **`RouterGuards`**: the `OnBeforeRouteLeave`/`OnBeforeRouteUpdate` composables, called during a route
  component's `Setup` with its explicit `IComponentContext` and outlet depth, then bound to the
  component's lifecycle, so a guard never outlives its instance.
- **`IRouteEnterGuard`** (`Abstraction/`): supplied explicitly through
  `RouteRecord.RouteEnterGuard` to contribute an in-component before-enter guard. No component is
  activated and no reflection is used before the route is confirmed.
- **`NavigationRedirectException`**: thrown when a guard-redirect chain exceeds the safety cap —
  infinite-redirect detection, active in every configuration.

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
using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

// Wire routes to unified component-tree requests and build a router over a history.
var router = new Router(RouterHistory.CreateMemory(),
[
    new RouteRecord(
        "/users/:id",
        component: ComponentTree.Template<UserView>(),
        argumentsResolver: RouteComponentArguments.FromParameters()),   // params become arguments
]);
NavigationFailure? failure = await router.Push("/users/42");   // awaitable; null on success

// Register Router in the IServiceProvider selected by the application, then pass that provider to
// the host builder with UseServiceProvider(...). Router does not create or modify a container.
// <RouterView/> now renders UserView with { id = "42" }; <RouterLink to="/users/42"/> is exact-active,
// and a plain left-click on it calls router.Push instead of triggering a page load.
```

```csharp
// Guards run in the pipeline's fixed order and either allow, abort, or redirect.
Action removeAuthGuard = router.BeforeEach((to, from, cancellationToken) =>
    Task.FromResult(to.Meta.ContainsKey("requiresAuth") && !IsSignedIn
        ? NavigationGuardResult.RedirectTo("/login")
        : NavigationGuardResult.Allow));

router.AfterEach((to, from, failure) => { /* failure is null on success */ });
router.OnError((error, to, from) => { /* an unexpected guard exception */ });

// In a route component's Setup: pass the explicit context and outlet depth.
RouterGuards.OnBeforeRouteLeave(
    context,
    (to, from, cancellationToken) =>
        Task.FromResult(
            hasUnsavedChanges
                ? NavigationGuardResult.Abort
                : NavigationGuardResult.Allow),
    depth: 0);

removeAuthGuard();   // registration handles unregister the guard
```

## Boundaries

- **Matcher and history stay framework-free; the built-ins reference contracts only.** The matcher and
  memory history run in a plain .NET test host, using no other Viu library. `[V01.01.08.03]` adds the
  `RouterView`/`RouterLink` components, which consume the component model and reactivity, so the
  assembly references `Assimalign.Viu.Components` and `Assimalign.Viu.Reactivity`, but not
  `Assimalign.Viu.Core`. It still references **no browser DOM adapter** (`Assimalign.Viu.Browser`):
  the built-ins produce platform-agnostic `IComponent` values that any host renderer can consume — the
  in-memory test renderer and the server renderer alike — never the DOM directly (a boundary the test
  suite asserts by reflection). `[V01.01.08.02]`'s browser history edge over the
  framework's `System.Runtime.InteropServices.JavaScript` primitive stays gated by
  `[SupportedOSPlatform("browser")]`.
- Trimming- and NativeAOT-safe: no reflection-based serialization, no dynamic code generation. Path
  patterns compile to interpreted regular expressions; the one compile-time-constant pattern uses
  the `[GeneratedRegex]` source generator. History state marshals as a flat primitives-only payload.
- Design rationale, the ranking model, and the WASM/AOT-driven design decisions:
  [DESIGN.md](DESIGN.md).
