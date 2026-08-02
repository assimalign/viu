# Viu for Visual Studio

Viu for Visual Studio adds editing support for canonical Viu single-file components (`.viu`) and
project-scoped tag-based `.vue` compatibility to Visual Studio.

## Features

- Syntax highlighting for component blocks, template markup and directives, C#, and CSS —
  PascalCase component tags render as type names, directives as keywords, and utility `class`
  values as variant/class segments, all using your theme's existing colors
- Diagnostics for malformed single-file-component block structure
- Completion for block headers and options, common template elements, directives, events, CSS
  properties, `Context.*`, and `Reactive.*`
- Candidate-aware Viu Utilities completion and generated CSS hover previews inside template
  `class` values, including custom tokens and prefixes from a directly included CSS-first project
  utility entry
- Hover documentation for core Viu concepts
- Full and incremental document synchronization through an isolated language-server process
- `.vue` template, C# script, style, and Viu Utilities routing when the nearest owning project uses
  `Assimalign.Viu.Sdk` or explicitly enables the language service; an explicit opt-out wins, and
  ordinary Vue projects remain outside Viu's language service

## Requirements

- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- An x64 or ARM64 Windows installation

The extension includes the matching self-contained language server, so no separate .NET runtime is
required.

## Preview status

This extension is currently in preview. Its first release provides syntax-aware editing.
Roslyn-backed C# completion, component discovery, navigation, rename, references, and source-mapped
compiler diagnostics are planned for later releases.

The current preview exposes the frozen Viu Utilities v4.3.3 compatibility surface. Viu Utilities is an
independent Viu feature compatible with documented Tailwind CSS v4.3.3 behavior. It is not
affiliated with or endorsed by Tailwind Labs.

Report problems and follow development in the
[Viu repository](https://github.com/assimalign/viu).
