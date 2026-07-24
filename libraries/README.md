# Viu abstraction redesign

This directory is the isolated implementation of the approved split of `Assimalign.Viu.Core`.
Source under `libraries/` remains unchanged until a separate migration is approved.

The primary runtime packages are:

- `Assimalign.Viu.Components`
- `Assimalign.Viu.Reactivity`
- `Assimalign.Viu.State`
- `Assimalign.Viu.Core`

Browser, ServerRenderer, Testing, Router, Router.Browser, Shared, Syntax, template compilation,
single-file component generation, and CSS tooling are wired to the same redesign graph.

The application boundary is host-generic. Core exposes `IApplication<TNode>` and
`Application<TNode>` for host-specific node handles; `BrowserApplication : Application<int>` is the
current host, while a WebView2 host can provide a different handle and renderer without changing
component, reactivity, or state APIs.

`IComponentFactory` and `IServiceProvider` are separate application-owned resolvers. Viu supplies
no custom dependency-injection container and no component-tree `provide`/`inject`.

Build and test the staging solution with:

```powershell
dotnet build .redesign/Assimalign.Viu.Redesign.slnx
dotnet test .redesign/Assimalign.Viu.Redesign.slnx
dotnet test analyzers/Assimalign.Viu.Generators.Reactive/test
dotnet test analyzers/Assimalign.Viu.Generators.Syntax/test
```

The repository-level `examples/Assimalign.Viu.WebApp` remains on the pre-redesign API and is not
part of this isolated staging solution. Migrating examples and SDK/framework packaging is an atomic
promotion step described in [MIGRATION.md](MIGRATION.md); the Browser compiled-render test project
is the current `.viu` plus renderer integration canary.

Read [DESIGN.md](DESIGN.md) for the implemented boundaries,
[DEVELOPER-EXAMPLES.md](DEVELOPER-EXAMPLES.md) for consumption examples, and
[MIGRATION.md](MIGRATION.md) for the later promotion into `libraries/`.

Known limitation: Suspense mount/update behavior is implemented, but Suspense hydration currently
fails explicitly. Boundary timeout/events, fallback-to-reveal transition choreography, and
hidden-branch post-effect delay are not yet at Vue parity.
