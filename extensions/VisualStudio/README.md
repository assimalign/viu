# Viu for Visual Studio

This area contains the first end-to-end Visual Studio editing experience for Viu single-file
components:

- `Assimalign.Viu.VisualStudio` is a thin out-of-process Visual Studio extension. It contributes the
  canonical `.viu` document type, project-scoped tag-based `.vue` compatibility, immediate `.viu`
  lexical syntax highlighting, and the language-server connection.
- `Assimalign.Viu.LanguageServer` is an editor-neutral Language Server Protocol executable.
- `Assimalign.Viu.LanguageService` owns document state and Viu language features without depending on
  Visual Studio.

The process boundary is intentional. Viu's parsers, and eventually Roslyn workspaces, remain outside
`devenv.exe`; the same language server can later serve other editors.

## Prerequisites

- .NET SDK 10
- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- The **Visual Studio extension development** workload

The client uses `Microsoft.VisualStudio.Extensibility` 17.14 and executes out of process.

## Build the complete extension

From the Viu repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\extensions\VisualStudio\Build.ps1
```

The script publishes self-contained, single-file language servers for both `win-x64` and
`win-arm64`, embeds both payloads into the VSIX, and writes the installable package to:

```text
_out/extensions/VisualStudio/Debug/Assimalign.Viu.VisualStudio.vsix
```

Pass `-Configuration Release` for a release build. Open
`extensions/VisualStudio/Assimalign.Viu.VisualStudio.slnx` to work on the extension in Visual Studio.
Set `Assimalign.Viu.VisualStudio` as the startup project and press F5 to launch the experimental
instance. Run `Build.ps1` once for the active configuration first so the standalone server publish
directory exists and is included in subsequent host builds. A direct host build fails with an
actionable error when either architecture payload is absent, rather than producing an incomplete
VSIX.

The installed extension does not require a separately installed .NET runtime for the language
server. The extension chooses the server matching the Visual Studio extension-host process
architecture at startup.

## Marketplace releases

The standard `area-visual-studio` workflow only builds and tests. The official
[`release`](../../.github/workflows/release.yml) workflow publishes a validated Marketplace preview
after a pull request merges into `main`, and only when the extension or one of its packaged parser
or utility-engine dependencies changed. It queries the existing listing to assign the next numeric
VSIX revision, then builds, tests, validates, and publishes from the protected
`visual-studio-marketplace` environment.

Visual Studio Marketplace does not provide a per-version prerelease channel. The Viu listing itself
is explicitly marked as a preview, and the release workflow verifies `<Preview>true</Preview>`
before every publication. The Marketplace metadata is in `vs-publish.json`; the public listing
content is in `Marketplace.md`.

The complete repository and Marketplace setup is documented in
[`docs/RELEASING.md`](../../docs/RELEASING.md).

## Editing features

- Syntax highlighting for Viu block headers, template markup and directives, C#, CSS, strings,
  comments, numbers, and punctuation
- Parser diagnostics for malformed single-file-component block structure
- Full and incremental document synchronization
- Completion for block headers and options, common template tags/directives/events, CSS properties,
  `Context.*`, and `Reactive.*`
- Manifest-backed Viu Utilities completion and hover with the exact generated CSS preview inside
  static and literal bound template `class` values
- Hover documentation for core Viu concepts
- Tag-based `.vue` document parsing, utility completion/hover, block completion, and explicit
  diagnostics for non-C# scripts; explicit C# ordinary and setup scripts share the generated
  partial-component contract in Viu SDK projects

## Viu Utilities IntelliSense

The extension and SDK use the same `Assimalign.Viu.Tooling.UtilityCss` parser, frozen
Tailwind CSS v4.3.3-compatible registry, theme model, and project stylesheet compiler. The
machine-readable compatibility contract is
[`compatibility-v4.3.3.json`](../../libraries/Assimalign.Viu.Tooling.UtilityCss/conformance/compatibility-v4.3.3.json):
382 utility roots, 88 variants, 21 theme namespaces, supported value/modifier modes, and the
CSS-first directive/function surface. Completion detail and hover are compiler output, not a
separate editor approximation.

For each component document, the language server reads a singular literal project item:

```xml
<ItemGroup>
    <ViuUtilityCss Include="Utilities.css" />
