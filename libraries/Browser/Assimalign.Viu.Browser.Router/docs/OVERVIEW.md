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

Web history passes the complete path, query, and fragment to Router, which matches only
`RouteLocation.Path`. For example, `/guide/quick-start?mode=full#reactivity` resolves the exact
`/guide/quick-start` record, exposes `RawQuery` as `mode=full`, and exposes `Fragment` as `reactivity`.
`Query` provides decoded, ordinal name/value access while `FullPath` and hrefs preserve the raw
suffix. Push, replace, back, and forward retain the complete location in the existing string state
links. Empty `?` and `#` delimiters are preserved too (`[RTR-3]`, `[RTR-12]`,
[V01.01.08.09](https://github.com/assimalign/viu/issues/365)).

Hash history treats the text after its hash base as that same complete location:
`#/guide?mode=full#reactivity` resolves path `/guide`, query `mode=full`, and fragment `reactivity`.
The document query before `#/` remains part of the host URL, not the route query.

A host can opt into fragment scrolling through the unchanged `ScrollBehavior` callback:

```csharp
router.ScrollBehavior = (to, from, savedPosition) => Task.FromResult<ScrollTarget?>(
    savedPosition is { } position
        ? new ScrollTarget(position)
        : to.Fragment.Length > 0
            ? new ScrollTarget("#" + to.Fragment)
            : null);
```

This example assumes fragments are valid CSS identifier text. Fragments remain raw and are not
percent-decoded; hosts choosing arbitrary element identifiers must decode and escape their selector
as appropriate. Browser.Router applies the returned selector after rendering; it adds no implicit
fragment scrolling. A fragment-only navigation still runs Router's guards and after-hooks and
updates the location; its unchanged matched chain does not re-enter records (`[RTR-9]`, `[RTR-12]`).

`RouterLinkDomBridge` converts `BrowserEvent` metadata into the host-neutral
`RouterLinkClickEvent`. If RouterLink prevents default navigation, the bridge propagates that result
to the live browser event. Browser retains DOM event decoding; Router retains navigation policy.

The caller owns the `Router` and every application composition dependency. Browser.Router borrows
them and does not dispose them (`[CMP-9]`, `[APP-6]`). Browser.Router owns the history web asset and
its generated export surface; Router retains no browser reference (`[RTR-10]`).

This package owns no route matching, component registration, component activation, or renderer
state. Its history and scroll bindings use source-generated, primitive-only interop and introduce no
reflection, runtime activation, or dynamic code generation.
