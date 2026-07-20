# Assimalign.Viu.RuntimeDom — design

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
`BrowserRuntime.InitializeAsync` from `/_content/Assimalign.Viu.RuntimeDom/viu-dom.js`. The
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

## Interop command-buffer batching ([V01.01.04.05])

The boundary is the budget, so the batched mode collapses a whole scheduler flush's node-ops into
**one** interop call. There is no upstream Vue counterpart — the prior art is Blazor's `RenderBatch`
— but it is behaviorally invisible: buffered and direct modes produce byte-identical DOM. It is a
construction-time choice (`BrowserRuntime.CreateApp(root, props, useCommandBuffer: true)`); the
renderer and RuntimeCore see the identical `RendererOptions<int>` either way. Default is direct;
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
- **Flush boundary** (`Scheduler.FlushBoundaryCallback`, the one minimal RuntimeCore seam): the batch
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
  `v-show` transition coalescing is #161.

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

## Non-goals (sequenced work)

- App bootstrap (`CreateApp`-equivalent, container clearing) — [V01.01.04.04] (#42).
- `v-model` runtime — [V01.01.04.06].
