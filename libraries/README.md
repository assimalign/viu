# Viu public libraries

This directory contains Viu's publicly consumable packages. Projects follow the area-based layout
`libraries/<Area>/<AssemblyId>/{src,test,docs}`; the area groups related packages without changing
their assembly ids or namespaces.

The areas are:

- `Runtime/` — `Assimalign.Viu.Components`, `Assimalign.Viu.Core`,
  `Assimalign.Viu.Reactivity`, and `Assimalign.Viu.State`;
- `Browser/` — `Assimalign.Viu.Browser` and the browser-specific router bridge;
- `Router/` — the host-neutral `Assimalign.Viu.Router` package;
- `ServerRenderer/` — DOM-free server rendering;
- `DevTools/` — optional runtime inspection and the in-memory testing host; and
- `Syntax/` — the five publicly consumable `netstandard2.0` parser packages for CSS, HTML,
  templates, and canonical or compatible single-file-component containers.

The Syntax cluster lives here deliberately. It is a public parsing surface for developers and
future extensible-tooling work, even though those assemblies execute at build or editor time rather
than in a Viu application's runtime. The `libraries/` boundary therefore means publicly consumable,
not runtime-only.

The runtime areas document the installed `[V01.01.15]` component model and its current
compiler/runtime contracts.

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
