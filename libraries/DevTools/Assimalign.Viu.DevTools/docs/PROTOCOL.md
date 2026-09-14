# Viu runtime inspection protocol

- Protocol name: `assimalign.viu.devtools`
- Current version: `1`

This document is the standalone wire contract for `[DVT-1]` through `[DVT-14]`. Viu owns the
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

The client treats every accepted handshake as a new local inspection epoch: it clears component
identities, selected snapshots, pending expansions/edits, inspector data, and timeline records,
then requests a fresh tree. The runtime replays inspector and layer registrations. Re-handshaking
with the same runtime does not recycle its object identifiers or elapsed clock. Reloading the
inspected application creates a new runtime session whose identifiers and clock are unrelated.
The postMessage adapter announces `assimalign.viu.devtools.ready` when its listener is installed;
this advisory asks an attached client to negotiate again and carries no inspection data. Socket
reconnection likewise requires a fresh handshake. These client rules are specified by [DVT-14].

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
Generated `IReactiveObject` members appear as lazily expanded objects using the generated
`GetMemberValues()` access surface; their properties are never discovered by reflection.
Unknown objects, failed getters/providers, cycles, and values beyond collection limits become a
`placeholder` with a type name and diagnostic display value; they never fault serialization.
Snapshot, expansion, inspector-state, and component-event reads suspend dependency collection, so
inspection cannot add reactive dependencies to an application effect.

## State edits ([V01.01.10.03], #83)

Version 1 adds `state.edit.request` and `state.edit.response` [DVT-13]. Older receivers ignore
these additive message types under [DVT-2]. A request carries:

```json
{ "identifier": 2, "section": "state", "path": ["count", "value"], "value": 12 }
```

The response is a reliable control message, independent of telemetry capacity:

```json
{ "identifier": 2, "path": ["count", "value"], "accepted": true }
```

Only `state` is writable; `parameters` is always rejected. Paths contain 1 through 64 ordinal,
case-sensitive segments and resolve against live state on each request. A reference can be
addressed directly (`["count"]`) or by its displayed child (`["count", "value"]`). Generated
object members use their declared names, for example `["model", "Count"]`. Dictionary/sequence
paths can lead to an editable reference or generated member; plain dictionary/sequence entries
and entire objects are never replaced. No reflection or runtime JSON contract discovery occurs.

The target fixes the conversion; the JSON value never selects a CLR type:

| CLR target | Accepted JSON |
| --- | --- |
| `bool` | `true` or `false` |
| `int` | Integral numeric token within signed 32-bit range |
| `long` | Integral numeric token within signed 64-bit range |
| `double` | Numeric token convertible to a finite double |
| `decimal` | Numeric token representable as a decimal |
| `string` | String token or null |

There is no numeric conversion from strings or booleans. Fractional tokens cannot edit integers;
out-of-range numbers and non-finite doubles are rejected. Nullable numeric members, enums, custom
types, and arbitrary object replacement are unsupported. `IReactiveReference<T>` writes use an
explicit typed setter. Generated members use `IReactiveObject.TrySetMemberValue`, which dispatches
only the table's declared property types through their normal setters. An equal-value write is
accepted but retains ordinary equality suppression. Read-only generated objects and computed
references (including computeds with authored setters) cannot be edited.
Computed ancestors are rejected before reading their values, regardless of their generic value
type, through the non-evaluating `IReactiveReference.IsComputed` classification. An ordinary mutable
object-valued reference may still be traversed to a supported generated member or scalar reference.

Rejected responses include `accepted: false` and a diagnostic `reason`. Reasons distinguish an
unmounted component, read-only parameters/target, computed target, unknown section, invalid or
missing path, scalar type/range mismatch, unsupported target, and a failing getter/provider/setter.
No-write rejections do not trigger reactivity. A user-defined setter may throw after a side effect;
that response explicitly says state may have changed and promises no rollback. Reads used to
resolve the target suspend ambient dependency collection; an accepted write uses ordinary reactive
notification and rendering. The client requests a fresh shallow snapshot after the response.

The public data-only records in `src/Protocol/`, `ProtocolCodec`, and
`DevToolsJsonSerializerContext` are shared by runtime and client. They contain wire data only:
clients must not access runtime internals or retain application objects [DVT-14].

## Custom inspectors and timeline layers

Registration emits `inspector.registered` with `{ identifier, displayName }`; disposal emits
`inspector.unregistered`. A client requests a provider tree with `inspector.tree.request`
`{ inspectorIdentifier }` and receives `inspector.tree`. It requests node state with
`inspector.state.request` `{ inspectorIdentifier, nodeIdentifier, depth }` and receives
`inspector.state`. Inspector state uses the same safe value representation as component state.

Timeline registration emits `timeline.layer.registered`
`{ identifier, displayName, color? }` and disposal emits `timeline.layer.unregistered`. Sessions
automatically register `reactivity`, `components`, and `scheduler`; custom identifiers must not
collide with those built-ins. Layer registration remains reliable under telemetry pressure.

## Reactivity timeline ([V01.01.10.02], #82)

Version 1 adds `timeline.event` envelopes inside the same complete batches as other messages.
Older clients ignore these unknown message types [DVT-2]. Each event payload has:

