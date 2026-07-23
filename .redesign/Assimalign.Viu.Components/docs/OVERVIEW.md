# Assimalign.Viu.Components

The proposed platform-neutral component-tree vocabulary. Every render-tree value is an
`IComponent`; specialized interfaces describe element, template, text, comment, static, fragment,
and teleport behavior.

The package also owns the component-resolution contract. `IComponentFactory` creates a fresh
`IComponentTemplate` per mounted template node without implementing or requiring
`IServiceProvider`. The built-in factory uses explicit activators; custom factories may use any
application-selected resolver.

`IComponentLifecycle` uses named, typed hooks instead of an enum-based callback registry. It exposes
the component-lifetime cancellation token, accepts synchronous or observed `Task` callbacks for
each lifecycle phase, and gives server prefetch an explicit awaited contract. Ordinary asynchronous
hooks do not delay lifecycle progression. Core owns the internal task observation and error-routing
machinery.

`ComponentOptimization` preserves the compiler/runtime block-tree contract on the unified tree:
patch flags, dynamic property names, dynamic children, and the `v-once` marker. Core may lower the
tree for hot-path dispatch, but it must copy this metadata without changing its semantics.

Components does not reference Reactivity, State, Core, a renderer, or a browser host. Its only
project dependency is the shared compiler/runtime flag vocabulary.

This is a design scaffold. See the root [DESIGN.md](../../DESIGN.md) for the role/lifetime split and
open decisions.
