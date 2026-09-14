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

`BrowserApplication` owns browser initialization, mount-target resolution, and mount or hydration.
Core remounts affected component instances in the post-flush phase for accepted template and script
metadata updates; Browser retains the current document so the applied managed delta remains loaded.
The .NET watch host owns rebuild, restart, and browser refresh for rude edits rejected by metadata
update. Core's `ApplicationLifetime` owns the host-independent state machine, middleware nesting,
shutdown signal, and failure reporting (`[APP-1]` through `[APP-5]`).

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

## State storage

`BrowserStateStorage` implements State's `IStateStorage` for the local or session area selected at
construction. The existing `viu-dom.js` module exposes three primitive operations; each read, write,
or removal is one interop crossing over an entire payload. No storage handles or event listeners
are retained. The adapter preserves missing-key null results and native quota/security failures
from [WHATWG Web Storage](https://html.spec.whatwg.org/multipage/webstorage.html); the State
persistence plugin catches failures and reports its diagnostic callback (`[STA-11]`,
[V01.01.09.04](https://github.com/assimalign/viu/issues/79)).

Storage operations require the initialized Browser bridge. Normal component setup follows
application initialization; hosts resolving persisted stores earlier must first await
`BrowserRuntime.InitializeAsync()`. Construction itself never accesses storage. State owns
serialization, JSON member filtering, restoration, and pre-flush write coalescing; Browser owns
only access to the selected storage area. Cross-tab synchronization is outside this contract.

## Custom elements

`BrowserCustomElements` is an explicit, disposable embedding owner with one renderer host and a
borrowed component factory and service provider. It holds Browser's exclusive host activation lease,
so every definition shares the active host and another Browser application or renderer waits for
owner disposal. It defines autonomous elements through one
parameterized `HTMLElement` factory in `viu-dom.js` ([V01.01.04.08](https://github.com/assimalign/viu/issues/46),
`[CEL-1]`, `[CEL-2]`). The external compatibility targets are the
[WHATWG HTML custom element lifecycle](https://html.spec.whatwg.org/multipage/custom-elements.html)
and [WHATWG DOM shadow trees, slots, and events](https://dom.spec.whatwg.org/). Viu owns parameter
conversion, mount scheduling, and resource ownership.

The browser reports connection, disconnection, attribute changes, and adoption into a shared queue.
The queue waits for the managed dispatcher, then forwards complete batches; managed code resolves
inputs and schedules rendering through Core. This permits ordinary HTML to contain elements before
WASM initialization and avoids a renderer per callback or per tag. An application-wide event observer
dispatches root-component emissions from their host elements, preserving the exact emitted name and
ordered argument list, including emissions during setup (`[CEL-2]`, `[CEL-5]`). Embedding intentionally
uses the public lower-level mount lifetime; application middleware does not wrap each element.

Registration reads `ComponentRegistration.Contract.Parameters`, including the declared CLR type
tokens generated at build time. That existing public seam makes a new component metadata registry,
runtime property inspection, and per-component JavaScript generation unnecessary. Kebab-case
attribute aliases are computed once and passed to `observedAttributes`. Canonical JavaScript
properties accept typed inputs without parsing strings. Names already present on `HTMLElement` or
reserved for custom-element lifecycle callbacks are rejected before an accessor could shadow the
native bridge surface. After each managed input batch, every resolved parameter refreshes the
synchronous property-getter cache, including values supplied by attributes and component defaults.
The separate input-source map canonicalizes only retained property inputs, so publishing an attribute
or default does not make it persist as a property input across a fresh mount. Numeric normalization
handles JavaScript's single number representation explicitly; unsupported managed values are never
serialized by reflection. Event payloads preserve primitives and opaque JavaScript object identity, substituting
null with a warning for unsupported CLR objects (`[CEL-3]` through `[CEL-5]`, `[EXE-4]`).

Shadow styles reuse the Browser SDK's bundled application and referenced-component `.viu.css`
links. Constructed stylesheets are shared once per owner document and adopted into each root;
cloned stylesheet links provide the loading-time and failure fallback (`[CEL-6]`, `[PKG-4]`).
Styles containing imports or relative resource URLs retain their clones so the browser resolves
dependencies against the stylesheet URL. A shared observer refreshes roots when the document's
stylesheet links change, including the Browser SDK's CSS hot-reload URL updates, and disconnects
when the last custom-element root leaves. Links marked `data-viu-stylesheet` permit explicit
inclusion when an application has customized its bundle URL.

This deliberately chooses the
application bundle as the stylesheet boundary: it keeps ordinary component CSS, CSS Modules, and
packaged component libraries on the same build path. Runtime extraction of one component's CSS
would require an additional generated dependency manifest and could omit selectors shared by its
children. A separate per-component style registry would duplicate the bundler's authority. Copying
CSS text into every root would repeat parsing and retain one copy per element. Relying only on
document links would leave shadow content unstyled. Removed scoped CSS is not an alternative
isolation mechanism (`[STY-1]`); the shadow root supplies the boundary.

In shadow mode, invocation slot delegates render native default and named `<slot>` nodes. A direct
text node or direct element with an absent or empty `slot` attribute supplies the default slot;
nonempty `slot` attributes on direct elements supply named slots. When no direct slottable targets
an outlet, Browser omits that invocation slot so the component's authored fallback content renders.
One per-instance observer coalesces direct-child and `slot` attribute changes and updates the
immutable slot set through the scheduler. The DOM
owns assignment; light-DOM children remain with the consumer, so Browser never clones or reparents
them into its managed subtree. This avoids inventing a second child-ownership model or translating
consumer HTML into virtual nodes. Slot arguments cannot establish reactive bindings on consumer
DOM. Light-DOM rendering supplies no projection, because native slot distribution requires a shadow
tree (`[CEL-7]`, `[CMP-18]`, `[CMP-19]`).

Disconnection unmounts through Core and releases bridge registrations, renderer handles, and both
sides of listener storage. Reconnection starts a fresh component lifetime; preserving a detached
instance was rejected because it would retain component effects and services with no mounted host.
Disposing the owner releases all remaining mounts and its renderer activation while leaving the
borrowed composition dependencies with their owner. The platform retains native definitions for the
document lifetime; disposal cannot unregister a tag. Registry diagnostics check the resource
boundary independently from visible DOM removal (`[CEL-8]`, `[EXE-14]`, `[CMP-9]`, `[CMP-10]`).

## AOT and WASM constraints

All JavaScript entry points are statically declared `JSImport` or `JSExport` boundaries, and payloads
are primitive handles, snapshots, or command frames. Browser does not use reflection-based
serialization, runtime member discovery, emitted code, or dynamically generated delegates
(`[EXE-4]`).

## Non-goals

Browser does not own the virtual-node vocabulary, mounted diff engine, component activation,
application composition dependencies, routing, or server serialization. Routing joins through
Browser.Router. Scoped CSS was removed on 2026-09-14 ([V01.01.06.17], #367). The single-element
`CssVariables.Bind` directive stays supported; generated reactive `v-bind()` application for ordinary
component styles remains deferred (`[STY-6]`–`[STY-8]`).
