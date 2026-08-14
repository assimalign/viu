# Viu Utilities for Visual Studio

Utility CSS IntelliSense for Visual Studio's modern HTML editor, delivered by a standalone language
server and rendered through Visual Studio's standard Language Server Protocol client.

## Features

- Utility-class completion inside `class` attribute values in `.html` and `.htm` files
- Generated CSS previews on hover
- Color swatches and color presentations where the active Visual Studio HTML surface exposes LSP
  document colors
- Project-aware results from the editor sidecars emitted by `Assimalign.Viu.UtilityCss.Build`, with
  built-in-theme completion when no sidecar is available
- Isolated, self-contained x64 and ARM64 language-server payloads; no Roslyn assemblies load into
  Visual Studio

The extension does not replace the HTML editor or add a grammar, formatter, classifier, completion
manager, or registry entry. Visual Studio keeps ownership of the document and the UI; the extension
only supplies standard LSP results.

## Scope

Viu `.viu` files remain owned by **Viu for Visual Studio**. Utility results there come from the Viu
extension's build-contributed class catalogs, so installing this VSIX does not start a second server
for the same document.

Razor `.razor` files are not registered in this release because compatibility with Razor's cohosted
tooling has not been verified. Classic `HTML` and legacy `.cshtml` are deferred with that probe:
Visual Studio matches base content types, and both legacy types sit in the same hierarchy used by
legacy Razor. Registering the shared base would also activate this client for `.razor`.

## Requirements

- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- An x64 or ARM64 Windows installation

The matching language server is included; no separate .NET runtime is required.

This extension is in preview. Report problems in the
[Viu repository](https://github.com/assimalign/viu).
