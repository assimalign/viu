# Viu runtime libraries

This directory contains Viu's shipping runtime libraries. Their documentation describes the
adopted `[V01.01.15]` component model; the atomic implementation swap lands on
`feature/V01.01.15-component-model`, using `.redesign/` only as the exact contract scaffold.

The principal packages are:

- `Assimalign.Viu.Reactivity` — change tracking and reactive lifetimes;
- `Assimalign.Viu.Components` — the closed `VirtualNode` algebra, authored component contract,
  registration, bindings, and render plans;
- `Assimalign.Viu.State` — the state-store convention attached through designed component seams;
- `Assimalign.Viu.Core` — application composition, lifetime, renderer engine, and host operations;
- `Assimalign.Viu.Browser` — the browser host and DOM interop;
- `Assimalign.Viu.ServerRenderer` and `Assimalign.Viu.Testing` — one-shot server rendering and
  in-memory testing hosts; and
- `Assimalign.Viu.Router` plus `Assimalign.Viu.Browser.Router` — host-neutral navigation and the
  browser history/click bridge.

The application lifetime boundary is host-neutral. Core owns `IApplication`, the read-only
application context, middleware, `ApplicationLifetime`, and the promoted `ApplicationState`.
Browser owns its integer DOM handles and mount operations. A future host supplies its own adapter
without changing component, reactivity, or state APIs.

`IComponentFactory` and nullable `IServiceProvider` are separate, application-owned values. Viu
supplies no dependency-injection container and no hierarchical component dependency channel.
Conventions attach through `ComponentContext.Services` plus the ambient reactive scope; activation
uses explicit `ComponentRegistration` delegates and never runtime constructor discovery.

Build and test the repository from its root:

```powershell
dotnet build Assimalign.Viu.slnx
dotnet test <project>/test/
```

The packaged-consumer showcase is maintained in
[`assimalign/viu-examples`](https://github.com/assimalign/viu-examples). Read the
[`[V01.01.15]` component-model plan](../docs/COMPONENT-MODEL-PLAN.md) for the adopted boundaries and
migration trains, [`docs/SPECIFICATION.md`](../docs/SPECIFICATION.md) for normative semantics, and
[`docs/DEVELOPER-EXAMPLES.md`](../docs/DEVELOPER-EXAMPLES.md) for consumption examples.
