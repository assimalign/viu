# Assimalign.Viu.Router — overview

The client router for Viu — the role `vue-router` v4 plays for Vue 3
(https://github.com/vuejs/router). This first feature delivers the **route table and matcher**: the
pure, DOM-free core that, given a set of route records and a location (a path or a named target),
resolves an immutable `RouteLocation` with its parent-to-child matched chain and parsed parameters.
It is the C# port of vue-router's `createRouterMatcher` and its path-parser ranking model
(`packages/router/src/matcher/`).

History integration, `RouterView`/`RouterLink`, the async navigation pipeline with guards, and lazy
routes are later features of the Router area (#69, `[V01.01.08.02]`–`[V01.01.08.05]`) and are not
part of this package yet.

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

## Boundaries

- References **no other Viu library** and **no JavaScript-interop assembly** — the matcher is pure
  and runs in a plain .NET test host (a boundary the test suite asserts by reflection).
- Trimming- and NativeAOT-safe: no reflection-based serialization, no dynamic code generation. Path
  patterns compile to interpreted regular expressions; the one compile-time-constant pattern uses
  the `[GeneratedRegex]` source generator.
- Design rationale, the ranking model, and the deliberate C#/WASM divergences from vue-router:
  [DESIGN.md](DESIGN.md).
