# Assimalign.Viu.Browser.Router

Browser.Router is the leaf integration between Viu's Browser host and Router. It references Core,
Browser, and Router, while those packages remain independent of this assembly. The package has three
responsibilities: install a router for the full browser-application lifetime, translate browser
click events for `RouterLink`, and provide web/hash history plus post-render scrolling (`[RTR-4]`,
`[RTR-9]`, `[RTR-10]`).

`UseRouter(IApplication, Router)` adds application middleware. Before the inner browser pipeline
mounts, the middleware installs the link-event bridge and awaits `Router.ReadyAsync` using the
application stopping token. After the inner pipeline finishes, a `finally` block removes the bridge.
The bridge therefore remains active from startup through unmount and is removed in reverse
middleware order (`[APP-4]`, `[APP-5]`).

`BrowserRouterHistory` creates deferred web and hash histories. Its packaged module batches the
leaving `(x,y)` capture with a push transition, keys saved offsets by history position for pop
navigation, and applies an absolute or selector-derived `ScrollTarget` in one interop call. The
controller waits for `Scheduler.NextTickAsync`; initial navigation is held until `UseRouter` observes
the mounted application, and every newer confirmed navigation invalidates older deferred work. The
module holds native scroll restoration at `manual` while any history subscription is active and
restores the page's prior policy after the last one disposes. An asynchronous `ScrollBehavior` may
delay longer (`[RTR-9]`, `[RTR-10]`).

`RouterLinkDomBridge` converts `BrowserEvent` metadata into the host-neutral
`RouterLinkClickEvent`. If RouterLink prevents default navigation, the bridge propagates that result
to the live browser event. Browser retains DOM event decoding; Router retains navigation policy.

The caller owns the `Router` and every application composition dependency. Browser.Router borrows
them and does not dispose them (`[CMP-9]`, `[APP-6]`). Browser.Router owns the history web asset and
its generated export surface; Router retains no browser reference (`[RTR-10]`).

This package owns no route matching, component registration, component activation, or renderer
state. Its history and scroll bindings use source-generated, primitive-only interop and introduce no
reflection, runtime activation, or dynamic code generation.
