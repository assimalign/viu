# Assimalign.Viu.FileRouting

[![NuGet version](https://img.shields.io/nuget/v/Assimalign.Viu.FileRouting?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.FileRouting) [![NuGet downloads](https://img.shields.io/nuget/dt/Assimalign.Viu.FileRouting?logo=nuget)](https://www.nuget.org/packages/Assimalign.Viu.FileRouting)

A standalone add-on that builds an eager Viu route table from page components at compile time.
Add one reference to an **`Assimalign.Viu.Sdk` or `Assimalign.Viu.Sdk.Browser` project**:

```xml
<PackageReference Include="Assimalign.Viu.FileRouting" Version="10.0.0-beta.11" />
```

Place `.viu` or compatible `.vue` components under `Pages/`, register the generated components with
the application's `ComponentFactory`, and use the generated table from your root namespace:

```csharp
GeneratedViuComponents.Register(componentFactory);
Router router = new(BrowserRouterHistory.CreateWeb(), GeneratedViuFileRoutes.Create());
```

The browser-history example also assumes the application's ordinary Browser.Router integration.
The add-on itself references only Router and Components and works with `RouterHistory.CreateMemory()`
in host-free applications. It requires the Viu SDK to supply component files and compile/register
their definitions. It adds no SDK or framework manifest entry.

| Page convention | Route |
|---|---|
| `Pages/Index.viu` | `/`, named `index` |
| `Pages/QuickStart.viu` | `/quick-start`, named `quick-start` |
| `Pages/Guides/GettingStarted.vue` | `/guides/getting-started` |
| `Pages/Blog/[Slug].viu` | `/blog/:slug`, forwarding `slug` |
| `Pages/Search/[[Term]].viu` | `/search/:term?`, forwarding `term` |
| `Pages/Files/[...Rest].viu` | `/files/:rest(.*)*`, forwarding `rest` |
| `Pages/Blog.viu` beside `Pages/Blog/` | `/blog` parent, folder entries become relative children |
| `Pages/Blog/Index.viu` below that layout | Empty child path, named `blog-index` |

Examples are independent: **page component file base names must be unique in an assembly**.
For example, root `Index.viu` and `Blog/Index.viu` cannot coexist. Rename one and use its route block
to retain the desired path. The generator also rejects different names that sanitize to the same
component registration identity. Other components outside the pages root are not route candidates;
their registration names must also remain unique in the application's component factory.
The uniqueness rule includes same-stem `.viu`/`.vue` peers: keep only one page input. The syntax
compiler normally prefers `.viu` under `[VUE-7]`; FileRouting's stricter page-input rule does not
change that parser behavior.

Static segments use deterministic ASCII kebab case: `XMLParser` becomes `xml-parser`,
`HTTP2Server` becomes `http2-server`, and digits stay attached (`Version2` becomes `version2`).
Dynamic names are lowercased without introducing hyphens: `[BlogSlug]` forwards `blogslug`.
Declare corresponding component inputs explicitly, for example `[Parameter("slug")]`.

| Build property | Default | Purpose |
|---|---|---|
| `ViuFileRoutingEnabled` | `true` | Set `false` to stop emitting `GeneratedViuFileRoutes`. |
| `ViuFileRoutingPagesDirectory` | `Pages` | Select a project-relative pages directory. |

An absent or empty pages root generates an empty table. No directory is scanned at runtime.
The table order is ordinal and stable; Router owns match ranking.

A `.viu` page can override its path and name with a tiny custom block:

```text
@route {
    path = "/custom/:id";
    name = "custom";
}
```

The compatible `.vue` form is `<route>path = "/custom/:id"; name = "custom";</route>`.
Nested overrides may be relative or empty. Every value is double quoted and every assignment ends
in `;`; only `path` and `name` are supported.
The `.viu` closing brace follows the ordinary container rule: column zero on its own later line.

The application renders the first matched record with `<RouterView/>` (depth 0). A layout rendered
at depth 0 must contain `<RouterView :depth="1"/>`; a layout at depth 1 uses depth 2. The generator
documents each layout's required child-outlet depth in its output, but does not modify templates.

**Current compiler limitation:** a sibling-file layout such as `Pages/Blog.viu` plus
`Pages/Blog/Entry.viu` produces a C# type/namespace collision in the unchanged component compiler.
FileRouting generates and tests the nested route table, but this physical layout cannot yet compile
through both generators. Use flat pages and manually compose nested descriptors until component
namespace generation receives a separate fix. See [the design limitation](docs/DESIGN.md#current-component-compiler-layout-limitation).

A layout template writes the literal outlet `<RouterView :depth="1"/>`. Since [V01.01.08.03.02]
(#359) the syntax checker reads RouterView's hand-authored registration as an opaque contract, so
the usage compiles without `VIU1401`. Register the name `RouterView` as an alias of
`RouterView.Registration` in the component factory, because compiled component tags resolve by
registered name. The [compiled fixture](../../../analyzers/Assimalign.Viu.Generators.FileRouting/test/Assimalign.Viu.Generators.FileRouting.CompiledFixtureTests/README.md)
demonstrates the working setup.

See [overview](docs/OVERVIEW.md), [design and exact conventions](docs/DESIGN.md), and
[generator diagnostics](../../../analyzers/Assimalign.Viu.Generators.FileRouting/docs/DIAGNOSTICS.md).
