# Assimalign.Viu.State design

## Registry lifetime topology

`StateStoreRegistry` creates one detached reactive root during construction. It creates a
non-detached scope while that root is current for each first-use definition:

```text
StateStoreRegistry
  -> detached root scope
       -> store A scope
       -> store B scope
```

The caller's ambient component scope is therefore never a store scope's parent. Removing a
definition stops only its child scope and disposes an `IDisposable` store. Disposing the registry
does the same for every entry, stops the root, clears the ordinal-keyed map, and clears
`StateStores.ActiveRegistry` when it points to that registry ([STA-2], [STA-3]).

The registry contract is synchronous. It deliberately does not block the single-threaded host loop
waiting for an `IAsyncDisposable` implementation. A store with only asynchronous cleanup must keep
that lifetime host-owned rather than relying on registry disposal.

`StateStores.EnterExecutionFlow()` installs a fresh ambient setup context and active-registry slot
for a request-oriented host. Its idempotent lease restores the previous flow when nested leases are
disposed in last-in, first-out order. The lease neither creates nor disposes a registry: the caller
continues to own the request registry explicitly. Keeping this host operation on the public facade
lets Core compose state isolation without cross-library friend access (`[EXE-1]`, `[CMP-33]`).

## Setup and convention attachment

Definitions contain a diagnostic `Identifier`, its equivalent ordinal `Key`, and one explicit
`StateStoreActivator<TStore>`. The registry invokes that delegate directly inside the store scope;
there is no constructor inspection or reflection-backed activation ([STA-1], [EXE-4]).

`IStateContext` exposes only the store scope, optional services, and optional watch scheduler. State
does not receive a component factory or component owner. `Use(ComponentContext)` looks in
`context.Services` for `IStateStoreRegistry`, then falls back to `StateStores.ActiveRegistry`. It
does not type-test or retain the component context, so global setup cannot depend on the first
component's mount order ([STA-4], [CMP-33]).

Setup failure stops the new child scope, restores the previous ambient setup context, and adds no
entry. A different definition claiming an existing ordinal key raises
`DuplicateStateStoreKeyException`; case-distinct keys remain distinct.

## Rich state-store model

`StateStore<TState>` is optional. It keeps one stable `IReactiveObject` instance and provides:

- `Patch(Action<TState>)`, which batches a typed group of changes;
- `Patch(TState)`, which calls an author-supplied typed state copier;
- `Reset()`, which creates fresh factory state and copies it onto the live instance;
- `Subscribe`, backed by one lazily created deep state watch; and
- `OnAction`, observing only methods that opt in through a protected action helper.

A store constructed without a factory and copier supports mutator patches but rejects object patch
and reset. This explicit boundary avoids state-shape reflection ([STA-5], [STA-6]).

With a scheduler, the shared state watch uses pre-flush delivery and deduplicates several writes
into one notification. Without a scheduler, direct writes deliver synchronously; a grouped patch
still notifies once because it executes in a reactive batch. The scheduler wrapper observes whether
a job was actually queued so a no-op patch cannot leak its mutation kind to a later write ([STA-7]).

Action completion hooks receive resolved asynchronous results. A fault runs error hooks and then
propagates. Callback collections are snapshotted before iteration so removal during delivery does
not corrupt the current pass ([STA-8]).

## AOT boundary

Store activation and state copying use typed delegates. Deep watching traverses the
source-generated `IReactiveObject` contract. State performs no dynamic code generation, runtime
constructor discovery, or reflection-based state serialization.

## Server payload and pre-mount restore

Payload participation is definition-local and explicit. `IStateStoreSerializer<TStore>` writes one
state value to a registry-owned `Utf8JsonWriter` and restores one `JsonElement` value. The standard
`StateStoreJsonSerializer<TStore,TState>` reaches only the caller's `JsonTypeInfo<TState>` plus
typed access and restore delegates, so trimming cannot select a reflection overload
([V01.01.09.03], [STA-6], [EXE-4]).

`StateStoreRegistry.CapturePayload()` serializes the current ordinal entry map. Definitions never
resolved in that registry are absent; a resolved definition without a serializer is an actionable
error instead of a silent omission. The resulting schema is exactly:

```json
{"version":1,"stores":{"store-key":{"shape":"belongs to the registered serializer"}}}
```

