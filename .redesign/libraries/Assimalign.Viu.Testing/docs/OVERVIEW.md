# Assimalign.Viu.Testing — overview

The DOM-free production test host: an in-memory renderer with a complete
`RendererOptions<TestNode>` implementation, operation and commit logging, hydration readers, a
server-markup parser, deterministic scheduling, and component and element wrappers. It runs on a
plain CoreCLR test host with no DOM, browser, WASM toolchain, or JavaScript interop. Specified by
`[RND-HOST-1]`–`[RND-HOST-4]`, `[HYD-2]`, and `[CONF-3]`.

## Public surface

- `ViuTest` mounts an immutable `VirtualNode`, an exact caller-supplied `IComponent`, or an explicit
  `ComponentRegistration`. `ComponentMountOptions` supplies the root invocation, descendant
  resolver and stubs, services, state, directives, and application diagnostics.
- `ComponentWrapper` and `ElementWrapper` query markup and text, locate elements and authored
  descendants, dispatch host events, capture per-context emitted events, drain scheduled work, and
  own root unmounting.
- `TestRenderer` pairs Core's production `Renderer<TestNode>` with `TestNodeOperations` and a
  `TestNodeOperationLog`. A `Commit` record makes the renderer's batch seam directly assertable.
- `TestNode`, `TestElement`, `TestText`, and `TestComment` form the in-memory host tree.
  `TestNodeSerializer` produces assertion markup and `TestEventDispatcher` invokes supported
  delegate shapes without reflection.
- `TestServerMarkup` parses ServerRenderer fragments using Core's `HydrationMarkers` vocabulary.
  `TestHydrationReader` reads the live tree; `FrozenTestHydrationReader` captures an immutable
  pre-walk matching a one-read browser snapshot.
- `TestSchedulerPump` installs through `Scheduler.UseFlushDispatcher` and restores through its
  returned lease. `ViuTest` also uses `Scheduler.Reset` at mount boundaries.

## Public-seam boundary

Testing has no production friend grant and performs no capability cast. Authored type queries read
`MountedComponentView.Instance`; ancestry reads `ComponentContext.Parent`; a wrapper's current host
range reads `FirstHostNode` and `LastHostNode`; emitted events arrive through
`ApplicationOptions.EventObserver`; and mount state is determined by reacquiring the renderer's
stable per-mount view identity. See [DESIGN.md](DESIGN.md).
