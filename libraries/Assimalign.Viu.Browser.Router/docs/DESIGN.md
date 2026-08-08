# Assimalign.Viu.Browser.Router design

## Why a leaf integration package

Router's matching, guards, history state, and route components must remain usable without Browser.
Browser's renderer and event system must remain usable without Router. Browser.Router is the one
downstream assembly allowed to know both packages, plus Core's public application middleware
contract (`[RTR-1]`, `[RTR-7]`).

This direction prevents host details from entering `RouterLink` and prevents navigation concepts
from entering Browser's general event machinery. No friend access or capability cast crosses the
boundary; integration uses public event and application seams (`[CMP-33]`).

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

## Non-goals

Browser.Router does not own `viu-history.js`, history marshalling, matching, guard order, router
registration, DOM rendering, application state transitions, or dependency disposal. It is only the
application-lifetime and event-translation join between the two independent packages.
