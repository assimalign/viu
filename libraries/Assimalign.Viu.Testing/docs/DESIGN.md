# Assimalign.Viu.Testing — design

## One renderer, another host

Testing supplies the same closed host contract as every production adapter:
`TestNodeOperations.Create` returns `RendererOptions<TestNode>`. Core remains the sole owner of
mounting, patching, built-in execution, hydration, and unmounting. The in-memory tree changes only
the host operations beneath that renderer, so tests exercise the real engine without a DOM
(`[RND-HOST-1]`, `[RND-HOST-3]`, `[CONF-3]`).

Each host write and each `Commit` enters `TestNodeOperationLog`. The log therefore separates final
tree assertions from operation-budget assertions. In particular, tests pin one commit for a
coalesced reactive flush through the per-renderer commit seam (`[RND-HOST-4]`, `[RND-IO-1]`).

## Mounted components remain engine-owned

Wrappers retain only Core's public, read-only `MountedComponentView<TestNode>`. Core guarantees one
stable view object for the life of a mount (`[RND-6]`). A child query filters `Instance` by authored
type and verifies ancestry through `Context.Parent`. Host queries reconstruct the inclusive range
between the view's current `FirstHostNode` and `LastHostNode`. `Exists` reacquires the current view
snapshot and uses reference identity, so an unmounted or remounted authored instance cannot be
mistaken for the original mount.

Emitted-event capture is keyed by public `ComponentContext` identity and composed with any observer
the caller configured through `ApplicationOptions.EventObserver`. No parent listener is replaced,
and root and descendant event histories remain separate (component-model seams S3 and S5).

## Deterministic scheduling

`TestSchedulerPump` installs `Scheduler.UseFlushDispatcher` and owns the returned restoration lease.
`ViuTest` resets ambient scheduler state before a mount and after disposal. Wrapper interactions
await event handlers, capture `Scheduler.NextTickAsync`, drain every queued continuation, and then await
the tick. Tests observe post-flush state without wall-clock delays or a test-framework
`SynchronizationContext` (component-model seam S2, `[SCH-9]`).

## Hydration fidelity

`TestServerMarkup` parses the focused HTML vocabulary ServerRenderer produces and recognizes marker
tokens only through Core's public `HydrationMarkers` constants (`[SSR-MARKERS-3]`). Entity decoding
models browser-visible text and attribute values. `TestHydrationReader` exposes live topology;
`FrozenTestHydrationReader` captures a complete immutable pre-walk so recovery remains readable
after host mutations, matching the browser's one-snapshot read budget (`[HYD-2]`, `[RND-IO-1]`).

## Non-goals

- Browser event propagation, layout, accessibility-tree, and CSS-engine simulation.
- General-purpose or error-recovering HTML parsing beyond ServerRenderer output.
- Mutable access to Core's mounted engine hierarchy.
- Ownership of caller-supplied services, state registries, factories, directives, or component
  registrations (`[APP-6]`, `[CMP-9]`).
