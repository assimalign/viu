# Viu Utilities for Visual Studio

This separate VSIX adds utility-CSS language features to HTML files without taking ownership of
their language, editor, grammar, formatting, or completion presentation. A thin in-process MEF
`ILanguageClient` starts the self-contained `Assimalign.Viu.UtilityCss.LanguageServer` payload over
stdio; Visual Studio renders the standard Language Server Protocol results.

## Version 1 scope

- `.html` and `.htm` through the modern Web Tools `html-delegation` top-level content type;
- utility-class completion inside class-attribute values;
- generated CSS in hover previews;
- color swatches and color presentations through LSP `textDocument/documentColor` where the active
  Visual Studio editor surface exposes them;
- `win-x64` and `win-arm64` self-contained language-server payloads.

The server reads the nearest UtilityCss editor sidecars emitted under `obj/**/utilitycss/` by
`Assimalign.Viu.UtilityCss.Build`. When no sidecar is available, completion falls back to the
built-in theme. It revalidates the manifest, catalog, entry stylesheet, and referenced stylesheets
on requests, so no parser or build task is loaded into `devenv.exe`.

This VSIX intentionally adds no pkgdef, file-extension registration, content type, classifier,
completion manager, middle layer, or custom hover UI.

## Composition boundaries

- `.viu` is not registered here. The Viu extension owns that editor and consumes build-contributed
  class catalogs for utility completion and hover.
- `.razor` is not registered. Current Razor tooling is cohosted, and a compatible second LSP-client
  activation path has not been verified.
- Classic `HTML` and legacy `.cshtml` are also deferred by the same probe. Visual Studio's language-
  client broker matches base content types, and the installed legacy hierarchy is
  `LegacyRazorCSharp` -> `LegacyRazor` -> `HTML` for both `.cshtml` and `.razor`. Registering any of
  those types would also register `.razor`, contrary to the version 1 boundary. Current `.cshtml`
  and `.razor` buffers likewise share the modern `Razor` type.

Follow-up compatibility work must probe Razor cohosting with both `.cshtml` and `.razor` open,
confirm that a second client does not duplicate or suppress Razor's own language traffic, and find
an exact classic-HTML/cshtml activation contract before any of those shared base types is registered.

## Build and verification

From the repository root:

```powershell
./extensions/VisualStudio/Build.ps1 -Configuration Release
```

The root orchestrator locates Visual Studio MSBuild through `vswhere`, builds both Visual Studio
extensions sequentially, and writes this artifact to
`_out/extensions/VisualStudio/Release/Assimalign.Viu.UtilityCss.VisualStudio.vsix`. The build fails
if either Windows payload is absent, if the UtilityCss dependency manifest contains
`Microsoft.CodeAnalysis`, if the VSIX gains a pkgdef/VsPackage asset, or if it exceeds 50 MB.

The source-linked tests cover architecture selection, path containment, command-line quoting, and
stdio process configuration without launching Visual Studio. Completion, hover rendering, and color
swatches are editor UI behavior and cannot be exercised headlessly; validate them in an experimental
Visual Studio hive before Marketplace publication.
