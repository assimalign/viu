# Assimalign.Viu.Router — design

Why the matcher and the history layer are shaped the way they are. What they are: see
[OVERVIEW.md](OVERVIEW.md). Upstream counterpart: `vue-router` v4's matcher
(`packages/router/src/matcher/`) and history (`packages/router/src/history/`, in
https://github.com/vuejs/router). Matching-syntax reference:
https://router.vuejs.org/guide/essentials/route-matching-syntax.html; history-mode reference:
https://router.vuejs.org/guide/essentials/history-mode.html.

## The matcher is a pure port

vue-router's `createRouterMatcher` is deliberately independent of the browser so it can be
unit-tested without a DOM — the same property the router epic (#69) calls out. The matcher and memory
history keep that property literal: their code references no other Viu library and no interop
assembly, so the whole table-build/resolve path runs in a plain .NET test host. The
`RouterView`/`RouterLink` components (`[V01.01.08.03]`) sit *on top of* this in the same assembly and
do reference the runtime (see "Components: depth, reactivity, and click guards"), but never the
browser DOM adapter — so the components stay renderer-agnostic and the matcher/history code stays
framework-free.

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
  rule on a record having a name, components, or a redirect. `RouteRecord.Component` now exists
  (`[V01.01.08.03]`), but the gate is intentionally *not* reintroduced: every criterion here is met
  with all records matchable (the empty-path-child ordering depends on it), and redirects/aliases —
  the rest of upstream's `isMatchable` — are still later features. When they land,
  `RouteMatcher.IsMatchable` remains the single place to reintroduce the gate.
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

## History: the policy is split from the interop edge

The web and hash histories (`[V01.01.08.02]`, the C# port of `packages/router/src/history/`) are
split in two so the browser one is testable without a browser:

- **`BrowserRouterHistory` is the policy** — base prepend/strip, the push/replace/`popstate` state
  machine, listener bookkeeping — and touches no DOM. Every environment effect is delegated to an
  injected **`IBrowserHistoryInterop`**. This mirrors `Assimalign.Viu.RuntimeDom`'s
  `BrowserEventInvokerRegistry`, which takes its bridge calls as delegates so it is unit-testable
  with recorded doubles. A `FakeBrowserHistoryInterop` records every crossing and can simulate a
  `popstate`, so base handling, the state round-trip, delta/direction, listener teardown, and the
  interop-call count are all pinned on a plain .NET host.
- **`JavaScriptBrowserHistoryInterop` is the thin edge** — `[JSImport]` bindings to
  `wwwroot/viu-history.js` — and does nothing but flatten the policy's URLs and states into
  primitive interop calls. The JS module is a dumb applier: the only decision it makes is reading the
  live `window.scrollX/Y` for the leaving entry (the one piece of state the DOM owns).

`createWebHashHistory` is not a second implementation: as upstream, it only computes a `#`-carrying
base and hands it to the same web policy (`RouterHistory.ResolveHashBase` → `BrowserRouterHistory`).

## History state: one position counter, computed in C#

`RouterHistoryState` is a flat, primitives-only payload (the port of upstream's `StateEntry`). The
monotonic `Position` counter is assigned by `RouterHistoryStateBuilder` — `+1` per push, preserved
across a replace, seeded from `history.length - 1` at bootstrap — **in C#**, not read from
`window.history.length` per call, so the identical arithmetic runs in memory and in the browser and
is unit-tested in isolation. A `popstate`'s signed distance is `arrived.Position - leaving.Position`,
which yields the `NavigationDirection`. State crosses the boundary as primitives only: a null
adjacency link and an absent scroll encode as an empty string / `false` flag, never a marshaled
object graph (`BrowserHistorySnapshotMarshaller` pins the wire format).

## History: batching and leak-free teardown

- **Reads are one crossing.** `ReadSnapshot()` returns the raw `location` components *and* the current
  entry's state together — no per-property getters. The policy caches location and state and never
  re-reads them per navigation; a test asserts exactly one snapshot read across a push/replace/go/pop
  sequence (the batched-read criterion).
- **Writes are one crossing.** A push is two History-API operations (amend the leaving entry, then
  push the new one), but the `Push` edge collapses them into a single interop call; a replace is one.
- **`popstate` is one `[JSExport]`**, routed by a subscription id (`BrowserHistoryInteropDispatch`,
  the history analogue of `BrowserEventDispatch`). The id lets many histories coexist and lets
  `Destroy` unsubscribe the exact JS listener — no leak across instances, which matters because test
  hosts create many.

## History: deliberate divergences from vue-router

- **Memory carries a full state per entry.** Upstream's memory history keeps `state = {}`; this port
  stores a real `RouterHistoryState` on every memory entry so the position counter round-trips and
  memory is a faithful reference model for the web state semantics — the `[V01.01.08.02]` requirement
  that memory reproduce the same push/replace/go behavior. Memory still skips the browser's
  amend-the-leaving-entry step (upstream memory does too), so a memory entry's `Forward` link is not
  back-patched.
- **Root-relative write URLs.** The browser edge writes `base + location` (web) or the `#…` slice
  (hash) rather than upstream's absolute `location.protocol + '//' + location.host + base + to`.
  `pushState` resolves the root-relative form identically and it keeps the write path DOM-free (no
  protocol/host read). The dropped branch is upstream's `<base>`-element write-time special case
  (`document.querySelector('base')`), an intentional simplification noted at the call site.
- **Scroll is the one JS-owned field.** The leaving entry's scroll anchor is read from the live window
  by the interop at apply time (the policy cannot read the DOM); every other field is C#-computed, and
  memory — having no DOM — leaves scroll `null`. Scroll *restoration* (consuming the anchors) is
  `[V01.01.08.05]`.

## Components: depth, reactivity, and click guards

`RouterView`/`RouterLink` (`[V01.01.08.03]`, the C# port of `RouterView.ts`/`RouterLink.ts`) are
ordinary `IComponentDefinition`s in this assembly. Issue #72 places them here and lets the Router area
reference Runtime Core and Reactivity; the matcher/history code keeps its own purity (the assembly
references no DOM adapter, pinned by `RouterAssembly_DoesNotReferenceTheBrowserDomAdapter`). Component
wiring lands on `RouteRecord` itself — `Component` and `PropertiesResolver` — mirroring vue-router's
`RouteRecordRaw.component`/`props`; the matcher never reads them, so its ranking/resolution is
unchanged.

**Depth flows through provide/inject.** Each `RouterView` injects `RouterInjectionKeys.ViewDepth`
(default 0), renders `route.matched[depth].Component`, and provides `depth + 1` for any view nested in
that component — the C# realization of upstream's `viewDepthKey`. Because setup runs
parent-before-child and inject reads the parent chain, a nested `RouterView` mounted inside a matched
component resolves the next depth automatically. (Upstream additionally *skips* component-less records
in the depth walk; Viu renders every matched record's component and treats a component-less record as
a comment placeholder — sufficient for nested layouts, noted as a simplification.)

**The reactive route drives re-render; `shouldUpdateComponent` gates it.** A `RouterView`'s render
reads `Router.CurrentRoute.Value` (a `shallowRef`), so every navigation re-runs every `RouterView`'s
render — exactly as upstream reads `routeToDisplay.value`. The "only the affected view re-renders"
contract is enforced one level down, at the *matched component*: the wrapper re-render produces
`h(component, props)`, and the renderer's `shouldUpdateComponent` compares props by value, so a
leaf-only change (the parent's component and props unchanged) leaves the parent's mounted component
untouched, while a param-only change patches the same component with new props instead of remounting
it. Pinned by run-count tests (`RouterView_LeafOnlyNavigation_ReRendersOnlyTheAffectedView`,
`RouterView_ParameterOnlyNavigation_UpdatesPropsWithoutRemounting`).

**Per-route props: three forms, one resolver.** `RouteComponentProperties.FromParameters()` maps the
resolved params to props (`props: true`), `FromValues(...)` returns a shared static bag (object form),
and a hand-written `RouteComponentPropertiesResolver` receives the whole `RouteLocation` (function
form). The static bag is shared by identity across renders, so a static-props component never
re-renders for a prop change.

**`RouterLink` click guards are DOM-free.** The anchor's `onClick` receives a `RouterLinkClickEvent`
(button, system modifiers, `DefaultPrevented`) — the platform-agnostic stand-in for the `MouseEvent`
upstream's `guardEvent` reads. Navigation is intercepted only for an unmodified, primary-button,
un-prevented click whose link is not `target="_blank"`; anything else falls through to the browser.
Active/exact-active matching mirrors upstream: the link is *active* when its target's leaf record is in
the current route's matched chain (an ancestor-or-self match) with the current params including the
target's, and *exact-active* additionally when that record is the current leaf with equal params.
Active classes are configurable per-link (the `activeClass`/`exactActiveClass` props) and globally
(`Router.LinkActiveClass`/`LinkExactActiveClass`), the prop winning.

## Non-goals (sequenced work)

- Async navigation pipeline and guards (redirect, cancellation), plus `currentLocation` param
  inheritance and route removal — `[V01.01.08.04]`. `Router.Push`/`Replace` navigate synchronously
  today.
- Lazy route components and scroll behavior — `[V01.01.08.05]`.
- Named views (`RouterView name`), the `custom`/slot-only `RouterLink`, and a location-object `to`
  — `RouterView` renders the single default component and `RouterLink` takes a string `to`.
- Redirects, aliases, and per-record `strict`/`sensitive` overrides on `RouteRecord` (only global
  `PathMatchingOptions` today).
