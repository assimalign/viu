# Viu for Visual Studio

This area contains the end-to-end Visual Studio editing experience for Viu single-file components:

- `Assimalign.Viu.VisualStudio` is a classic **in-process** VSSDK extension — one VSIX. It
  contributes the `viu` content type, the `.pkgdef` that claims the `.viu` file extension, the Viu
  color theme (its own classification types and their format definitions), and the language client
  that starts and connects the language server.
- `Assimalign.Viu.Tooling.LanguageServer` is an editor-neutral Language Server Protocol executable
  that runs in its **own process**.
- `Assimalign.Viu.Tooling.LanguageService` owns document state and Viu language features without
  depending on Visual Studio.

Only the extension lives under `extensions/VisualStudio/`. The language server and the language
service are editor-neutral developer tooling and live at
`tooling/Assimalign.Viu.Tooling.LanguageServer` and `tooling/Assimalign.Viu.Tooling.LanguageService`;
`Assimalign.Viu.VisualStudio.slnx` and `Build.ps1` drive all three together.

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

The extension targets `net48` — a classic VSSDK package loads inside `devenv.exe` and so lives on
the .NET Framework line — and compiles against the 17.14 editor and language-client contracts, which
Visual Studio itself supplies at run time.

## Build the complete extension

From the Viu repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\extensions\VisualStudio\Build.ps1
```

The script publishes self-contained, single-file language servers for both `win-x64` and
`win-arm64`, packages the extension through **Visual Studio's** MSBuild (located with `vswhere`,
because the VSSDK build tasks are .NET Framework MSBuild tasks that cannot load under
`dotnet build`), verifies the resulting container, and writes the installable package to:

```text
_out/extensions/VisualStudio/Debug/Assimalign.Viu.VisualStudio.vsix
```

Pass `-Configuration Release` for a release build. The verification step is not cosmetic: it asserts
that the manifest, the MEF assembly, the `.pkgdef`, `language-server.json`, and both architecture
payloads actually reached the archive, and that the manifest still declares both the MEF component
and the VsPackage asset. A direct host build also fails with an actionable error when either
architecture payload is absent, rather than producing an incomplete VSIX.

For the same .NET Framework MSBuild reason, `Assimalign.Viu.VisualStudio.csproj` is **not** in
`Assimalign.Viu.slnx` — that solution is gated by `dotnet build`. Its test project is, and CI names
the test projects individually instead of handing over a solution `dotnet` can no longer build.

### Working on the extension in Visual Studio

Open `extensions/VisualStudio/Assimalign.Viu.VisualStudio.slnx`. Run `Build.ps1` once for the active
configuration first, so the standalone server publish directory exists and is picked up by
subsequent in-IDE builds.

Building the project produces the `.vsix` but deliberately **does not** install it into an
experimental hive: the project sets `DeployExtension=false`, so no build — command line or in-IDE —
writes into a hive as a side effect. The supported loop is to install the packaged VSIX (double-click
it, or run `VSIXInstaller.exe` against it) and restart Visual Studio. A developer who wants the
classic F5-into-the-experimental-instance loop opts in per build with `-p:DeployExtension=true`.

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

- The **Viu color theme** for both hybrid `.viu` containers (tag-based `<template>`/`<style>` plus
  `@script`, with the legacy @-blocks still colored during the migration window). Template, markup,
  and style constructs get ten Viu-owned classification types — framework tag, element tag, component
  tag (bold), directive, attribute, attribute value, interpolation delimiter (bold), tag delimiter,
  style selector, style custom property — each with a Viu default color and its own editable entry
  under "Viu — …" in Tools > Options > Fonts and Colors
- Embedded C# — the `@script` block, interpolation interiors, and binding-expression interiors —
  colors with the editor's and Roslyn's own classification types, so it inherits whatever theme you
  chose for C#. A `class` value is deliberately one uninterrupted attribute-value color
- **Auto-closing** as you type. `{`, `(`, and `[` pair in every section — typing `{` twice gives you
  `{{}}` for an interpolation. Quotes pair only where they mean a string: `"` in attribute-value
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
- Manifest-backed Viu Utilities completion and hover with the exact generated CSS preview inside
  static and literal bound template `class` values
- Hover documentation for core Viu concepts
- Tag-based `.vue` document parsing, utility completion/hover, block completion, and explicit
  diagnostics for non-C# scripts, with explicit C# ordinary and setup scripts sharing the generated
  partial-component contract in Viu SDK projects — a **language-server** capability, live for hosts
  that route `.vue` to it, and not reachable from Visual Studio today (see below)

## Viu Utilities IntelliSense

The extension and SDK use the same `Assimalign.Viu.Tooling.UtilityCss` parser, frozen
Tailwind CSS v4.3.3-compatible registry, theme model, and project stylesheet compiler. The
machine-readable compatibility contract is
[`compatibility-v4.3.3.json`](../../tooling/Assimalign.Viu.Tooling.UtilityCss/conformance/compatibility-v4.3.3.json):
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
`<script>` and `<style>` is not a utility source. The same template-only boundary applies to the
hybrid `.viu` container: `<template>` content is the only utility source, never `@script` or
`<style>` (nor the legacy `@template`/`@style` blocks during their migration window).

The project lookup is intentionally narrow: the `ViuUtilityCss` item must be one direct literal
relative path. An MSBuild property, wildcard, multiple entries, missing file, or unreadable
reference graph falls back to the built-in registry and default theme instead of guessing an
evaluated project state. Completion can propose a functional custom utility root, but it does not
invent project-specific values that are not present in the theme or source text.

These completions are syntax-aware rather than Roslyn project-semantic. Ordinary `.cs` files,
arbitrary C# strings, and runtime-built class fragments are not scanned. Code-first utility
discovery is deliberately deferred. Roslyn-backed C# completion, component discovery,
go-to-definition, rename, references, and source-mapped compiler diagnostics remain the next
language-service layer; see
[Assimalign.Viu.VisualStudio/docs/DESIGN.md](Assimalign.Viu.VisualStudio/docs/DESIGN.md).

Viu Utilities is an independent Viu feature compatible with documented Tailwind CSS v4.3.3
behavior. It is not affiliated with or endorsed by Tailwind Labs. The extension does not install,
bundle, or coordinate with Tailwind CSS IntelliSense.

In Visual Studio the language client activates on the `viu` content type alone: opening a `.viu`
buffer starts the server, canonical and loose files alike, with no solution scan and no project
discovery. `.vue` is deliberately out of scope for this host — Visual Studio's Web Tools claims that
file extension explicitly, so a `.vue` buffer never carries a Viu content type and neither the Viu
colors nor the language client attach to one.

The language server's own `.vue` admission rules are unchanged and remain live for the hosts that can
reach them: it accepts a `.vue` file only when its nearest owning project uses `Assimalign.Viu.Sdk`
or explicitly sets `ViuVisualStudioLanguageServiceEnabled` to `true`, an explicit `false` wins even
in a Viu SDK project, and collocated Viu and non-Viu project files make ownership ambiguous, so the
non-evaluating probe fails closed instead of guessing. The gate is re-checked for open, change,
diagnostics, completion, and hover on every document.

Stylesheet regeneration is an SDK `dotnet watch` feature, not a language-server side effect.
During a Debug `dotnet watch` session, Viu's CSS sidecar rebuilds changed component and utility
stylesheets and lets the .NET browser-refresh client replace their `<link>` URLs. That CSS-only
boundary preserves the mounted application and browser state. Visual Studio's ordinary Hot Reload
command does not invoke the watch-list contract; launch through `dotnet watch` for automatic Viu
stylesheet updates. Template/C# code generation continues through the normal .NET build and Hot
Reload pipeline; generated change markers remount the affected component on .NET 10 browser
WebAssembly so updated managed code executes reliably.
