# Assimalign.Viu.Browser design

## Host boundary

Browser is a concrete Core host with `TNode = int`. Handles are opaque identifiers shared with
`viu-dom.js`; zero is the no-node sentinel. Core never sees a DOM object, and JavaScript never makes
rendering decisions. `RendererOptions<int>` is the complete join between the packages
(`[RND-HOST-1]` through `[RND-HOST-3]`, `[EXE-11]`, `[EXE-12]`).

Browser owns every DOM-specific choice: HTML, SVG, MathML, and `foreignObject` namespaces;
property-versus-attribute selection; class and style normalization; form coercion; event modifiers;
directives; transition timing; selector resolution; and hydration snapshots. Adding one of those
policies to Core would make the generic renderer host-specific.

## Buffered interop

Renderer writes append primitive operations to a versioned binary command frame. Core crosses the
host seam through `Commit`, and Browser applies the frame in one interop call. A live-DOM read first
commits pending writes, so read-after-write ordering is deterministic (`[RND-HOST-4]`,
`[RND-IO-1]`, `[EXE-13]`).

Managed code allocates ordinary handles. Selector lookups and hydration snapshots introduce foreign
handles, so the allocator advances beyond every observed value before issuing another handle.
Managed and JavaScript registries release handles and listeners on both sides (`[RND-IO-4]`,
`[EXE-14]`).

Events use a stable per-element invoker. Updating a handler swaps the managed delegate without
removing and re-adding the host listener. Static content uses Core's optional bulk-insertion
operation. These paths protect the interop budget in `[RND-IO-2]`, `[RND-IO-3]`, and `[RND-IO-5]`.
The invoker explicitly dispatches `IElementEvent` delegates before concrete `BrowserEvent`
delegates, preserving contravariant delegate ordering and exact payload identity without reflection
(`[V01.01.11.06]`).

## Application lifetime

`BrowserApplication` owns browser initialization, mount-target resolution, mount or hydration, and
full-page development reload. Core's `ApplicationLifetime` owns the host-independent state machine,
middleware nesting, shutdown signal, and failure reporting (`[APP-1]` through `[APP-5]`).

Startup is asynchronous. Middleware wraps the complete interval from pre-mount initialization
through unmount; lower-level mount APIs deliberately bypass that pipeline for embedding and tests.
The component factory, services, state registry, directive resolver, and diagnostics are immutable
borrowed composition values and are never disposed by Browser (`[APP-6]`, `[APP-7]`, `[CMP-9]`).

Only one browser renderer lease may control the shared DOM bridge at a time. The lease makes global
interop dispatch explicit and guarantees that callbacks cannot target an abandoned renderer.

## Hydration, directives, and transitions

Browser snapshots the live DOM once and exposes it through Core's generic hydration reader. Core
owns tree matching and mismatch recovery; Browser owns node classification, property reads, and
foreign-handle registration (`[HYD-1]`, `[HYD-2]`).

Deferred component ranges use the optional host trigger operation. Idle registrations use
`requestIdleCallback` when available and a cancelable timer otherwise; visibility observes every
top-level element between the markers; media registrations subscribe through `matchMedia`; and
interaction registrations capture the first configured event within the range. The JavaScript side
delivers every trigger asynchronously after registration, disconnects observers and listeners on
fire or cancellation, and replays a cloned interaction only after Core reports activation complete
(`[HYD-LAZY-3]` through `[HYD-LAZY-5]`).

When hydration has application state, Browser performs one `textContent` interop operation that
consumes and removes `script[data-viu-state]` after the bridge is ready and before mount-target
resolution. It parses the versioned State payload without reflection and restores the composed
registry before component setup. Removal occurs before Core snapshots the mount container, so the
transport cannot appear as an extra root sibling. A missing island or a registry without
`IStateStorePayloadRegistry` fails startup before the first render, preventing a default-state
hydration mismatch ([HYD-8], [V01.01.09.03], [EXE-4]).

Browser directives are registered through the application's public directive resolver. The model
directives, `VShow`, and transition operations use host elements and browser events without adding
members to `ComponentContext`. Transition nodes remain host-neutral descriptions; Browser supplies
class scheduling, geometry, and completion behavior through the public host seam (`[BLT-7]` through
`[BLT-10]`, `[CMP-33]`).

## AOT and WASM constraints

All JavaScript entry points are statically declared `JSImport` or `JSExport` boundaries, and payloads
are primitive handles, snapshots, or command frames. Browser does not use reflection-based
serialization, runtime member discovery, emitted code, or dynamically generated delegates
(`[EXE-4]`).

## Non-goals

Browser does not own the virtual-node vocabulary, mounted diff engine, component activation,
application composition dependencies, routing, server serialization, or scoped-CSS rewriting.
Routing joins through Browser.Router. Generated scope identifiers arrive as ordinary attributes;
host-driven scope stamping and reactive style-variable application remain absent (`[STY-1]`,
`[STY-6]`).
