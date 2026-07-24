# Viu for Visual Studio

Viu for Visual Studio adds editing support for Viu single-file components (`.viu`) to Visual
Studio.

## Features

- Syntax highlighting for component blocks, template markup and directives, C#, and CSS
- Diagnostics for malformed single-file-component block structure
- Completion for block headers and options, common template elements, directives, events, CSS
  properties, `Context.*`, and `Reactive.*`
- Hover documentation for core Viu concepts
- Full and incremental document synchronization through an isolated language-server process

## Requirements

- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- An x64 or ARM64 Windows installation

The extension includes the matching self-contained language server, so no separate .NET runtime is
required.

## Preview status

This extension is currently in preview. Its first release provides syntax-aware editing.
Roslyn-backed C# completion, component discovery, navigation, rename, references, and source-mapped
compiler diagnostics are planned for later releases.

Report problems and follow development in the
[Viu repository](https://github.com/assimalign/viu).
