# Viu runtime inspection protocol

- Protocol name: `assimalign.viu.devtools`
- Current version: `1`

This document is the standalone wire contract for `[DVT-1]` through `[DVT-7]`. Viu owns the
message semantics. The browser transport applies the WHATWG HTML web-messaging model, and the
socket transport applies the WHATWG WebSockets message model; those standards define transport
delivery only, not Viu's inspection data.

## Framing

Every outbound transport frame is one UTF-8 JSON batch. A post-flush drain creates at most one
interop call and one transport frame, regardless of the number of messages in that drain.

```json
{
  "protocol": "assimalign.viu.devtools",
  "messages": [
    {
      "version": 1,
      "type": "handshake.response",
      "payload": { "accepted": true, "version": 1 }
    }
  ]
}
```

Clients may send the same batch shape or one envelope object. A receiver ignores an unknown
`type`, an unknown property, or a batch for another protocol without closing the session. A
malformed frame is discarded in isolation. Every payload described below uses camel-case property
names. Optional null properties may be omitted.

## Version negotiation

The client begins with `handshake.request`:

```json
{ "version": 1, "type": "handshake.request", "payload": { "supportedVersions": [1] } }
```

The runtime selects the newest mutually supported version and replies with
`handshake.response`:

```json
{ "accepted": true, "version": 1 }
```

When there is no common version, `accepted` is false, `version` is absent, and `reason` is a
diagnostic string. Messages other than a handshake are ignored until negotiation succeeds.

## Component tree

The runtime emits these real-time messages:

- `component.mounted` and `component.updated`: `{ identifier, parentIdentifier?, index, name, key? }`
- `component.unmounted`: `{ identifier }`
- `component.reordered`: `{ identifier, index }`
- `component.event`: `{ identifier, name, arguments }`

These renderer observations are bounded telemetry. When overload evicts observations, the next
drain emits `telemetry.dropped` with `{ count }` at the first evicted envelope's position and before
the retained telemetry it summarizes. The count is the number evicted since the previous drain, so
a client can request an authoritative tree snapshot instead of silently continuing from an
incomplete event stream. A session-wide enqueue sequence stable-merges reliable control and bounded
telemetry, preserving their shared chronology even when they wait behind an active send.

Identifiers are positive integers stable for one authored instance. They are backed by weak
identity entries; inspection never extends a component lifetime. `index` is the structural order
among direct authored component children, independent of intervening host elements. A client can
request an authoritative tree with `tree.snapshot.request` and receives `tree.snapshot`:

```json
{
  "roots": [
    {
      "identifier": 1,
      "name": "ApplicationRoot",
      "children": [
        { "identifier": 2, "name": "Counter", "key": "counter", "children": [] }
      ]
    }
  ]
}
```

## Component snapshots and lazy expansion

`component.snapshot.request` carries `{ identifier, depth }`. The response
`component.snapshot` carries:

```json
{
  "identifier": 2,
  "parameters": [ { "name": "title", "value": { "kind": "scalar", "typeName": "System.String", "displayValue": "Count", "expandable": false, "path": ["title"], "children": [] } } ],
  "state": [],
  "events": [ { "name": "changed", "hasValidator": false } ]
}
```

The runtime clamps `depth` to its configured maximum. At the depth boundary an object, sequence,
or reactive reference has `expandable: true` and no children. The client sends
`component.expand.request` with `{ identifier, section, path, depth }`, where `section` is
`parameters` or `state`; `component.expand` returns the current value at that path. Expansion
re-reads live state and therefore never requires a strongly held snapshot graph.

Value `kind` is `null`, `scalar`, `object`, `array`, `reference`, or `placeholder`. A `reference`
has a `value` child containing the current `IReactiveReference` value, including computed values.
Unknown objects, failed getters/providers, cycles, and values beyond collection limits become a
`placeholder` with a type name and diagnostic display value; they never fault serialization.
Snapshot, expansion, inspector-state, and component-event reads suspend dependency collection, so
inspection cannot add reactive dependencies to an application effect.

## Custom inspectors and timeline layers

Registration emits `inspector.registered` with `{ identifier, displayName }`; disposal emits
`inspector.unregistered`. A client requests a provider tree with `inspector.tree.request`
`{ inspectorIdentifier }` and receives `inspector.tree`. It requests node state with
`inspector.state.request` `{ inspectorIdentifier, nodeIdentifier, depth }` and receives
`inspector.state`. Inspector state uses the same safe value representation as component state.

Timeline registration emits `timeline.layer.registered`
`{ identifier, displayName, color? }` and disposal emits `timeline.layer.unregistered`. Version 1
defines registration only. It deliberately defines no timeline-event emission message; event
recording belongs to `[V01.01.10.02]`.

## Serialization and transport invariants

- Every runtime JSON payload is serialized with the package's source-generated
  `JsonSerializerContext`; runtime contract discovery is forbidden.
- Renderer telemetry is bounded and oldest-first when capacity is exhausted. A
  `telemetry.dropped` marker reports each drain's loss count without consuming telemetry capacity.
- Handshake responses, requested snapshot and inspector responses, and registration changes use a
  separate reliable queue; telemetry pressure cannot evict them.
- A monotonic session sequence stable-merges both queues, and the loss marker takes the first
  evicted envelope's position so retained observations cannot cross a later control response.
- Hook calls never perform transport I/O. The scheduler drains after render in its post-flush
  phase, then sends asynchronously.
- The browser adapter sends a whole batch through one `window.postMessage` interop call and removes
  its `message` listener on disposal. See the
  [WHATWG HTML web-messaging standard](https://html.spec.whatwg.org/multipage/web-messaging.html).
- The WebSocket adapter sends a whole batch as one text message and bounds inbound reassembly. See
  the [WHATWG WebSockets standard](https://websockets.spec.whatwg.org/).
