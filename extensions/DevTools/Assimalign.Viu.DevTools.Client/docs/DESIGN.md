# DevTools client design

The client implements [V01.01.10.03](https://github.com/assimalign/viu/issues/83) and specification
clauses `[DVT-13]` and `[DVT-14]`. Viu defines message semantics in
[`PROTOCOL.md`](../../../../libraries/DevTools/Assimalign.Viu.DevTools/docs/PROTOCOL.md). The WHATWG
[HTML web-messaging model](https://html.spec.whatwg.org/multipage/web-messaging.html) and
[WebSockets model](https://websockets.spec.whatwg.org/) define transport delivery only.

## Boundaries and ownership

The library depends on the public protocol DTOs and `DevToolsJsonSerializerContext`. It never
references Core internals, an inspected `IComponent`, `IRuntimeInspectionHook`, or a friend assembly.
JSON values are the entire application-data boundary. Safe value rows flatten only fields and
children already received; rendering does not request more state. Native input handlers use
`IElementEvent`, so the component library has no Browser assembly dependency. Native click actions
use parameterless delegates. Generated glue preserves `Action<IElementEvent>` and
`Func<IElementEvent, Task>` input handlers directly, allowing each host to dispatch its portable event
payload without a Browser-specific object-delegate adapter.

The host supplies and owns the session and registers all five compiled components. Every pane
subscribes to `Changed` during setup and unsubscribes before unmount. A small local reactive revision
causes a render only when that pane's store revision or connection generation changes. Timeline-only
frames therefore do not flatten or render the unchanged component tree, snapshot, or custom inspector.
The outer panel renders only connection-status changes. Transport callbacks never perform component mutations or
synchronous replies; a client scheduler defers bounded application and outgoing batches. The default
scheduler yields between frames. Tests may supply a deterministic scheduler through the public
`IDevToolsClientScheduler` contract.

The `.viu` files stay directly under `src/` because the compiler derives namespaces from directories.
That preserves the library's flat namespace without changing the sanctioned compiler convention.
Presentation CSS is packaged as `assets/Assimalign.Viu.DevTools.Client/viu-devtools-client.css`;
the host links that asset. In-repository generator dogfooding currently assumes Browser static asset
targets when component-style bundling is enabled, so this plain class library disables that bundling
and carries an ordinary stylesheet. This avoids introducing Browser SDK targets into the client
library. A generated setup-hook XML comment uses a literal `Context` label because a cref to the
generated property does not bind in the authored `.viu` source mapping; this documentation-only
compiler correction keeps panel compilation free of warnings.

## Stores and rendering

The component tree preserves node objects keyed by protocol identity and applies mount, update,
remove, and reorder messages to their sibling lists. A tree snapshot replaces the authoritative
state only after handshake or explicit telemetry recovery. The tree pane emits keyed rows in
preorder; an ordinary keyed reorder moves existing rendered rows.

Snapshots start at depth zero. An explicit Expand action requests one further level and merges the
response at the exact section/path. Paths are arrays on the wire; their dotted spelling is only a
presentation label. Internal keys encode each segment's length before its text, so dictionary names
containing slashes, NUL characters, or other delimiters cannot collide with nested paths.
Edit selection retains that array and the selected component identity. Changing
the selection clears the pending editor. The runtime performs JSON-to-CLR conversion and applies
the accepted setter through ordinary reactivity, so no client-side state mutation can bypass it.

Timeline admission is constant work per event and evicts the oldest retained record at capacity.
The client frame budget bounds the number of admitted messages before yielding. `GetWindow` scans
the bounded retention window and materializes only the requested rows; the pane renders no more than
50 keyed event buttons. Previous/next controls change that window, and layer/range controls reset
its offset. Zoom uses inclusive elapsed microseconds, never a wall-clock conversion. This is a
windowed list with explicit navigation, not a full-history DOM hidden by CSS.

Selecting an event builds a retained relation set using correlation, dependency, effect, and
component identifiers. The pane computes the set once per refresh and checks membership for visible
rows. Incomplete chains and drop notices remain valid states. Inspector registrations and values
follow the same protocol-only approach: generic tree and state rows do not branch on inspector
identity.

## Reconnection

Every accepted handshake starts a fresh client view, even when the remote runtime retains its
identity registry and monotonic elapsed clock. A transport reconnect first discards pending data and
requests, then negotiates again. Thus an identifier from the old connection cannot select or edit
an unrelated component in a reloaded application. The current tree is requested after acceptance;
registration replay restores timeline layers and custom inspectors. Unknown envelopes and malformed
frames are isolated from the connection, while receive-window overflow forces a fresh handshake.

The sample uses two independent WebAssembly runtimes in a same-origin iframe arrangement. Merely
creating a second Viu root in the inspected runtime would share ambient hooks and could capture the
panel's own render work. The iframe keeps the session transport as the only coupling.

## Non-goals

The client does not provide a browser extension manifest, deploy a WebSocket relay, persist captured
application state, or retain unbounded history. Custom inspector editing is outside the version 1
state edit contract. State editing supports the primitive reflection-free runtime table; it does not
construct arbitrary CLR objects or mutate parameters and computed values. No runtime serializer
discovery, emitted code, or reflection-based member access is involved.
