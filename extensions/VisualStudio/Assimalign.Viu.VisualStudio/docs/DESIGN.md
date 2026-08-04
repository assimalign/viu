# Visual Studio language tooling design

## Decision

**Recorded decision (2026-08-04): the Visual Studio client is a classic in-process VSSDK package,
and the language server stays a separate process.** This reverses the earlier decision to build the
client on the out-of-process `VisualStudio.Extensibility` model, and the reversal is recorded rather
than quietly rewritten: the out-of-process client was chosen for isolation, and isolation turned out
to be a property of the *server* boundary, not of the client. Specified by `[TOOL-1]`.

The out-of-process model was abandoned because its editor surface is preview-grade and fought every
feature Viu needed. The concrete catalog, so nobody re-litigates it from memory:

- **Document-type binding is runtime-only, so the XML editor wins.** Document types contributed
  through the manifest are applied after an editor factory has already claimed the file, and the XML
  editor's wildcard sniffer claims any document opening with a well-formed tag — which every `.viu`
  container does (see "File extension ownership"). The fix for that is a `.pkgdef`, which leads
  directly to the third item.
- **`AppliesTo` reduces to the last document type AND the last glob.** Multiple document-type or
  path clauses on one part did not compose as a union; the manifest kept the last of each and
  intersected them, so a part could not express "`.viu` or `.vue`" at all.
- **A `.pkgdef` is unshippable out of process.** The asset requires `VssdkCompatibleExtension`,
  which in turn mandates `RequiresInProcessHosting` — an out-of-process extension that ships its own
  file association stops being out of process. The workaround was a second, code-free VSIX
  (`Assimalign.Viu.VisualStudio.Registration`): two installs for one feature.
- **No custom classification types.** There is no out-of-process equivalent of the MEF
  `ClassificationTypeDefinition`/`ClassificationFormatDefinition` pair, so every token had to borrow
  one of the editor's built-in categories. No Viu colors, no bold, no "Viu — …" entries in
  Tools > Options > Fonts and Colors. Semantic tokens shared the same ceiling, because Visual Studio
  maps them onto the same built-in classifications.
- **The remote tag bridge rejected even ordinary names.** It refused Roslyn's standard `method`, so
  the borrowed palette had holes too and semantic method spans fell back to `identifier`.
- **The contracts were preview-gated.** Both the language-server and the tagger APIs were reachable
  only behind `VSEXTPREVIEW_*` opt-ins in the 17.14 line, so the client shipped on surfaces
  Microsoft had not committed to.

**What the migration deliberately kept.** The server boundary is unchanged, and it was always the
load-bearing half of the original decision:

- the language server remains a separate stdio process, so the Viu parsers and Roslyn never load
  into `devenv.exe` and a parser fault or dependency conflict cannot destabilize the IDE;
- the server stays editor-neutral — it speaks the Language Server Protocol and reads file paths, and
  knows nothing about Visual Studio;
- the Visual Studio Code extension consumes the identical binary, published by the identical shared
  target (`build/Targets/Build.LanguageServer.targets`), so the two hosts cannot drift.

Only the client crossed the boundary, and the client is exactly the layer the original decision
described as "document registration, process lifetime, and editor presentation". No server change
was required by the migration and none was made.

**Revisit condition.** If `VisualStudio.Extensibility` ships stable editor classification — custom
classification types with user-editable format definitions — together with a pkgdef-equivalent file
association, the thin client could migrate back with the server untouched. Those two capabilities
are the whole of what forced the move; everything else the client does (start a process, hand over
two streams, lex a buffer) has an out-of-process shape already.

## Components

