# Browser host design

## Boundary

Browser is a host adapter, not an application or component model. Core owns component activation,
lifecycle, reactivity scheduling, tree diffing, and mounted runtime state. Components owns the
immutable public tree. Browser owns only browser-specific operations:

```text
IComponent tree
    -> Core Renderer<int>
        -> Browser RendererOptions<int>
            -> direct JS bridge or binary command buffer
```

The integer renderer node is an opaque browser handle. Core never resolves CSS selectors, imports a
JavaScript module, or understands DOM attributes. Conversely, Browser does not activate templates
or own the application component factory, service provider, or state registry.

This keeps `Application<TNode>` reusable for a future WebView2 application. WebView2 may choose a
different node handle and transport while reusing the same Core and Components APIs.

## Application lifecycle

`BrowserApplication` derives from `Application<int>` and receives an immutable
`IApplicationContext`. `MountAsync(string)` initializes the JavaScript bridge, resolves the selector,
then enters the generic application mount path. `MountCore` clears a client-mount container and calls
`Renderer<int>.Render(root, container, context)`. `UnmountCore` renders null into the same container.

The application composition root retains ownership of all supplied resolvers. Browser wraps the
application's `IComponentFactory` only to resolve `Transition`, `TransitionGroup`, and Core
`BaseTransition` by type or generated name before delegating all other requests. This wrapper does not implement
`IServiceProvider`. Browser installs no custom dependency-injection stack and exposes no
provide/inject API.

## DOM operations

`BrowserNodeOperations` maps Core operations to the bridge:

- create, insert, move, remove, and text operations;
- parent and next-sibling reads;
- browser property/attribute/event patching;
- static-content insertion;
- scoped-style attribute stamping; and
- teleport selector resolution.

The JavaScript boundary remains explicit and trimming safe. There is no reflection-based
activation, dynamic code generation, or retained `JSObject` per node/event.

## Hydration

`CreateServerRendererBuilder` selects Core's `Renderer<int>.Hydrate` path and deliberately does not
clear the mount container. Direct and buffered options create a `BrowserHydrationReader` from one
`snapshotHydration` bridge call per root or teleport target. All structural, kind, text, and
attribute reads then stay in managed memory.

Buffered hydration advances its managed handle allocator beyond the maximum handle in each
snapshot. A localized mismatch can therefore create replacement nodes without colliding with any
server node registered by the bridge.

## Event tasks

DOM dispatch must return stop/prevent flags synchronously to JavaScript. A Task-returning event
handler is therefore invoked synchronously up to its first suspension; its returned Task is retained
and observed without blocking the JavaScript event callback. Synchronous and asynchronous failures
flow to the application error handler.

`DomRenderHelpers._withModifiers` and `_withKeys` have dedicated `Func<Task>` and
`Func<BrowserEvent, Task>` overloads. A passing guard returns the original handler Task; a failing
guard returns `Task.CompletedTask`. Multicast Task-returning delegates are expanded so every returned
Task is observed.

An asynchronous continuation cannot call `preventDefault` after the JavaScript dispatch has
returned. Developers must express `.prevent` or other synchronous event intent in the generated
guard, before awaiting.

## Command buffering

Buffered operations allocate handles in managed code and serialize DOM writes into one binary
frame. Reads force pending writes to commit before querying live DOM. Selector-based teleport
resolution also records the returned foreign handle so later managed allocation cannot collide.

Buffered Browser supplies `ApplyPending` as `RendererOptions<int>.Commit`. Core queues that
application-scoped callback for synchronous renders and reactive component updates, then drains
host commits:

1. runs before post-render lifecycle callbacks that read DOM;
2. runs after those callbacks if they enqueue additional host writes;
3. participates in `NextTick`.

The callback belongs to each renderer rather than a process-global Browser hook. A future WebView2
transport can supply its own `RendererOptions<TNode>` implementation without changing Core's
application contract.

## Browser directives

