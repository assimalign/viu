# Assimalign.Viu.Core

Core is the host-neutral Application Model and mounted rendering engine. Components owns the
immutable `VirtualNode` vocabulary; Core activates authored components, maintains their mounted
instances, schedules updates, executes structural built-ins, and translates tree changes into
the operations supplied by a host. Core contains no browser handles or interop [RND-HOST-1] through
[RND-HOST-4].

## Mounted rendering

`Renderer<TNode>` mounts, patches, moves, hydrates, and unmounts the ten closed node variants. Each
renderer owns its mounted tree and stable `MountedComponentView<TNode>` identities. Element and
fragment blocks retain ordered mounted dynamic-occurrence lists aligned with compiler `RenderPlan`
information, so aliased immutable descriptions still own distinct mounted and host state. Updates
also preserve the distinct `Cached` and `Bail` whole-value paths; keyed children retain host identity
and minimize moves [RND-1] through [RND-6], [RND-BLOCK-1] through [RND-BLOCK-6], and [RND-KEY-1]
through [RND-KEY-3].

`RendererOptions<TNode>` is the complete host contract. It carries creation, insertion, removal,
navigation, binding-patch, commit, static-content, teleport-resolution, and hydration-reader
operations. A missing optional operation means that capability is unavailable. Host batching is
crossed only through `Commit`; Core does not infer host namespaces or timing policy.

Teleport, KeepAlive, Suspense, and Transition are internal executors over structural nodes. Their
descriptions remain lazy through `ComponentInvocation` slots. Transition properties and callbacks
ride the transition invocation; host options remain limited to the renderer contract. Suspense owns
pending-branch storage, nested dependency accounting, fallback ownership, and reveal, subject to
the explicit limits in [BLT-11] through [BLT-13]. Asynchronous component definitions deduplicate a
shared load while every mount retains its own wrapper and target activation [BLT-14].

## Component lifetime and application composition

Persistent renderer mounts and one-shot server rendering share the same activation core. One
`ComponentRenderFrame`, sized once per activation from the component contract's fixed value or stable
compiler provider, is retained per activation; only compatibility contracts without cache metadata
receive the legacy capacity. A structural hot-reload remount reads the provider's updated method body
without reinitializing the generated contract ([V01.01.06.14], #350; [SFC-CG-9]).
`ComponentHost.RenderAsync(ComponentRenderRequest)` returns an
`IComponentRenderScope` after setup, awaited server prefetch, and one render. Disposing that lease
aborts the lifetime without client hooks; nested requests use the still-live parent context [SSR-4],
[SSR-5], and [SSR-10]. `ComponentHost.ExecuteAsync` instead invokes one host operation with the
activated `IComponent`, `ComponentRenderFrame`, and public scope, while Core retains error routing
and teardown and reports a named success or handled-failure outcome [SSR-TARGET-3].

`RuntimeComponentContext` is the single internal implementation of Components'
`ComponentContext`. It owns resolved bindings, per-mount defaults and warning suppression,
listener-once state, the exposed value, component watches, the active Suspense boundary, and error
propagation. Observed failures traverse ancestor `OnErrorCaptured` callbacks before the application
error handler [CMP-12] through [CMP-23].

`ApplicationContext` snapshots application composition, including components, directives, nullable
services, state, diagnostics, and event observation. `ApplicationLifetime` owns the one-way
start/stop/failure state machine. `Scheduler` orders component and watcher jobs, deduplicates work,
and drains pre-flush, commit, and post-flush phases [APP-1] through [APP-7] and [SCH-1] through
[SCH-12].

Browser uses one shared single-event-loop execution state. Request-oriented hosts call the public
`RuntimeExecution.EnterExecutionFlow` boundary, which composes the public Reactivity and State flow
boundaries for every runtime-tree or compiled render. Independently owned request graphs therefore
do not share the current component, scope, tracking/batching state, active registry, or scheduler
queues [EXE-1]. Individual graphs remain single-event-loop and not thread-safe.

## Hydration and development updates

`HydrationMarkers` is the single marker vocabulary shared with serialization and hosts.
`HydrationNodeReader<TNode>` supplies a host snapshot reader; matching nodes are adopted and the
smallest mismatched range is remounted. Class and style comparison is semantic, and
`data-allow-mismatch` suppresses expected divergence [SSR-MARKERS-1] through [SSR-MARKERS-3] and
[HYD-1] through [HYD-7].

For a deferred component marker range, Core adopts the existing host nodes immediately but leaves
setup, effects, rendering, and descendant discovery dormant. It asks the host for one trigger,
queues activation post-flush, reschedules when strategy data changes, and cancels registrations and
queued work on unmount. Pending asynchronous definitions preserve adopted markup until their target
or terminal error presentation is ready (`[HYD-LAZY-1]` through `[HYD-LAZY-5]`).

Generated development builds call the hidden `ComponentHotReload.Register` binary interface with
stable component and marker type identities. `ApplyUpdates` classifies marker sets without
reflection: style-only changes leave mounted state untouched, while template and script changes
remount affected instances. There is no public component metadata interface and no compiler service
surface in Core [SFC-CG-2] through [SFC-CG-4].

Mounted node variants, component activations, built-in state, and update registrations remain
internal. Hosts consume the public operations and renderer contracts; no host is a compile-time
friend of Core.
