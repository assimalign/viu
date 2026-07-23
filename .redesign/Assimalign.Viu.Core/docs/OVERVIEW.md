# Assimalign.Viu.Core

The proposed application and runtime integration layer. It references Components, Reactivity, and
State, while retaining the existing `Assimalign.Viu` root namespace exception.

This scaffold covers the application composition boundary only:

- The application receives a root `IComponent`.
- The application receives an already-composed `IComponentFactory`.
- The application receives an independent `IServiceProvider`.
- A custom implementation may use one object for both roles, but the contracts do not require it.
- Core borrows both resolvers and does not dispose them.
- Core owns each per-mount template returned by the factory and disposes it on setup failure or
  unmount when it implements `IDisposable`.
- Viu does not create dependency-injection scopes automatically; a custom factory may bind a scope
  to its returned template.
- State is optional at the context surface while the Core-to-State project dependency remains under
  review.
- Core has no service container, registration, lifetime, or constructor-activation API.
- The application-level error handler from the shipping Core contract still needs to be restored so
  observed lifecycle and event-task faults have a terminal application sink.

The shipping renderer, scheduler, hydration, and internal mounted-instance state would remain in
Core and consume the independent Components and Reactivity packages. Lowering must preserve
`ComponentOptimization` so the existing block-child patch path remains available.
