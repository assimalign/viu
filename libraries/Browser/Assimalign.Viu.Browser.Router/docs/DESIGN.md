# Assimalign.Viu.Browser.Router design

## Why a leaf integration package

Router's matching, guards, history state, and route components must remain usable without Browser.
Browser's renderer and event system must remain usable without Router. Browser.Router is the one
downstream assembly allowed to know both packages, plus Core's public application middleware
contract (`[RTR-1]`, `[RTR-7]`).

This direction prevents host details from entering `RouterLink` and prevents navigation concepts
from entering Browser's general event machinery. No friend access crosses the boundary: Router
detects the public `IInitializableRouterHistory` and `IRouterScrollController` capabilities, while
the application middleware calls Router's public initial-scroll completion signal. Integration uses
only reviewed public event, history-capability, and application seams (`[CMP-33]`, `[RTR-7]`).

## Application-lifetime installation

`UseRouter` registers middleware on an already-built `IApplication`. The middleware installs the
link bridge, awaits router readiness, invokes the inner delegate, and removes the bridge in
`finally`. Awaiting readiness before the inner delegate ensures the initial location is resolved
before routed content mounts. Keeping the inner delegate pending ensures the bridge survives for the
entire live application, including unmount (`[RTR-3]`, `[APP-4]`).

The application stopping token is passed to readiness, so shutdown can cancel startup without
inventing a second lifetime signal. Cleanup remains ordinary reverse-order middleware cleanup,
including failure paths (`[APP-5]`). `UseRouter` does not add services or registrations: composition
has already been frozen, and Router must already be available to `RouterView` and `RouterLink`
through nullable services (`[APP-2]`, `[RTR-4]`).

## Browser event translation

RouterLink's event value contains only navigation-relevant click facts: button, modifiers, and
prevention state. `RouterLinkDomBridge` maps those fields from `BrowserEvent`, invokes Router's
host-neutral binding, and propagates a newly prevented result to Browser. The bridge does not match
routes or decide whether a click should navigate; RouterLink owns that policy, including its
fallthrough `target="_blank"` check.

The bridge installs one process-local adapter for the single-threaded browser runtime. Middleware
brackets that installation with `try`/`finally`; the direct `Install` and `Uninstall` methods exist
only for lower-level embedding, and `Uninstall` leaves a different installed adapter untouched.

## Ownership and AOT

The router is borrowed. The middleware neither disposes it nor takes ownership of the application,
service provider, component factory, or state registry (`[CMP-9]`, `[APP-6]`). The package adds no
serialization or activation path and uses only statically bound public APIs, preserving trimming and
WASM AOT compatibility (`[EXE-4]`).

The browser history owns path normalization and flat state construction through local internal
helpers; it does not consume Router's internal memory-history machinery. The JavaScript edge captures
the page's `history.scrollRestoration` policy when the first history subscribes, holds it at
`manual` while any subscription remains, and restores the captured value only after the last history
disposes. That prevents native restoration from racing or corrupting Viu's saved-position ledger.

A confirmed navigation always reaches the scroll capability, even when its current behavior is
null. Browser.Router clears any deferred initial request before processing that newer confirmation,
so a pre-mount redirect cannot replay the initial route's target after the redirected view mounts
(`[RTR-9]`, `[RTR-10]`).

## Complete browser locations

Browser.Router composes browser URL components and strips the configured history base; Router owns
the path/query/fragment model and matches only its path. Query parsing, decoded accessors, location
equality, duplicate classification, and link-class rules therefore stay host-free (`[RTR-12]`,
[V01.01.08.09](https://github.com/assimalign/viu/issues/365)). For web history, the route suffix is
the document query and fragment. For hash history, the complete route is inside the outer fragment,
so `#/guide?mode=full#reactivity` retains its inner query and fragment; the document query outside
that hash base is unrelated to the route.

The JavaScript edge reads the serialized suffix from `location.href` because the browser's
`location.search` and `location.hash` getters erase present-but-empty delimiters. The first `?`
before the first `#` starts the query, and a `?` after `#` is fragment text. The
[WHATWG URL Standard](https://url.spec.whatwg.org/#url-parsing) is the external URL format for that
separation. It does not define Viu's navigation semantics. The existing fourteen-string snapshot
and primitive pop-state dispatch stay the same shape: only the raw search and hash strings gain
preserved empty delimiters. `RouterHistoryState.Back`, `Current`, and `Forward` carry complete
serialized locations; no parsed query collections cross interop (`[RTR-3]`).

Fragment scrolling remains host policy. `ScrollBehavior` receives the complete destination and can
return `new ScrollTarget("#" + to.Fragment)` for a fragment suitable as CSS identifier text, or
derive another target after its own decoding/escaping policy. Browser.Router performs that selector
lookup and one scroll write after rendering. A null callback or null target causes no scroll;
query-only and fragment-only navigation does not introduce an implicit effect (`[RTR-9]`).

## Non-goals

Browser.Router does not own matching, guard order, router registration, DOM rendering, application
state transitions, or dependency disposal. It does own the browser-only `viu-history.js`, history
marshalling, saved-position bookkeeping, and scroll application because those effects cannot enter
the host-free Router package (`[RTR-9]`, `[RTR-10]`).
