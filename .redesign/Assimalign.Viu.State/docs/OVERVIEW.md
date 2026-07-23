# Assimalign.Viu.State

The proposed application-state boundary. It depends on Reactivity for effect-scope ownership and
Components for the application service resolver plus optional component ownership.

The scaffold models a setup-style, per-registry store:

- `StateStoreDefinition<TStore>` captures an explicit AOT-safe setup delegate.
- `StateStoreRegistry` creates each store once in its own detached reactive scope.
- `IStateContext` exposes that scope, the component factory/service provider, and the optional
  component that first requested the store.

Whether this replaces or sits below the existing `Assimalign.Viu.Store` package is intentionally
unresolved. See the root [DESIGN.md](../../DESIGN.md).

