# Assimalign.Viu.Router — design

Why the matcher and the history layer are shaped the way they are. What they are: see
[OVERVIEW.md](OVERVIEW.md). The normative statement of routing behavior is
[§12 of the Viu Specification](../../../docs/SPECIFICATION.md#12-routing).

## The matcher is pure

The matcher and memory history reference no other Viu library and no interop assembly, so the whole
table-build/resolve path runs in a plain .NET test host with no DOM — the property the router epic
(#69) calls out, and the one that makes route resolution cheap to test exhaustively. The
`RouterView`/`RouterLink` components (`[V01.01.08.03]`) sit *on top of* this in the same assembly and
do reference the runtime (see "Components: depth, reactivity, and click guards"), but never the
browser DOM adapter — so the components stay renderer-agnostic and the matcher/history code stays
framework-free. Specified by `[RTR-1]` and `[RTR-7]`.

The matching pipeline has three stages, each pinned by its own tests:

1. **Tokenizer** (`PathTokenizer`): a character-by-character state machine, including the
   "a repeatable parameter must be alone in its segment" guard (so `/user-:id+` is legal but
   `/:a:b:c+` is not), escaped-`)` handling inside custom patterns, and the special cases where an
   empty path yields one empty segment and `/` yields one empty-value `Static` token.
2. **Compiler + ranker** (`PathParserFactory`, `PathScore` / `PathParserScoreComparer`): the
   regular-expression string, the per-segment score arrays, and the two-level comparison — including
   the fractional strict/case-sensitive bonuses (0.7, 0.25) that must stay below 1 so they only break
   ties. The weight scale is frozen: changing one weight silently reorders existing route tables.
3. **Resolution + insertion** (`RouteMatcher`): the score-sorted matcher list, the binary-search
   insertion with the equal-score ancestor rule, path- and name-based resolve, and the
   parent-to-child matched chain.

## Ranking: specificity, never table order

The matcher list is kept sorted by descending score, and `Resolve(path)` returns the first (most
specific) matcher whose pattern matches. Because the score comes from the pattern's shape — static
segments (`+Static`) outweigh dynamic (`+Dynamic`), a custom pattern earns `+BonusCustomPattern`,
and the catch-all `(.*)` takes the large `BonusWildcard` penalty that also cancels its custom-pattern
bonus — the order routes were registered never changes the winner. The ranking tests register the
least-specific route first on purpose to prove it.

The one subtle case is the **empty-path default child**. A child with `path: ""` compiles to the
same full path (and therefore the same score) as its parent, so the insertion search places it
*ahead of* the equally scored parent. Navigating to the parent path then resolves the child and
yields the two-entry `[parent, child]` matched chain — the layout-with-default-view pattern.

## Matcher scope decisions

- **Every record is currently matchable.** There is no gate that makes a record participate in the
  insertion-ancestor rule only when it has a name, a component, or a redirect. Every criterion here
  is met with all records matchable, and the empty-path-child ordering depends on it. If redirects
  and aliases later need such a gate, `RouteMatcher.IsMatchable` is the single place to introduce it.
- **No parameter inheritance from the current location.** Named resolution interpolates exactly the
  parameters passed in (projected to the route's declared keys) and raises
  `MissingRequiredParameter` otherwise. The navigation pipeline (`[V01.01.08.04]`) does own a current
  route, but relative named navigation that inherits its parameters is deliberately not wired:
  implicit inheritance makes a resolution depend on where the user happened to be.
- **Path only.** The matcher resolves the path portion. Query strings and fragments are normalized
  and merged at the router level, not here.
- **Typed accessors instead of an untyped value union at the edge.** Internally a repeatable value
  stays an ordered `string[]` and a single value stays a `string`, including the case where an
  unmatched optional-repeatable parameter parses to an empty *string* rather than an empty array.
  Consumers read through `GetString` / `GetInteger` / `GetStrings`, which parse on demand — no boxed
  `object`, no reflection over a dictionary. `GetInteger` on a non-integer raises `FormatException`
  and a missing key raises `KeyNotFoundException` (idiomatic .NET), while route-definition and
  interpolation failures raise the typed `RouteMatcherException`. Specified by `[RTR-2]`.

## AOT / trimming: no runtime codegen

WASM has no `new Function`/`eval` and reflection-emit is off the table, so nothing here compiles code
at runtime:

- **Path patterns compile to interpreted `Regex`.** A route table is runtime data, so each pattern
  string is assembled at table-build time and handed to `new Regex(...)` with the interpreted engine
  (never `RegexOptions.Compiled`, which relies on reflection emit). The interpreted engine is fully
  trimming- and NativeAOT-safe.
- **The one compile-time-constant pattern uses `[GeneratedRegex]`.** The metacharacter-escaping
  pattern for literal path text is known at build time, so `RegularExpressionPatterns` emits it
  through the Roslyn regex source generator — the AOT-preferred path — rather than constructing it
  reflectively. The valid-parameter-name check is a direct character test, needing no regular
  expression at all.
- **Custom parameter patterns must use non-capturing groups** (`(?:…)`): the compiler wraps each
  parameter in a single capturing group and maps capturing groups to keys positionally, so a
  capturing group inside a user pattern would shift that mapping. Invalid custom patterns are
  validated at table-build time and raise `InvalidCustomPattern`.
- **`.NET` end-anchor nuance.** `.NET`'s `$` also matches immediately before a trailing newline.
  Route paths never contain newlines, so this is inert; noted here so a future reader does not
  mistake it for a bug.

## Value semantics for cheap comparison

The navigation pipeline and reactivity layer need to compare and snapshot locations cheaply, so
`RouteLocation` and `RouteParameters` implement value equality (locations compare their matched
records by identity; parameters compare structurally and hash order-independently). `RouteRecord`
keeps reference identity on purpose — the matched chain points at the exact record instances the
consumer supplied.

## History: the policy is split from the interop edge

The web and hash histories (`[V01.01.08.02]`) are split in two so the browser one is testable
without a browser:

- **`BrowserRouterHistory` is the policy** — base prepend/strip, the push/replace/`popstate` state
  machine, listener bookkeeping — and touches no DOM. Every environment effect is delegated to an
  injected **`IBrowserHistoryInterop`**. It is the same seam shape `Assimalign.Viu.Browser`'s
  `BrowserEventInvokerRegistry` uses — bridge calls as delegates, so the policy is unit-testable with
  recorded doubles. A `FakeBrowserHistoryInterop` records every crossing and can simulate a
  `popstate`, so base handling, the state round-trip, delta/direction, listener teardown, and the
  interop-call count are all pinned on a plain .NET host.
- **`JavaScriptBrowserHistoryInterop` is the thin edge** — `[JSImport]` bindings to
  `wwwroot/viu-history.js` — and does nothing but flatten the policy's URLs and states into
  primitive interop calls. The JS module is a dumb applier: the only decision it makes is reading the
  live `window.scrollX/Y` for the leaving entry (the one piece of state the DOM owns).
- **`DeferredBrowserRouterHistory` is the asynchronous composition seam.** `CreateWeb` and
  `CreateWebHash` return it without importing JavaScript. It accepts `Listen` before readiness so the
  `Router` constructor stays synchronous, then `Router.ReadyAsync` loads the bridge and constructs
  the ordinary `BrowserRouterHistory` policy. Until that completes, every other synchronous history
  member throws one actionable readiness exception; `Destroy` remains safe for startup teardown.

Hash mode is not a second implementation: after lazy bridge initialization,
`RouterHistory.CreateWebHash` computes a `#`-carrying base and hands it to the same web policy
(`RouterHistory.ResolveHashBase` → `BrowserRouterHistory`), so there is one state machine to reason
about.

## History state: one position counter, computed in C#

`RouterHistoryState` is a flat, primitives-only payload. The
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

## History: scope decisions

- **Memory carries a full state per entry.** Every memory entry stores a real `RouterHistoryState`
  rather than an empty placeholder, so the position counter round-trips and memory is a genuine
  reference model for the web state semantics — the `[V01.01.08.02]` requirement that memory
  reproduce the same push/replace/go behavior. Memory still skips the browser's
  amend-the-leaving-entry step, so a memory entry's `Forward` link is not back-patched.
- **Root-relative write URLs.** The browser edge writes `base + location` (web) or the `#…` slice
  (hash) rather than an absolute `protocol + host + base + location`. `pushState` resolves the
  root-relative form identically and it keeps the write path DOM-free (no protocol/host read). The
  `<base>`-element write-time special case (`document.querySelector('base')`) is deliberately not
  reproduced; the simplification is noted at the call site.
- **Scroll is the one JS-owned field.** The leaving entry's scroll anchor is read from the live window
  by the interop at apply time (the policy cannot read the DOM); every other field is C#-computed, and
  memory — having no DOM — leaves scroll `null`. Scroll *restoration* (consuming the anchors) is
  `[V01.01.08.05]`.

## Components: depth, reactivity, and click guards

`RouterView`/`RouterLink` (`[V01.01.08.03]`) are
ordinary `IComponentTemplate`s in this assembly. Router references the standalone Components and
Reactivity contracts, but not Core or a host renderer; the matcher/history code keeps its own purity
(the assembly references no DOM adapter, pinned by
`RouterAssembly_DoesNotReferenceTheBrowserDomAdapter`). Component wiring lands on `RouteRecord`
itself — `Component` and `ArgumentsResolver`; the matcher never reads them, so its
ranking/resolution is unchanged.

**Depth is explicit.** Viu deliberately has no hierarchical component dependency API (`[CMP-24]`), so
`RouterView` declares a `depth` component argument (default `0`) and renders
`route.matched[depth].Component`. A layout that contains another outlet supplies the next depth
explicitly, for example `ComponentTree.Template<RouterView>(argumentsWithDepthOne)`. An implicit
depth would require exactly the ancestor-lookup mechanism Viu rejected; passing it keeps component
dependencies visible and avoids recreating an injection mechanism under a router-specific name.
Viu renders every matched record's component and treats a component-less record as a comment
placeholder. Specified by `[RTR-4]`.

**The reactive route drives re-render and template identity preserves instances.** A `RouterView`'s render
reads `Router.CurrentRoute.Value` (a shallow reference), so every navigation re-runs every
`RouterView`'s render. The matched value is an `IComponent` in
the unified tree. When it is an `ITemplateComponent`, RouterView copies the request only when route
arguments must be merged. Core compares the template type/name and key, so a parameter-only
navigation patches the existing mounted template rather than remounting it, and a leaf-only
navigation preserves the parent layout instance. RouterView combines the matched `RouteRecord`
identity with the stored request key. Different records that point at the same template therefore
remount it, ensuring lifecycle-bound leave/update guard ownership moves to the new record; parameter
changes on the same record retain the instance.

**Per-route arguments: three forms, one resolver.** `RouteComponentArguments.FromParameters()` maps
the resolved route parameters to same-named component arguments, `FromValues(...)` returns a shared
fixed argument snapshot, and a hand-written `RouteComponentArgumentsResolver` receives the whole
`RouteLocation`. Resolved route arguments override same-named arguments already present on the stored
template request.

**`RouterLink` click guards are DOM-free.** The anchor's `onClick` receives a `RouterLinkClickEvent`
(button, system modifiers, `DefaultPrevented`) — a platform-agnostic carrier the host's event bridge
fills from the native `MouseEvent`. Navigation is intercepted only for an unmodified, primary-button,
un-prevented click whose link is not `target="_blank"`; anything else falls through to the browser,
so open-in-new-tab and the context menu keep working.
The matching rule: the link is *active* when its target's leaf record is in
the current route's matched chain (an ancestor-or-self match) with the current parameters including
the target's, and *exact-active* additionally when that record is the current leaf with equal
parameters.
Active classes are configurable per-link (the `activeClass`/`exactActiveClass` arguments) and
globally (`Router.LinkActiveClass`/`LinkExactActiveClass`), the per-link argument winning. Both
components resolve `Router` only through `IComponentContext.Services`; the application owns the
provider and Router adds no container or builder-registration abstraction.

## Navigation guards: the async pipeline

`Router.Push`/`Replace` run the guard phases in a fixed order — in-component before-leave (deepest
child first) → global `BeforeEach` → reused-record before-update → per-record `BeforeEnter` →
(async component resolution, a no-op seam until `[V01.01.08.05]`) → in-component before-enter →
global `BeforeResolve` → confirm → `AfterEach` — pinned by an ordering test that mounts a real view
tree and records every hook.

- **Guards decide by return value, never by a continuation.** A `NavigationGuard` returns
  `Task<NavigationGuardResult>` (`Allow`/`Abort`/redirect). An exhaustive result type lets the
  compiler check that every path decides and lets the pipeline guarantee a guard decides exactly
  once, where a callback form permits both "never called" and "called twice"; it also maps cleanly
  onto C# `Task`. A guard's own long-running work can observe the threaded `CancellationToken`.
  Specified by `[RTR-5]`.
- **Changing-record classification is by identity.** Leaving = in `from` not `to`, updating = in
  both, entering = in `to` not `from`, comparing `RouteRecord`s by reference —
  the same identity semantics the matched chain already relies on. Leaving guards run deepest-child
  first (the reversed `from.matched`).
- **Supersession is cooperative cancellation.** Each navigation opens a `CancellationTokenSource`;
  starting a newer one cancels the previous. The pipeline re-checks the token at the head of every
  phase, after each guard, and once more before finalize, so a superseded chain runs no further guards
  and reports a `Cancelled` failure — it never mutates router state after being superseded (the
  interleaving tests pin this with a gated guard). On the single-threaded event loop there are no
  locks; the only ordering that matters is "no state write after the token is cancelled", enforced by
  those checkpoints and by finalize being synchronous (no `await` between the final check and the
  commit).
- **Redirects re-enter the pipeline.** A redirect result resolves its target through the matcher and
  recurses into `PushWithRedirect` (carrying `redirectedFrom`), so the redirected navigation runs
  every guard again from the top. `AfterEach` fires only for the final confirmed navigation, not the
  intermediate redirected one. A fixed redirect-depth cap throws `NavigationRedirectException` in
  every configuration, not only in development builds: an unbounded chain fails as a stack overflow
  in production, which is far harder to diagnose than a typed exception.
- **Confirm order: state first, DOM later.** `finalizeNavigation` writes history (for an application
  push/replace) and then sets `CurrentRoute` — a single shallow-reference trigger that *queues* the
  render flush without running it. `AfterEach` runs synchronously immediately after, so it observes
  the committed route while the DOM still shows the previous one. Pinned by tests that assert the
  mounted view's post-flush `mounted` hook lands after `AfterEach`.
- **Failures are returned; exceptions fault.** Abort/cancel/duplicate complete as a
  `NavigationFailure` value, so `Push` never throws for an outcome a correct application produces,
  while an unexpected guard exception (or the redirect-loop cap) is routed to the `OnError` handlers
  and faults the returned task. `RouterLink` observes its fire-and-forget navigation so a fault never
  strands unobserved. Specified by `[RTR-6]`.

### The initial navigation runs the pipeline from the start sentinel

`CurrentRoute` begins at `RouteLocation.Start`: path `/`, no name, no parameters, and an **empty
matched chain**. The
constructor deliberately does *not* eagerly resolve `history.Location` into the current route, because
that pre-resolution is exactly what made the first `Push` to the already-resolved entry URL a
`Duplicated` no-op that skipped the guard pipeline — so a global `beforeEach` redirect for the entry
URL (the classic `{ path: '/', redirect: '/x' }`) never fired for a page loaded directly at that URL
(`[V01.01.08.07]`, #219).

- **Distinguishing the initial pass from an in-session duplicate.** The same-location dedup is gated
  on `from.Matched.Count > 0`. The sentinel has an empty matched chain, so the initial navigation is
  never deduplicated and always runs the full pipeline; every in-session navigation starts from a
  matched route, so same-location pushes still short-circuit to `Duplicated`. The sentinel is
  value-equal to an *unmatched* `/` resolution, so this count gate — not value equality — is what
  keeps them apart.
- **`ReadyAsync` initializes, triggers, and awaits the first navigation.** Viu has no router-install
  hook, so one idempotent method loads a deferred browser-history bridge when needed, starts the
  initial navigation, and awaits it. The first call's cancellation token covers both initialization
  and every guard; that call memoizes the resulting task, so later calls return the same task and
  cannot replace its token. The navigation uses the current history location and the full pipeline
  with `from` = the sentinel (so the leave phase is trivially empty and every enter/global guard fires
  once). A bootstrap awaits it before mounting so the first render already reflects the resolved (or
  redirected) route. It always settles after initialization: an aborted or cancelled initial
  navigation completes with its typed failure rather than hanging.
- **The first confirm replaces, never pushes.** `finalizeNavigation` forces a replace when `from` is
  the start sentinel (by `ReferenceEquals`), so the application's entry URL is not left as a stale
  back-target; through an initial redirect the reference stays the sentinel across the whole chain
  because nothing is committed until the final confirm.
- **RouterView is empty at the sentinel.** With an empty matched chain, every `RouterView` resolves no
  record at its depth and renders nothing until the initial navigation confirms.
- **No compensating `go` for the initial resolution.** The initial navigation runs through the
  application push path (`ReadyAsync` → `Navigate` → `PushWithRedirect`), never the popstate listener,
  so the pop path's compensating `history.go` (below) cannot fire for it — an aborted initial
  navigation simply leaves `CurrentRoute` at the sentinel and history untouched.

### In-component guards hook the component lifecycle, never reflection

The in-component before-leave and before-update guards need per-instance state, so they are
**registration-based** (`RouterGuards.OnBeforeRouteLeave`/`OnBeforeRouteUpdate`). A route template
passes its `IComponentContext` and explicit
outlet depth. The helper resolves `Router` from `context.Services`, selects the current matched record
at that depth, registers the guard in a `RouteRecord`-keyed side-table, and binds removal to
`context.Lifecycle.OnUnmounted`. There is no ambient component accessor, hierarchical lookup, or
reflection over user types, so a trimmer cannot strip a guard.

The in-component before-enter guard has no instance (the component is not yet mounted), so it is
**interface-based**: an `IRouteEnterGuard` is supplied explicitly through
`RouteRecord.RouteEnterGuard`. That lets the guard run before the route is confirmed without
reflection over user types and without activating a component factory early. There is no
post-activation instance callback either — a guard decides by return value, so it cannot defer work
until the component exists.

### popstate runs the same pipeline

Browser back/forward (and memory `Go`) drive the identical guard pipeline through the history
listener. Because the URL has already moved when the listener fires, a failure restores it with a
compensating `history.go(-delta, triggerListeners: false)`, and the
confirm step for a pop only updates `CurrentRoute` (no history write, since the entry already exists).
A redirect during a pop restores the popped URL and then re-navigates the redirect target as a push.
All of this is exercised DOM-free through memory history, whose `Go` reproduces the same
listener/delta semantics as the browser edge.

## Non-goals (sequenced work)

- `currentLocation` param inheritance for relative named navigation and route removal — deferred from
  `[V01.01.08.04]` (the guarded async pipeline itself landed; see "Navigation guards" below).
- Lazy route components and scroll behavior — `[V01.01.08.05]`. Route components resolve eagerly, so
  the pipeline's async-component-resolution stage is currently a documented no-op seam.
- Named views, a slot-only `RouterLink` that hands the resolved href to the caller instead of
  emitting an anchor, and a location-object `to` — `RouterView` renders the single default component
  and `RouterLink` takes a string `to`.
- Redirects, aliases, and per-record `strict`/`sensitive` overrides on `RouteRecord` (only global
  `PathMatchingOptions` today).
