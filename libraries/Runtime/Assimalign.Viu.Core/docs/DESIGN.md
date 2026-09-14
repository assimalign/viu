# Assimalign.Viu.Core design

## Runtime role

Core is the host-neutral mounted runtime and application model. Components owns immutable
descriptions and authored contracts; Core resolves registrations, activates authored behavior,
creates mounted instances, schedules reactive renders, executes structural built-ins, and
translates tree changes into host operations.

The public Core surface is rooted at `Assimalign.Viu`, because these runtime primitives form the
framework's central execution vocabulary. Mounted nodes, activations, built-in state, and the live
`RuntimeComponentContext` stay internal.

## Generic renderer

`Renderer<TNode>` owns one mounted tree and dispatches the ten closed `VirtualNodeKind` variants. It
never writes host state into a `VirtualNode`. Stable mounted identities hold host handles, ranges,
parent links, effects, previous descriptions, and built-in execution state. Every render position
has its own mounted identity even when positions share an immutable description; optimized blocks
retain an ordered mounted occurrence list rather than selecting a representative by description
identity. When a tracked description also occurs in untracked positions, identity cannot select the
tracked subset and the renderer falls back to the full structural diff (`[CMP-1]` through `[CMP-3]`,
`[RND-1]` through `[RND-4]`).

Patch behavior is selected from explicit compiler data. Block-local children constrain visits;
`PatchFlags` choose narrow attribute or text paths; `Cached` reuses a whole value; `Bail` forces full
diff. Absent optimization data always falls back to the correct generic path
(`[RND-BLOCK-1]` through `[RND-BLOCK-7]`, `[RND-FLAGS-1]` through `[RND-FLAGS-6]`). Keyed children
preserve retained identities and minimize moves while enforcing the key contract (`[RND-KEY-1]`
through `[RND-KEY-6]`).

`RendererOptions<TNode>` is the entire host contract. Required operations create, place, remove,
navigate, and patch host nodes; optional operations expose teleport targets, commit batching, static
insertion, and hydration. Missing optional capabilities fail explicitly. Core contains no host
handles, namespace policy, browser object, or interop (`[RND-HOST-1]` through `[RND-HOST-4]`).

## Component activation and lifetime

Core resolves a `ComponentNode` through the borrowed `IComponentFactory`, invokes its activator,
runs synchronous setup inside a new reactive scope, and retains one `ComponentRenderFrame` for that
activation. The frame uses the registration contract's cache size; the compatibility fallback is
only for registrations that do not declare it.

`RuntimeComponentContext` is the live implementation of `ComponentContext`. It owns per-mount
defaults, warning suppression, once-listener state, lifecycle callbacks, component watches, exposed
values, error propagation, and the active Suspense boundary. It attaches integrations only through
the public seams in `[CMP-33]`.

Core owns activated `IComponent` instances and disposes them on setup failure or unmount. It borrows
the factory, nullable services, state registry, directive resolver, and diagnostics from the caller's
composition root and never disposes them (`[CMP-9]` through `[CMP-11]`).

`ComponentHost.RenderAsync` reuses the same activation path for one-shot hosts. Its returned scope
keeps the parent context and reactive lifetime alive while a server consumes the tree; disposal
aborts that lifetime without client mount hooks. `ComponentHost.ExecuteAsync` uses the same path but
invokes one host operation with only the public component instance, render frame, and render scope.
Core retains activation, error routing, and disposal, and returns a named success or handled-failure
outcome so direct-render output can remain transactional (`[SSR-4]`, `[SSR-5]`, `[SSR-10]`,
`[SSR-TARGET-3]`).

## Scheduling and application lifetime

Each application snapshots composition into an immutable `IApplicationContext`.
`ApplicationLifetime` owns Created, Starting, Running, Stopping, Stopped, and Failed transitions;
middleware wraps the entire live interval and unwinds in reverse order. Cancellation requests
shutdown, while failures cancel before reaching the one-shot terminal handler (`[APP-1]` through
`[APP-7]`).

