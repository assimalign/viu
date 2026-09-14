# Building the documentation site

This is Viu's documentation home, extending the API-reference pipeline from
[V01.01.13.04](https://github.com/assimalign/viu/issues/101) through the in-repository scope of
[V01.01.13.05](https://github.com/assimalign/viu/issues/102).
Run `pwsh scripts/Build-ApiReference.ps1` from the repository root after the prerequisite
solution restore described in [CONTRIBUTING.md](../CONTRIBUTING.md#build-the-api-reference).

The local tool manifest pins docfx. Its bundled `default` and `modern` templates need no theme
download or CDN. There are no external xref maps, plugins, or build-time network requests after
tool restore. Generated inputs and logs live under `_out/api-reference-work/`; the finished site
lives under `_out/api-reference/`. Neither belongs in source control.
The script disables CLI telemetry and background workload-manifest downloads for its duration,
then restores the caller's environment.

The summary records docfx, SDK, and .NET 10 runtime versions. Metadata uses one canonical copy of
each public assembly and restored dependency. It supplies managed runtime dependencies while
leaving docfx's core/facade choices intact for the mixed `net10.0`/`netstandard2.0` inputs.

The packaging module discovers shipping libraries and rejects inventory drift. All five public
Syntax parsers are added explicitly despite their non-packable project setting. UtilityCss and
FileRouting are included as standalone add-ons. The task-only UtilityCss.Build package remains
covered by the solution's XML compilation gate but contributes no API pages. Tests, analyzers,
compiler/editor implementation projects, language-server executables, SDK tasks and framework
containers are not metadata inputs.

The Templates parser compiles linked copies of Components' `PatchFlags` and `SlotStability`.
It has a separate metadata group and namespace filter; the final API tree keeps the canonical
Components declarations once. Public `EditorBrowsable(Never)` host and generated-code contracts
are documented; internal/private declarations remain excluded.

`Build.Packaging.targets` enables XML files for packable projects, including Release, without
suppressing CS1591. Release alone does not promote CS1591 to an error. This pipeline builds with
warnings as errors and explicitly enables XML output for the otherwise non-packable Syntax
parsers. A fresh `CustomAdditionalCompileInputs` marker, imported through a generated targets file,
forces compilation so cached permissive builds cannot hide missing comments. It avoids solution
`Rebuild`, which cleans SDK staging paths that other solution projects consume. The generated
targets and marker stay under `_out/`; repository build settings remain unchanged. The pipeline
never passes the XML-output switch to test projects.

The specification's paragraph-start clause definitions are the only mapping authority. The
preprocessor adds `clause-` plus the lowercase id as an explicit anchor to the staged specification
and links bracketed clause citations in staged XML to it. Unknown citations and duplicate clause
definitions fail generation. WBS ids such as `[V01.01.13.04]` are work items, not clause ids.
Source XML comments and the normative Markdown remain unchanged.

## Navigation and publication

One table of contents has Overview, Getting started, Guides, Libraries, Specification, and API
reference. `New-ViuDocumentationNavigation` discovers every Markdown guide under `docs/guide/`,
every `libraries/<Area>/<Assembly>/docs/OVERVIEW.md`, and the SDK overview plus Markdown reader
documents under `sdks/<Sdk>/docs/`. Library areas are Runtime, Browser, Router, State,
ServerRenderer, DevTools, Syntax, and Utilities, followed by SDKs. State keeps its Runtime source
path and has its own reader-facing group. A new library area fails discovery until deliberately
placed in navigation.

These pages and their directly linked reader documentation publish with repository-relative paths,
so docfx validates local links and fragments. A `DESIGN.md` linked from an overview is therefore
published; further links do not recursively crawl repository documentation. Further repository
navigation and agent/contributor source files link to the exact Git revision after checking that
their targets exist locally. The landing-page source retains valid repository-relative links;
staging relocates that page to the site's root `index.html` and rewrites links accordingly.

The script evaluates `ViuVersion` through MSBuild and injects it into the landing page and docfx
metadata for the navigation bar and footer. The landing page identifies the implemented behavior
covered by that version; linked planning material remains distinct from shipped guarantees.
Generation fails on docfx warnings. The offline repository link gate is
`pwsh scripts/Test-DocumentationLinks.ps1`; it runs before the site build in the Documentation
workflow.

## Static search

Docfx generates `index.json` from local rendered pages during `docfx build`. The bundled templates
ship `public/docfx.min.js`, `public/search-worker.min.js`, and their local JavaScript chunks with the
artifact; `_enableSearch` enables that client-side search. The page starts the module worker, which
fetches `../index.json` and builds its in-memory Lunr index. With the checked-in default options,
no language extension or remote resource is requested. An HTTP ETag allows an optional IndexedDB
cache; neither is required for search.
The build fails if the index omits the landing page, getting-started guide, specification, or all
API pages, and reports the search-entry count in `_out/api-reference-work/summary.json`.

Search needs no hosted search API, database, external index, CDN, or application server. It loads
the local index and ranks results in the browser. Serve `_out/api-reference/` with an ordinary
static HTTP file server (including GitHub Pages); the static host only returns files. Opening
`index.html` directly with a `file://` URL is not supported because browsers restrict local fetches
and worker loading. This is a browser-origin restriction, not a search-service dependency. Once the
tool and build prerequisites are restored, generation is offline; once the files are served locally,
search also works without internet access. Verification for this change executed the emitted worker
and its bundled dependency against only the local index, with network access absent: searches for
`StaticSiteGenerator` and `Getting started` returned the API page and guide respectively. This checks
the actual search engine and local inputs; it does not replace a browser interaction check.

## Publication and planned migration

Deployment stays off until the owner enables GitHub Pages with **GitHub Actions** as its source and
sets the repository Actions variable **`VIU_DEPLOY_DOCS=true`**. The main-only deployment job consumes
the successful `api-reference` artifact. See [CONTRIBUTING.md](../CONTRIBUTING.md#publish-the-documentation-site)
for the switch and [ADR-0006](../adr/0006-documentation-site-generator.md) for the interim generator
decision and tracked Viu static-prerendering migration in `assimalign/viu-docs`.
