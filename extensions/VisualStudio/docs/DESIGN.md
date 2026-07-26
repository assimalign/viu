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
            -> Assimalign.Viu.LanguageServer
                 -> Assimalign.Viu.LanguageService
                      -> Assimalign.Viu.Syntax.SingleFileComponent
                      -> Assimalign.Viu.Tooling.UtilityCss
```

`Assimalign.Viu.VisualStudio` performs fast lexical classification using Visual Studio's built-in
classification categories. The out-of-process API cannot currently define custom classification
colors, so user themes remain authoritative. Semantic method spans map to the base `identifier`
category and punctuation maps to `operator`: Visual Studio does not register the SDK's `method`
name, while `punctuation` is supplied only when Roslyn editor features are present. These fallbacks
keep the VSIX independent of a particular managed-language workload.

`Assimalign.Viu.LanguageServer` owns protocol framing and translates protocol values into
editor-neutral contracts. It writes protocol messages only to standard output; standard error is
reserved for diagnostics.

`Assimalign.Viu.LanguageService` caches the current text and the format-appropriate immutable
container parse for each open `.viu` or accepted `.vue` document. It exposes block diagnostics,
completion catalogs, shared utility-class completion, project-defined utilities and variants, and
generated-CSS hover documentation. It does not otherwise load a Roslyn workspace.

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
[`compatibility-v4.3.3.json`](../../../libraries/Assimalign.Viu.Tooling.UtilityCss/conformance/compatibility-v4.3.3.json)
and independently authored
[`golden-vectors-v4.3.3.json`](../../../libraries/Assimalign.Viu.Tooling.UtilityCss/conformance/golden-vectors-v4.3.3.json).
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
The container parser supplies only `.viu` `@template` or `.vue` `<template>` text to that context.
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

1. Extract the generator's component-name, script-region, generated-context, and source-mapping logic
   into a shared `Assimalign.Viu.Tooling.SingleFileComponent` library.
2. Have both the source generator and language service consume that projection builder so editor and
   compiler behavior cannot drift.
3. Load the containing project through `MSBuildWorkspace` in the language-server process.
4. Add the projected partial component as a synthetic Roslyn document.
5. Map Roslyn completion, hover, signature help, definitions, references, and diagnostics back to the
   original `@script` block and template-expression spans.
6. Integrate the existing template and CSS syntax trees for precise semantic tokens and recoverable
   embedded-language diagnostics.

Parsing remains cancellable and off the Visual Studio UI path. Before enabling whole-project semantic
analysis, the server needs snapshot caching, edit debouncing, and per-document cancellation.

## Packaging

`Build.ps1` publishes self-contained, single-file .NET language-server executables for `win-x64`
and `win-arm64`, then passes their common directory to the extension build through
`ViuLanguageServerPublishPath`. The extension selects the executable matching
`RuntimeInformation.ProcessArchitecture`. This keeps the installed extension independent of a
machine-wide .NET runtime and makes the two architectures declared by the VSIX manifest real
payload guarantees. The VSIX layout is:

```text
Assimalign.Viu.VisualStudio/
  Assimalign.Viu.VisualStudio.dll
  language-server.json
  LanguageServer/
    win-x64/
      Assimalign.Viu.LanguageServer.exe
    win-arm64/
      Assimalign.Viu.LanguageServer.exe
```

The server path is resolved relative to the installed extension and rejected if configuration tries
to escape that directory. The host build validates both executable paths before packaging, so a
clean direct build cannot silently emit a VSIX without its language server.

## References

- [VisualStudio.Extensibility overview](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/visualstudio-extensibility)
- [Language server provider](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/language-server-provider/language-server-provider)
- [Classification tagger walkthrough](https://learn.microsoft.com/visualstudio/extensibility/visualstudio.extensibility/editor/walkthroughs/classification)
- [Visual Studio language configuration](https://learn.microsoft.com/visualstudio/extensibility/language-configuration)