The scheduler deduplicates jobs, orders component work by mounted identity, separates pre-flush and
post-flush watches, commits hosts at defined boundaries, and restores requeueable state after failure
(`[SCH-1]` through `[SCH-12]`). Scheduling policy belongs to Core; host batching crosses only the
per-renderer `Commit` delegate.

The default Browser path retains one shared event-loop state. Request hosts call the public
`RuntimeExecution.EnterExecutionFlow` boundary, which composes the corresponding public Reactivity
and State flow boundaries, selects fresh Core and scheduler bookkeeping, and restores the caller's
state afterward. Scheduler continuations capture that logical execution context even when the
thread pool dispatches them; a per-flow gate also serializes that continuation with the
synchronous-render flush. This permits distinct request-owned graphs to render concurrently and
prevents Core from racing its own scheduler, without making any individual graph thread-safe
(`[EXE-1]`, `[EXE-3]`).

## Structural built-ins and hydration

Teleport, KeepAlive, Suspense, and Transition are internal executors for their Components-owned
nodes. Their public descriptions remain immutable and lazy; Core owns retained branches, dependency
accounting, reveal, movement, and teardown. Host-specific transition effects stay behind the public
host contract.

### Suspense generations and reveal

`MountedSuspense<TNode>` owns runtime anchors, a storage container, the content candidate, and the
currently visible branch. `SuspenseBoundary` owns one generation's dependency set, deferred effect
buffer, failure state, and parent-boundary participation. This keeps timeout and reveal policy in
the existing executor instead of introducing another component activation system. The public
description remains one lazy invocation; its `timeout` argument and zero-argument `pending`,
`fallback`, and `resolve` listeners require no compiler-specific runtime seam ([BLT-11], [BLT-13],
[BLT-16], [BLT-17]; [V01.01.03.20], [#36](https://github.com/assimalign/viu/issues/36)).

| State | Trigger / next state | Visible range | Event and effect behavior |
| --- | --- | --- | --- |
| Creating | Ready content → Resolved | Newly inserted content | Emit `resolve`; release post-flush work |
| Creating | Pending dependency without positive timeout → Pending fallback | Newly mounted fallback | Emit `pending`, then `fallback`; retain hidden effects |
| Creating | Pending dependency with positive timeout → Pending retained | Empty placeholder | Emit `pending`; start deadline |
| Resolved | Matching content root patches → Resolved | Existing live content | No boundary event or remount; new async descendants own their loading presentation |
| Resolved | New pending replacement → Pending retained | Previous content | Emit `pending`; start positive clock deadline if configured |
| Pending retained | Zero timeout or positive deadline → Pending fallback | Fallback | Emit `fallback` only when it is shown |
| Pending retained / Pending fallback | Matching patch or new dependency → Same state | Unchanged | Patch storage; extend dependency set; no second `pending` |
| Pending retained / Pending fallback | Different content root → New generation | Previous visible branch | Dispose old dependencies, timer, and buffered effects |
| Pending retained / Pending fallback | Last successful settlement → Leaving or Resolved | Outgoing branch, then content | Finish root transition leave before insertion; defer content enter until reveal; emit `resolve` and release effects |
| Pending retained / Pending fallback | Fault → Failed | Branch visible at failure | Cancel timeout, route `suspense dependency`, suppress reveal and effects |
| Failed | Explicit boundary patch → Creating | Retained visible branch | Replace failed generation even if its content root still matches |
| Any live state | Unmount → Disposed | Removed | Cancel timer and jobs; discard hidden effects; dispose owned mounts |

The absent/negative timeout contract preserves previous content indefinitely. Initial mount selects
fallback immediately without a positive timeout; with a positive timeout it retains an empty
placeholder until deadline or earlier reveal. New asynchronous work discovered during hydration
always selects client fallback immediately. Positive deadlines use the scheduler's scoped `TimeProvider`;
timer callbacks enqueue scheduler work and never mutate a mounted branch directly. Generation
identity prevents expired timers or completed tasks from revealing a replacement generation.

Root replacement uses the renderer's ordinary identity check: node type, key, and the element name
or component reference where applicable. A matching resolved root patches its live branch and keeps
its mounted instances. Its generation accepts no more asynchronous dependencies and does not
re-enter pending; new asynchronous descendants use their own presentation options. This prevents
an update to an existing component from silently cloning or remounting that component merely to
create a hidden candidate. Different content roots create fresh generations in storage; matching
pending roots continue patching storage and may acquire more dependencies ([BLT-19]).

Mounted callbacks, template-reference publication, updated callbacks, directives, and component
post-flush watches use the same boundary buffer. Queued jobs still honor lifetime disposal and
ordinary scheduler deduplication. Normal post-flush ordering and host commit apply when the buffer
is released; never-revealed components still dispose their scopes and instances without running
mounted callbacks ([BLT-18], [SCH-4], [SCH-10]).
The initial synchronous `Reactive.WatchEffect` execution remains setup work; only its scheduled
post-flush reactions enter the boundary buffer.

A nested boundary registers completion with its pending parent and settles that participation only
after the inner reveal. It forwards buffered effects to the parent rather than publishing host
references to content that is still detached. This gives inner-first `resolve` ordering while
mounted work waits for the outer reveal. Asynchronous definitions with `Suspensible = false` bypass
dependency participation and keep their own loading/failure presentation ([BLT-20]).

Failure is terminal for one generation: ancestor error hooks and the application handler see the
named `suspense dependency` source, but neither another successful dependency nor a stale timeout
can change the visible branch. An explicit boundary patch replaces the failed generation even
when the content root matches, allowing recovery to start fresh ([BLT-21]).

Core owns the `HydrationMarkers` wire vocabulary and the generic hydration walk. A host supplies a
snapshot reader; Core adopts matching nodes and remounts only the smallest mismatched range
(`[SSR-MARKERS-1]` through `[SSR-MARKERS-3]`, `[HYD-1]` through `[HYD-7]`).

Suspense hydration reuses the generic content walk and adds runtime anchors around the adopted
range afterward. SSR emits its settled default content with no additional Suspense marker pair.
When client setup discovers new asynchronous work, ordinary localized mismatch recovery handles
an unresolved wrapper, the resulting client branch moves to storage, and fallback is mounted
immediately. An empty default slot claims no server range, and the walk preserves following
siblings under both live and snapshot readers ([BLT-12]).

Deferred hydration is marker-range bookkeeping, not a second renderer. Core validates and adopts
the range as opaque host state, registers the invocation's data-only strategy through
`ScheduleHydrationTrigger`, and creates the ordinary mounted component only from a post-flush job.
Unmount cancels both the host registration and any queued or pending asynchronous activation.
Nested boundaries remain undiscovered until their parent activates; asynchronous wrappers wait for
their resolved target or terminal error and remove the strategy before forwarding the target, so
one authored definition owns one boundary (`[HYD-LAZY-1]` through `[HYD-LAZY-5]`).

## Generated-code and AOT constraints

Compiled renderers interact through `ComponentRenderFrame`, immutable nodes, `RenderPlan`, the
public one-operation `ComponentHost.ExecuteAsync` callback, and the hidden hot-reload registration
interface. Update classification uses generated component and marker identities; Core does not
reflect over authored types or expose compiler services (`[SFC-CG-2]` through `[SFC-CG-4]`).

Core supports trimming, browser WASM AOT, and NativeAOT. It performs no reflection-based
serialization, dynamic code generation, runtime constructor discovery, or host-object marshalling
(`[EXE-4]`). Its runtime model assumes the single-threaded application event loop.

## Non-goals

Core does not own authored node vocabulary, host namespace and binding policy, browser interop,
server HTML rules, router behavior, dependency disposal, automatic dependency-injection scopes, or
compiler parsing and lowering. A host consumes public renderer and application seams; it is not a
compile-time friend of Core.
