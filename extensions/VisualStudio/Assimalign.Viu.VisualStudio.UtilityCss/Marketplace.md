# Viu Utilities for Visual Studio

![Viu logo](https://raw.githubusercontent.com/assimalign/viu/main/assets/branding/nuget/viu-nuget-mono-light-256.png)

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

Standalone `.html` and `.htm` files use Visual Studio's `HTML` content type at default settings. The
extension also registers Web Tools' `html-delegation` type for projected HTML and installations with
the HTML LSP feature enabled. Legacy Razor derives from `HTML`, so the client may activate for a
legacy `.cshtml` or `.razor` buffer; a URI-filtering middle layer drops those host-document messages
while allowing `.html`, `.htm`, and Razor-projected `__virtual.html` documents.

## Requirements

- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- The **ASP.NET and web development** component group
- An x64 or ARM64 Windows installation

The matching language server is included; no separate .NET runtime is required.

This extension is in preview. Report problems in the
[Viu repository](https://github.com/assimalign/viu).
