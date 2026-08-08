# Assimalign.Viu.Browser.Router

Browser.Router is the leaf integration between Viu's Browser host and Router. It references Core,
Browser, and Router, while those packages remain independent of this assembly. The package has two
responsibilities: install a router for the full browser-application lifetime and translate browser
click events for `RouterLink` (`[RTR-4]`, `[RTR-7]`).

`UseRouter(IApplication, Router)` adds application middleware. Before the inner browser pipeline
mounts, the middleware installs the link-event bridge and awaits `Router.ReadyAsync` using the
application stopping token. After the inner pipeline finishes, a `finally` block removes the bridge.
The bridge therefore remains active from startup through unmount and is removed in reverse
middleware order (`[APP-4]`, `[APP-5]`).

`RouterLinkDomBridge` converts `BrowserEvent` metadata into the host-neutral
`RouterLinkClickEvent`. If RouterLink prevents default navigation, the bridge propagates that result
to the live browser event. Browser retains DOM event decoding; Router retains navigation policy.

The caller owns the `Router` and every application composition dependency. Browser.Router borrows
them and does not dispose them (`[CMP-9]`, `[APP-6]`). Web and hash history assets remain in the
Router package because their JavaScript module calls Router's generated export surface (`[RTR-3]`).

This package owns no route matching, component registration, component activation, renderer state,
DOM command protocol, or JavaScript history module. It introduces no reflection, runtime activation,
or dynamic code generation.
