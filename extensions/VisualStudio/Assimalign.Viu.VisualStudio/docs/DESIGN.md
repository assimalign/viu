# Visual Studio language tooling design

## Decision

Viu uses the out-of-process `VisualStudio.Extensibility` model as a thin Visual Studio client and a
standalone Language Server Protocol process as the semantic boundary.

This is a better long-term fit than an in-process Visual Studio SDK language service:

- failures and parser/Roslyn dependency conflicts do not destabilize `devenv.exe`;
- the language engine is reusable by Visual Studio, Visual Studio Code, Rider, and other clients;
- Visual Studio's language-server surface covers completion, hover, diagnostics, navigation,
  formatting, code actions, references, and rename;
- the client-specific layer stays limited to document registration, process lifetime, and editor
  presentation.

The Visual Studio language-server and tagger APIs are still marked preview in the 17.14 extensibility
line. They are isolated in `ViuLanguageServerProvider`, `ViuClassificationTaggerProvider`, and
`ViuClassificationTagger` so a future API migration does not reshape the language service.

## Components

```text
Visual Studio
  -> Assimalign.Viu.VisualStudio (out of process)
       -> classification tagger
       -> stdio Language Server Protocol connection
            -> Assimalign.Viu.Tooling.LanguageServer
                 -> Assimalign.Viu.Tooling.LanguageService
                      -> Assimalign.Viu.Syntax.SingleFileComponent
                      -> Assimalign.Viu.Tooling.SingleFileComponent (the shared build/editor projection, [V01.01.06.11])
                      -> Assimalign.Viu.Tooling.UtilityCss
```

`extensions/VisualStudio/` holds `Assimalign.Viu.VisualStudio` and the payload-free
`Assimalign.Viu.VisualStudio.Registration` companion (see "File extension ownership"). Everything
below the stdio boundary is editor-neutral developer tooling and lives under the repository's
`tooling/` root (`tooling/Assimalign.Viu.Tooling.LanguageServer`,
`tooling/Assimalign.Viu.Tooling.LanguageService`, and the build-time cores they consume).

`Assimalign.Viu.VisualStudio` performs fast lexical classification using Visual Studio's built-in
classification categories. It lexes both container syntaxes of the hybrid `.viu` format
([V01.01.06.10]): tag-delimited top-level `<template>`/`<style>` sections (with nested-`<template>`
depth tracking so slot fragments do not end a section) plus the `@script` @-block, and the legacy
`@template`/`@style` @-blocks keep highlighting during the migration window.

The out-of-process `VisualStudio.Extensibility` model cannot define custom classification types or
format definitions: there is no equivalent of the in-process MEF
`ClassificationTypeDefinition`/`ClassificationFormatDefinition` exports, so the extension cannot
introduce its own colors, set bold or italic, or add "Viu Component"-style entries to
Tools > Options > Fonts and Colors. Every token must borrow one of Visual Studio's built-in
categories, the user's theme owns the actual color values, and LSP semantic tokens share the same
ceiling (Visual Studio maps them onto the same built-in classifications). The result is a real
palette upgrade assembled entirely from the theme's existing colors — a bespoke Viu palette would
require an in-process editor component, which this extension deliberately is not (see
"Bespoke Viu colors and the companion package" below for the one place it could live, and why it
does not live there yet). The recorded mapping decisions:

- PascalCase (or dotted) template tag names classify as components and borrow the `type` category,
  so components render in the same teal Visual Studio uses for C# and Razor type names; lowercase
  tag names stay `markup node`. Casing is the only signal a purely lexical classifier has — it never
  consults a component registry — and Viu's ordinal name resolution makes the authored spelling
  meaningful (specified by `[CMP-6]`).
- Directive attribute names (`v-*`, `@event`, `:bind`, `#slot`, including valueless directives such
  as `v-else`) borrow `keyword` so they pop against plain markup attributes.
- `{{ }}` interpolation delimiters borrow `keyword`, and interpolation interiors run the C# token
  passes so expressions color like script code.
- `class` attribute values split lexically into utility variant prefixes (`hover:`, `md:` →
  `keyword`) and utility classes (→ `string`, `[...]` arbitrary values included); candidate
  validation stays in the language server per the source boundaries below.