```json
{
  "sequence": 42,
  "timestamp": 18025,
  "correlationIdentifier": 7,
  "layerIdentifier": "reactivity",
  "kind": "dependency.triggered",
  "label": "Count",
  "dependencyIdentifier": 3,
  "ownerIdentifier": 4,
  "version": 2
}
```

`sequence` is the common session enqueue order, including control and component telemetry;
gaps are permitted. `timestamp` is monotonic elapsed **microseconds since session construction**,
not a wall-clock time; equal timestamps are ordered by sequence. `correlationIdentifier` names
the upcoming/current scheduler flush chain, including writes that precede the queued flush.
Identifiers are meaningful only within the connected runtime/session. A handshake restarts sampling
and clears pending queues, but does not recycle object identities or the elapsed clock.

| Layer | `kind` | Additional fields |
| --- | --- | --- |
| `reactivity` | `state.write` | `dependencyIdentifier`, `ownerIdentifier?`, `effectIdentifier?`, `version` |
| `reactivity` | `dependency.tracked`, `dependency.triggered` | `dependencyIdentifier`, `ownerIdentifier?`, `effectIdentifier?`, `version` |
| `reactivity` | `effect.scheduled` | `effectIdentifier`, causal `dependencyIdentifier?` |
| `reactivity` | `effect.run.started`, `effect.run.completed` | `effectIdentifier`; completion includes `succeeded` |
| `components` | `component.mounted`, `component.updated`, `component.unmounted` | `componentIdentifier`, active `effectIdentifier?` |
| `scheduler` | `flush.started`, `flush.completed` | completion includes `preFlushCount`, `renderCount`, `postFlushCount`, `succeeded` |

`state.write` denotes an explicit dependency trigger (including a forced trigger), preceding
`dependency.triggered`; computed invalidation propagation emits only the latter. This is dependency
causality, not an old/new-value log: application values are never retained. An accepted batched
effect invalidation emits `effect.scheduled`; multiple writes may coalesce into one scheduled run.
`effectIdentifier` on dependency events identifies the ambient subscriber, which may also be a
computed subscriber. Component identifiers match the component tree; dependency/effect/owner
identifiers share a separate weak registry. Completion is emitted even for throwing effects/flushes.

Names come from generated literal property names or `Reactive.WithDebugLabel(reference, "name")`.
Anonymous dependencies use `dependency-{identifier}`. Weak identity entries and queued scalar
records do not extend source-object lifetimes, and naming never calls arbitrary `ToString()`.

Timeline options are captured at session construction:

| Option | Default | Meaning |
| --- | --- | --- |
| `TimelineCapacity` | 1024 | Fixed number of admitted events in the ring; oldest is evicted on overflow |
| `TimelineSamplingInterval` | 1 | Capture candidate 1, then 1 + N, 1 + 2N, and so on across all event kinds |
| `MaximumTimelineEventsPerSecond` | 10000 | Maximum sampled events admitted in each elapsed one-second window |

All values must be positive. Sampling omissions are deliberate and do not produce loss markers.
Ring evictions and rate-limit rejections produce one `timeline.dropped` envelope with `{ count }`
at the earliest lost event's sequence position on the next drain. Counts reset after draining;
rate windows and sampling positions continue across drains. The timeline ring is independent of
`BufferCapacity` and `telemetry.dropped`. Loss and sampling can omit either side of a pair: clients
must tolerate incomplete runs and flushes. Reliable control messages cannot be evicted by either
telemetry buffer. The drain merges all queues by sequence and serializes timeline values only then.

Flush counts include attempted application jobs, including a throwing job, and exclude disposed
jobs and the recorder's own drain callbacks. A post-flush callback that queues more application
work stays in the same correlation chain. Diagnostics-only drains do not emit flush pairs. The
completed hook schedules an asynchronous drain without queuing another scheduler flush solely for
its own observation. The inspection application lives in `extensions/DevTools/` and consumes these
messages through the same public protocol contracts.

## Serialization and transport invariants

- Every runtime JSON payload is serialized with the package's source-generated
  `JsonSerializerContext`; runtime contract discovery is forbidden.
- Renderer telemetry is bounded and oldest-first when capacity is exhausted. A
  `telemetry.dropped` marker reports each drain's loss count without consuming telemetry capacity.
- Handshake responses, state-edit responses, requested snapshot and inspector responses, and registration changes use a
  separate reliable queue; telemetry pressure cannot evict them.
- A monotonic session sequence stable-merges all queues, and the loss marker takes the first
  evicted envelope's position so retained observations cannot cross a later control response.
- Hook calls never perform transport I/O or timeline serialization. The post-flush callback
  requests a drain, flush completion finalizes timeline counts, then serialization and sending run
  asynchronously after the hook returns.
- The browser adapter sends a whole batch through one `window.postMessage` interop call and removes
  its `message` listener on disposal. See the
  [WHATWG HTML web-messaging standard](https://html.spec.whatwg.org/multipage/web-messaging.html).
- The WebSocket adapter sends a whole batch as one text message and bounds inbound reassembly. See
  the [WHATWG WebSockets standard](https://websockets.spec.whatwg.org/).
