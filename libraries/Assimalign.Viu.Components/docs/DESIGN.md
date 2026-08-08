# Assimalign.Viu.Components design

## Model ownership

Components owns both halves of Viu's authored model: the immutable render description and the
component contract that produces it. The package references only Reactivity. It contains no mounted
host state, renderer, scheduler, browser policy, or application lifetime.

The separation is deliberate. `VirtualNode` describes one render result;
`ComponentReference`/`ComponentContract`/`ComponentRegistration` describe static identity and
activation; `IComponent` is one activated authored instance; and Core alone owns mounted
bookkeeping. State from one lifetime is never written into another (`[CMP-1]`, `[CMP-2]`).

## Closed virtual tree

`VirtualNode` has a `private protected` constructor and exactly ten sealed variants. A variant fixes
its `VirtualNodeKind`, so kind and runtime type cannot diverge. Hosts and Core may exhaustively
dispatch the algebra without reflection or an extension-node escape hatch (`[CMP-3]`).

Structural built-ins carry lazy `ComponentInvocation` slots rather than evaluated children.
`ComponentNode` is only a non-activating request: its reference, immutable invocation, key, mount
reference, and `RenderPlan` remain descriptions until Core resolves the registration at mount time
(`[CMP-7]`).

## Authored behavior and activation

`IComponent` has one synchronous member, `Setup(ComponentContext)`, returning a
`ComponentRenderer`. Setup is synchronous so Core can run it inside the mount's reactive scope
before asynchronous work interleaves. The renderer receives that mount's `ComponentRenderFrame`
and returns the current immutable tree (`[CMP-8]`).

`IComponentFactory` resolves explicit registrations; it is neither a service provider nor an
activator. `ComponentRegistration` exposes the reference, readable contract, and activator.
`ComponentRegistration.Define` is the reflection-free code-first composition form. There is no
constructor discovery, options-object activation path, or implicit service lookup (`[CMP-4]`
through `[CMP-6]`, `[CMP-34]`).

The caller's composition root owns factories and services. Core owns each activated authored
instance and disposes it on setup failure or unmount; Viu does not invent dependency-injection scopes
(`[CMP-9]` through `[CMP-11]`).

## Invocation and mounted bindings

`ComponentInvocation` is the parent's immutable arguments, slots, listeners, and directives.
`ComponentBindings` is the mounted component's resolved parameters, slots, and fallthrough set.
They intentionally share no interface. `ComponentBindings.Resolve` is a pure transformation that
normalizes aliases, classifies listeners and fallthrough, validates values, and reports diagnostics.
Core owns default-value caching, warning timing, once-listener state, and fallthrough application
(`[CMP-12]` through `[CMP-19]`).

`ComponentContext` exposes only cross-cutting component operations: bindings, nullable services,
lifecycle, reactive scope, watch scheduling, explicit parent context, emit, expose, warn, and scoped
watch. Core supplies the live implementation. State, routing, styling, and host integrations attach
through nullable services, the ambient reactive scope, generated code, host operations, or
application composition; they do not add convention-specific context members or rely on casts
(`[CMP-24]`, `[CMP-33]`).

## Generated-code ABI

`ComponentRenderFrame` is per mount. It owns block tracking and the exact contract-declared cache
used by compiled renderers for static subtree identity, stable handlers, and memo snapshots. Generated
code calls through its frame parameter and qualified public APIs, not mutable static helper state
(`[SFC-CG-2]`, `[SFC-OPT-1]`).

`RenderPlan`, `PatchFlags`, `ShapeFlags`, and `SlotStability` are data contracts between compiled output
and Core. Their meanings and numeric layouts are frozen; Core may optimize only when the supplied
plan proves the relevant structure (`[RND-FLAGS-1]` through `[RND-FLAGS-6]`, `[RND-BLOCK-1]`
through `[RND-BLOCK-7]`). Hand-authored trees default to `RenderPlan.None` and remain correct through
full diff (`[CMP-34]`).

## Non-goals

Components does not mount or patch trees, schedule updates, implement built-ins, serialize HTML,
interpret DOM events, own application lifetime, discover constructors, or provide an ambient
hierarchical dependency API. Scoped style identity is absent while scoped CSS is deferred
(`[CMP-24]`, `[STY-1]`).
