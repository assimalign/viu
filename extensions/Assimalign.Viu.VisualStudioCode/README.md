# Viu for Visual Studio Code

A Visual Studio Code extension for Viu single-file components: a `.viu` language contribution with a
container grammar, and a client that starts the standalone Viu language server over stdio.

## Status

**This is a scaffold, not a published product.** It compiles, it packages, and it starts the same
language server the Visual Studio extension ships — but it has never been released to the Visual
Studio Code Marketplace or to the Open VSX registry, it has no icon or gallery banner, it has no
tests, and its `version` is a placeholder. Do not treat the `publisher`/`name` pair as claimed.

What is deliberately deferred:

- Marketplace and Open VSX publication, and the release workflow that would drive them.
- Semantic tokens. The container grammar is lexical; Viu Utilities class-value splitting, component
  resolution, and `@script` semantic colorization belong to the language server and arrive as
  semantic tokens rather than as TextMate guesses.
- Per-region comment toggling. `language-configuration.json` is a single document-wide
  configuration, so <kbd>Ctrl</kbd>+<kbd>/</kbd> uses `<!-- -->` everywhere; making it produce `//`
  inside `@script` and `/* */` inside `<style>` needs a language-service contribution.
- A multi-line `<template …>` or `<style …>` opening tag. A TextMate `begin` pattern is matched
  against one line, so an opening tag split across lines is not recognized as a block opener. The
  container parser accepts it ([FORMAT.md §4](../../tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md)).
- Extension bundling. The client is compiled with `tsc` and ships its `node_modules` production
  dependency; there is no esbuild/webpack step yet.

## What it contributes

| Contribution | Value |
| --- | --- |
| Language id | `viu`, bound to the `.viu` extension |
| Grammar | `source.viu` (`syntaxes/viu.tmLanguage.json`) |
| Language configuration | `language-configuration.json` |
| Activation | `onLanguage:viu` |
| Client | `src/extension.ts` → `out/extension.js`, `vscode-languageclient` 8.x over stdio |
| Server payload | `server/<runtime identifier>/Assimalign.Viu.Tooling.LanguageServer[.exe]` |

Settings: `viu.languageServer.enabled`, `viu.languageServer.path` (point at a server you built
yourself), and `viu.trace.server`.

## The `.vue` decision