```text
Visual Studio (devenv.exe)
  -> Assimalign.Viu.VisualStudio  (classic VSSDK package, in process)
       -> Assimalign.Viu.VisualStudio.pkgdef   claims .viu for the Source Code (Text) Editor
       -> viu content type    MEF; bases: code, code-languageserver-preview
       -> ViuClassifier       MEF; the Viu palette over ViuLexicalClassifier
       -> ViuLanguageClient   MEF ILanguageClient; starts the server, hands Visual Studio its streams
            -> stdio Language Server Protocol connection
                 -> Assimalign.Viu.Tooling.LanguageServer  (separate process)
                      -> Assimalign.Viu.Tooling.LanguageService
                           -> Assimalign.Viu.Syntax.SingleFileComponent
                           -> Assimalign.Viu.Tooling.SingleFileComponent (the shared build/editor projection, [V01.01.06.11])
                           -> Assimalign.Viu.Tooling.UtilityCss
```

`extensions/VisualStudio/` holds exactly one project, `Assimalign.Viu.VisualStudio`, and one build
entry point, `Build.ps1`. Everything below the stdio boundary is editor-neutral developer tooling
and lives under the repository's `tooling/` root (`tooling/Assimalign.Viu.Tooling.LanguageServer`,
`tooling/Assimalign.Viu.Tooling.LanguageService`, and the build-time cores they consume).

`Assimalign.Viu.VisualStudio` performs fast lexical classification with no server round trip. It
lexes both container syntaxes of the hybrid `.viu` format ([V01.01.06.10]): tag-delimited top-level
`<template>`/`<style>` sections (with nested-`<template>` depth tracking so slot fragments do not end
a section) plus the `@script` @-block, and the legacy `@template`/`@style` @-blocks keep highlighting
during the migration window.

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

## The Viu color theme

Classification splits by ownership, specified by `[TOOL-3]`.

**Embedded C# resolves the editor's own classification types.** Every kind a C# token pass can emit —
keyword, string, number, comment, operator, punctuation, identifier, class name, method name —
resolves the name the editor and Roslyn already register, so the `@script` block, interpolation
interiors, and binding-expression interiors color exactly as the user's chosen C# theme colors C#.
Viu registers none of those names and expresses no opinion about them: the theme applies 1:1.

Resolution is defensive, because three of those names (`punctuation`, `class name`, `method name`)
come from Roslyn's editor features rather than the core editor. Each buffer resolves every kind once
and walks a fixed fallback chain when a name is absent: `method name` and `class name` fall back to
`identifier`, `punctuation` falls back to `operator`, and a kind that resolves to nothing at all is
dropped rather than mis-colored. A Visual Studio without a managed-language workload therefore still
colors Viu templates in full, and script spans degrade to plain identifiers instead of vanishing.
The chain lives in `ViuClassificationTypeNames.GetFallbackClassificationTypeName`.

**Template, markup, and style constructs resolve ten Viu-owned classification types.**
`ViuClassificationTypes` registers each one with `text` as its base definition, and each has exactly
one `ClassificationFormatDefinition` carrying a Viu default color and a `[UserVisible(true)]` entry
in Tools > Options > Fonts and Colors. The `viu.` name prefix keeps them from colliding with any
other extension's; the "Viu — " display prefix groups them together in that list.

| Classification type | Fonts and Colors entry | Default | Covers |
| --- | --- | --- | --- |
| `viu.tag` | Viu — framework tag | `#569CD6` | `template`, `slot`, `style`, `script`, and the legacy `@template`/`@style` headers |
| `viu.element` | Viu — element tag | `#7EE787` | HTML element tag names |
| `viu.component` | Viu — component tag | `#2BD9BC`, **bold** | PascalCase or dotted tag names |
| `viu.directive` | Viu — directive | `#C586C0` | `v-*`, `:bind`, `@event`, `#slot`, including valueless forms such as `v-else` |
| `viu.attribute` | Viu — attribute | `#9CDCFE` | plain attribute names and CSS property names |
| `viu.attribute.value` | Viu — attribute value | `#FFAB70` | attribute values, quotes included |
| `viu.interpolation.delimiter` | Viu — interpolation delimiter | `#FFD866`, **bold** | the `{{` and `}}` of an interpolation |
| `viu.delimiter` | Viu — tag delimiter | `#6E7681` | `<`, `>`, `</`, `/>`, and the attribute `=` |
| `viu.style.selector` | Viu — style selector | `#D7BA7D` | selectors in a style block |
| `viu.style.custom.property` | Viu — style custom property | `#4EC9B0` | `--name` theme tokens |