</ItemGroup>
```

That stylesheet may contain:

- `@import "viu-utilities"` with `source(...)`, `prefix(...)`, `theme(inline|static)`, and
  `important`;
- `@source` path and inline inclusion/exclusion forms;
- `@theme` normal, `inline`, `static`, `reference`, and `default` declarations;
- static and functional `@utility` definitions;
- selector and `@slot` `@custom-variant` definitions;
- authored `@variant` and `@apply` composition;
- recursive relative `@reference` graphs for shared theme, utility, and variant definitions.

The `@source` forms govern SDK build-time candidate discovery. The language server does not crawl
those roots or turn inline source entries into completion suggestions; IntelliSense proposes class
text in the template being edited and uses the loaded configuration to resolve authored
candidates.

The server refreshes the entry and its reference graph before completion and hover requests.
Project theme tokens, prefixes, global important mode, custom static utilities, referenced
definitions, and custom variants therefore use the same executable semantics as the SDK build.
Built-in arbitrary values/properties/variants, CSS-variable forms, negatives, fractions, modifiers,
prefixes, and trailing important syntax are resolved on the authored candidate and shown on hover.

IntelliSense activates only inside static `class="..."` text and literal portions of bound class
values. For `.vue`, the utility engine scans and edits only `<template>` content; text in
`<script>` and `<style>` is not a utility source. The same template-only boundary applies to
`.viu` `@template` versus `@script`/`@style`.

The project lookup is intentionally narrow: the `ViuUtilityCss` item must be one direct literal
relative path. An MSBuild property, wildcard, multiple entries, missing file, or unreadable
reference graph falls back to the built-in registry and default theme instead of guessing an
evaluated project state. Completion can propose a functional custom utility root, but it does not
invent project-specific values that are not present in the theme or source text.

These completions are syntax-aware rather than Roslyn project-semantic. Ordinary `.cs` files,
arbitrary C# strings, and runtime-built class fragments are not scanned. Code-first utility
discovery is deliberately deferred. Roslyn-backed C# completion, component discovery,
go-to-definition, rename, references, and source-mapped compiler diagnostics remain the next
language-service layer; see [docs/DESIGN.md](docs/DESIGN.md).

Viu Utilities is an independent Viu feature compatible with documented Tailwind CSS v4.3.3
behavior. It is not affiliated with or endorsed by Tailwind Labs. The extension does not install,
bundle, or coordinate with Tailwind CSS IntelliSense.

Visual Studio requires language-server providers to declare an applicable document type, and that
filter cannot include an owning-project build property. The compatibility document type is
therefore necessarily solution-wide while the extension is active. The extension activates for a
canonical `.viu` buffer or for a solution containing a project whose evaluated
`ViuVisualStudioLanguageServiceEnabled` property is `true`. The language server then gates open,
change, diagnostics, completion, and hover independently for every document. It accepts a `.vue`
file only when its nearest owning project uses
`Assimalign.Viu.Sdk` or explicitly sets `ViuVisualStudioLanguageServiceEnabled` to `true`.
An explicit `false` wins even in a Viu SDK project. This keeps non-Viu `.vue` files in a mixed
solution functionally untouched, while canonical `.viu` and loose `.viu` files remain available
without project discovery. If Viu and non-Viu project files are collocated in the same nearest
directory, the non-evaluating ownership probe fails closed instead of guessing.

Stylesheet regeneration is an SDK `dotnet watch` feature, not a language-server side effect.
During a Debug `dotnet watch` session, Viu's CSS sidecar rebuilds changed component and utility
stylesheets and lets the .NET browser-refresh client replace their `<link>` URLs. That CSS-only
boundary preserves the mounted application and browser state. Visual Studio's ordinary Hot Reload
command does not invoke the watch-list contract; launch through `dotnet watch` for automatic Viu
stylesheet updates. Template/C# code generation continues through the normal .NET build and Hot
Reload pipeline; generated change markers remount the affected component on .NET 10 browser
WebAssembly so updated managed code executes reliably.
