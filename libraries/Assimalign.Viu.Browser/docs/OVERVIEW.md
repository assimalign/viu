# Assimalign.Viu.Browser

`Assimalign.Viu.Browser` is the browser host for the redesigned Viu runtime. It supplies
`RendererOptions<int>` over opaque DOM handles and layers selector-based mounting and JavaScript
module initialization on Core's host-neutral `IApplication` contract. `BrowserApplication`
implements that contract directly, owns its mount APIs, and targets `#app` by default.

## Application composition

`BrowserApplicationBuilder` is constructed directly and implements the lean `IApplicationBuilder`
contract. `ApplicationOptions` is the one composition surface for the root component tree,
component factory, service provider, optional state registry and directives, and diagnostics:

```csharp
using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = ComponentTree.Template<ApplicationRoot>();
        options.Components = components;
        options.Services = services;
        options.State = state;
        options.WarnHandler = RecordWarning;
        options.ErrorHandler = RecordError;
    })
    .Build()
    .Use(async (context, next) =>
    {
        await RestoreSessionAsync(context.Stopping);
        await next(context);
    })
    .RunAsync();
```

The builder snapshots the options at `Build()`, so the built context cannot be recomposed. The
application borrows the supplied factory, provider, and state registry. Browser wraps the
component factory with AOT-safe `Transition`, `TransitionGroup`, and Core `BaseTransition`
resolution, then delegates every application component request to the supplied factory. The wrapper is not an
`IServiceProvider`; Browser neither creates nor disposes an application dependency-injection
container. Component-tree provide/inject APIs are not part of the redesign.

`IApplication.StartAsync` launches the middleware pipeline independently and returns after the
Browser terminal has initialized the bridge, resolved `#app`, and mounted. `StopAsync` signals the
context's `Stopping` token, awaits that pipeline, and unmounts before middleware cleanup unwinds.
Core's `RunAsync` extension performs start, waits for shutdown, and then stops, so it remains pending
for the complete mounted lifetime. `Mount` and `MountAsync` are Browser-owned lower-level embedding
and testing APIs; they bypass top-level lifetime middleware [APP-7].

`IApplication` and `IApplicationContext` stay free of browser types. A future WebView2 host can
implement `IApplication` directly, own its mount surface, and supply another
`RendererOptions<TNode>` without referencing this assembly.

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
