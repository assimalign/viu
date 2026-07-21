# Assimalign.Viu.Browser — design

Why the browser package is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md).
Upstream counterpart: `@vue/runtime-dom`
(https://github.com/vuejs/core/tree/main/packages/runtime-dom).

## The boundary is the budget

Every node-op is (potentially) a marshaled interop call, so the package is organized around
spending as few as possible and keeping each one cheap:

- **Decision logic lives in .NET; JS stays a dumb applier.** The patchProp decision tree
  (`BrowserPropertyPatcher`) resolves class/style/prop-vs-attribute entirely in C# and lands at
  most one leaf call per resolution. This keeps every leaf expressible as an opcode for the
  planned command buffer ([V01.01.04.05]) and makes the tree unit-testable with no DOM
  (recorded leaf delegates).
- **Int handles, not `JSObject` proxies**, identify nodes across the boundary — measured
  decision, see [ADR-0001](ADR-0001-interop-marshaling.md).
- **Value writes are compare-and-set in JS** (`setValueGuarded`): one interop call that skips
  the DOM write when unchanged, so caret and IME state survive re-renders without a read call.
- **The renderer passes the element tag into `patchProp`** (a `PatchPropertyDelegate` parameter)
  because upstream reads `el.tagName` inside patchProp — which would be an interop round-trip
  per patch for a handle-based platform.

## Handle lifecycle (deterministic, two-sided)

The JS registry (`Map<handle, Node>` + per-handle listener maps) is released *deterministically*,
never swept:

- `remove(handle)` detaches the node, walks the removed subtree, releases every registered
  descendant handle and its DOM listeners, and **returns the released handles**; the C# side
  purges its listener delegates for those handles in the same call.
- `setElementText` does the same for replaced children before writing.
- The `?diagnostics=1` mode of the example app runs the mount/unmount stress cycle behind the
  [V01.01.04.01] criterion; 100 cycles of a listener-carrying tree return the registries to
  baseline exactly (verified live 2026-07-17).

## Failure semantics

Bridge failures surface as typed `BrowserDomException`s carrying the operation name and handle.
The JS side throws structured `viu-dom|operation|handle|message` errors for its own guard
failures (unknown handle, bad anchor, unparsable tag); the C# wrapper layer translates those and
any other `JSException` at the op that raised it.

## Knowledge sets instead of `key in el`

Upstream's `shouldSetAsProp` probes the live element (`key in el`). A handle platform cannot
without paying a round-trip, so the patcher uses curated sets for the criteria-named cases
(boolean IDL properties, boolean attributes whose IDL name differs, enumerated attributes) and
falls back to the attribute path for unknown keys — upstream's own fallback. The full per-tag
knowledge tables are [V01.01.01.03] (#5); style-value normalization (camelCase keys, arrays)
is [V01.01.01.02] (#4). Until then style map keys are CSS property names (kebab-case or
`--custom`).

## Shipping the JS half (WasmAppHost constraint)

The bridge module (`src/wwwroot/viu-dom.js`) is owned by this package and loaded by
`BrowserRuntime.InitializeAsync` from `/_content/Assimalign.Viu.Browser/viu-dom.js`. The
canonical RCL (Razor SDK) static-web-asset flow is **not usable here**: measured on .NET SDK
10.0.10, the non-Blazor WebAssembly dev host (WasmAppHost) stops serving the app entirely when
it references a Razor-SDK class library, and its dev serving pattern only maps the app's source
`wwwroot/`. The library therefore stays on the plain SDK and the central build
(`build/Targets/Build.StaticWebAssets.targets`) copies referenced libraries' `wwwroot/**` into a
consuming WASM app's source `wwwroot/_content/<AssemblyName>/` (gitignored) — same URL shape as
the RCL convention, so call sites won't change when NuGet packaging ships the files as real
static web assets later.

## Events: the invoker pattern ([V01.01.04.03])

One JS listener is attached per (element, event, capture); a re-rendered handler is a .NET
delegate swap on the invoker — zero `addEventListener`/`removeEventListener` interop between
renders. Prop-name suffixes (`onClickOnce`/`Capture`/`Passive`, combined) map to listener
options at attach time. The listener applies Vue's attach-timestamp guard JS-side
(`e.timeStamp < attached` ignores events that fired before their patch attached the listener —
zero interop for guarded events) and forwards the complete typed payload as primitives through
the single `[JSExport]` dispatch entry (`BrowserEventDispatch.DispatchBrowserEvent`), which
returns stop/prevent flags the listener applies to the live event. `BrowserEvents.WithModifiers`
and `.WithKeys` port `withModifiers`/`withKeys` (guards run .NET-side over the payload).
Handler exceptions route to the registry's error sink — a debug trace until the app
error-handling pipeline ([V01.01.03.12]) replaces it — and never escape into the JS listener.

### Renderer-agnostic handlers: the object-payload bridge ([V01.01.08.03.01])

A component that renders through the node-ops abstraction rather than the DOM directly — so it also
runs against the in-memory Testing renderer and SSR — attaches an `Action<object?>` handler that
expects a *platform-free* payload, not a `BrowserEvent`. The Router's `RouterLink` is the first: its
`onClick` reads a DOM-free `RouterLinkClickEvent` (button, system modifiers, `DefaultPrevented`), the
stand-in for the `MouseEvent` vue-router's `guardEvent` inspects. The DOM runtime must not know that
payload type (`Assimalign.Viu.Router` is an opt-in package, not a framework member, and must never be
dragged into every app's closure), so the seam is inverted: the invoker registry recognizes the
`Action<object?>` shape and routes it through an ambient `BrowserObjectEvents.Invoker`
(`BrowserObjectEventInvoker`) that a *browser integration layer* installs. That invoker owns the
whole conversion — synthesize the payload from the `BrowserEvent`, call the handler, and apply the
handler's prevent/stop decision back to the `BrowserEvent` so it re-crosses the boundary in the same
single dispatch return. With no invoker installed, dispatching such a handler surfaces a
`NotSupportedException` to the error sink rather than silently dropping the event. This mirrors the
Testing renderer's `Action<object?>` dispatch (`TestEventDispatcher`) — same handler shape, host-
specific payload — and keeps the coupling to `RouterLinkClickEvent` in the dedicated
`Assimalign.Viu.Router.Browser` bridge, never here.

The dispatch payload carries the live event's arrival-time `event.defaultPrevented` (a new
`[JSExport]` field) so the bridge honors `guardEvent`'s already-prevented bail. `BrowserEvent` keeps
that arrival state apart from a handler's own `PreventDefault()` request: a guard sees the combined
state through `DefaultPrevented` (matching the DOM), but only handler-requested prevention re-crosses
the boundary in the response flags — the browser already applied the arrival one.

## Interop command-buffer batching ([V01.01.04.05])

The boundary is the budget, so the batched mode collapses a whole scheduler flush's node-ops into
**one** interop call. There is no upstream Vue counterpart — the prior art is Blazor's `RenderBatch`
— but it is behaviorally invisible: buffered and direct modes produce byte-identical DOM. It is a
construction-time choice (`BrowserRuntime.CreateApp(root, props, useCommandBuffer: true)`); the
renderer and Core see the identical `RendererOptions<int>` either way. Default is direct;
buffered is opt-in for this delivery.

- **Encoder** (`DomCommandBuffer`): every write op — create/insert/remove/text and each `patchProp`
  leaf — encodes as `[opcode][operands]` into one growable, reused `byte[]`. A per-flush string table
  interns repeated tag/prop/event names; a magic + version header byte pair lets the JS applier reject
  a drifted frame loudly. Little-endian is explicit (`BinaryPrimitives` ↔ `DataView` littleEndian).
  Zero per-flush managed allocation at steady state.
- **One-way handles**: a buffered `createElement/Text/Comment` cannot return the JS id, so .NET
  pre-allocates the handle from its own counter and the op carries "create X **AS** handle N"; the JS
  applier registers the node under N. The counter rides each frame header so the JS side keeps its
  own foreign-node allocator (querySelector/parentNode/nextSibling) above it, and a forced-flush read
  folds any foreign handle it returns back into the counter — the two allocators never collide.
- **Reads force a flush**: ops that genuinely return data (`parentNode`, `nextSibling`,
  `querySelector`, `insertStaticContent`) commit the pending batch first (one apply), then read the
  live bridge. Steady-state block-tree updates issue no such reads, so hundreds of mutations collapse
  into exactly one apply — the interop-call-counter acceptance criterion.
- **Released handles**: `remove`/`setElementText` no longer return per op; the applier collects every
  handle it releases while draining the batch and returns them from the single apply call, feeding the
  same `PurgeReleasedHandles` path (this settles ADR-0001's "revisit the released-handle shape with
  the command buffer" consequence).
- **Flush boundary** (`Scheduler.FlushBoundaryCallback`, the one minimal Core seam): the batch
  applies after the render queue drains and *before* post-flush callbacks (so mounted/updated hooks
  read the committed DOM), and again *after* them (so post-flush DOM writes — a `v-show` `updated`
  hook — commit within the same flush). The callback is idempotent, so a render-only flush still
  crosses the boundary once.
- **Span marshaling**: the frame crosses as `[JSMarshalAs<JSType.MemoryView>] Span<byte>` — the JS
  applier reads a typed-array view over WASM memory, not a copied argument — and a static switch over
  the opcode replays each op, reusing the exact `dom.*` leaves the direct path uses.
- **Differential proof**: the full renderer scenario battery runs through direct and buffered modes
  and asserts byte-identical serialized DOM; an instrumented apply counter proves one boundary
  crossing per flush over hundreds of mutations. Full 10k-row benchmarks belong to [V01.01.11.04].
- **Transition sequencing barrier**: opcodes 21/22/23 (`AddTransitionClass`, `RemoveTransitionClass`,
  `ForceReflow`) let the CSS-transition choreography ride the buffer without coalescing away the
  browser's transition trigger ([V01.01.04.07.02]); opcodes 24/25 (`SetMoveTransform`,
  `ClearMoveStyles`) extend the same model to the FLIP move write frame, and add the wire format's first
  `float64` operands ([V01.01.04.07.03]) — see the next two sections.

## Transition class sequencing under batching ([V01.01.04.07.02])

A CSS transition is *sequencing*, not just final state: add `*-enter-from`/`*-enter-active`, force a
reflow, then swap `*-from` → `*-to` on the next frame (upstream `Transition.ts` `forceReflow` +
double-`requestAnimationFrame` `nextFrame`). Naive batching would coalesce a whole flush's writes into
one style recalc — from- and to-classes applied together, the transition never triggered — and, worse,
routing the class writes *direct to the bridge* while the node create/insert stay buffered references a
handle the applier has not registered yet. The buffered `DomTransitionOperations`
(`BufferedBrowserNodeOperations.Activate`) resolves both with two barrier kinds, and preserves
batching everywhere else (one interop crossing per flush is unchanged):

- **Reflow barrier — an in-drain command-buffer op.** `ForceReflow` encodes as opcode 23 (no
  operands). Class writes stay buffered and ordered with the node ops; while draining the *single*
  frame the applier performs a real `document.body.offsetHeight` read at the barrier's position, so a
  leave's `*-leave-from` commits to its own style recalc before `*-leave-active` (upstream #2593). The
  frame still crosses the boundary exactly once — the batching AC holds.
- **Frame boundary — the `nextFrame` continuation.** The `*-to` swap is scheduled through the real
  double-`requestAnimationFrame`; the buffered adaptor applies the continuation's writes when it runs,
  two frames after the from/active frame committed, so the two class states land in distinct browser
  frames and can never coalesce. `whenTransitionEnds`/`measurePositions`/`hasCssTransform`/`whenMoveEnds`
  force a flush and hit the live bridge (so `getComputedStyle`/`getBoundingClientRect` observe the
  committed classes/layout); their resolve callbacks flush the finishing class removals.
- **Proof.** `BufferedTransitionSequencingTests` drives the real renderer + real `<Transition>` through
  the buffered applier and asserts, per flush, that from/active land in one frame, the to-swap in a
  distinct later frame, and the leave reflow op lands *between* the from- and active-class writes.
- FLIP *move* batching (the `<TransitionGroup>` reorder pass) is the next section ([V01.01.04.07.03]);
  persisted `v-show` transitions (the same barriers on show/hide toggling) are the
  [V01.01.04.07.01] section below.

## Transition FLIP move batching ([V01.01.04.07.03])

`<TransitionGroup>` animates keyed reorders with a FLIP (First-Last-Invert-Play): snapshot each child's
position before the patch, read it again after, apply an inverting `transform` to every child that
moved, force one reflow, then add the resolved `*-move` class and clear the transform so each child
animates from its old spot to its new one — cleaning up on `transitionend` (upstream
`TransitionGroup.ts` `recordPosition`/`applyTranslation`/`forceReflow`, gated by the `hasCSSTransform`
clone probe). Upstream reads `getBoundingClientRect` per child inside a same-process JS loop; on a
handle platform every read is a boundary crossing, so a naive port costs N crossings per pass. Two
design choices keep policy in .NET (the "decision logic lives in .NET; JS stays a dumb applier" split)
while collapsing the FLIP to a constant number of crossings:

- **Batched read pass — one crossing per snapshot.** `DomTransitionOperations.MeasurePositions` takes an
  array of handles and returns the flat `[left, top, …]` rectangles in a single crossing (the
  `dom.measurePositions` read op, mirroring the history bridge's `readSnapshot` flat-primitives pattern).
  Which children moved and their inverting deltas are computed .NET-side; JS only reads rectangles. A
  reorder of N children costs exactly two read crossings — the pre-patch snapshot and the post-patch
  read — regardless of N. In buffered mode the read forces the pending patch to commit first, so
  `getBoundingClientRect` observes the settled post-reorder layout, then reads all N in one call.
- **Batched write frame — one crossing for the move.** The FLIP transform writes join the command
  buffer: `SetMoveTransform` (opcode 24, carrying `float64` deltas) and `ClearMoveStyles` (opcode 25)
  sit alongside `AddTransitionClass` (21) and the `ForceReflow` barrier (23). `TransitionGroup` emits
  the whole write pass — every child's transform, then the reflow barrier, then each child's move class
  and transform clear — before registering any `whenMoveEnds` listener, so the buffered adaptor commits
  the entire frame in one crossing in exact upstream order. (`whenMoveEnds` stays a direct per-element
  `transitionend` registration, like `addEventListener`; it is a listener attach, not a read, and its
  resolve flushes the finishing move-class removal.) An interrupted reorder force-finishes the in-flight
  move first (upstream `callPendingCbs`) so the re-measure sees settled positions.
- **Wire version.** These opcodes and the `float64` operand bumped the frame version `0x02` → `0x03`
  on both `DomCommandBuffer.cs` and `viu-dom.js`; the test-side `CommandBufferDecoder` (the DOM-free
  oracle for the JS applier) decodes them identically.
- **Proof.** `TransitionGroupTests` pins, through the recording adapter, the move class + inverting
  transform applied only to moved children and removed on `transitionend`, the `hasCSSTransform` gate,
  an interrupted reorder converging with no residue, cleanup leaving none — and, run-count-pinned, that
  each position pass is exactly one batched read crossing carrying the full child batch regardless of
  child count. `DomCommandBufferTests` round-trips the FLIP opcodes through the buffer and decoder,
  asserting the `float64` deltas survive and the transform/reflow/class/clear order is preserved.

## Persisted v-show transitions ([V01.01.04.07.01])

A `<Transition>` wrapping a `v-show` element must run the enter/leave choreography on the binding
*toggling* — the element stays mounted and only its visibility changes — where a `<Transition>`
around a `v-if` runs it on mount/remove. Upstream calls this *persisted* mode (`Transition.ts`
`persisted` + `directives/vShow.ts`). The pivotal upstream insight is that the `persisted` flag
changes **who** calls the transition hooks, not the hook contract: the renderer's mount/insert and
remove paths skip a persisted transition (`needTransition` = `transition && !transition.persisted`),
and the `v-show` directive drives `beforeEnter`/`enter`/`leave` itself from its `beforeMount`/
`mounted`/`updated` hooks. `BaseTransition`'s state machine (`Assimalign.Viu.Core`) is
untouched — the enter/leave cancellation that converges an interrupted toggle already lives on the
shared `TransitionState`, keyed by the once-boxed `node.El` identity that survives every re-render
(`next.El = current.El`), so the directive just has to call the same hooks with that element.

- **The seam.** `v-show` reads `node.Transition` (the hooks `BaseTransition` stamped on the vnode) and
  keys on the resolved `Persisted` flag — the exact complement of the renderer's persisted skip
  (`VirtualNode.Transition is { Persisted: false }`). When persisted: `beforeEnter` (then
  `setDisplay(true)`, then `enter`) reveals on show; `leave(el, () => setDisplay(false))` hides on
  hide — the element is hidden only once the leave *completes*, in the leave's `done` callback. This
  is Viu's explicit form of upstream's coupling, which relies on the compiler injecting `persisted`
  whenever a `<Transition>` wraps a `v-show` child (the `transformTransition` compiler-dom transform —
  a separate `Assimalign.Viu.Syntax.Templates` follow-up; the runtime honors a `persisted` flag from
  any source). Keying on `Persisted` rather than upstream's raw `transition`-truthiness means the
  directive drives the hooks *exactly* when the renderer does not, so the two never both fire even
  without that compiler guarantee.
- **Original-display lifecycle.** The element's original inline `display` is captured **once**, in
  `beforeMount`, into `BrowserModelState.OriginalDisplay` (derived from the vnode `style` prop, not an
  interop read — upstream `vShowOriginalDisplay`). Every later reveal restores it (an empty original
  removes the inline `display` so a stylesheet value wins); every hide sets `display:none` after the
  leave. The saved value is never overwritten by a toggle, so it survives arbitrarily many cycles. An
  interrupted toggle converges to the final visibility with the saved display intact: a show
  interrupting a leave cancels the leave (its `done` writes a transient `display:none`) and then the
  reveal immediately overwrites it visible; a hide interrupting an enter cancels the enter (no display
  write) and the leave's completion hides it.
- **Buffered mode.** The persisted path needs no new command-buffer machinery: the enter/leave class
  ops ride the same opcodes 21/22/23 with the same reflow + `nextFrame` barriers as a `v-if`
  transition, and the `setDisplay` writes ride the buffered `SetStyleProperty`/`RemoveStyleProperty`
  leaves. Because the `v-show` `updated` hook is a post-flush callback, the flush boundary applies the
  reveal's `beforeEnter` classes and its display write together in one crossing, and the `nextFrame`
  continuation commits the `*-to` swap in a distinct later frame — the from/active-vs-to barrier holds,
  and the leave's reflow still lands between `*-leave-from` and `*-leave-active` in a single frame.
- **Proof.** `VShowTransitionTests` drives the real renderer + real `<Transition>` + real `v-show`
  through a merged recording adapter (class choreography + display toggles) and pins, run-count-exact,
  the enter/leave class sequence on toggle without unmounting, the capture-once/restore-across-cycles
  display lifecycle, and both interruption directions converging with the right visibility and no
  orphaned classes. `BufferedTransitionSequencingTests` extends the batched-mode battery with the
  persisted show/hide path, asserting the same one-frame from/active, distinct-frame to-swap, and
  leave reflow barrier — and that the element stays mounted (display:none, never host-removed).

## TransitionGroup attribute fallthrough ([V01.01.04.07.04])

`<TransitionGroup tag="ul" class="list">` must land `class`/`style`/arbitrary attributes on the
rendered `<ul>` wrapper, exactly as any single-root component inherits its non-prop attrs. Upstream
`TransitionGroup.ts` does nothing special for this: it returns `createVNode(tag, null, children)` and
the standard `renderComponentRoot` + `mergeProps` fallthrough
(`packages/runtime-core/src/componentAttrs.ts`) merges the instance's fallthrough attrs onto that root.
The initial DOM port instead set `inheritAttrs: false` and read `tag`/`moveClass`/the transition props
straight off the raw vnode — a blunt switch that killed *all* fallthrough, including the class/style a
consumer puts on the group.

The fix is to participate in the same standard mechanism rather than hand-copy attributes:

- **Declare the props, drop the override.** `TransitionGroup.Properties` now declares the full upstream
  set — `tag`, `moveClass`, and `TransitionPropsValidators` (`BaseTransitionPropsValidators` +
  `DOMTransitionPropsValidators`). `ComponentPropertyResolution` routes every declared name into
  `instance.Properties` and leaves only the undeclared attributes (`class`/`style`/`id`/`data-*`/…) in
  `instance.Attributes`. With `InheritAttributes` back at its default (`true`, unlike `Transition`/
  `KeepAlive`, because the group owns a real root element), `renderComponentRoot` clones the wrapper
  vnode with `mergeProps(root.props, attrs)` — the identical path RouterLink and every ordinary
  component use. The component still *reads* `tag`/`moveClass`/`name`/hooks from the raw vnode
  (`ResolveTransitionProperties` is unchanged); declaring props only redirects those names away from the
  fallthrough set, so nothing about the FLIP or class choreography moves.
- **Two class channels, no cross-contamination.** The wrapper's fallthrough `class` travels the element
  prop channel (`patchProp` on the tag), while the children's `*-enter`/`*-leave`/`*-move` classes
  travel the transition-class channel (`AddTransitionClass` on each child element). They target
  different elements and different code paths, so the wrapper never picks up a move class and the
  children never pick up the wrapper's class.
- **Fragment mode is target-less and silent.** With no `tag` the root is a fragment, and
  `renderComponentRoot` only inherits onto an element root, so the undeclared attrs are dropped. Viu
  emits no "extraneous attributes" warning for this (a deliberate simplification, pinned by test).
- **Proof.** `TransitionGroupTests` pins class/style/arbitrary fallthrough onto the `tag` element with
  the declared `tag`/`name` consumed (not leaked), fragment mode dropping the attrs with no warning
  (captured `RuntimeWarnings` sink), the wrapper/children class channels staying separate across a FLIP
  reorder, and a reactive fallthrough-attr change patching the same wrapper element in place.

## Hydration reads: one batched snapshot per root ([V01.01.07.03])

Client hydration (the walker lives in `Assimalign.Viu.Core`) reads the existing server DOM to
decide what to adopt: node kind, tag, text/comment data, first-child/next-sibling structure, and a few
attributes. Answering each of those with a `JSImport` call per node would make hydration the chattiest
path in the framework — the opposite of "the boundary is the budget". So the browser answers them the
same way it answers a FLIP measure or a history read: **one crossing that returns a flat snapshot**.

- **`dom.snapshotHydration(container)`** walks the container's subtree once (`createTreeWalker`),
  registers a bridge handle for every node (so an adopted node's `int` flows straight into the write-side
  `patchProp`/`insert`/`remove`), and returns a compact serialization — per node: `handle parent firstChild
  nextSibling kind`, then `tag attrCount [name value]*` for an element or `data` for text/comment. Strings
  are length-prefixed (`<len>:<chars>`) so arbitrary content needs no escaping; the length is a UTF-16
  code-unit count, matching `.NET` `string.Length`.
- **`BrowserHydrationReader`** parses that once into a handle→node map and answers every
  `HydrationNodeReader` read locally — **zero** further crossings for the whole walk. A teleport target
  lies outside the root's subtree, so it takes one additional snapshot (teleports are rare); `registerNode`
  dedups, so an overlapping re-snapshot is harmless.
- **Buffered mode** treats the snapshot as a read: it commits any pending command-buffer frame first (so
  the walk sees committed DOM), then snapshots — the same "reads force a flush" rule `parentNode`/
  `nextSibling`/`querySelector` follow. `CreateSSRApp` uses the direct path; the buffered wrapper carries a
  snapshot source too so a buffered renderer is not left mount-only.
- **`BrowserRuntime.CreateSsrApp(...).Mount(...)`** does **not** clear the container (unlike a client
  `CreateApp` mount) — the server content is precisely what hydration reuses.

Over-registration is deliberate and bounded: a text node deep inside an adopted element gets a handle even
though no vnode points at it, but the whole subtree's handles are released together when the element is
removed (`releaseSubtree`), so a mount/unmount cycle still returns the registries to baseline.

## Non-goals (sequenced work)

- App bootstrap (`CreateApp`-equivalent, container clearing) — [V01.01.04.04] (#42).
- `v-model` runtime — [V01.01.04.06].
- Compiler `persisted` injection (`transformTransition`: mark a `<Transition>` persisted when its
  single child carries `v-show`) — a `Assimalign.Viu.Syntax.Templates` follow-up; the runtime already
  honors the `persisted` flag ([V01.01.04.07.01]).
