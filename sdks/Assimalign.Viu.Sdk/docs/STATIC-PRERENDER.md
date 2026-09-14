# Static prerendering

Static prerendering renders an explicit route list into complete UTF-8 documents that boot the same
WebAssembly application. The generator and console helper live in the ordinary opt-in
`Assimalign.Viu.ServerRenderer` package. The base SDK carries the publish target and process task;
neither library gains a web-framework dependency. Specified by
[V01.01.07.05](https://github.com/assimalign/viu/issues/68), `[SSG-1]` through `[SSG-6]`, and `[PKG-4]` in
[the specification](../../../docs/SPECIFICATION.md).

## Application composition

The server executable explicitly registers its root and route components, supplies a fresh request
scope and `ServerRenderApplication` for every route, and creates each router with memory history.
Its entry point delegates command parsing and exit status to `StaticSiteGeneratorHost.RunAsync`:

```csharp
return await StaticSiteGeneratorHost.RunAsync(args, CreateStaticSiteGenerator);
```

`CreateStaticSiteGenerator` is the application's factory, returning its configured generator. It
owns the registrations and request-scope factory; no assembly discovery or reflective activation
takes place. Materialized stores use the application's existing source-generated serialization
contexts. Each request goes through `ServerRenderAdaptor.RenderDocumentAsync`, which embeds a
composed state registry's payload and preserves Viu's hydration markers. A failing route stops
generation and is named in the error; already emitted routes remain on disk.

The host accepts an optional leading `prerender` token, `--output`, repeated `--route`,
`--routes <file>`, `--host-page`, `--base`, and optional `--mount` (default `#app`). Direct console
use defaults to `/` when no routes are supplied. For example:

```console
dotnet run --project Server -- prerender --output published/wwwroot --host-page published/wwwroot/index.html --route / --route /guide/intro --base /
```

Routes are base-stripped locations. Query and fragment remain available during router resolution,
but never become path components: `/` emits `index.html`, and `/guide/intro` emits
`guide/intro/index.html`. The base option configures memory history, without adding an output
directory or rewriting URLs in the host page.

## Publish integration

Configure the Browser application that owns the published host page:

```xml
<PropertyGroup>
    <ViuStaticPrerender>true</ViuStaticPrerender>
    <ViuStaticPrerenderProject>../Server/Server.csproj</ViuStaticPrerenderProject>
    <ViuStaticPrerenderRoutes>/;/guide/intro</ViuStaticPrerenderRoutes>
</PropertyGroup>
```

Then run `dotnet publish --configuration Release`. `ViuStaticPrerender` runs after `Publish`,
restores and builds the named single-target server executable, then launches its built assembly
through `dotnet exec`. Browser runtime identifiers, output paths, trimming, and publish globals
are removed from that nested build, and recursive static prerendering is disabled. The task passes
each option directly to the child process without a command shell, forwards emitted-file reports,
and fails the build when the host returns a nonzero exit code.

| Property | Contract and default |
| --- | --- |
| `ViuStaticPrerender` | `false`; set to `true` to enable the after-publish target |
| `ViuStaticPrerenderProject` | Required server executable project, relative to the publishing project or absolute |
| `ViuStaticPrerenderRoutes` | Semicolon-separated base-stripped locations |
| `ViuStaticPrerenderRoutesFile` | Optional UTF-8 file with one route per line; relative to the publishing project or absolute |
| `ViuStaticPrerenderHostPage` | Published `wwwroot/index.html` when present; otherwise published `index.html` |
| `ViuStaticPrerenderOutput` | Published static web root containing the resolved host page (`PublishDir/wwwroot` for Browser applications) |
| `ViuStaticPrerenderBase` | `/`; the memory-history base, without host-page rewriting |

At least one route source is required. Both sources may be supplied together. Use a routes file
for locations containing semicolons or other MSBuild syntax. Relative file properties are resolved
against the publishing project before the server process starts. The output setting selects only
where documents are emitted: a custom output directory does not copy the application's published
assets into it.

The document shell reads the **published** host page once and replaces the contents of its `#app`
mount element. The selector grammar is a single `#id`, not a general CSS selector. Its opening and
closing tags and every surrounding character remain intact,
including `OverrideHtmlAssetPlaceholders` results, import maps, stylesheet links, and fingerprinted
bootstrap references. The authored host page must already contain a correct `<base href>` or
root-relative asset references so nested static routes boot successfully. The Browser application
must choose hydration and restore its state registry before mounting, as required by `[HYD-8]`.

The built-in host-page shell rejects teleport output because target placement belongs to the host.
Applications with enabled teleports call `StaticSiteGenerator.GenerateAsync` with their own
`IServerRenderDocumentShell`, which must place each completed teleport buffer at the correct target
under `[SSR-14]` and `[HYD-6]`. After replacing a static document, the filesystem output removes
obsolete sibling `.gz` and `.br` files so content negotiation cannot serve the pre-prerender page.

The packaged [prerender fixture](../../../scripts/fixtures/EndToEndPrerenderApp/) publishes root
and nested routed pages and shares the existing Chromium hydration checks. Its
[server executable](../../../scripts/fixtures/EndToEndPrerenderHost/) provides the explicit
request-scope, state-registry, and component registrations.

The single target source lives at
[`Targets/Assimalign.Viu.Sdk.StaticPrerender.targets`](../Targets/Assimalign.Viu.Sdk.StaticPrerender.targets).
`Sdk/Sdk.targets` imports it for package consumers, and `sdks/Directory.Build.targets` imports it
directly for SDK dogfooding. Other in-repository consumers can import that same file explicitly and
use the built base SDK task assembly. No new package inventory entry is required.

The documentation-site consumer in `assimalign/viu-docs` is a separate follow-up.