The Browser builder installs its built-in directive resolver by default. Developers may replace it
through the host-neutral `UseDirectiveResolver` builder method. The default resolver currently
maps compiler-emitted names for:

- `show` to `VShow`;
- `modelText` to `VModelText`;
- `modelCheckbox` to `VModelCheckbox`; and
- `modelRadio` to `VModelRadio`;
- `modelSelect` to `VModelSelect`; and
- `modelDynamic` to `VModelDynamic`.

Directive hooks receive the boxed integer host handle, immutable current element component, and
current/previous binding values from Core. Direct mode writes through bridge-backed property
leaves. Buffered mode writes through the command buffer and uses the renderer commit callback, so
mounted and updated hooks observe and extend the same render boundary.

Select model bindings use `DirectiveBinding.GetDescendantElements("option")` to traverse the
mounted descendants of the bound select in document order, including fragments and child
templates. Each result carries both the immutable option component and mounted browser handle, so
raw bound object values retain identity while selected writes target the correct host option.
Dynamic model bindings forward each lifecycle hook according to the current tag and input type.

## Component CSS variables

The single-file-component generator emits
`CssVariables.UseCssVariables(Context, getter)` from the generated setup path. The explicit
`IComponentContext` makes ownership unambiguous and avoids restoring the former ambient component
instance.

After mount, a post-flush watcher tracks the getter's reactive dependencies and applies each hashed
custom property to every current outermost host element reported by `ComponentHost`. The updated
lifecycle hook reapplies the same values when a component changes its element or fragment roots.
Before unmount, the component stops the watcher.

Direct mode batches all properties for one element into one bridge operation. Buffered mode writes
the properties into the command frame, then asks the owning component context to queue its
renderer-specific host commit. This guarantees that a CSS-only reactive update reaches the DOM
before `NextTick`, even though it does not rerender the component.

## Transitions

Browser `Transition` is an `IComponentTemplate` that consumes the DOM transition arguments and
renders Core `BaseTransition` with resolved `BaseTransitionProperties`. Core owns transition
identity, cancellation, mode sequencing, insertion, and deferred removal. Browser owns class
names, double-animation-frame scheduling, reflow, computed or explicit end timing, and browser
element handles.

For a persisted transition, Core binds a host-neutral `ComponentTransition` to directive bindings.
`VShow` invokes `BeforeEnter` and `Enter` before revealing an element, or invokes `Leave` and applies
`display: none` only in its completion callback. The renderer skips its own insertion/removal
transition for persisted hooks, so the directive and renderer never both drive the same phase.

Direct mode installs bridge-backed `DomTransitionOperations`. Buffered mode replaces that ambient
surface with an adapter: class and transform writes plus the forced-reflow barrier are command
opcodes, while timing and layout reads flush first and delegate to the bridge. Next-frame and end
callbacks commit the writes they produce. The enter-from/active frame and enter-to frame therefore
remain distinct, and the leave reflow stays ordered between leave-from and leave-active inside one
command frame.

`TransitionGroup` creates one `ComponentTransitionScope`, attaches its resolved transition hooks to
every keyed direct child, and uses that shared scope to finish pending enters before measuring a
move. Core's `ComponentHost.GetKeyedChildElements<int>` supplies ordered child-key-to-first-host-
element snapshots: before-update observes the outgoing mounted tree, and updated observes the
patched incoming tree. Browser maps the snapshots by key and performs the FLIP pass as:

1. measure all outgoing positions in one operation;
2. finish pending move and enter callbacks;
3. measure all incoming positions in one operation;
4. write all inverse transforms;
5. force one reflow; and
6. add the move class, clear inverse styles, and register move-end cleanup.

The same sequence runs through direct bridge operations or buffered opcodes. Core carries only
opaque host elements and transition callbacks, never CSS, rectangles, transforms, or DOM handles.

Teleport, CSS variables, and all model directives are no longer deferred: Core supplies teleport
target resolution, component root access, context-bound host commit scheduling, and directive
descendant-host lookup without introducing browser types.
