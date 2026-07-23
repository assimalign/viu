# Assimalign.Viu.Core

The proposed application and runtime integration layer. It references Components, Reactivity, and
State, while retaining the existing `Assimalign.Viu` root namespace exception.

This scaffold covers the application composition boundary only:

- The application receives a root `IComponent`.
- The application receives an already-composed `IComponentFactory`.
- `IApplicationContext.Services` is the same object exposed as `Components`.
- State is optional at the context surface while the Core-to-State project dependency remains under
  review.
- Core has no service container, registration, lifetime, or constructor-activation API.

The shipping renderer, scheduler, hydration, and internal mounted-instance state would remain in
Core and consume the independent Components and Reactivity packages.