`RestorePayload` applies matching live entries immediately and retains the immutable payload for
later definitions. `GetOrCreate` constructs the store in its ordinary registry-owned scope, applies
the matching value before adding or returning the entry, and disposes the new store plus its scope
if restoration fails. Browser invokes this operation after its bridge initializes but before mount;
ServerRenderer also selects request-local setup and active-registry ambient state, so separately
owned payload maps and reactive lifetimes cannot cross concurrent requests [EXE-1].

## Plugin creation and extension ownership

The registry snapshots plugin registrations before setup, then runs each plugin inside the new
store scope before staged SSR restore and publication. No materialized entry is visible until
creation completes. A private in-progress map prevents recursive construction and lets later
plugins retrieve extensions attached by earlier plugins. Failures stop and dispose the candidate
without publishing it; nested successfully created stores retain their own registry lifetimes
([STA-10]).

The plugin context retains the definition object so a plugin with a known store type can inspect
its typed setup and serializer metadata. The registry itself exposes no ambient component owner.
Extensions use exact static type keys, without reflection-based activation or member discovery.
Duplicate keys throw; one disposable instance attached under several types is disposed once by
reference. Cleanup clears extension values and continues after individual failures. Plugin objects,
services, and storage providers remain externally owned. Optional default interface methods reject
unsupported operations on custom registry implementations, preserving their existing store contract.

## Persistence selection and failure boundary

Persistence opt-in adds an immutable descriptor to an already serializable definition. The
first-party plugin observes `StateStore<TState>` through an internal mutation subscription adapter;
the adapter calls the same public subscription path, sharing its one deep pre-flush watcher. There
is no additional timer or scheduler queue and no new reactive property traversal ([STA-7], [STA-11]).

Only opted-in definitions defer state notifications while setup, plugins, and staged SSR restore
run. This covers a subscriber attached by setup or by an earlier plugin even in synchronous mode.
The deferred callback observes the final live state. The plugin remembers its initial selected JSON
so restoring stored values alone does not write them back; changed selection is serialized once per
notification and written as one versioned payload. An excluded-only mutation produces no write.
Scheduler-free hosts keep synchronous direct-write delivery and grouped patch batching.

The payload reuses `StateStorePayload` version 1 with exactly one matching definition key. Selection
operates on JSON nodes. Dot-separated paths address object members; arrays are atomic, missing paths
are ignored, empty includes select all, and exclusions take precedence. Object selections merge
recursively into serialized setup defaults so excluded or absent members preserve defaults. Unknown
selected names, duplicate members, and incompatible known kinds are rejected before the typed
serializer applies anything. JSON nullability and array-element validation belong to that serializer,
which must use source-generated metadata. No runtime schema discovery is introduced.

Serialized setup defaults define recognized object member names. A participating serializer must
emit selected optional/default-valued members rather than omit them; newly introduced keys in an
empty dynamic dictionary are not discoverable through this contract. Each definition sharing a
storage area needs a distinct storage key, assigned by the host, because a stored envelope identifies
one definition and another definition's envelope is incompatible.

Invalid payloads are removed and reported. Read and write failures, including Browser security and
quota failures, report an operation-specific diagnostic and leave the application usable. A failed
write leaves the previous selection marker unchanged, allowing retry on a later notification.
Diagnostic callbacks are isolated from application behavior. A custom restore delegate should
validate before applying; if it changes state and then throws, the plugin attempts recovery through
the same serializer with captured defaults. If that delegate also rejects recovery, the diagnostic
includes both failures; arbitrary user mutation cannot be transactionally reversed without another
explicit state copier. This limitation does not affect corrupt JSON rejected before application.

`IStateStorage` carries only whole-string operations. `InMemoryStateStorage` has independent ordinal
maps; server hosts decide their lifetime and must not share them unsafely across concurrent requests.
Browser implements the same interface in its own package over its already initialized module. State
never references Browser, and there are no reflection serializer overloads or new host dependencies.

Persistence does not provide migrations, cross-tab synchronization, asynchronous storage, automatic
retry timers, encryption, or arbitrary-object mutation observation. Applications should compose one
persistence plugin per registry. These are explicit non-goals of [V01.01.09.04]
([issue #79](https://github.com/assimalign/viu/issues/79)); the storage adapter only implements the
[WHATWG Web Storage](https://html.spec.whatwg.org/multipage/webstorage.html) compatibility boundary.
