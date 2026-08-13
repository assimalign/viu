# Assimalign.Viu.Testing — overview

The DOM-free production test host: an in-memory renderer with a complete
`RendererOptions<TestNode>` implementation, operation and commit logging, hydration readers, a
server-markup parser, deterministic scheduling, and component and element wrappers. It runs on a
plain CoreCLR test host with no DOM, browser, WASM toolchain, or JavaScript interop. Specified by
`[RND-HOST-1]`–`[RND-HOST-4]`, `[HYD-2]`, and `[CONF-3]`.

## Public surface

- `ComponentTest` mounts an immutable `VirtualNode`, an exact caller-supplied `IComponent`, or an explicit
  `ComponentRegistration`. `ComponentMountOptions` supplies the root invocation, descendant
  resolver and stubs, services, state, directives, and application diagnostics.
- `ComponentWrapper` and `ElementWrapper` query markup and text, locate elements and authored
  descendants, dispatch host events, capture per-context emitted events, drain scheduled work, and
  own root unmounting.
- `TestRenderer` pairs Core's production `Renderer<TestNode>` with `TestNodeOperations` and a
  `TestNodeOperationLog`. It owns a `TestSynchronizationContext`, scopes that context around render
  and hydrate calls, exposes `Drain`, `Pump`, and `Run`, and reports queued work when disposed. A
  `Commit` record makes the renderer's batch seam directly assertable (`[V01.01.11.05]`).
- `TestNode`, `TestElement`, `TestText`, and `TestComment` form the in-memory host tree.
  `TestNodeSerializer` produces assertion markup. `TestElementEvent` implements the portable
  Components `IElementEvent` payload; `TestEventDispatcher` invokes object, portable-interface,
  and exact generic payload delegates without reflection. Listener option suffixes normalize to
  their event name, and `Once` removes the listener before its first invocation
  (`[V01.01.11.06]`).
- `TestServerMarkup` parses ServerRenderer fragments using Core's `HydrationMarkers` vocabulary.
  `TestHydrationReader` reads the live tree; `FrozenTestHydrationReader` captures an immutable
  pre-walk matching a one-read browser snapshot.
- `TestSynchronizationContext.Install` saves and restores the preceding ambient context. Queued
  continuations run first-in, first-out on explicit `Drain`, while `Pump` and `Run` fail immediately
  when an incomplete operation has no runnable continuation. `TestSchedulerPump` can share that
  same queue through `Scheduler.UseFlushDispatcher`; `ComponentTest` uses the renderer-owned context
  and resets Scheduler state at mount boundaries (`[V01.01.11.05]`).
- `TestHydrationTriggers` is the deterministic host seam for idle, visible, media-query, and
  interaction activation. Trigger methods enter Core's ordinary post-flush path; counters expose
  completion and interaction replay without a DOM or wall clock (`[HYD-LAZY-3]` through
  `[HYD-LAZY-5]`).

## Public-seam boundary

Testing has no production friend grant and performs no capability cast. Authored type queries read
`MountedComponentView.Instance`; ancestry reads `ComponentContext.Parent`; a wrapper's current host
range reads `FirstHostNode` and `LastHostNode`; emitted events arrive through
`ApplicationOptions.EventObserver`; and mount state is determined by reacquiring the renderer's
stable per-mount view identity. See [DESIGN.md](DESIGN.md).
