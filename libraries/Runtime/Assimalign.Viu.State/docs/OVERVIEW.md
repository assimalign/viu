# Assimalign.Viu.State

State is a state-management **convention** layered on the component model. It references
Reactivity and Components, and owns reusable store definitions, explicit registry lifetimes,
detached registry roots, attached per-store effect scopes, and optional service/watch composition.

Request-oriented runtime composition enters `StateStores.EnterExecutionFlow()` before assigning a
request-owned active registry. The returned idempotent lease restores the prior ambient setup and
registry values after the request, while registry creation and disposal remain explicit. This host
seam is public by design; no sibling library receives friend access to State internals (`[EXE-1]`,
`[CMP-33]`).

A lightweight store may be any object returned by an AOT-safe `StateStoreActivator<TStore>`.
`StateStore<TState>` is the optional richer base for `Patch`, `Reset`, `Subscribe`, and `OnAction`
over a source-generated reactive state object. The live state object is never replaced, and object
copying uses an explicit typed delegate rather than runtime member enumeration ([STA-1], [STA-5],
[STA-6]).

A mounted component obtains a store through `StateStoreDefinition<TStore>.Use(ComponentContext)`,
which resolves the registry through `context.Services`, then the ambient
`StateStores.ActiveRegistry`, and otherwise throws — the same seam every convention uses. There is
no component capability interface, no context cast, and no privileged context member; adding this
convention modified nothing in Components or Core ([STA-4], [CMP-33]).

Each registry indexes entries with an ordinal store key. Reusing the same definition returns the
same instance; a second definition claiming that key raises `DuplicateStateStoreKeyException`.
The registry creates one detached root effect scope, creates each store scope as its child, and
restores the ambient setup context even when setup fails. Removing a definition ends only its store
lifetime; disposing the registry ends every store lifetime and clears the ambient registry when it
points to that registry ([STA-2], [STA-3]).

SSR state uses an explicit `IStateStoreSerializer<TStore>` on each participating definition.
`StateStoreJsonSerializer<TStore,TState>` accepts state-access and restore delegates plus a
source-generated `JsonTypeInfo<TState>`; no reflection serializer fallback exists. A capture
contains only materialized stores and fails actionably if one lacks registration. Restore updates
an already materialized store immediately and stages unmatched keys so a later `GetOrCreate`
applies server state before returning the store ([V01.01.09.03], [EXE-4]).

`StateStorePayload` uses the fixed schema
`{"version":1,"stores":{"store-key":state}}`. It validates version, member shape, and ordinal
store-key uniqueness, then exposes normalized JSON with HTML-sensitive characters and Unicode line
separators escaped for inert script-island transport.

Plugins register through `registry.Use(IStateStorePlugin)` before resolving stores. Each registration
runs in order for subsequently created stores, with the exact definition, store, services, and
store scope exposed by `StateStorePluginContext`. `TryGetStore<TStore>` supports typed behavior;
`SetExtension<TExtension>` and `registry.GetExtension<TExtension>(store)` attach and retrieve values
by exact declared type. Disposable extensions and attached handlers end with the store scope
([STA-10], [V01.01.09.04]).

`StateStorePersistencePlugin` supports definition-local opt-in on a serializable
`StateStore<TState>`. Compose it with `StateStorePersistenceOptions(localStorage, sessionStorage,
diagnostic)` and pass a `StateStorePersistenceDescriptor` when defining the store. The descriptor
selects a storage key, `StateStorageKind.Local` or `Session`, and optional include/exclude JSON
member paths. For example, `includePaths: ["Count", "Preferences.Theme"]` selects those serialized
members; `excludePaths: ["Preferences.Secret"]` removes that member even if its parent is included.
Names are ordinal JSON names from the registered serializer; arrays are selected whole.

Persistence restores selected values into setup defaults before creation notifications. A staged
SSR payload is applied afterward. Writes reuse the pre-flush state subscription and contain one
complete versioned payload per changed selection, never one per property. Invalid data is removed
and reported through the diagnostic callback; storage failures do not crash the store. The State
package supplies `IStateStorage` and `InMemoryStateStorage`; Browser supplies `BrowserStateStorage`
after its normal interop initialization. Storage and diagnostic composition stay host-owned
([STA-11], [EXE-4]).

See [DESIGN.md](DESIGN.md) for lifetime, scheduler, failure recovery, and AOT boundaries, and
the [work item](https://github.com/assimalign/viu/issues/79) for delivery scope.