**This extension does not claim the `.vue` file extension or contribute a `.vue` language.** Viu
compiles tag-based `.vue` containers as a shipping compatibility feature
([V01.01.06.09](https://github.com/assimalign/viu/issues/250)), and in Visual Studio that feature is
surfaced as a `viu-vue` document type — Visual Studio's language-server contract requires a declared
document type and cannot express an owning-project condition in that filter. Visual Studio Code has
no such constraint, and the calculus there is different: `.vue` already has a mature ecosystem whose
tooling owns that language id. A second extension declaring `contributes.languages` for `.vue` would
fight it for grammar and language-configuration ownership, for every user, in every workspace.

So the split is:

- The **`viu` language id and the grammar bind to `.viu` only.**
- The **LSP client's document selector additionally carries `{ pattern: '**/*.vue' }`.** A document
  selector is not a language claim: it changes nothing about how Visual Studio Code colors, folds,
  or comments a `.vue` file, and it adds no `.vue` activation event. Because the extension activates
  on `onLanguage:viu`, that pattern can only ever apply inside a session where a `.viu` document was
  already opened — which means a Viu project.
- The **server decides.** It performs a per-document owning-project check and declines any `.vue`
  file whose nearest owning project neither uses `Assimalign.Viu.Sdk` nor sets
  `ViuVisualStudioLanguageServiceEnabled` to `true`; an explicit `false` wins even in a Viu SDK
  project. Non-Viu `.vue` files in a mixed workspace are functionally untouched.

## The grammar approach

Visual Studio Code resolves TextMate scopes freely, so the grammar contributes **real Viu scopes**
rather than mapping Viu constructs onto a fixed set of built-in categories (which is what the
Visual Studio classifier must do — see
[the Visual Studio extension's DESIGN.md](../VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md)).
Two consequences:

1. **Block bodies delegate to embedded languages** instead of re-implementing token rules. `@script`
   bodies embed `source.cs`, `<style>` bodies embed `source.css`, and both interpolation interiors
   and directive attribute values embed `source.cs`. `contributes.grammars.embeddedLanguages` maps
   `meta.embedded.block.csharp`, `meta.embedded.expression.csharp`, and `meta.embedded.block.css` so
   bracket matching and word-based suggestions follow the embedded language.
2. **Viu-specific constructs get their own scopes.** PascalCase and dotted component tags are
   `support.class.component.viu` rather than `entity.name.tag`; `v-*`, `:bind`, `@event`, and
   `#slot` attributes are `entity.other.attribute-name.directive.viu`; interpolation delimiters are
   `punctuation.section.embedded.*.viu`.

The block-slicing rules come from
[`FORMAT.md`](../../tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md), not from a
generic HTML grammar. In particular `@script` ends at the **first later line whose first column is
`}`** (§3.2) — the grammar anchors on `^\}` rather than balancing braces, because that is the
container's actual termination rule. A top-level `<script>` tag is scoped `invalid.illegal`, matching
the parser's error 1017 (§6.2), and the legacy `@template`/`@style` blocks keep coverage for the
duration of their migration window (§6.1).

The `template` region is hand-written with HTML-shaped scopes rather than including
`text.html.basic`: the HTML grammar's own tag rules would claim directive attributes and component
tags before the Viu rules could.

## Build

The language server is a plain stdio LSP executable with no editor coupling. It is published
self-contained and single-file per runtime identifier by
[`build/Targets/Build.LanguageServer.targets`](../../build/Targets/Build.LanguageServer.targets) —
the same shared target the Visual Studio extension uses.

```powershell
powershell -ExecutionPolicy Bypass -File .\extensions\Assimalign.Viu.VisualStudioCode\Build.ps1
```

That script:

1. publishes the server for every packaged runtime identifier, into
   `_out/extensions/VisualStudioCode/<configuration>/LanguageServer/<rid>/`;
2. stages each payload into `extensions/Assimalign.Viu.VisualStudioCode/server/<rid>/`;
3. runs `npm install` and `npm run compile`.

Useful switches:

- `-Configuration Release` — release publish (no symbols in the single file).
- `-RuntimeIdentifier linux-x64` — publish and stage a subset. This is the normal preparation for a
  platform-specific package, because `vsce` has no per-target payload filtering of its own.
- `-SkipNodeBuild` — publish and stage only; skip `npm`.

Or run the pieces by hand:

```powershell
npm install
npm run compile      # or: npm run check   (type-check without emitting)
```

### Runtime identifiers

The full set lives in one place — `ViuLanguageServerAllRuntimeIdentifiers` in the shared target —
and `Build.ps1` reads it back rather than restating it. The Visual Studio VSIX deliberately stays at
`win-x64;win-arm64`: it embeds every payload found in its publish directory, and at roughly 18 MB
apiece a five-runtime VSIX would exceed the Marketplace size gate. Each host publishes to its own
`ViuLanguageServerPublishRoot`, and `ViuValidateLanguageServerPayloadRuntimeIdentifiers` fails the
build if a payload the host did not ask for is sitting in its publish directory.

Only Windows runtimes carry an `.exe` suffix; a Linux or macOS payload is
`Assimalign.Viu.Tooling.LanguageServer` with no extension. The targets, `Build.ps1`, and
`src/extension.ts` all resolve the name from the runtime identifier for that reason.

### Packaging

Package one VSIX per platform, staging only that platform's payload first, so a user downloads one
server rather than five:

```powershell
.\Build.ps1 -Configuration Release -RuntimeIdentifier linux-x64
npx @vscode/vsce package --target linux-x64
```

| `vsce --target` | Runtime identifier |
| --- | --- |
| `win32-x64` | `win-x64` |
| `win32-arm64` | `win-arm64` |
| `linux-x64` | `linux-x64` |
| `darwin-x64` | `osx-x64` |
| `darwin-arm64` | `osx-arm64` |

A VSIX built on Windows carries no POSIX file mode, so the staged Linux and macOS payloads arrive
without the executable bit. The client restores it (`chmod 0755`) before spawning the server.

`vsce` is intentionally **not** invoked by `Build.ps1`: publication is a release decision, and this
extension has not been released.
