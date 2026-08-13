# Assimalign.Viu.Reactivity design

## Engine shape

`IReactiveReference` and `IReactiveReference<T>` are public substitution contracts.
`ReactiveValue` and `Subscriber` retain shared fields and virtual dispatch for first-party hot
paths; sealed leaf implementations permit devirtualization. External implementations must perform
their own tracking and triggering, while `Reactive.CustomReference` is the preferred controlled
extension point (`[RCT-5]`).

Dependency edges are intrusive and versioned. A tracked read connects the active subscriber to a
dependency; cleanup removes stale edges after each run. Batches delay notification until the outer
batch ends. Computed values evaluate lazily and reuse cached output until a dependency version
changes (`[RCT-2]` through `[RCT-4]`).

## Lifetimes and scheduling

An effect scope owns effects, watchers, child scopes, and cleanup callbacks. Stopping it is
idempotent and unlinks dependencies even when user code throws. `WatchHandle` always represents an
actual watcher and controls only that watcher's stop/pause/resume state (`[RCT-10]`, `[RCT-12]`).

`Reactive.EnterExecutionFlow()` installs fresh ambient dependency-tracking, batching, and
effect-scope bookkeeping for a request-oriented host. Its idempotent lease restores the previous
flow when nested leases are disposed in last-in, first-out order. The seam isolates engine
bookkeeping, not caller-owned reactive values or lifetimes; each request must still own its graph.
Keeping the operation on the public facade lets Core compose the lifecycle without cross-library
friend access (`[EXE-1]`, `[CMP-33]`).

Watch scheduling is a caller-supplied cold seam. Core adapts it to application flush phases; State
can borrow it for store notifications. Reactivity remains unaware of components and hosts.

## AOT boundary and non-goals

The `Assimalign.Viu.Generators.Reactivity` analyzer emits property wrappers and typed traversal at
build time. The runtime never discovers members, emits code, or serializes object shapes by
reflection. The engine assumes the browser event loop and does not promise concurrent mutation.

Implicit deep conversion of arbitrary objects and runtime proxying are non-goals (`[RCT-6]`,
`[RCT-7]`). Host scheduling, component error policy, and state-store lifetime stay with their
owning libraries.
