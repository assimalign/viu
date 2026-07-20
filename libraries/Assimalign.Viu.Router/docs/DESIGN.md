# Assimalign.Viu.Router — design

Why the matcher is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). Upstream
counterpart: `vue-router` v4's matcher (`packages/router/src/matcher/` in
https://github.com/vuejs/router). Matching-syntax reference:
https://router.vuejs.org/guide/essentials/route-matching-syntax.html.

## The matcher is a pure port

vue-router's `createRouterMatcher` is deliberately independent of the browser so it can be
unit-tested without a DOM — the same property the router epic (#69) calls out. This package keeps
that property literal: it references no other Viu library and no interop assembly, so the whole
table-build/resolve path runs in a plain .NET test host. The navigation pipeline, history, and
components (#69, later features) will sit *on top of* this and supply the browser coupling.

The port is faithful to upstream at three levels, each pinned by tests and code links:

1. **Tokenizer** (`PathTokenizer` ⇄ `tokenizePath`): the character-by-character state machine,
   including the `> 1` "a repeatable param must be alone" guard (so `/user-:id+` is legal but
   `/:a:b:c+` is not), the escaped-`)` handling inside custom patterns, and the special cases
   `""` → `[[]]` and `/` → one empty-value `Static` token.
2. **Compiler + ranker** (`PathParserFactory` ⇄ `tokensToParser`, `PathScore` /
   `PathParserScoreComparer` ⇄ `PathScore` / `comparePathParserScore`): the regular-expression
   string, the per-segment score arrays, and the two-level comparison are reproduced value-for-value
   — including the fractional strict/case-sensitive bonuses (0.7, 0.25) that must stay below 1 so
   they only break ties.
3. **Resolution + insertion** (`RouteMatcher` ⇄ `createRouterMatcher`): the score-sorted matcher
   list, the binary-search insertion with the equal-score ancestor rule, path- and name-based
   resolve, and the parent-to-child matched chain.

## Ranking: specificity, never table order

The matcher list is kept sorted by descending score, and `Resolve(path)` returns the first (most
specific) matcher whose pattern matches. Because the score comes from the pattern's shape — static
segments (`+Static`) outweigh dynamic (`+Dynamic`), a custom pattern earns `+BonusCustomPattern`,
and the catch-all `(.*)` takes the large `BonusWildcard` penalty that also cancels its custom-pattern
bonus — the order routes were registered never changes the winner. The ranking tests register the
least-specific route first on purpose to prove it.

The one subtle case is the **empty-path default child**. A child with `path: ""` compiles to the
same full path (and therefore the same score) as its parent, so `findInsertionIndex` places it
*ahead of* the equally scored parent (`getInsertionAncestor`). Navigating to the parent path then
resolves the child and yields the two-entry `[parent, child]` matched chain — the layout-with-
default-view pattern.

## Deliberate divergences from vue-router

- **Every record is currently matchable.** Upstream's `isMatchable` gates the insertion-ancestor
  rule on a record having a name, components, or a redirect. Components and redirects are later
  Router features, so there is nothing yet to gate on; treating every record as matchable is what
  keeps the empty-path-child ordering working and is sufficient for every criterion in this ticket.
  When components/redirect land, `RouteMatcher.IsMatchable` is the single place to reintroduce the
  gate.
- **No `currentLocation` parameter inheritance.** Upstream's named resolve can inherit params from
  the current location for relative navigation. That needs a "current route", which is a
  navigation-pipeline concern (`[V01.01.08.04]`). Named resolution here interpolates exactly the
  parameters passed in (projected to the route's declared keys) and raises
  `MissingRequiredParameter` otherwise.
- **Path only.** The matcher resolves the path portion. Query strings and fragments are normalized
  and merged at the router level, not here.
- **Typed accessors replace `string | string[]` at the edge.** Internally a repeatable value stays
  an ordered `string[]` and a single value stays a `string` (matching upstream `PathParams`, including
  the quirk that an unmatched optional-repeatable parameter parses to an empty *string*, not an empty
  array). Consumers read through `GetString` / `GetInteger` / `GetStrings`, which parse on demand —
  no boxed `object`, no reflection over a dictionary. `GetInteger` on a non-integer raises
  `FormatException` and a missing key raises `KeyNotFoundException` (idiomatic .NET), while
  route-definition and interpolation failures raise the typed `RouteMatcherException`.

## AOT / trimming: no runtime codegen

WASM has no `new Function`/`eval` and reflection-emit is off the table, so nothing here compiles code
at runtime:

- **Path patterns compile to interpreted `Regex`.** A route table is runtime data, so each pattern
  string is assembled at table-build time and handed to `new Regex(...)` with the interpreted engine
  (never `RegexOptions.Compiled`, which relies on reflection emit). The interpreted engine is fully
  trimming- and NativeAOT-safe. This mirrors vue-router, whose `tokensToParser` likewise builds a
  `RegExp` from a runtime string.
- **The one compile-time-constant pattern uses `[GeneratedRegex]`.** `REGEX_CHARS_RE` (escaping
  literal path text) is known at build time, so `RegularExpressionPatterns` emits it through the
  Roslyn regex source generator — the AOT-preferred path — rather than constructing it reflectively.
  The valid-parameter-name check is a direct character test, needing no regular expression at all.
- **Custom parameter patterns should use non-capturing groups** (`(?:…)`), exactly as vue-router
  expects: the compiler wraps each parameter in a single capturing group and maps capturing groups
  to keys positionally, so a capturing group inside a user pattern would shift that mapping. Invalid
  custom patterns are validated at table-build time and raise `InvalidCustomPattern`.
- **`.NET` end-anchor nuance.** `.NET`'s `$` also matches immediately before a trailing newline,
  whereas JavaScript's `$` (no `m` flag) matches only at the very end. Route paths never contain
  newlines, so this is inert; noted here so a future reader does not mistake it for a bug.

## Value semantics for cheap comparison

The navigation pipeline and reactivity layer need to compare and snapshot locations cheaply, so
`RouteLocation` and `RouteParameters` implement value equality (locations compare their matched
records by identity; parameters compare structurally and hash order-independently). `RouteRecord`
keeps reference identity on purpose — the matched chain points at the exact record instances the
consumer supplied.

## Non-goals (sequenced work)

- History integration (browser/hash/memory behind `IRouterHistory`) — `[V01.01.08.02]`.
- `RouterView` / `RouterLink` components — `[V01.01.08.03]`.
- Async navigation pipeline and guards (redirect, cancellation), plus `currentLocation` param
  inheritance and route removal — `[V01.01.08.04]`.
- Lazy route components and scroll behavior — `[V01.01.08.05]`.
- Components, redirects, aliases, and per-record `strict`/`sensitive` overrides on `RouteRecord`
  (only global `PathMatchingOptions` today).
