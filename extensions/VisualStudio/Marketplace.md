# Viu for Visual Studio

Viu for Visual Studio adds editing support for Viu single-file components (`.viu`) to Visual Studio.
One package: the `.viu` file association, the Viu color theme, and the Viu language server.

## Features

- **The Viu color theme.** Template, markup, and style constructs get their own colors — framework
  tags, element tags, component tags (bold), directives, attributes and values, interpolation
  delimiters (bold), tag delimiters, style selectors, and custom properties. Every one of them is a
  separate, editable entry under "Viu — …" in Tools > Options > Fonts and Colors, so the defaults are
  a starting point rather than a decision made for you
- **Your C# theme inside a component.** The `@script` block, `{{ interpolations }}`, and binding
  expressions color with Visual Studio's own C# classifications, so embedded code looks exactly like
  the C# in the file next to it
- **`.viu` opens in the right editor automatically.** The package registers the file extension, so
  there is no per-file Open With step
- **Auto-closing that knows which section you are in.** `{`, `(`, and `[` pair everywhere — typing
  `{` twice gives you `{{}}` for an interpolation. Quotes pair only where they mean a string, so an
  apostrophe in template prose stays an apostrophe. Typing `>` after an open tag inserts its end tag
  with the caret between them, `</` completes the nearest unclosed element, and `<!--` completes to
  `<!-- | -->`. Nothing fires inside `@script` or `<style>`, where `>` is a generic argument or a CSS
  child combinator, and void elements such as `<br>` insert nothing
- Diagnostics for malformed single-file-component block structure
- Completion for block headers and options, common template elements, directives, events, CSS
  properties, `Context.*`, and `Reactive.*`
- Candidate-aware Viu Utilities completion and generated CSS hover previews inside template
  `class` values, including custom tokens and prefixes from a directly included CSS-first project
  utility entry
- Hover documentation for core Viu concepts
- Full and incremental document synchronization through an isolated language-server process, so the
  Viu parsers and Roslyn never run inside Visual Studio itself

## Requirements

- Visual Studio 2022 17.14 or newer, or Visual Studio 2026
- An x64 or ARM64 Windows installation

The extension includes the matching self-contained language server, so no separate .NET runtime is
required. The Visual Studio core editor is the only prerequisite component; C# support enhances the
colors inside `@script` but is not required.

## Preview status

This extension is currently in preview. Its first release provides syntax-aware editing.
Roslyn-backed C# completion, component discovery, navigation, rename, references, and source-mapped
compiler diagnostics are planned for later releases.

Viu also reads the tag-based `.vue` single-file-component container, and its language server routes
`.vue` documents in Viu SDK projects. That capability is not surfaced by this Visual Studio package:
Visual Studio's own Web Tools owns the `.vue` file extension, and displacing it is a separate
decision. `.vue` editing support in Visual Studio may follow in a later release.

The current preview exposes the frozen Viu Utilities v4.3.3 compatibility surface. Viu Utilities is an
independent Viu feature compatible with documented Tailwind CSS v4.3.3 behavior. It is not
affiliated with or endorsed by Tailwind Labs.

Report problems and follow development in the
[Viu repository](https://github.com/assimalign/viu).
