# Viu Visual Studio extensions

This area contains two independently installable classic in-process VSSDK extensions:

- `Assimalign.Viu.VisualStudio` contributes the `viu` content type, the `.pkgdef` that claims the
  `.viu` file extension, the Viu color theme, and the language client that starts the Viu language
  server.
- `Assimalign.Viu.UtilityCss.VisualStudio` contributes only an `ILanguageClient` for the modern
  Visual Studio HTML editor. It starts the standalone UtilityCss language server and leaves content
  types, file ownership, and all LSP presentation with Visual Studio. Its exact scope and Razor
  follow-up are documented in
  [its README](Assimalign.Viu.UtilityCss.VisualStudio/README.md).

The clients launch editor-neutral language servers in separate processes.
`Assimalign.Viu.LanguageService` owns Viu document state and language features without depending on
Visual Studio; the standalone UtilityCss server consumes only the UtilityCss engine and syntax
parsers.

Only the thin Visual Studio clients live under `extensions/VisualStudio/`. The Viu language server
and language service are editor-neutral developer tooling and live at
`tooling/Editor/Assimalign.Viu.LanguageServer` and
`tooling/Editor/Assimalign.Viu.LanguageService`;
the standalone UtilityCss server lives at
`libraries/Utilities/Assimalign.Viu.UtilityCss.LanguageServer`. The two extension solutions keep
their F5 deployment loops independent, while the root `Build.ps1` builds both sequentially.

The process boundary that matters is the server's, and it is intentional: Viu's parsers and Roslyn
stay outside `devenv.exe`, and the same server binary already serves the Visual Studio Code
extension. Only the thin client — content type, colors, process lifetime — runs in the IDE, because
the editor surfaces a Viu palette needs exist nowhere else. The full decision record, including why
the earlier out-of-process client was abandoned and what would justify going back, is in
[Assimalign.Viu.VisualStudio/docs/DESIGN.md](Assimalign.Viu.VisualStudio/docs/DESIGN.md).

## Prerequisites

- .NET SDK 10
- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- The **Visual Studio extension development** workload

Both extensions target `net48` — a classic VSSDK package loads inside `devenv.exe` and so lives on
the .NET Framework line — and compiles against the 17.14 editor and language-client contracts, which
Visual Studio itself supplies at run time.

## Build both extensions

From the Viu repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\extensions\VisualStudio\Build.ps1
```

The script publishes each self-contained, single-file language server for both `win-x64` and
`win-arm64`, packages both extensions through **Visual Studio's** MSBuild (located with `vswhere`,
because the VSSDK build tasks are .NET Framework MSBuild tasks that cannot load under
`dotnet build`), verifies both containers, and writes the installable packages to:

```text
_out/extensions/VisualStudio/Debug/Assimalign.Viu.VisualStudio.vsix
_out/extensions/VisualStudio/Debug/Assimalign.Viu.UtilityCss.VisualStudio.vsix
```

Pass `-Configuration Release` for a release build. Validation requires each manifest, MEF assembly,
configuration, and both architecture payloads; keeps the Viu pkgdef/VsPackage requirement; rejects
either registry asset from the UtilityCss VSIX; proves the UtilityCss dependency manifests are
Roslyn-free; reports each package size; and fails above the 50 MB Marketplace budget.

For the same .NET Framework MSBuild reason, neither VSIX csproj is in `Assimalign.Viu.slnx` — that
solution is gated by `dotnet build`. Their editor-free test projects are, and CI names the Visual
Studio projects individually rather than handing either VSSDK solution to `dotnet`.

### Working on the extension in Visual Studio

Open the solution for the package being developed:

- `extensions/VisualStudio/Assimalign.Viu.VisualStudio.slnx`
- `extensions/VisualStudio/Assimalign.Viu.UtilityCss.VisualStudio.slnx`

Run the root `Build.ps1` once for the active configuration first, so both standalone server publish
directories exist and are picked up by subsequent in-IDE builds.

Command-line builds produce the `.vsix` without installing it into an experimental hive. Inside
Visual Studio, each extension solution's `<Deploy />` entry and host-split `DeployExtension`
property preserve the classic F5-into-the-experimental-instance loop for that VSIX alone. The Viu
project follows deployment by running `devenv /RootSuffix Exp /UpdateConfiguration`, because the
VSSDK copy-and-enable target does not refresh Visual Studio's cached image-manifest library.

For the equivalent command-line loop, build and deploy the Viu package through its script. Use a
version greater than any copy already present in the experimental hive:

```powershell
./Assimalign.Viu.VisualStudio/Build.ps1 `
  -Configuration Debug `
  -Version 10.0.2 `
  -DeployExperimental
```

