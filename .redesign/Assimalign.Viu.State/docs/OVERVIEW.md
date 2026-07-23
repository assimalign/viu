# Assimalign.Viu.State

The proposed application-state boundary. It depends on Reactivity for effect-scope ownership and
Components for component resolution plus optional component ownership. Application services are a
separate `IServiceProvider`; State does not assume the component factory implements it.

The scaffold models a setup-style, per-registry store:

- `StateStoreDefinition<TStore>` captures an explicit AOT-safe setup delegate.
- `StateStoreRegistry` creates each store once in its own detached reactive scope.
- `IStateContext` exposes that scope, the component factory, the independent service provider, and
  the optional component that first requested the store.

`Assimalign.Viu.State` is the successor to and replacement for `Assimalign.Viu.Store`. The shipping
refactor moves the existing Store behavior and compatibility requirements into this package rather
than maintaining two competing application-state abstractions. See the root
[DESIGN.md](../../DESIGN.md).
