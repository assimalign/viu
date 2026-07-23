# Assimalign.Viu.Browser

`Assimalign.Viu.Browser` is the browser host for the redesigned Viu runtime. It supplies
`RendererOptions<int>` over opaque DOM handles and layers selector-based mounting and JavaScript
module initialization on Core's platform-neutral `Application<int>`.

## Application composition

`BrowserApplicationBuilder` inherits the host-neutral builder. An application supplies its root
component tree, component factory, service provider, and optional state registry independently:

```csharp
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

BrowserApplicationBuilder builder =
    BrowserApplication.CreateBuilder(
        ComponentTree.Template<ApplicationRoot>());

builder.UseComponentFactory(components);
builder.UseServiceProvider(services);
builder.UseStateRegistry(state);

BrowserApplication application = builder.Build();
await application.MountAsync("#app");
```

The application borrows the supplied factory, provider, and state registry. Browser wraps the
component factory with AOT-safe `Transition`, `TransitionGroup`, and Core `BaseTransition`
resolution, then delegates every application component request to the supplied factory. The wrapper is not an
`IServiceProvider`; Browser neither creates nor disposes an application dependency-injection
container. Component-tree provide/inject APIs are not part of the redesign.

`IApplication` and `IApplicationContext` stay free of browser types. A future WebView2 host can
derive its own application from `Application<TNode>` and supply another `RendererOptions<TNode>`
without referencing this assembly.

## Implemented browser behavior

- Direct DOM operations through one integer-handle JavaScript bridge.
- Optional binary command buffering for mount, explicit render boundaries, and unmount.
- Browser property versus attribute selection, class/style handling, and invoker-based events.
- Synchronous and Task-returning DOM event handlers. Modifier and key wrappers preserve returned
  Tasks, and dispatch observes asynchronous faults through the application error handler.
- Application-default browser directive resolution for `v-show` and text, checkbox, radio, select,
  and runtime-dynamic `v-model`. The directives use the same direct or buffered property/event
  paths as normal attributes and preserve current/previous immutable binding values across updates.
- Unified Components trees, template roots, scoped-style identifiers, fragments, static content,
  keyed updates, and teleport target resolution.
- Browser hydration through one batched subtree snapshot. Matching server nodes are adopted without
  clearing the container; Core performs localized mismatch recovery.
- Generated single-file-component CSS variables through an explicit
  `CssVariables.UseCssVariables(context, getter)` setup call. Reactive changes update every current
  outermost element without rerendering the component, including fragment roots.
- The single-child `<Transition>` built-in, including CSS class choreography, explicit durations,
  appear, cancellation, modes supplied by Core `BaseTransition`, persisted `v-show`, and direct or
  command-buffered sequencing.
- The keyed-list `<TransitionGroup>` built-in, including shared enter/leave state, optional wrapper
  elements, batched FLIP position reads, move transforms and classes, interrupted move cleanup, and
  direct or command-buffered sequencing.
- Low-level bridge initialization, selector resolution, and handle-registry diagnostics.

`TransitionGroup` uses `ComponentTransitionScope` to share host-neutral transition state across its
keyed children. `ComponentHost.GetKeyedChildElements<int>` supplies ordered child-key-to-host-handle
snapshots during before-update and updated lifecycle phases. Browser retains all rectangle
batching, transform writes, reflow, move classes, and move-end listeners in
`DomTransitionOperations`; Core remains free of DOM concepts.

Command-buffered mount, direct render, reactive component rerender, lifecycle-hook writes, and
unmount all commit through `RendererOptions<int>.Commit`. Core drains the callback before and after
post-render hooks, so `Scheduler.NextTick` observes the applied DOM frame.