The switch installs into the released Visual Studio instance's `Exp` root suffix and completes the
same configuration refresh before returning. `-ExperimentalRootSuffix` selects a different named
experimental hive when needed. The script refuses to deploy while that selected Visual Studio
installation is running; another installation, such as Visual Studio Insiders, is unaffected.

The installed extension does not require a separately installed .NET runtime for the language
server. It chooses the server matching the Visual Studio process architecture at startup.

### Troubleshooting: `.viu` opens with no colors

The VSIX ships a `.pkgdef` claiming `.viu` for the Source Code (Text) Editor at priority `0x32`,
which outranks every wildcard editor factory — including the XML editor's content sniffer, which
would otherwise claim any file starting with `<template>`. Nothing manual is needed.

One case survives it. Visual Studio consults the **per-user** editor mapping before the priority
ladder, so if you once used Open With… → **Set as Default** on a `.viu` file, that choice still wins.
If it was the Source Code (Text) Editor, everything works; if it was the XML editor, the file keeps
an XML content type, neither the Viu colors nor the language server attach, and the XML parser
reports editor-only errors against C# and container syntax (they never affect a build). Clear the
override with Open With… → **Source Code (Text) Editor** → **Set as Default**.

See [Assimalign.Viu.VisualStudio/docs/DESIGN.md](Assimalign.Viu.VisualStudio/docs/DESIGN.md), "File
extension ownership", for the editor-factory ladder and the mechanism.

### Troubleshooting: `.viu` has the generic document icon

The VSIX ships a `ShellFileAssociations\.viu` GUID/ID moniker and a root-level
`ViuFileIcon.imagemanifest` with 16- and 32-pixel sources for light, dark, and high-contrast
backgrounds. Visual Studio caches every discovered image manifest in the selected hive's
`ImageLibrary\ImageLibrary.cache`; copying and enabling a development VSIX alone does not invalidate
that cache. Use `-DeployExperimental` for local deployment, or close Visual Studio and run
`devenv /RootSuffix <suffix> /UpdateConfiguration` once after installing. A fresh process then uses
the Viu glyph in both CPS/SDK-style Solution Explorer trees and loose-file views.

## Marketplace releases

The standard `area-visual-studio` workflow only builds and tests. The official
[`release`](../../.github/workflows/release.yml) workflow publishes a validated Marketplace preview
after a pull request merges into `main`, and only when the extension or one of its packaged parser
or editor dependencies changed. It queries the existing listing to assign the next numeric
VSIX revision, then builds, tests, validates, and publishes from the protected
`visual-studio-marketplace` environment.

Visual Studio Marketplace does not provide a per-version prerelease channel. The Viu listing itself
is explicitly marked as a preview, and the release workflow verifies `<Preview>true</Preview>`
before every publication. The Marketplace metadata is in `vs-publish.json`; the public listing
content is in `Marketplace.md`.

The complete repository and Marketplace setup is documented in
[`docs/RELEASING.md`](../../docs/RELEASING.md).

## Editing features

- The **Viu color theme** for both hybrid `.viu` containers (tag-based `<template>`/`<style>` plus
  `@script`, with the legacy @-blocks still colored during the migration window). Template, markup,
  and style constructs get ten Viu-owned classification types — framework tag, element tag, component
  tag (bold), directive, attribute, attribute value, interpolation delimiter (bold), tag delimiter,
  style selector, style custom property — each with a Viu default color and its own editable entry
  under "Viu — …" in Tools > Options > Fonts and Colors
- Embedded C# — the `@script` block, interpolation interiors, and binding-expression interiors —
  colors with the editor's and Roslyn's own classification types, so it inherits whatever theme you
  chose for C#. A `class` value is deliberately one uninterrupted attribute-value color
