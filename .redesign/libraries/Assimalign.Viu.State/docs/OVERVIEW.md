# Assimalign.Viu.State

State is a state-management **convention** layered on the component model. It references
Reactivity and Components, and owns store definitions, explicit registry lifetime, detached store
effect scopes, and optional service/watch composition.

A mounted component obtains a store through `StateStoreDefinition<TStore>.Use(ComponentContext)`,
which resolves the registry through `context.Services`, then the ambient
`StateStores.ActiveRegistry`, and otherwise throws — the same seam every convention uses. There is
no component capability interface, no context cast, and no privileged context member; adding this
convention modified nothing in Components or Core.