Two of the ten are bold, and both choices are deliberate. Which tags are components is the single
most load-bearing fact about a Viu template, so component tags carry weight as well as color. And the
interpolation delimiters mark where markup stops and an expression starts — the expression inside
colors as ordinary C#, so the boundary has to carry itself. In the other direction, the tag delimiter
is the most muted color in the palette on purpose: tag structure should recede so that names carry
the reading.

Component detection is casing, and only casing. A purely lexical classifier never consults a
component registry, and Viu's ordinal name resolution makes the authored spelling meaningful
(specified by `[CMP-6]`), so a PascalCase or dotted tag name is a component and a lowercase one is an
element.

**Recorded user decision — a `class` value is one uninterrupted color.** `UtilityVariant` and
`UtilityClass` both map to `viu.attribute.value`, so a list of utilities reads as a value rather than
as a syntax exhibit. The lexer still separates each leading variant prefix (`hover:`, `md:` — the
colon included) from the utility that follows, and a `[...]` arbitrary value is never split on its
inner colons, so the utility stays one token. The language server and the Visual Studio Code grammar
both act on that distinction; the Visual Studio editor simply does not color the parts apart.
Candidate validation stays in the language server per the source boundaries below.

**Recorded decision — the method-position rule for binding values.** Binding-expression interiors run
the C# token passes, and the method position has two halves. Call syntax — an identifier immediately
followed by `(` — is a method wherever a C# pass runs, the `@script` block included. In an
event-handler binding the handler slot is itself a method position, so a bare identifier there is a
method even without parentheses: `@click="Increment"` names a method exactly as `@click="Increment()"`
does. An identifier followed by `.` is a receiver rather than the handler, so
`@click="ViewModel.Increment"` still colors its two halves apart.

A plain binding (`:value="Count"`, `v-if="Visible"`) has no method position and names component state,
so its bare identifiers stay identifiers. That exemption is load-bearing: the PascalCase-is-a-type
heuristic that serves general C# well would otherwise color every bound property as a type. The
heuristic still runs unchanged in the `@script` block and in interpolation interiors, which really are
general C#; a binding value is the one position where the leading name is a member by construction.

### Classification caching

`ViuClassifier` lexes the **whole document** and caches the result on the snapshot. A line's
classification depends on the container section enclosing it — the lexer has to see the `<template>`
above line three hundred to color line three hundred — so asking for a range would cost exactly what
asking for everything costs. Every `GetClassificationSpans` call is therefore answered by filtering
that one result, and exactly one snapshot's spans are held at a time: this is a reuse cache, not a
history. The editor reaches the classifier more than once for the same snapshot per keystroke, which
is precisely the repetition the cache absorbs.

Change notification is whole-buffer for the same reason: adding a `</template>` on line three changes
how line three hundred is colored, so no narrower invalidation would be correct. The editor
re-requests only the ranges it is actually displaying, so the cost of the wide notification is
bounded by the visible text rather than by the document.

The classifier is stored in the buffer's property collection and subscribes to that same buffer, so
the subscription is a self-reference that dies with the buffer; `IClassifier` offers no disposal point
to unsubscribe from.

The out-of-process tagger this replaces reported the whole document too, but for a different reason:
each report was a JSON-RPC round trip, and reporting only the requested lines left every unscrolled
line drawn in the default text color until a round trip completed. That failure mode does not exist
in process. Whole-document lexing is a property of the container format and survives the transport
change; the version watermarking and requested-range bookkeeping that made the round trips bearable
did not, and is gone.

## Activation, and where `.vue` stands in Visual Studio

