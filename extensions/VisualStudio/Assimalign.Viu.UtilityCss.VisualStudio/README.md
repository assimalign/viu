# Viu Utilities for Visual Studio

This separate VSIX adds utility-CSS language features to HTML files without taking ownership of
their language, editor, grammar, formatting, or completion presentation. A thin in-process MEF
`ILanguageClient` starts the self-contained `Assimalign.Viu.UtilityCss.LanguageServer` payload over
stdio; Visual Studio renders the standard Language Server Protocol results.

## Version 1 scope

- `.html` and `.htm` through Web Tools' default `HTML` content type and its
  `html-delegation` projection/HTML-LSP content type;
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
completion manager, or custom hover UI. Its only middle layer is a URI boundary that prevents
HTML-derived legacy Razor host documents from reaching the server.

## Composition boundaries

- `.viu` is not registered here. The Viu extension owns that editor and consumes build-contributed
  class catalogs for utility completion and hover.
- `HTML` is registered because standalone `.html`/`.htm` buffers use it while the
  `WebTools.Languages.Html.LSP` feature flag remains at its default off setting.
- `LegacyRazor` derives from `HTML`, so legacy `.cshtml` and `.razor` buffers can cause the client to
  activate. The middle layer drops their `textDocument/*` messages unless the URI ends in `.html`
  or `.htm`; Web Tools' Razor-projected `__virtual.html` documents remain eligible through
  `html-delegation`.

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

The source-linked tests cover content-type names, URI filtering, architecture selection, path
containment, command-line quoting, and stdio process configuration without launching Visual Studio.
Completion, hover rendering, and color swatches are editor UI behavior and cannot be exercised
headlessly; validate them in an experimental Visual Studio hive before Marketplace publication.
