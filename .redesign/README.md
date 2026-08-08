# Viu item 2e staging system

This directory is the isolated, compilable staging system for the adopted Viu component-runtime
model through item 2e, `[V01.01.15.02]`. It contains the complete Reactivity, Components, State,
Core, Browser, Router, Browser.Router, ServerRenderer, and Testing libraries plus the compiled
single-file-component fixture that proves generated and code-first registrations compose.

`docs/SPECIFICATION.md` is the semantic authority. The component ownership and lifetime decisions
come from `docs/COMPONENT-MODEL-PLAN.md`. The staging resolver deliberately prevents projects from
silently binding to same-named shipping libraries. No shipping project consumes this directory.

## Runtime boundaries

- Components owns the closed `VirtualNode` algebra and authored `IComponent` contract.
- Core owns application composition, mounting, reconciliation, scheduling, hydration, and public
  host operations.
- Reactivity and State retain independent lifetimes; State attaches through services or its ambient
  registry.
- Router owns matching, histories, reactive route state, guards, route components, and its frozen
  browser-history interop asset. Browser.Router attaches navigation to Browser application lifetime.
- Browser, ServerRenderer, and Testing consume public Core seams without production friend access.

## Gates

```powershell
dotnet restore .redesign/Assimalign.Viu.Redesign.slnx
dotnet build .redesign/Assimalign.Viu.Redesign.slnx --no-restore -warnaserror
dotnet test .redesign/Assimalign.Viu.Redesign.slnx --no-build --no-restore
```

## Staging-only boundaries

- Promotion into the shipping trees and package/framework validation belong to item 2f.
- Browser scroll restoration remains a documented non-goal under `[RTR-8]`; history state still
  preserves scroll coordinates.
- Scoped CSS variables and compile-time custom-property diagnostics remain deferred by
  `[STY-6]` through `[STY-8]`.