- **Auto-closing** as you type. `{`, `(`, and `[` pair in every section, and pressing Enter between a
  paired `{ }` in `@script` expands it into an indented block with the closing brace on its own line,
  the way the C# editor does. In a template, typing `{` immediately after a `{` writes the
  interpolation scaffold `{{}}` with the caret between — the same shape the Visual Studio Code client
  produces — and typing `}` where one already sits walks the caret over it. Quotes pair only where
  they mean a string: `"` in attribute-value
  position and in script, `'` in script only, so an apostrophe in template prose stays an apostrophe.
  Typing `>` after an open tag inserts its end tag with the caret between them, `</` completes the
  nearest unclosed element, and `<!--` completes to `<!-- | -->`; none of that fires inside `@script`
  or `<style>`, where `>` is a generic argument or a CSS child combinator. Void elements
  (`<br>`, `<input>`, …) and tags you self-closed insert nothing. Pairs follow the editor's
  Automatic Brace Completion option in Tools > Options; element closing is always on
- Parser diagnostics for malformed single-file-component block structure
- Full and incremental document synchronization
- Completion for block headers and options, common template tags/directives/events, CSS properties,
  `Context.*`, and `Reactive.*`
- Completion and authored-rule hover for class selectors declared in the component's own
  `<style>` blocks, inside static and literal-bound template `class` values
- Hover documentation for core Viu concepts
- Tag-based `.vue` document parsing, component-style completion/hover, block completion, and explicit
  diagnostics for non-C# scripts, with explicit C# ordinary and setup scripts sharing the generated
  partial-component contract in Viu SDK projects — a **language-server** capability, live for hosts
  that route `.vue` to it, and not reachable from Visual Studio today (see below)

## Standalone utility add-on

The standalone engine is independently published from
`libraries/Utilities/Assimalign.Viu.UtilityCss` and remains outside the Viu SDK. Its separate Visual
Studio VSIX supplies completion, generated-CSS hover, and standard LSP document colors for the
modern HTML editor. It does not attach to `.viu`: the Viu language service consumes build-emitted
class catalogs there, preserving one language client per document. The Tailwind CSS v4.3.3
compatibility target belongs only to the add-on.

The generic `LanguageCompletionItem.ColorValue` and Language Server Protocol `Color` completion
kind remain as dormant transport. The Visual Studio swatch adapter can present those values if a
future completion producer supplies them, but no current component-style completion does so.

Roslyn-backed C# completion, component discovery, go-to-definition, rename, references, and
source-mapped compiler diagnostics remain the next language-service layer; see
[Assimalign.Viu.VisualStudio/docs/DESIGN.md](Assimalign.Viu.VisualStudio/docs/DESIGN.md).

In Visual Studio the language client activates on the `viu` content type alone: opening a `.viu`
buffer starts the server, canonical and loose files alike, with no solution scan and no project
discovery. `.vue` is deliberately out of scope for this host — Visual Studio's Web Tools claims that
file extension explicitly, so a `.vue` buffer never carries a Viu content type and neither the Viu
colors nor the language client attach to one. The packaged-consumer gate for [V01.01.06.09.01]
(issue #253) therefore proves `.vue` compilation, diagnostics, assets, and Browser `dotnet watch`
behavior through the installed SDKs; it does not claim an unreachable full-Visual-Studio activation
path. That host constraint remains a recorded product boundary until an activation mechanism can
coexist with Web Tools.

The language server's own `.vue` admission rules are unchanged and remain live for the hosts that can
reach them: it accepts a `.vue` file only when its nearest owning project uses
`Assimalign.Viu.Sdk`, uses `Assimalign.Viu.Sdk.Browser`, or explicitly sets
`ViuVisualStudioLanguageServiceEnabled` to `true`; an explicit `false` wins even in a Viu SDK
project, and collocated Viu and non-Viu project files make ownership ambiguous, so the non-evaluating
probe fails closed instead of guessing. The gate is re-checked for open, change,
diagnostics, completion, and hover on every document.

Stylesheet regeneration is a Browser SDK `dotnet watch` feature, not a language-server side effect.
During a Debug `dotnet watch` session, Viu's CSS sidecar rebuilds changed component stylesheets and
lets the .NET browser-refresh client replace their `<link>` URLs. That CSS-only
boundary preserves the mounted application and browser state. Visual Studio's ordinary Hot Reload
command does not invoke the watch-list contract; launch through `dotnet watch` for automatic Viu
stylesheet updates. Template/C# code generation continues through the normal .NET build and Hot
Reload pipeline; generated change markers remount the affected component on .NET 10 browser
WebAssembly so updated managed code executes reliably.
