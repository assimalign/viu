# Assimalign.Viu.Browser

Browser is Viu's DOM host. It supplies Core with `RendererOptions<int>`; the integer is an opaque
JavaScript node handle and zero is reserved as the no-node sentinel. The host owns HTML, SVG, MathML,
and `foreignObject` namespace selection, property-versus-attribute policy, events, form coercion,
class and style handling, directives, transitions, hydration snapshots, and deterministic handle
cleanup. Core remains host-neutral. This is the boundary specified by `[RND-HOST-1]`, `[EXE-11]`,
`[EXE-12]`, and `[HYD-2]`.

All renderer writes are encoded into a versioned binary command frame and applied by `viu-dom.js`
in one interop call per commit. Reads commit pending writes before consulting the live DOM. Managed
handle allocation advances past handles discovered through selectors and hydration snapshots, and
event handlers use delegate-swapping invokers. These guarantees are specified by `[RND-IO-1]`
through `[RND-IO-4]` and `[EXE-13]` through `[EXE-14]`.

`BrowserEvent` implements Components' portable `IElementEvent` contract. The invoker passes the
same `BrowserEvent` instance to portable handlers, so input value, keyboard key, checked state, and
multiple selections use the same authored handler shape as the in-memory Testing host. Concrete
`BrowserEvent` handlers remain available for browser-only modifier and response-intent behavior
(`[V01.01.11.06]`).

`BrowserApplication` uses Core's public `ApplicationLifetime` for the platform-invariant state
machine and keeps only Browser initialization, selector resolution, mounting, and hydration.
Accepted template and script metadata updates remain in Core:
affected component instances remount in the post-flush phase while the Browser document remains
loaded. The .NET watch host owns rebuild, restart, and browser refresh for rejected rude edits.
Browser directives are available through the default application directive resolver, and CSS
transitions consume the public transition-node contract. Top-level startup is asynchronous;
lower-level mount APIs deliberately bypass lifetime middleware as specified by `[APP-1]`, `[APP-2]`,
`[APP-6]`, and `[APP-7]`.

Scoped CSS was removed on 2026-09-14 ([V01.01.06.17], #367). Ordinary component stylesheets and
CSS Modules remain supported. The Browser `CssVariables.Bind` directive remains available; generated
reactive application for `v-bind()` in ordinary styles is deferred (`[STY-6]`–`[STY-8]`).

For a hydrating application with a composed state registry, Browser consumes and removes the single
`script[data-viu-state]` JSON island after bridge initialization and calls the registry's explicit
payload restore contract before resolving the mount target. Removal also keeps an island placed in
the mount container out of Core's hydration snapshot. Stores resolved earlier update immediately;
stores resolved during component setup receive their server state before first render
([HYD-8], [V01.01.09.03], [EXE-4]).

`BrowserStateStorage` supplies State's persistence plugin with `localStorage` or `sessionStorage`
through the existing browser module. Its constructor selects `StateStorageKind.Local` by default;
create a second instance with `StateStorageKind.Session` when composing both areas. Each operation
crosses interop once for the complete payload, and the plugin diagnoses rejected storage access or
writes. Normal application startup initializes the bridge before component setup; resolving a
persisted store earlier requires awaiting `BrowserRuntime.InitializeAsync()` first (`[STA-11]`,
[V01.01.09.04](https://github.com/assimalign/viu/issues/79)).

Browser also implements Core's deferred-hydration trigger seam. It maps idle requests to
`requestIdleCallback` with a timer fallback, visibility to `IntersectionObserver`, media conditions
to `matchMedia`, and interactions to marker-range capture listeners. Every registration is
cancelable; the first captured interaction is replayed asynchronously only after activation and the
scheduled host commit (`[HYD-LAZY-3]` through `[HYD-LAZY-5]`).

`BrowserCustomElements` embeds Viu components in ordinary HTML as autonomous custom elements
([V01.01.04.08](https://github.com/assimalign/viu/issues/46), `[CEL-1]` through `[CEL-8]`). Initialize
the shared bridge, give the owner the application's explicit component factory and optional services,
then register each component reference with a lowercase hyphenated tag:

```csharp
await BrowserRuntime.InitializeAsync();
var customElements = new BrowserCustomElements(componentFactory, services);
customElements.Define(ComponentReference.ForType(typeof(Counter)), "viu-counter");
```

Retain the owner for the embedding lifetime and dispose it during host shutdown. All definitions use
one exclusively active renderer host; dispose the owner before activating another Browser application
or renderer. The factory and services remain the external composition root's responsibility.
The host page still boots the packaged WASM application with `index.html` and `main.js`; it can
declare `<viu-counter count="3">` before initialization completes. No component-specific JavaScript
is generated.

Generated `ComponentParameter` metadata supplies observed attributes and canonical JavaScript
properties. `initialCount` maps to `initial-count` by default; set
`CustomElementOptions.AttributeNameMapper` to supply a different mapping, or return an empty name to
disable a parameter's attribute. Definition rejects a canonical parameter name that collides with
a native `HTMLElement` member or custom-element lifecycle callback. Boolean attributes use
presence/absence and the literal `false`; numeric attributes parse with invariant culture and retain
the previous value on invalid input; removing a numeric or string attribute restores the component's
declared default; strings pass through verbatim. Properties accept typed values:
`element.count = 4` is a numeric input, while `element.count = "4"` is not. A batch of changes schedules
one component update. Emitted events become bubbling, composed `CustomEvent` objects whose `detail`
is the ordered argument array. Unsupported managed argument values become null with a warning;
there is no reflection-based object serialization (`[CEL-3]` through `[CEL-5]`).

Shadow roots are enabled by default. They adopt the host page's bundled `.viu.css` stylesheets,
sharing constructed sheets per document with cloned `<link>` elements as the fallback. Component
slots supply native `<slot>` elements while consumer-owned direct text or element children target
them; removing the final slottable restores the component's authored fallback content. The slot set
updates when direct light-DOM children are added or removed or their `slot` attributes change. Set
`UseShadowRoot = false` to render in the host element's light DOM using document styles; native slot
projection is unavailable in that mode. Disconnect unmounts and releases handles and listeners;
reconnect creates a fresh component instance (`[CEL-6]` through `[CEL-8]`). The packaged consumer at
`scripts/fixtures/EndToEndCustomElementApp` demonstrates attributes, properties, styles, slots and
fallbacks, events, and registry cleanup.
