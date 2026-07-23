# Assimalign.Viu.Reactivity

The proposed independent home for Viu's existing reactive engine: reactive values, dependencies,
subscribers, effects, effect scopes, collections, watch primitives, and reactive-object generator
contracts.

The shipping refactor should move the proven implementation and its run-count/benchmark tests out
of Core. It should not rewrite the engine. This scaffold introduces only `IReactiveScope`, the
boundary State and Core need in order to prove the proposed package graph.

The package has no dependency on Components, State, or Core.

