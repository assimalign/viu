# Assimalign.Viu.Reactivity

Reactivity is Viu's independent single-threaded change-tracking engine. It owns reference cells,
computed values, dependency/subscriber links, effects, effect scopes, watch primitives, reactive
collections, traversal, and source-generated reactive-object contracts. Its production project has
no Viu project reference (`[RCT-1]` through `[RCT-12]`).

`Reactive` is the discoverable facade. Public reference interfaces form the cold extensibility
boundary; first-party values and subscribers use abstract-class dispatch and intrusive links on the
notification hot path. Batches defer delivery, computed values cache by dependency version, and
effect scopes own deterministic cleanup.

Reactive objects are generated at build time. Runtime member interception, reflection activation,
and dynamic code generation are not part of the model. Collections use dedicated typed
implementations, and deep traversal follows explicit reactive contracts rather than inspecting
arbitrary object graphs (`[RCT-6]`, `[RCT-7]`).

Core supplies a scheduler when component watches must join a render flush. Standalone callers may
use synchronous delivery or supply `IReactiveWatchScheduler`; this does not introduce a dependency
from Reactivity to Components, Core, State, or a host.
