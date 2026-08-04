# Assimalign.Viu.Components

The proposed platform-neutral component-tree vocabulary. Every render-tree value is an
`IComponent`; specialized interfaces describe element, template, text, comment, static, fragment,
and teleport behavior.

`ITeleportComponent.IsDeferred` postpones target-side setup until the current render's post-flush
phase, allowing a target rendered later in the same tree to resolve. Disabled Teleport content still
mounts at its logical position immediately; only its target-side setup is deferred
([`[BLT-2]`](../../../docs/SPECIFICATION.md#71-teleport)).

The package also owns the component-resolution contract. `IComponentFactory` creates a fresh
`IComponentTemplate` per mounted template node without implementing or requiring
`IServiceProvider`. The built-in factory uses explicit activators and resolves a registered name by
trying the raw name, then its camel-case spelling, then the Pascal-case spelling of that, so a
`my-widget` request can resolve `myWidget` or `MyWidget`. Registrations remain ordinal and exact
duplicate names fail; custom factories may use any application-selected resolver
([`[CMP-6]`](../../../docs/SPECIFICATION.md#43-activation)).

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
`IComponentContext.Arguments`; Core accepts both its camel-case and kebab-case parent spellings. A
required-value or validator failure warns without discarding the resolved value, so a bad input is
reported rather than silently replaced.

`ParameterAttribute` and `EventAttribute` are the declarative form of the same two contracts. A
single-file component may put `[Parameter]` on the settable property that receives an argument and
`[Event]` on the `partial void` method that emits an event; the source generator reads both at build
time and synthesizes the equivalent `ComponentParameter`/`ComponentEvent` declarations, the
per-render assignment from `IComponentContext.Arguments`, and the typed `Emit` implementation. Nothing
is discovered by reflection — the attributes never exist at runtime as a lookup key, only as the
build-time input that produced the same static declarations an imperative component writes by hand.
The canonical name is the camel-case spelling of the member name (`Title` -> `title`, `URL` -> `url`),
overridable with an explicit constant `Name`; requiredness comes from `IsRequired` or the C#
`required` modifier; and the property's initializer is captured once per mounted instance as its
default. The two forms are exclusive per kind: a component declares its parameters either with
attributes or with its own `Parameters` member, never both
([`[CMP-26]`-`[CMP-31]`](../../../docs/SPECIFICATION.md#49-attribute-declared-parameters-and-events)).

A declaration also carries its parameter's declared value type through
`IComponentParameter.ParameterType` (default-implemented as null, so no existing implementor breaks).
The type is descriptive only -- Core never converts a supplied argument to it, because argument
resolution stays a reflection-free dictionary lookup -- and exists so a declaration says the same
thing in metadata that it says in source. That is what lets a consumer's template be checked where it
is compiled: an attribute the component declares no parameter for, a missing required parameter, and
a statically decidable type mismatch are all build diagnostics
([`[SFC-USE-1]`-`[SFC-USE-5]`](../../../docs/SPECIFICATION.md#88-component-usage-validation)). A
component that builds its `Parameters` collection imperatively carries nothing readable in metadata,
so its usages are never checked -- the gap the attribute form closes.

`ComponentEvent` optionally validates the complete ordered argument list.
`IComponentContext.Emit` accepts zero or more arguments, while `ComponentEventListener` supports
single-payload and all-arguments handlers in synchronous and task-returning forms. The listener can
be marked `IsOnce`; generated `onSavedOnce` properties use the `savedOnce` listener-map convention.
Components only transports these contracts. Core owns matching, once-per-mount tracking, task
observation, and error routing
([`[CMP-14]`–`[CMP-17]`](../../../docs/SPECIFICATION.md#45-parameters-events-and-fallthrough)).

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

`ComponentTemplateBase` is the authoring base class the single-file-component generator puts under
every compiled component with a template block. It holds the mounted `IComponentContext` the
generated setup bridge assigns and re-declares every `IComponentLifecycle` registration method as a
**protected** pass-through, so a component writes `OnMounted(() => ...)` at the root of its class
rather than `Context.Lifecycle.OnMounted(() => ...)`. The two forms are specified equivalents --
same registrar, one shared registration order, so they may be mixed
([`[CMP-32]`](../../../docs/SPECIFICATION.md#410-root-level-lifecycle-registration),
[`[SFC-CG-4]`](../../../docs/SPECIFICATION.md#85-the-code-generation-contract)). A component that
declares a member of its own with one of these names hides the pass-through under ordinary C#
rules -- a warning, never an error -- and reaches the hook through the context form. The base
deliberately does not implement `IComponentTemplate`: the generated declaration members are explicit
interface implementations, which C# allows only on a type that lists the interface itself. A
hand-authored component may derive from it too, assigning `Context` at the head of its own `Setup`.

`ComponentOptimization` preserves the compiler/runtime block-tree contract on the unified tree:
patch flags, dynamic property names, dynamic children, and the `v-once` marker. Core may lower the
tree for hot-path dispatch, but it must copy this metadata without changing its semantics.

Components does not reference Reactivity, State, Core, a renderer, or a browser host. Its only
project dependency is the shared compiler/runtime flag vocabulary.

See the root [DESIGN.md](../../DESIGN.md) for the role/lifetime split and the decisions that guided
this implementation.
