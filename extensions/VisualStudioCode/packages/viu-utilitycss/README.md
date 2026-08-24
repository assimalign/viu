# Viu Utilities for Visual Studio Code

Viu Utilities adds utility CSS IntelliSense to HTML-based files without taking ownership of their
language, grammar, formatting, or other editor features. Its standalone language server provides
class-value completions, generated CSS hover previews, document colors, and color presentations.

Marketplace publication runs through the protected owner setup in
[`docs/RELEASING.md`](../../../../docs/RELEASING.md). The source `version` remains a placeholder;
release packaging injects the numeric central version without modifying the manifest.

The extension activates for:

- `.html` and `.htm` files (`.htm` uses Visual Studio Code's `html` language id and also has an
  explicit document-selector pattern);
- `.cshtml` and `.razor` files;
- `.vue` files;
- Viu `.viu` single-file components.

Completion and hover are offered only while the cursor is inside a class-attribute value. For
`.viu` and `.vue`, the server scans the template block rather than script or style content.

## Project configuration

For each open document, the server finds the nearest ancestor `.csproj` and discovers the newest
`obj/**/utilitycss/utilitycss.manifest.v1.json` emitted by `Assimalign.Viu.UtilityCss.Build`. That
manifest identifies the project's entry stylesheet and records the build's resolved-theme hash.
The server reconstructs the theme through the Utility CSS engine and checks the manifest, entry
stylesheet, and resolved `@reference` modification times on requests. Changes therefore update
editor results without restarting Visual Studio Code. It merges the sibling class catalog first so
build-selected, source-used arbitrary candidates remain available, then fills the bounded result
from the live engine query. When no sidecar is available, completions use the built-in default
theme.

The manifest and catalog formats are documented in
[`EDITOR-SIDECAR.md`](../../../../libraries/Utilities/Assimalign.Viu.UtilityCss.Build/docs/EDITOR-SIDECAR.md).
Sidecars are enabled by the build package and can be disabled with
`ViuUtilityCssEmitEditorSidecar=false` when editor discovery is not wanted.

## Composing with the Viu extension

Viu Utilities complements the [`viu`](../viu) extension; it does not replace it. Install both for a
Viu application: the Viu extension owns `.viu` syntax and Viu language features, while this package
adds utility CSS results inside template class values. This package intentionally contributes no
language definition or TextMate grammar, so it also composes with the existing HTML, Razor, and Vue
extensions in a workspace.

Settings:

- `viuUtilityCss.languageServer.enabled` starts or suppresses the bundled server.
- `viuUtilityCss.languageServer.path` selects a locally built server executable instead of the
  bundled payload.
- `viuUtilityCss.trace.server` controls Language Server Protocol tracing.

## Build and package

The package uses the same TypeScript and `vscode-languageclient` stack as `packages/viu`. Its
`Build.ps1` publishes `Assimalign.Viu.UtilityCss.LanguageServer` through the repository's shared
language-server publish target, stages payloads under `server/<runtime identifier>/`, installs npm
dependencies, and compiles the client.

Build every Visual Studio Code package and create platform-specific VSIX artifacts with the root
orchestrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\extensions\VisualStudioCode\Build.ps1 `
    -Configuration Release `
    -RuntimeIdentifier win-x64
```

The artifacts are written to `_out/extensions/VisualStudioCode/<configuration>/Vsix/`. With no
`-RuntimeIdentifier`, the orchestrator creates one package per supported runtime. Use `-SkipVsix`
to run only the package builds, preserving the payload-staging workflow.

The runtime mapping is:

| `vsce --target` | .NET runtime identifier |
| --- | --- |
| `win32-x64` | `win-x64` |
| `win32-arm64` | `win-arm64` |
| `linux-x64` | `linux-x64` |
| `darwin-x64` | `osx-x64` |
| `darwin-arm64` | `osx-arm64` |

The staged `server/`, compiled `out/`, installed `node_modules/`, and generated `.vsix` files are
ignored. Source packages under `packages/` are explicitly re-included by the area `.gitignore`,
because the repository-wide ignore rules otherwise treat that directory name as a NuGet restore
folder.
