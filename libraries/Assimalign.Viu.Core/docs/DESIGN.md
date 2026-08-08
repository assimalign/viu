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
aborts that lifetime without client mount hooks (`[SSR-4]`, `[SSR-5]`, `[SSR-10]`).

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

## Structural built-ins and hydration

Teleport, KeepAlive, Suspense, and Transition are internal executors for their Components-owned
nodes. Their public descriptions remain immutable and lazy; Core owns retained branches, dependency
accounting, reveal, movement, and teardown. Host-specific transition effects stay behind the public
host contract.

Core owns the `HydrationMarkers` wire vocabulary and the generic hydration walk. A host supplies a
snapshot reader; Core adopts matching nodes and remounts only the smallest mismatched range
(`[SSR-MARKERS-1]` through `[SSR-MARKERS-3]`, `[HYD-1]` through `[HYD-7]`).

## Generated-code and AOT constraints

Compiled renderers interact through `ComponentRenderFrame`, immutable nodes, `RenderPlan`, and the
hidden hot-reload registration interface. Update classification uses generated component and marker
identities; Core does not reflect over authored types or expose compiler services
(`[SFC-CG-2]` through `[SFC-CG-4]`).

Core supports trimming, browser WASM AOT, and NativeAOT. It performs no reflection-based
serialization, dynamic code generation, runtime constructor discovery, or host-object marshalling
(`[EXE-4]`). Its runtime model assumes the single-threaded application event loop.

## Non-goals

Core does not own authored node vocabulary, host namespace and binding policy, browser interop,
server HTML rules, router behavior, dependency disposal, automatic dependency-injection scopes, or
compiler parsing and lowering. A host consumes public renderer and application seams; it is not a
compile-time friend of Core.
