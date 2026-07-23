# Assimalign.Viu.Reactivity

The proposed independent home for Viu's existing Vue-shaped reactive engine: references,
computeds, dependencies, subscribers, effects, effect scopes, collections, watch primitives,
reactive-object generation, traversal, and introspection.

Commit `80bb967` is the documentation tail of the Core consolidation. The useful code baseline is
its ancestor `470142e`, the last snapshot with a standalone `Assimalign.Viu.Reactivity` package;
`0fe2d9c` is the commit that moved that implementation into RuntimeCore. The split should restore
the earlier package boundary while retaining improvements made after consolidation.

The shipping refactor should move the proven implementation and its run-count/benchmark tests out
of Core. It should not rewrite or fork the engine. The scaffold restores
`IReactiveReference`/`IReactiveReference<T>` as the public reference contracts while retaining
`ReactiveValue`/`ReactiveValue<T>` as the first-party engine base. All Reactivity-owned interfaces
use the `IReactive` prefix.

`IReactiveEffectScope` and `IReactiveEffectScopeFactory` remain scaffold-only seams used to compile
the State dependency; they are not replacements for the public `EffectScope` and
`Reactive.EffectScope(...)` APIs. See [DESIGN.md](DESIGN.md) for the target surface.

The package has no dependency on Components, State, or Core.
