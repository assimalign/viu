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

`BrowserApplication` uses Core's public `ApplicationLifetime` for the platform-invariant state
machine and keeps only Browser initialization, selector resolution, mounting, hydration, and
full-page hot-reload signaling. Browser directives are available through the default application
directive resolver, and CSS transitions consume the public transition-node contract. Top-level
startup is asynchronous; lower-level mount APIs deliberately bypass lifetime middleware as specified
by `[APP-1]`, `[APP-2]`, `[APP-6]`, and `[APP-7]`.

Scoped-style identifiers need no Browser-specific path: compiled virtual trees carry the stable
`data-v-*` value as an ordinary attribute, so the same buffered binding operation used for authored
attributes applies it (`[STY-1]`).

For a hydrating application with a composed state registry, Browser consumes and removes the single
`script[data-viu-state]` JSON island after bridge initialization and calls the registry's explicit
payload restore contract before resolving the mount target. Removal also keeps an island placed in
the mount container out of Core's hydration snapshot. Stores resolved earlier update immediately;
stores resolved during component setup receive their server state before first render
([HYD-8], [V01.01.09.03], [EXE-4]).

Browser also implements Core's deferred-hydration trigger seam. It maps idle requests to
`requestIdleCallback` with a timer fallback, visibility to `IntersectionObserver`, media conditions
to `matchMedia`, and interactions to marker-range capture listeners. Every registration is
cancelable; the first captured interaction is replayed asynchronously only after activation and the
scheduled host commit (`[HYD-LAZY-3]` through `[HYD-LAZY-5]`).
