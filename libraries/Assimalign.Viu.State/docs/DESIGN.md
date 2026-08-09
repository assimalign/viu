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