- Style custom properties (`--name`) borrow `type` so theme tokens stand apart from ordinary
  declarations.
- Semantic method spans map to the base `identifier` category and punctuation maps to `operator`:
  Visual Studio does not register the SDK's `method` name, while `punctuation` is supplied only
  when Roslyn editor features are present. These fallbacks keep the VSIX independent of a
  particular managed-language workload.

If the Extensibility SDK later adds custom classification registration, the mapping table in
`ViuClassificationTagger.GetClassificationType` is the single place to upgrade.

### Reporting granularity

`ViuClassificationTagger` reports the **whole document** on every report, not the ranges Visual
Studio asked about. Visual Studio caches what a tagger reports: `UpdateTagsAsync` takes the
calculated ranges separately from the tags so the range set can mark a region as answered
independently of whether that answer held any tags ("all previous tags that are fully overlapping
`updatedRanges` will be considered outdated"), and the companion `GetAllRequestedRangesAsync` — "all
the ranges for which tags have been requested so far" — exists only because that cache survives
across requests.

Out of process, each request and each report is a JSON-RPC round trip. Reporting only the requested
lines therefore left every line the user had not scrolled to yet uncalculated on the Visual Studio
side, so scrolling drew those lines in the default text color until the round trip completed — a
visible colorization flicker. Classification is whole-document lexing anyway (the lexer must see the
container sections above a line to color it), so widening the report costs nothing to compute and
lets Visual Studio answer later scrolling from its own cache with no round trip.

Two guards keep the wider payload cheap. The lexer result is cached on the document version, because
Visual Studio raises a text-view change and then requests tags for the same snapshot — one keystroke
reaches the tagger twice. And a version already delivered is not reported again unless
`recalculateAll` says Visual Studio's copy is stale; the version watermark advances only after the
report completes, so a cancelled or failed report cannot leave the document uncolored. The change
notification no longer calls `GetAllRequestedRangesAsync` at all: it is a round trip that returns a
strict subset of what is reported regardless.

The reporting policy is expressed in offsets rather than editor types
(`ViuClassificationReportHandler`) because `ITextDocumentSnapshot` carries internal members and
cannot be implemented outside the Visual Studio SDK; only the thin snapshot adapter in the tagger
touches editor types, and it is the one part of the path that unit tests cannot reach.

### Bespoke Viu colors and the companion package

The `Assimalign.Viu.VisualStudio.Registration` companion (see "File extension ownership") is a
classic in-process VSIX, so it *could* host what this extension cannot: MEF
`ClassificationTypeDefinition` and `ClassificationFormatDefinition` exports, meaning real
Viu-owned colors, bold/italic, and "Viu Component"-style entries in Tools > Options >
Fonts and Colors. That capability is recorded here so the option is not rediscovered later; it is
**deliberately not used today** — the companion's entire payload is the `.pkgdef`.

If bespoke theming is ever prioritized, there are two delivery paths and they are not equally
credible:

- **(a) Companion defines the classification types; this out-of-process tagger emits their names.**
  Cheapest if it works, because classification logic stays in one place. It is **unverified and
  doubtful**: the remote tag bridge already refused a name as ordinary as Roslyn's standard
  `method` (which is why semantic method spans fall back to `identifier` above), so there is no
  evidence it will accept names invented by an in-process sibling package. Anyone taking this path
  must prove the round trip before designing around it.
- **(b) The companion hosts a thin in-process classifier of its own**, reusing the same lexical
  logic that `ViuLexicalClassifier` implements, and owns both the classification types and the
  tagging for `.viu` buffers. This path is **guaranteed to work** — it is the ordinary MEF editor
  extensibility model — at the cost of running classification in process for `.viu` buffers and
  keeping one lexer shared by two hosts.

**(b) is the recommended route** if the feature is taken up: a verified in-process classifier is
worth more than an unverified cross-process name channel, and the lexer is already an isolated,
Visual Studio-free component that both hosts can share. Neither path changes the language-server
boundary — semantic work stays out of process either way.

`Assimalign.Viu.Tooling.LanguageServer` owns protocol framing and translates protocol values into
editor-neutral contracts. It writes protocol messages only to standard output; standard error is
reserved for diagnostics.

`Assimalign.Viu.Tooling.LanguageService` caches the current text and the format-appropriate immutable
container parse for each open `.viu` or accepted `.vue` document. It exposes block diagnostics,
completion catalogs, declaration-aware `@script` member completion ([V01.01.12.07.04] #261 — a
syntax-only Roslyn parse of the script block, cached on the block text; no compilation, no
workspace), semantic `@script` completion when the host feeds a restored project context
([V01.01.12.23] #259 — an artifact-fed `CSharpCompilation` answered through
`SemanticModel.LookupSymbols`; still no workspace), shared utility-class completion,
project-defined utilities and variants, and generated-CSS hover documentation. It never loads a
Roslyn workspace.

Visual Studio requires a language-server provider's `DocumentFilter` to select a document type; it
rejects a path-only filter, and a document-type filter cannot express an owning-project
build-property condition. The client therefore contributes canonical `.viu` and compatibility
`.vue` document types. The extension activates when a canonical `.viu` buffer opens or when a
solution contains a project whose evaluated `ViuVisualStudioLanguageServiceEnabled` property is
`true`. This keeps compatibility dormant in ordinary solutions without sacrificing canonical or
loose `.viu` editing.

Before admitting a `.vue` document, the language-server host performs a deliberately narrow
nearest-owning-project check for `Assimalign.Viu.Sdk` or the explicit
`ViuVisualStudioLanguageServiceEnabled` marker. It stops at the first directory containing a
project so an unrelated nested Vue project is not claimed by a Viu ancestor. An explicit literal
`false` marker overrides SDK-name detection. The host repeats this eligibility check for document
changes, diagnostics, completion, and hover, so Visual Studio's necessarily broad path routing
cannot activate Viu behavior in a non-Viu `.vue` document in a mixed solution. If the nearest
directory contains both eligible and ineligible project files, ownership is ambiguous without an
evaluated project-system query and the probe fails closed.

## Viu Utilities project context

`Assimalign.Viu.Tooling.UtilityCss` is the single compiler/editor authority. Its contract is frozen
to Tailwind CSS v4.3.3 by
[`compatibility-v4.3.3.json`](../../../../tooling/Assimalign.Viu.Tooling.UtilityCss/conformance/compatibility-v4.3.3.json)
and independently authored
[`golden-vectors-v4.3.3.json`](../../../../tooling/Assimalign.Viu.Tooling.UtilityCss/conformance/golden-vectors-v4.3.3.json).
The manifest enumerates 382 utility roots, 88 variants, 21 theme namespaces, value and modifier
modes, source forms, directives, functions, and canonical ordering. The language service consumes
that registry directly; completion detail and hover display executable compiler output.

For each completion or hover request, the language-server host:

1. finds the nearest owning Viu project;
2. reads exactly one literal `<ViuUtilityCss Include="...">` item;
3. reloads the project stylesheet;
4. builds its recursive relative `@reference` graph;
5. parses the virtual import/source configuration and immutable theme;
6. compiles local and referenced custom utility/variant definitions;
7. resolves the authored candidate through the same built-in and project compilers used by the SDK.

The CSS-first project entry supports:

- `@import "viu-utilities"` with `source(<path>|none)`, `prefix(...)`,
  `theme(inline|static)`, and `important`;
- path and inline `@source` inclusion/exclusion, including brace expansion and numeric ranges;
- normal, `inline`, `static`, `reference`, and `default` `@theme` declarations;
- static, nested, negative, and functional `@utility`;
- selector and block `@custom-variant`, authored `@variant`, and built-in/custom `@apply`;
- `@reference` composition for shared theme, utility, and variant definitions;
- `--value()`, `--modifier()`, `--default()`, `--spacing()`, and `--alpha()`.

`"viu-utilities"` is a Viu compiler sentinel, not a package. The VSIX does not install, load,
bundle, or coordinate with Tailwind CSS or Tailwind CSS IntelliSense. Viu Utilities is an
independent Viu feature compatible with documented Tailwind CSS v4.3.3 behavior; it is not
affiliated with or endorsed by Tailwind Labs.

The pre-`MSBuildWorkspace` project lookup is intentionally conservative. Multiple utility entries,
an MSBuild property or wildcard in `Include`, a missing file, or an unreadable reference falls back
to the built-in registry and default theme. The service does not guess an evaluated path. Custom
static utilities and theme-backed values are offered directly; functional definitions can expose
their root, but completion does not invent project-specific functional values absent from the
theme or authored source.

## Source and update boundaries

Utility IntelliSense activates only in static class attributes and literal class-binding strings.
The container parser supplies only `.viu` or `.vue` `<template>` text (including the legacy `.viu`
`@template` container during its migration window) to that context.
Script and style regions, ordinary `.cs`, arbitrary C# strings, and runtime-built class fragments
are never utility candidate sources. Complete alternatives must appear in template text or be
included through `@source inline(...)`. Code-first utility discovery is a separate deferred
feature.

The `@source` forms above govern SDK build-time candidate discovery. The language server does not
crawl configured source roots or convert inline source entries into completion suggestions; it
uses the loaded configuration to resolve class text authored in the template being edited.

The language server reads CSS-first configuration for editor semantics but does not write bundles
or refresh the browser. In a Debug `dotnet watch` session, the packaged SDK launches one
project-scoped CSS sidecar. It watches component files, the utility entry, explicit utility-source
items, and supported automatically discovered markup; batches regeneration; and lets the .NET
browser-refresh client replace the generated stylesheet links. A CSS file reached only through
`@reference` is re-read on compilation but is not independently a watch trigger unless it is also
an explicit `ViuUtilityCssSource`. This CSS-only update does not remount the Viu application or
discard browser state. Visual Studio's ordinary Hot Reload command does not invoke that watch-list
contract. Template/C# generation remains on the normal .NET build/Hot Reload path and remounts the
affected component on .NET 10 browser WebAssembly.

## Semantic IntelliSense roadmap

Project-aware IntelliSense requires one authoritative `.viu`/`.vue` to C# projection and source map:

1. **Delivered ([V01.01.06.11], #258).** The generator's component-name, script-region,
   generated-context, and source-mapping logic is extracted into the shared
   `Assimalign.Viu.Tooling.SingleFileComponent` library
   (`tooling/Assimalign.Viu.Tooling.SingleFileComponent`, see its `docs/DESIGN.md`); the source
   generator and this language service both consume it, and the two-host conformance test
   (`analyzers/Assimalign.Viu.Generators.Syntax/test/SingleFileComponentProjectionConformanceTests.cs`)
   pins ordinal-identical generated source, hint names, and diagnostics.
2. Have both the source generator and language service consume that projection builder so editor and
   compiler behavior cannot drift. (The extraction landed this for today's surfaces — the shared
   `ScriptBlockAnalyzer` member description and the block-to-file position composition that places
   outline symbol children (`SingleFileComponentDiagnostics.ComposeToFilePosition`, the same
   arithmetic as the emitted `#line` map); the full projected-document
   consumption arrives with steps 3–5.)
3. **Delivered ([V01.01.12.23], #259) — recorded decision: artifact-fed, not `MSBuildWorkspace`.**
   The server never evaluates the project. `ViuProjectAssetsReader` resolves the restore's own
   artifact (`obj\project.assets.json`) with pure `System.Text.Json`: package compile assets
   against the declared `packageFolders` order using NuGet's `.nupkg.metadata` restore-success
   marker; `Microsoft.NETCore.App` reference assemblies from the installed SDK's targeting pack
   (dotnet root pinned by the restore's `runtimeIdentifierGraphPath`, then `DOTNET_ROOT*`, then
   `%ProgramFiles%\dotnet`); every other shared framework (`Assimalign.Viu.App`) from its pinned
   `<name>.Ref` `downloadDependencies` package; project references from built outputs
   (newest wins, missing outputs dropped with a named remedy). No `MSBuildWorkspace`, no BuildHost
   processes, and no muxer/`global.json`/arm64 resolution exposure. The consumer prerequisite is
   exactly: **the project restored once (`dotnet restore`) plus an installed .NET SDK** for the
   `Microsoft.NETCore.App.Ref` targeting pack — the packed-consumer round trip
   (`scripts/Test-LanguageServerSemanticRoundTrip.ps1`, wired into `area-visual-studio.yml`)
   proves that documented prerequisite is sufficient against the published single-file server.
   Degradation is graceful and visible: a per-project status ladder (missing artifacts, partial
   resolution, stale artifacts, active) reports through `window/logMessage` on state-signature
   transitions — never per keystroke, re-warning on re-degradation — while loose files outside any
   Viu project stay silent, and the no-project-state path answers byte-identically to the
   pre-semantic service (both pinned by `LanguageServerSemanticDegradationTests`). Status messages
   carry the absolute project path so two same-named projects stay distinguishable. Two recorded
   decisions in this ladder: a healthy project reports "active" once on first resolution — not only
   on a degraded-to-available transition — so availability is visible in both directions; and an
   unresolvable package compile asset degrades the whole project to syntax-only with a warning
   naming the package, rather than serving a partial compilation whose missing references would
   surface as misleading binder misses (a partial-compilation rung remains a possible refinement).
4. **Delivered ([V01.01.12.23], #259) — SemanticModel backend for increment 1.** The open
   document's live text is projected through the shared library (generator-identical by
   construction), emitted, parsed, and added as an immutable fork of a per-project cached
   `CSharpCompilation` holding sibling `.cs` sources and projected sibling components. Completion
   binds with `SemanticModel.LookupSymbols` (member access binds the receiver first): no
   workspace, no MEF composition, no Features assemblies, zero new packages in the trimmed
   single-file publish. The recorded increment-2 upgrade behind the same engine seam is
   `AdhocWorkspace` + `CompletionService` (Features/Workspaces packages re-entering the central
   catalog at the single Microsoft.CodeAnalysis 5.3.x line, without `PrivateAssets=all`); its gate
   is an integration test against the actual trimmed single-file publish (MEF and
   `Assembly.Location`-dependent code paths are exactly what in-process tests cannot see) plus a
   VSIX size re-measure against the 50MB Marketplace budget (estimated +10–12 MB per compressed
   RID). Known gap, recorded as a non-goal for this increment: sibling `.cs` members produced by
   the **Reactivity source generator** are absent from the editor compilation — only the Syntax
   generator's projection is shared. The later option is running the ref pack's packaged
   generators through `CSharpGeneratorDriver`, with version-skew and load-isolation questions to
   settle then.
5. **Delivered for completion ([V01.01.12.23], #259).** `GeneratedScriptDocumentMapper` maps the
   member and using regions bidirectionally through the emitter's simple-form `#line` directives
   (line-affine, column-identity) and suppresses — never misplaces — positions in scaffold or
   render-body spans. Completion and resolve documentation ride the map today; hover, signature
   help, definitions, references, and diagnostics remain open work on the same map.
6. Integrate the existing template and CSS syntax trees for precise semantic tokens and recoverable
   embedded-language diagnostics.

Parsing remains cancellable and off the Visual Studio UI path: feature requests dispatch
concurrently with `$/cancelRequest` support, semantic engine work is serialized under its own gate,
and every cache re-validates by cheap stats on the next request (no file watchers).

## File extension ownership

Nothing in a Visual Studio installation registers the `.viu` file extension — a scan of all 525
`.pkgdef` files in VS 18 Community finds no entry for it. An extension with no registration falls
through to the **wildcard** (`"*"`) editor factories, which are offered in descending priority:

| Editor factory | Wildcard priority | Outcome for `.viu` |
| --- | --- | --- |
| TextMate | `0x25` | Declines — no `.viu` grammar |
| XML Editor Chooser | `0x21` | Declines — no designer registered |
| **Microsoft XML Editor** | `0x20` | **Claims the document** |
| Source Code (Text) Editor | `0x1e` | Never reached |

`Microsoft.XmlEditor.Sniffer.SniffXmlDocument` accepts any document whose first token is a
well-formed start tag. A `.viu` container opens with `<template>`, so the sniff succeeds on that
first tag and never reads the `@script` block below it. The factory then attaches the XML language
service and sets `guidVSBufferDetectLangSid` to false, which **suppresses later content-type
detection** — the buffer stays XML-derived for its lifetime.

Three consequences follow, and they explain behavior that otherwise looks like extension bugs:

- The `viu` content type contributed through `documentTypes` never reaches the buffer, so any part
  filtered on document type `viu` — including `ViuLanguageServerProvider`, whose filter *must* name a
  document type — is never activated. **Semantic IntelliSense does not work until `.viu` is
  reassociated.**
- The XML parser reports its own diagnostics against C# and container syntax: `@script {` becomes
  "Invalid token 'Text' at root level of document", `Get<string>` becomes "Tag was not closed", and
  `<style scoped>` becomes "Missing attribute value on attribute 'scoped'". These carry no error
  code, are editor-only, and never affect a build.
- `.vue` is unaffected, because Web Tools claims that extension explicitly at `0x33`, outranking the
  wildcard.

`ViuClassificationTaggerProvider` is therefore filtered on the built-in `text` document type, which
both the XML and plain-text content types derive from, and selects containers itself in
`IsSingleFileComponent`. Colorization consequently works regardless of which editor won the file.

**The supported resolution is per-user**: Open With → *Source Code (Text) Editor* → Set as Default.
`GetDesignerFactory` consults the user-defined editor mapping before the priority ladder, so this
bypasses the sniffer; the text editor attaches no language service, so the buffer then takes its
content type from this extension's own `documentTypes` registration and the language server starts.

**The durable fix is a `.pkgdef`** claiming `.viu` for the text editor at `0x32`, the band Microsoft
itself uses for explicitly owned extensions (`json`, `html`, `css`), with `0x31` on the
with-encoding sibling factory — the same one-lower offset Visual Studio's own registrations use.
`0x32` outranks every wildcard factory in the ladder above, so the text editor claims the file
outright.

This extension still cannot carry that `.pkgdef`: the asset requires `VssdkCompatibleExtension`,
which in turn mandates `RequiresInProcessHosting`, so the main extension would move in process and
lose the isolation this architecture is built on. **The in-process conversion remains rejected.**
The registration therefore ships as a second, minimal classic VSIX,
[`Assimalign.Viu.VisualStudio.Registration`](../../Assimalign.Viu.VisualStudio.Registration/src/Assimalign.Viu.VisualStudio.Registration.csproj)
(#264, superseding the 2026-08-03 "not implemented" decision). Its entire payload is the `.pkgdef`;
it contributes no code, no MEF parts, and no managed assembly to the VSIX. The two packages are
independent — the main extension works without it, and it only changes which editor factory claims
the file extension. `Build.Registration.ps1` builds it through Visual Studio's MSBuild located by
`vswhere`, because the classic VSSDK build tasks are .NET Framework tasks and cannot run under
`dotnet build`; that is also why the project is in neither solution.

Two omissions in that `.pkgdef` are deliberate:

- There is **no `[$RootKey$\Languages\File Extensions\.viu]` entry**. That key attaches a legacy
  language service to the buffer, which stamps its own content type and re-breaks precisely what
  the registration fixes. The text editor attaches no language service, so the buffer takes its
  content type from this extension's `documentTypes` registration and the language server starts.
- **`.vue` is not claimed.** Web Tools owns it explicitly at `0x33`; overriding a first-party
  explicit registration is a separate product decision, not a side effect of fixing `.viu`.

Do not reach for a TextMate grammar as a workaround either: TextMate's factory outranks XML and
would evict it, but it would stamp its own TextMate-derived content type on the buffer, trading one
wrong content type for another and still leaving the language server inactive.

The per-user Open With → Set as Default association remains valid and is documented as the
no-install fallback in [`extensions/VisualStudio/README.md`](../../README.md).

## Packaging

The shared `ViuPublishLanguageServer` target in `build/Targets/Build.LanguageServer.targets`
publishes self-contained, single-file .NET language-server executables for `win-x64` and
`win-arm64`; `Build.ps1` and the extension build both drive that one target, so an in-IDE build can
never package a stale server. The publish is a nested `dotnet publish` process that runs while the
rest of the solution keeps building, so it stamps `ViuLanguageServerNestedPublish=true` on itself
and `build/Targets/Build.Global.props` redirects its entire project graph into isolated
`bin/language-server/` and `obj/language-server/` output roots. Without that isolation the
publish-only global properties (embedded symbols in Debug) rebuilt the shared libraries into the
same `bin`/`obj` folders the solution build was reading, deleting their standalone `.pdb` files and
causing intermittent MSB3030 failures in the test projects that copy those references. The
extension selects the executable matching `RuntimeInformation.ProcessArchitecture`.

Self-contained packaging is the current implementation, **not an invariant**. Recorded decision
(2026-08-02, [V01.01.12.23] #259): the fully shipped product is expected to require a locally
installed .NET SDK and the `Assimalign.Viu.Sdk` package for complete end-to-end functionality —
in particular the Roslyn-workspace semantic features, whose project evaluation cannot be frozen
into the VSIX because it must match the SDK the consumer's project builds with. The boundary is:
baseline features (container parsing, diagnostics, utility IntelliSense) keep working with no
machine prerequisites; semantic features may depend on local SDK state and must degrade gracefully
— never silently — when it is absent. An earlier revision of this section stated machine
independence as a design guarantee; that overstated the intent. The VSIX layout is:

```text
Assimalign.Viu.VisualStudio/
  Assimalign.Viu.VisualStudio.dll
  language-server.json
  LanguageServer/
    win-x64/
      Assimalign.Viu.Tooling.LanguageServer.exe
    win-arm64/
      Assimalign.Viu.Tooling.LanguageServer.exe
```

The server path is resolved relative to the installed extension and rejected if configuration tries
to escape that directory. The host build validates both executable paths before packaging, so a
clean direct build cannot silently emit a VSIX without its language server.

The companion registration package is the opposite extreme — its whole layout is one file, and the
project sets `IncludeAssemblyInVSIXContainer=false` so the empty assembly the C# compiler produces
never enters the container:

```text
Assimalign.Viu.VisualStudio.Registration/
  Assimalign.Viu.VisualStudio.Registration.pkgdef
```

Its manifest declares the pkgdef as a single `Microsoft.VisualStudio.VsPackage` file asset and
takes its identity version from `$(VersionPrefix)` through a local `GetVsixVersion` target, so the
central repository version reaches the VSIX the same way it reaches the main extension. Both
packages are Preview and target Community/Professional/Enterprise `[17.14,19.0)` on `amd64` and
`arm64`.

The semantic IntelliSense increment ([V01.01.12.23] #259) shipped with **zero new packages**: the
publish already carried `Microsoft.CodeAnalysis` + `Microsoft.CodeAnalysis.CSharp` 5.3.0 with
unrestricted runtime assets, and the SemanticModel backend needs nothing else. The consumerless
`Microsoft.CodeAnalysis.CSharp.Workspaces` 5.0.0 and `Microsoft.CodeAnalysis.Workspaces.MSBuild`
5.0.0 central pins were removed from `build/Targets/Build.References.Packages.targets` rather than
version-aligned; the comment there records that any future Workspaces/Features re-entry (the
CompletionService increment) must land at the single 5.3.x line without `PrivateAssets=all` so the
assemblies flow into this publish, and that the consumerless `Microsoft.Build`/`Microsoft.Build.Locator`
pins are flagged for a separate cleanup item. The trimming and publish stance is unchanged:
`PublishTrimmed` with partial trim mode, single file, compression, and English-only satellite
resources. Measured by the packed-consumer round trip on the two-file scaffold
(`scripts/Test-LanguageServerSemanticRoundTrip.ps1`, 2026-08-02, Debug win-arm64): single-file
payload 17,880,455 bytes (within the pre-semantic ballpark — no new packages entered the
closure); first semantic completion (cold: materializing roughly 170 reference assemblies plus
the first bind) 787 ms; semantic completion after a subsequent edit 41 ms via the cached base
compilation; consumer `dotnet restore` 6.2 s with a warm package cache.

## References

- [VisualStudio.Extensibility overview](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/visualstudio-extensibility)
- [Language server provider](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/language-server-provider/language-server-provider)
- [Classification tagger walkthrough](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/editor/walkthroughs/classification)
- [Visual Studio language configuration](https://learn.microsoft.com/visualstudio/extensibility/language-configuration)
