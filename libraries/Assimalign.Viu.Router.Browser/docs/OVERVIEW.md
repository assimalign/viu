# Assimalign.Viu.Router.Browser — overview

The browser integration layer between `Assimalign.Viu.Router` and `Assimalign.Viu.Browser`: the
one small adapter that lets `RouterLink` intercept a real DOM click and navigate client-side.
`RouterLink` is renderer-agnostic — it renders through the node-ops abstraction and never references
the DOM adapter — and `Browser` stays Router-agnostic, so the DOM-event-to-router mapping cannot live
in either package it bridges. Specified by
[`[RTR-7]`](../../../docs/SPECIFICATION.md#12-routing).

## What it provides

- **`UseRouter(router)`** — application middleware that installs the DOM bridge, awaits the router's
  cancellable initial navigation, enters the application terminal, and removes the bridge after
  unmount or failure. The caller retains ownership of the router.
- **`RouterLinkDomBridge`** — `Install()`/`Uninstall()` wire the bridge into the DOM event system
  (`BrowserObjectEvents.Invoker`). `Invoke` converts the dispatched `BrowserEvent`'s click metadata
  (mouse button, the Control/Shift/Alt/Meta modifiers, and the arrival-time `defaultPrevented`) into
  the DOM-free `RouterLinkClickEvent` the link's guard reads, then propagates the guard's
  `PreventDefault` decision back onto the live event so the browser's full page load is suppressed.

## Using it

```csharp
await Application
    .CreateBuilder()
    .AddRootComponent(ComponentTree.Template<AppRoot>())
    .AddComponentFactory(components)
    .AddServiceProvider(services)
    .Build()
    .UseRouter(router)
    .RunAsync();
```

`RouterHistory.CreateWeb()` and `CreateWebHash()` are lazy. `UseRouter` reaches
`Router.ReadyAsync(execution.Stopping)`, which initializes the history bridge before initial
navigation; ordinary bootstrap no longer calls `RouterHistory.InitializeAsync()` first [RTR-3].

Only browser apps that use the Router need this package — it is not part of the base
`Assimalign.Viu.App` framework, so a non-router app never pays for it.

## Boundaries

- References both `Assimalign.Viu.Router` and `Assimalign.Viu.Browser`; nothing references it back.
- `Browser` stays Router-agnostic and `Router` stays DOM-free (pinned by
  `RouterAssembly_DoesNotReferenceTheBrowserDomAdapter`); this package is the only place the two meet.
- Real-browser click behavior is exercised by the end-to-end harness ([V01.01.11.03], #87); the unit
  tests here pin the mapping and the prevent-default plumbing DOM-free.

See [DESIGN.md](DESIGN.md) for why it is a separate package and how the event seam works.
