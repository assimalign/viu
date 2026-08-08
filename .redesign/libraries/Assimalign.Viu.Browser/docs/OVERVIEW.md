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