`ViuLanguageClient` is a MEF `[Export(typeof(ILanguageClient))]` filtered on the `viu` content type,
and that filter is the whole of activation: opening a `.viu` buffer starts the server, and nothing
else does. There is no document-type manifest, no solution scan, and no build-property condition —
the in-process client has no equivalent of the out-of-process `AppliesTo` machinery and needs none.

The `viu` content type carries a second base definition,
`CodeRemoteContentDefinition.CodeRemoteContentTypeName` (`code-languageserver-preview`), alongside
`code`. Activation matches on the
content type itself, but completion, hover, outlining, light bulbs, and the Error List entries the
server publishes are contributed by editor parts registered against that base — without it the server
would run and reach no surface. The base derives from `code`, so it does not displace the existing
one.

**Recorded decision (2026-08-04): `.vue` is out of scope for the Visual Studio client, in two
separate halves.**

- **Colorization is out of scope and intended to stay there.** Visual Studio's Web Tools claims
  `.vue` explicitly at wildcard priority `0x33` and contributes its own content type and colorizer.
  The Viu classifier is filtered on the `viu` content type, which no `.vue` buffer carries, so nothing
  collides today. Claiming `.vue` would mean overriding a first-party explicit registration and
  displacing an editing experience the user already has — an intrusion, and a separate product
  decision rather than a side effect of shipping Viu colors.
- **Language-service support is currently unreachable, not refused.** The server's own `.vue`
  admission rules are untouched and fully live: before admitting a `.vue` document it performs a
  deliberately narrow nearest-owning-project check for `Assimalign.Viu.Sdk` or the explicit
  `ViuVisualStudioLanguageServiceEnabled` marker, stopping at the first directory containing a project
  so an unrelated nested Vue project is not claimed by a Viu ancestor; an explicit literal `false`
  marker overrides SDK-name detection; the check repeats for document changes, diagnostics,
  completion, and hover; and a directory holding both eligible and ineligible project files is
  ambiguous without an evaluated project-system query, so the probe fails closed. The Visual Studio
  Code client reaches that gate. The Visual Studio client cannot, because it activates on content type
  `viu` alone and a `.vue` buffer never carries it. Making `.vue` semantics reachable from Visual
  Studio is future work, worth doing only if it is demanded, and would need an activation path that
  coexists with Web Tools rather than replacing it.

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

Three consequences follow, and they explain behavior that would otherwise look like extension bugs:

- The `viu` content type never reaches the buffer, so **every** MEF part filtered on it — the
  classifier and `ViuLanguageClient` alike — is never activated. Neither colorization nor semantic
  IntelliSense works while the XML editor owns the file.
- The XML parser reports its own diagnostics against C# and container syntax: `@script {` becomes
  "Invalid token 'Text' at root level of document", `Get<string>` becomes "Tag was not closed", and
  `<style scoped>` becomes "Missing attribute value on attribute 'scoped'". These carry no error
  code, are editor-only, and never affect a build.
- `.vue` is unaffected, because Web Tools claims that extension explicitly at `0x33`, outranking the
  wildcard.

