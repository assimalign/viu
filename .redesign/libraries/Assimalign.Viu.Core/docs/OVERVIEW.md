# Assimalign.Viu.Core

Core is the host-neutral Application Model and mounted rendering engine. Components owns the
immutable `VirtualNode` vocabulary; Core activates authored components, maintains their mounted
instances, schedules updates, executes structural built-ins, and translates tree changes into
the operations supplied by a host. Core contains no browser handles or interop [RND-HOST-1] through
[RND-HOST-4].

## Mounted rendering

`Renderer<TNode>` mounts, patches, moves, hydrates, and unmounts the ten closed node variants. Each
renderer owns its mounted tree and stable `MountedComponentView<TNode>` identities. Element and
fragment updates consume compiler `RenderPlan` information where available, including the distinct
`Cached` and `Bail` whole-value paths; keyed children preserve retained host identity and minimize
moves [RND-1] through [RND-6], [RND-BLOCK-1] through [RND-BLOCK-6], and [RND-KEY-1] through
[RND-KEY-3].

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
`ComponentRenderFrame`, sized from the component's compiler contract, is retained per activation;
only compatibility contracts without cache metadata receive the legacy capacity.
`ComponentHost.RenderAsync(ComponentRenderRequest)` returns an
`IComponentRenderScope` after setup, awaited server prefetch, and one render. Disposing that lease
aborts the lifetime without client hooks; nested requests use the still-live parent context [SSR-4],
[SSR-5], and [SSR-10].

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

## Hydration and development updates

`HydrationMarkers` is the single marker vocabulary shared with serialization and hosts.
`HydrationNodeReader<TNode>` supplies a host snapshot reader; matching nodes are adopted and the
smallest mismatched range is remounted. Class and style comparison is semantic, and
`data-allow-mismatch` suppresses expected divergence [SSR-MARKERS-1] through [SSR-MARKERS-3] and
[HYD-1] through [HYD-7].

Generated development builds call the hidden `ComponentHotReload.Register` binary interface with
stable component and marker type identities. `ApplyUpdates` classifies marker sets without
reflection: style-only changes leave mounted state untouched, while template and script changes
remount affected instances. There is no public component metadata interface and no compiler service
surface in Core [SFC-CG-2] through [SFC-CG-4].

Mounted node variants, component activations, built-in state, and update registrations remain
internal. Hosts consume the public operations and renderer contracts; no host is a compile-time
friend of Core.
