# Assimalign.Viu.Router — overview

The client router for Viu — the role `vue-router` v4 plays for Vue 3
(https://github.com/vuejs/router). Two features have landed:

- The **route table and matcher** (`[V01.01.08.01]`): the pure, DOM-free core that, given a set of
  route records and a location (a path or a named target), resolves an immutable `RouteLocation`
  with its parent-to-child matched chain and parsed parameters. The C# port of vue-router's
  `createRouterMatcher` and its path-parser ranking model (`packages/router/src/matcher/`).
- **History integration** (`[V01.01.08.02]`): the `IRouterHistory` abstraction with three modes —
  memory, web (HTML5 History API), and hash — behind the `RouterHistory` factory. The C# port of
  vue-router's `packages/router/src/history/`.

`RouterView`/`RouterLink`, the async navigation pipeline with guards, and lazy routes are later
features of the Router area (#69, `[V01.01.08.03]`–`[V01.01.08.05]`) and are not part of this package
yet.

## What it contains

Public surface (all under namespace `Assimalign.Viu.Router`):

- **`RouteMatcher`** (entry point): the route table and matcher. Construct it from a set of
  `RouteRecord`s (`new RouteMatcher(routes)`), then `Resolve(path)` for path resolution or
  `ResolveNamed(name, parameters)` for named resolution. `AddRoute`, `HasNamedRoute`, and
  `GetRoutes` round out the surface. The C# port of the object returned by `createRouterMatcher`.
- **`IRouteMatcher`** (`Abstraction/`): the resolve/add/query contract the later navigation pipeline
  depends on, implemented by `RouteMatcher`.
- **`RouteRecord`**: an immutable route definition — `Path`, optional `Name`, nested `Children`,
  and arbitrary `Meta`. A reference type with identity semantics (the same instance appears in every
  matched chain it participates in). The C# port of vue-router's route record.
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

## Boundaries

- References **no other Viu library** — the matcher and the memory history run in a plain .NET test
  host (a boundary the test suite asserts by reflection). `[V01.01.08.02]` added a browser history
  edge over the framework's `System.Runtime.InteropServices.JavaScript` primitive, gated by
  `[SupportedOSPlatform("browser")]`; the matcher and memory mode reference **no interop** and the
  web/hash **policy** is exercised off-browser through an injected seam.
- Trimming- and NativeAOT-safe: no reflection-based serialization, no dynamic code generation. Path
  patterns compile to interpreted regular expressions; the one compile-time-constant pattern uses
  the `[GeneratedRegex]` source generator. History state marshals as a flat primitives-only payload.
- Design rationale, the ranking model, and the deliberate C#/WASM divergences from vue-router:
  [DESIGN.md](DESIGN.md).
