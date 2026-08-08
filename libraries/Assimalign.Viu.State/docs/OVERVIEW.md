# Assimalign.Viu.State

State is a state-management **convention** layered on the component model. It references
Reactivity and Components, and owns reusable store definitions, explicit registry lifetimes,
detached registry roots, attached per-store effect scopes, and optional service/watch composition.

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

See [DESIGN.md](DESIGN.md) for lifetime, scheduler, and AOT boundaries.
