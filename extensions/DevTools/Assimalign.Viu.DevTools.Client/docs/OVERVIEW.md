# DevTools inspection client

`Assimalign.Viu.DevTools.Client` is the Viu application component library for
[V01.01.10.03](https://github.com/assimalign/viu/issues/83). Its five `.viu` components render the
version 1 inspection protocol. The client receives component identities, snapshot values, timeline
records, and inspector registrations as JSON; it holds no inspected runtime objects.

The client belongs to the ecosystem area `extensions/DevTools/`. It uses ordinary public Components,
Core, and Reactivity contracts and the shared source-generated protocol context from
`Assimalign.Viu.DevTools`. Neither the client nor its protocol dependency belongs to a shared Viu
framework pack. See [the protocol](../../../../libraries/DevTools/Assimalign.Viu.DevTools/docs/PROTOCOL.md)
and [the specification](../../../../docs/SPECIFICATION.md), clauses `[DVT-13]` and `[DVT-14]`.

## Panes

| Pane | Behavior |
| --- | --- |
| Components | Keeps keyed component rows in authored sibling order. Selecting a component requests depth zero. |
| Properties and state | Shows parameters and exposed state. Expand requests one further level at a selected path. Edit accepts primitive text, sends a protocol request, and displays the runtime's acceptance or rejection. |
| Timeline | Shows at most 50 keyed rows at once, with previous/next windows, registered layer filters, and an inclusive microsecond range. Selecting an event highlights its retained correlated chain. Loss notices remain visible. |
| Custom inspectors | Uses each registration's display name, protocol tree labels, and safe state values. It has no inspector-specific type or dispatch. |

All pane interactions send protocol requests through `DevToolsClientSession`. Plain string values
are escaped as JSON strings by the edit control; numbers, booleans, and null use their JSON literal
forms. Runtime rules decide whether a path is writable. Parameters, computed references, unavailable
paths, unsupported types, and invalid conversions produce explicit rejections.

## Composition

Construct a `DevToolsClientSession` with either `PostMessageDevToolsClientTransport` or
`WebSocketDevToolsClientTransport`, call `StartAsync`, and provide it as the `session` argument of
the registered `DevToolsPanel` component. Call this library's `GeneratedViuComponents.Register`
with the application's component factory to register all five panes, and link the package's
`assets/Assimalign.Viu.DevTools.Client/viu-devtools-client.css`
stylesheet from the host page. The host owns session disposal; pane unmounting only removes subscriptions. The library's
portable `IElementEvent` handlers let the same compiled panes run in Browser or the DOM-free testing
host.

The host must copy both packaged assets from `assets/Assimalign.Viu.DevTools.Client/` to
`wwwroot/_content/Assimalign.Viu.DevTools.Client/`: `viu-devtools-client.js` and
`viu-devtools-client.css`. The transport imports the exact stable JavaScript URL; preserve that
filename when configuring application-module fingerprinting. The fixture's project file shows
package-path content registration and its host page supplies the stylesheet link. Asset copying is
explicit; the component library does not add Browser build targets to a host-neutral consumer.

`scripts/fixtures/EndToEndDevToolsApp` is the packaged Browser SDK host and inspected sample. It is
under `scripts/fixtures/` because repository builds resolve packaged Browser SDK consumers in the
Packaging lane. Its iframe loads the panel with its own WebAssembly runtime, keeping panel activity
out of the inspected application's ambient inspection hooks. The sample exposes a reactive counter,
lazy nested state, a mountable child, and a custom inventory inspector. The parent page changes its
DOM through ordinary reactivity after an accepted counter edit.

An externally hosted panel may select the WebSocket transport and point it at the inspected
application's relay endpoint. The client implements the endpoint connection; deployment and hosting
of a relay are application concerns.

## Connection and lifetime

Transport callbacks queue complete frames. Deferred processing applies a configurable number of
messages per turn, then notifies the panes once. The default budget is 128 messages and the default
timeline retention is 2,048 events. A new connection or accepted handshake clears old tree identities,
snapshots, selections, expansions, timeline events, and inspector registrations before requesting an
authoritative tree. Unknown message types are ignored. A renderer telemetry loss requests a new tree
snapshot; timeline loss remains a visible gap because missing history cannot be recovered.

The session, stores, and components target one event loop and are not thread-safe. Dispose the
session to cancel receive work and release transport listeners. An application owns its component
factory, session, and browser host independently.
