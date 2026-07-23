# Assimalign.Viu.Reactivity

The independent home for Viu's Vue-shaped reactive engine: references,
computeds, dependencies, subscribers, effects, effect scopes, collections, watch primitives,
reactive-object generation, traversal, and introspection.

Commit `80bb967` is the documentation tail of the Core consolidation. The useful code baseline is
its ancestor `470142e`, the last snapshot with a standalone `Assimalign.Viu.Reactivity` package;
`0fe2d9c` is the commit that moved that implementation into RuntimeCore. The split should restore
the earlier package boundary while retaining improvements made after consolidation.

The redesign ports the proven implementation and its behavioral tests out of Core without
rewriting the linked-list engine. It restores
`IReactiveReference`/`IReactiveReference<T>` as the public reference contracts while retaining
`ReactiveValue`/`ReactiveValue<T>` as the first-party engine base. All Reactivity-owned interfaces
use the `IReactive` prefix.

`EffectScope` implements `IReactiveEffectScope`, and `ReactiveEffectScopeFactory` supplies the
abstraction-facing factory used by State. These complement rather than replace the public
`Reactive.EffectScope(...)` API. Runtime pre/post watch scheduling stays behind
`IReactiveWatchScheduler`, which Core implements.

The package has no dependency on Components, State, or Core.
