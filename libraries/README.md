# Viu abstraction redesign

This directory is the isolated implementation of the approved split of `Assimalign.Viu.Core`.
Source under `libraries/` remains unchanged until a separate migration is approved.

The primary runtime packages are:

- `Assimalign.Viu.Components`
- `Assimalign.Viu.Reactivity`
- `Assimalign.Viu.State`
- `Assimalign.Viu.Core`

Browser, ServerRenderer, Testing, Router, Browser.Router, Shared, Syntax, template compilation,
single-file component generation, and CSS tooling are wired to the same redesign graph.

The application lifetime boundary is host-neutral. Core exposes `IApplication`, the immutable and
runtime-state-carrying `IApplicationContext`, the lean `IApplicationBuilder`, lifetime middleware,
and the `RunAsync` extension. `BrowserApplication` implements that contract directly and owns its
integer-handle mount APIs. A WebView2 host can implement the same lifetime while providing a
different handle, mount surface, and renderer without changing component, reactivity, or state APIs.

`IComponentFactory` and `IServiceProvider` are separate application-owned resolvers. Viu supplies
no custom dependency-injection container and no component-tree `provide`/`inject`.

Build and test the staging solution with:

```powershell
dotnet build .redesign/Assimalign.Viu.Redesign.slnx
dotnet test .redesign/Assimalign.Viu.Redesign.slnx
dotnet test analyzers/Assimalign.Viu.Generators.Reactive/test
dotnet test analyzers/Assimalign.Viu.Generators.Syntax/test
```

The packaged-consumer showcase is maintained in
[`assimalign/viu-examples`](https://github.com/assimalign/viu-examples). The Browser
compiled-render test project remains the in-repository `.viu` plus renderer integration canary.

Read [`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md) for the implemented boundaries,
[`docs/DEVELOPER-EXAMPLES.md`](../docs/DEVELOPER-EXAMPLES.md) for consumption examples, and
[`docs/MIGRATION.md`](../docs/MIGRATION.md) for the later promotion into `libraries/`.

Known limitation: Suspense mount/update behavior is implemented, but Suspense hydration currently
fails explicitly. Boundary timeout/events, fallback-to-reveal transition choreography, and
hidden-branch post-effect delay are not implemented — see
[§17 of the specification](../docs/SPECIFICATION.md#17-non-goals-and-current-limits).
