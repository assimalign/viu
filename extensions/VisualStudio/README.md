# Viu for Visual Studio

This area contains the first end-to-end Visual Studio editing experience for Viu single-file
components:

- `Assimalign.Viu.VisualStudio` is a thin out-of-process Visual Studio extension. It contributes the
  `.viu` document type, immediate lexical syntax highlighting, and the language-server connection.
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
after a pull request merges into `main`, and only when `extensions/VisualStudio/` changed. It queries the existing
listing to assign the next numeric VSIX revision, then builds, tests, validates, and publishes from
the protected `visual-studio-marketplace` environment.

Visual Studio Marketplace does not provide a per-version prerelease channel. The Viu listing itself
is explicitly marked as a preview, and the release workflow verifies `<Preview>true</Preview>`
before every publication. The Marketplace metadata is in `vs-publish.json`; the public listing
content is in `Marketplace.md`.

The complete repository and Marketplace setup is documented in
[`docs/RELEASING.md`](../../docs/RELEASING.md).

## Current editing features

- Syntax highlighting for Viu block headers, template markup and directives, C#, CSS, strings,
  comments, numbers, and punctuation
- Parser diagnostics for malformed single-file-component block structure
- Full and incremental document synchronization
- Completion for block headers and options, common template tags/directives/events, CSS properties,
  `Context.*`, and `Reactive.*`
- Hover documentation for core Viu concepts

These completions are intentionally syntax-aware rather than project-semantic in this first slice.
Roslyn-backed C# completion, component discovery, go-to-definition, rename, references, and source
mapped compiler diagnostics are the next language-service layer; see [docs/DESIGN.md](docs/DESIGN.md).