**The fix ships in this VSIX**, as
[`Assimalign.Viu.VisualStudio.pkgdef`](../src/Assimalign.Viu.VisualStudio.pkgdef) (#264): it claims
`.viu` for the text editor at `0x32`, the band Microsoft itself uses for explicitly owned extensions
(`json`, `html`, `css`), with `0x31` on the with-encoding sibling factory — the same one-lower offset
Visual Studio's own registrations use. `0x32` outranks every wildcard factory in the ladder above, so
the text editor claims the file outright. The text editor attaches no language service, so the buffer
then takes its content type from this extension's `FileExtensionToContentTypeDefinition`, and both the
classifier and the language client activate.

Carrying the pkgdef is precisely what the out-of-process model could not do: the asset requires
`VssdkCompatibleExtension`, which mandates `RequiresInProcessHosting`. That is one of the reasons the
extension is now in process (see "Decision"), and with the extension in process the constraint is
simply satisfied — the manifest declares the file as an ordinary
`Microsoft.VisualStudio.VsPackage` asset beside the MEF component. The
`Assimalign.Viu.VisualStudio.Registration` companion that once carried this exact registry claim is
**retired**; it was never published, and its whole payload now ships here.

Two omissions in that `.pkgdef` are deliberate:

- There is **no `[$RootKey$\Languages\File Extensions\.viu]` entry**. That key attaches a legacy
  language service to the buffer, which stamps its own content type and re-breaks precisely what the
  registration fixes.
- **`.vue` is not claimed**, for the reasons in "Activation, and where `.vue` stands in Visual
  Studio".

Do not reach for a TextMate grammar as a workaround either: TextMate's factory outranks XML and would
evict it, but it would stamp its own TextMate-derived content type on the buffer, trading one wrong
content type for another and still leaving both Viu parts inactive.

One user-level override survives the pkgdef and is worth knowing about when triaging a report of "no
colors": `GetDesignerFactory` consults the per-user editor mapping *before* the priority ladder, so a
user who once set Open With → *Set as Default* on `.viu` keeps whatever they chose. If that choice was
the Source Code (Text) Editor everything still works; if it was the XML editor, the pkgdef never gets
a vote. Clearing the override restores the shipped behavior. This is documented as a troubleshooting
note in [`extensions/VisualStudio/README.md`](../../README.md); it is no longer an installation step.

## Packaging

The shared `ViuPublishLanguageServer` target in `build/Targets/Build.LanguageServer.targets`
publishes self-contained, single-file .NET language-server executables for `win-x64` and
`win-arm64`; `Build.ps1` and the extension build both drive that one target, so an in-IDE build can
never package a stale server. The extension selects the executable matching
`RuntimeInformation.ProcessArchitecture`.

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
  Assimalign.Viu.VisualStudio.pkgdef
  language-server.json
  LanguageServer/
    win-x64/
      Assimalign.Viu.Tooling.LanguageServer.exe
    win-arm64/
      Assimalign.Viu.Tooling.LanguageServer.exe
```

The server path is resolved relative to the installed extension — derived from the client assembly's
own location, since an in-process part has no host-supplied installation path — and rejected if
configuration tries to escape that directory. The host build validates both executable paths before
packaging, and `Build.ps1` re-verifies the packaged container afterwards, so neither a clean direct
build nor a silently-stopped inclusion rule can emit a VSIX without its language server or its
pkgdef.

Every assembly the extension compiles against is supplied by Visual Studio at run time, so the
package references are compile-only and `IncludeCopyLocalReferencesInVSIXContainer` is false: nothing
from the editor, language-client, or `System.Text.Json` packages enters the container. The manifest
declares two assets — the MEF component and the pkgdef — and takes its identity version from
`$(VersionPrefix)` through a local `GetVsixVersion` target, so the central repository version reaches
the VSIX the same way it reaches every other Viu artifact. The package is Preview and targets
Community/Professional/Enterprise `[17.14,19.0)` on `amd64` and `arm64`, with the Visual Studio core
editor as its only prerequisite — Roslyn's classification types are resolved defensively at run time,
so a managed-language workload is an enhancement rather than a requirement.

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

- [Editor classification and colorization](https://learn.microsoft.com/visualstudio/extensibility/walkthrough-highlighting-text)
- [Language Server Protocol support in Visual Studio](https://learn.microsoft.com/visualstudio/extensibility/adding-an-lsp-extension)
- [Registry (pkgdef) file layout](https://learn.microsoft.com/visualstudio/extensibility/internals/registering-verbs-for-file-name-extensions)
- [Visual Studio language configuration](https://learn.microsoft.com/visualstudio/extensibility/language-configuration)
- [Language Server Protocol specification](https://microsoft.github.io/language-server-protocol/)
