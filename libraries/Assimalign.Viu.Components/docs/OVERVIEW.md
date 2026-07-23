# Assimalign.Viu.Components

The proposed platform-neutral component-tree vocabulary. Every render-tree value is an
`IComponent`; specialized interfaces describe element, template, text, comment, static, fragment,
and teleport behavior.

`ITeleportComponent.IsDeferred` models Vue 3.5's `defer` property. It postpones target-side setup
until the current render's post-flush phase, allowing a target rendered later in the same tree to
resolve. Disabled Teleport content still mounts at its logical position immediately; only its
target-side setup is deferred.

The package also owns the component-resolution contract. `IComponentFactory` creates a fresh
`IComponentTemplate` per mounted template node without implementing or requiring
`IServiceProvider`. The built-in factory uses explicit activators and resolves registered names in
Vue's raw, camel-case, then Pascal-case order, so a `my-widget` request can resolve `myWidget` or
`MyWidget`. Registrations remain ordinal and exact duplicate names fail; custom factories may use
any application-selected resolver.

An `ITemplateComponent` is a non-activating mount request. It identifies its template by either an
explicit `Type` or a registered name; Core is the layer that later selects the matching
`IComponentFactory.Create(...)` overload. Template requests also carry immutable argument, slot,
parent-listener, directive, key, and optimization snapshots. The context supplied to the activated
template exposes the current slots and fallthrough attributes separately from declared arguments.
Generated requests retain the raw `onX` properties in their argument snapshot as well as the typed
listener map so Core can partition declared component events from undeclared listeners that fall
through to the rendered root.

`ComponentSlots` is the developer-facing slot builder. Its `SlotFlags` metadata preserves the
compiler's stable, dynamic, and forwarded-slot classifications when a template request takes its
immutable snapshot. Core uses that marker to skip child renders for structurally stable slots while
still forcing updates for dynamic and effectively dynamic forwarded slots.

`ComponentParameter` supports required values, one default-factory evaluation per mount, and an
optional validator. The declaration name is the canonical key exposed through
`IComponentContext.Arguments`; Core accepts both its camel-case and kebab-case parent spellings.
Like [Vue prop validation](https://vuejs.org/guide/components/props.html#prop-validation), a
validator failure warns without discarding the resolved value.

`ComponentEvent` optionally validates the complete ordered argument list.
`IComponentContext.Emit` accepts zero or more arguments, while `ComponentEventListener` supports
single-payload and all-arguments handlers in synchronous and task-returning forms. The listener can
be marked `IsOnce`; generated `onSavedOnce` properties use the `savedOnce` listener-map convention.
Components only transports these contracts. Core owns matching, once-per-mount tracking, task
observation, and error routing. See [Vue component
events](https://vuejs.org/guide/components/events.html).

Generated templates expose their style scope through `IComponentTemplate.ScopeIdentifier`.
Authoring-time `ComponentDirectiveBinding` values identify a registered directive by name and
snapshot its value, argument, and modifiers. Element requests carry those bindings directly;
template requests carry bindings that Core transfers to the rendered root. Directive resolution
and lifecycle-hook execution remain renderer responsibilities.

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

See the root [DESIGN.md](../../DESIGN.md) for the role/lifetime split and the decisions that guided
this implementation.
