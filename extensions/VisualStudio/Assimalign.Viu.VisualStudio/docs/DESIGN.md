# Visual Studio language tooling design

## Decision

**Recorded decision (2026-08-04): the Visual Studio client is a classic in-process VSSDK package,
and the language server stays a separate process.** This reverses the earlier decision to build the
client on the out-of-process `VisualStudio.Extensibility` model, and the reversal is recorded rather
than quietly rewritten: the out-of-process client was chosen for isolation, and isolation turned out
to be a property of the *server* boundary, not of the client. Specified by `[TOOL-1]`.

The out-of-process model was abandoned because its editor surface is preview-grade and fought every
feature Viu needed. The concrete catalog, so nobody re-litigates it from memory:

- **Document-type binding is runtime-only, so the XML editor wins.** Document types contributed
  through the manifest are applied after an editor factory has already claimed the file, and the XML
  editor's wildcard sniffer claims any document opening with a well-formed tag — which every `.viu`
  container does (see "File extension ownership"). The fix for that is a `.pkgdef`, which leads
  directly to the third item.
- **`AppliesTo` reduces to the last document type AND the last glob.** Multiple document-type or
  path clauses on one part did not compose as a union; the manifest kept the last of each and
  intersected them, so a part could not express "`.viu` or `.vue`" at all.
- **A `.pkgdef` is unshippable out of process.** The asset requires `VssdkCompatibleExtension`,
  which in turn mandates `RequiresInProcessHosting` — an out-of-process extension that ships its own
  file association stops being out of process. The workaround was a second, code-free VSIX
  (`Assimalign.Viu.VisualStudio.Registration`): two installs for one feature.
- **No custom classification types.** There is no out-of-process equivalent of the MEF
  `ClassificationTypeDefinition`/`ClassificationFormatDefinition` pair, so every token had to borrow
  one of the editor's built-in categories. No Viu colors, no bold, no "Viu — …" entries in
  Tools > Options > Fonts and Colors. Semantic tokens shared the same ceiling, because Visual Studio
  maps them onto the same built-in classifications.
- **The remote tag bridge rejected even ordinary names.** It refused Roslyn's standard `method`, so
  the borrowed palette had holes too and semantic method spans fell back to `identifier`.
- **The contracts were preview-gated.** Both the language-server and the tagger APIs were reachable
  only behind `VSEXTPREVIEW_*` opt-ins in the 17.14 line, so the client shipped on surfaces
  Microsoft had not committed to.

**What the migration deliberately kept.** The server boundary is unchanged, and it was always the
load-bearing half of the original decision:

- the language server remains a separate stdio process, so the Viu parsers and Roslyn never load
  into `devenv.exe` and a parser fault or dependency conflict cannot destabilize the IDE;
- the server stays editor-neutral — it speaks the Language Server Protocol and reads file paths, and
  knows nothing about Visual Studio;
- the Visual Studio Code extension consumes the identical binary, published by the identical shared
  target (`build/Targets/Build.LanguageServer.targets`), so the two hosts cannot drift.

Only the client crossed the boundary, and the client is exactly the layer the original decision
described as "document registration, process lifetime, and editor presentation". No server change
was required by the migration and none was made.

**Revisit condition.** If `VisualStudio.Extensibility` ships stable editor classification — custom
classification types with user-editable format definitions — together with a pkgdef-equivalent file
association, the thin client could migrate back with the server untouched. Those two capabilities
are the whole of what forced the move; everything else the client does (start a process, hand over
two streams, lex a buffer) has an out-of-process shape already.

## Components

```text
Visual Studio (devenv.exe)
  -> Assimalign.Viu.VisualStudio  (classic VSSDK package, in process)
       -> Assimalign.Viu.VisualStudio.pkgdef   claims .viu for the Source Code (Text) Editor
       -> viu content type    MEF; bases: code, code-languageserver-preview
       -> ViuClassifier       MEF; the Viu palette over ViuLexicalClassifier
       -> ViuLanguageClient   MEF ILanguageClient; starts the server, hands Visual Studio its streams
            -> stdio Language Server Protocol connection
                 -> Assimalign.Viu.Tooling.LanguageServer  (separate process)
                      -> Assimalign.Viu.Tooling.LanguageService
                           -> Assimalign.Viu.Syntax.SingleFileComponent
                           -> Assimalign.Viu.Tooling.SingleFileComponent (the shared build/editor projection, [V01.01.06.11])
                           -> Assimalign.Viu.Tooling.UtilityCss
```

`extensions/VisualStudio/` holds exactly one project, `Assimalign.Viu.VisualStudio`, and one build
entry point, `Build.ps1`. Everything below the stdio boundary is editor-neutral developer tooling
and lives under the repository's `tooling/` root (`tooling/Assimalign.Viu.Tooling.LanguageServer`,
`tooling/Assimalign.Viu.Tooling.LanguageService`, and the build-time cores they consume).

`Assimalign.Viu.VisualStudio` performs fast lexical classification with no server round trip. It
lexes both container syntaxes of the hybrid `.viu` format ([V01.01.06.10]): tag-delimited top-level
`<template>`/`<style>` sections (with nested-`<template>` depth tracking so slot fragments do not end
a section) plus the `@script` @-block, and the legacy `@template`/`@style` @-blocks keep highlighting
during the migration window.

`Assimalign.Viu.Tooling.LanguageServer` owns protocol framing and translates protocol values into
editor-neutral contracts. It writes protocol messages only to standard output; standard error is
reserved for diagnostics.

`Assimalign.Viu.Tooling.LanguageService` caches the current text and the format-appropriate immutable
container parse for each open `.viu` or accepted `.vue` document. It exposes block diagnostics,
completion catalogs, declaration-aware `@script` member completion ([V01.01.12.07.04] #261 — a
syntax-only Roslyn parse of the script block, cached on the block text; no compilation, no
workspace), semantic `@script` completion when the host feeds a restored project context
([V01.01.12.23] #259 — an artifact-fed `CSharpCompilation` answered through
`SemanticModel.LookupSymbols`; still no workspace), shared utility-class completion,
project-defined utilities and variants, and generated-CSS hover documentation. It never loads a
Roslyn workspace.

## The Viu color theme

Classification splits by ownership, specified by `[TOOL-3]`.

**Embedded C# resolves the editor's own classification types.** Every kind a C# token pass can emit —
keyword, string, number, comment, operator, punctuation, identifier, class name, method name —
resolves the name the editor and Roslyn already register, so the `@script` block, interpolation
interiors, and binding-expression interiors color exactly as the user's chosen C# theme colors C#.
Viu registers none of those names and expresses no opinion about them: the theme applies 1:1.

Resolution is defensive, because three of those names (`punctuation`, `class name`, `method name`)
come from Roslyn's editor features rather than the core editor. Each buffer resolves every kind once
and walks a fixed fallback chain when a name is absent: `method name` and `class name` fall back to
`identifier`, `punctuation` falls back to `operator`, and a kind that resolves to nothing at all is
dropped rather than mis-colored. A Visual Studio without a managed-language workload therefore still
colors Viu templates in full, and script spans degrade to plain identifiers instead of vanishing.
The chain lives in `ViuClassificationTypeNames.GetFallbackClassificationTypeName`.

**Template, markup, and style constructs resolve ten Viu-owned classification types.**
`ViuClassificationTypes` registers each one with `text` as its base definition, and each has exactly
one `ClassificationFormatDefinition` carrying a Viu default color and a `[UserVisible(true)]` entry
in Tools > Options > Fonts and Colors. The `viu.` name prefix keeps them from colliding with any
other extension's; the "Viu — " display prefix groups them together in that list.

| Classification type | Fonts and Colors entry | Default | Covers |
| --- | --- | --- | --- |
| `viu.tag` | Viu — framework tag | `#569CD6` | `template`, `slot`, `style`, `script`, and the legacy `@template`/`@style` headers |
| `viu.element` | Viu — element tag | `#7EE787` | HTML element tag names |
| `viu.component` | Viu — component tag | `#2BD9BC`, **bold** | PascalCase or dotted tag names |
| `viu.directive` | Viu — directive | `#C586C0` | `v-*`, `:bind`, `@event`, `#slot`, including valueless forms such as `v-else` |
| `viu.attribute` | Viu — attribute | `#9CDCFE` | plain attribute names and CSS property names |
| `viu.attribute.value` | Viu — attribute value | `#FFAB70` | attribute values, quotes included |
| `viu.interpolation.delimiter` | Viu — interpolation delimiter | `#FFD866`, **bold** | the `{{` and `}}` of an interpolation |
| `viu.delimiter` | Viu — tag delimiter | `#6E7681` | `<`, `>`, `</`, `/>`, and the attribute `=` |
| `viu.style.selector` | Viu — style selector | `#D7BA7D` | selectors in a style block |
| `viu.style.custom.property` | Viu — style custom property | `#4EC9B0` | `--name` theme tokens |

Two of the ten are bold, and both choices are deliberate. Which tags are components is the single
most load-bearing fact about a Viu template, so component tags carry weight as well as color. And the
interpolation delimiters mark where markup stops and an expression starts — the expression inside
colors as ordinary C#, so the boundary has to carry itself. In the other direction, the tag delimiter
is the most muted color in the palette on purpose: tag structure should recede so that names carry
the reading.

Component detection is casing, and only casing. A purely lexical classifier never consults a
component registry, and Viu's ordinal name resolution makes the authored spelling meaningful
(specified by `[CMP-6]`), so a PascalCase or dotted tag name is a component and a lowercase one is an
element.

**Recorded user decision — a `class` value is one uninterrupted color.** `UtilityVariant` and
`UtilityClass` both map to `viu.attribute.value`, so a list of utilities reads as a value rather than
as a syntax exhibit. The lexer still separates each leading variant prefix (`hover:`, `md:` — the
colon included) from the utility that follows, and a `[...]` arbitrary value is never split on its
inner colons, so the utility stays one token. The language server and the Visual Studio Code grammar
both act on that distinction; the Visual Studio editor simply does not color the parts apart.
Candidate validation stays in the language server per the source boundaries below.

**Recorded decision — the method-position rule for binding values.** Binding-expression interiors run
the C# token passes, and the method position has two halves. Call syntax — an identifier immediately
followed by `(` — is a method wherever a C# pass runs, the `@script` block included. In an
event-handler binding the handler slot is itself a method position, so a bare identifier there is a
method even without parentheses: `@click="Increment"` names a method exactly as `@click="Increment()"`
does. An identifier followed by `.` is a receiver rather than the handler, so
`@click="ViewModel.Increment"` still colors its two halves apart.

A plain binding (`:value="Count"`, `v-if="Visible"`) has no method position and names component state,
so its bare identifiers stay identifiers. That exemption is load-bearing: the PascalCase-is-a-type
heuristic that serves general C# well would otherwise color every bound property as a type. The
heuristic still runs unchanged in the `@script` block and in interpolation interiors, which really are
general C#; a binding value is the one position where the leading name is a member by construction.

### Classification caching

`ViuClassifier` lexes the **whole document** and caches the result on the snapshot. A line's
classification depends on the container section enclosing it — the lexer has to see the `<template>`
above line three hundred to color line three hundred — so asking for a range would cost exactly what
asking for everything costs. Every `GetClassificationSpans` call is therefore answered by filtering
that one result, and exactly one snapshot's spans are held at a time: this is a reuse cache, not a
history. The editor reaches the classifier more than once for the same snapshot per keystroke, which
is precisely the repetition the cache absorbs.

Change notification is whole-buffer for the same reason: adding a `</template>` on line three changes
how line three hundred is colored, so no narrower invalidation would be correct. The editor
re-requests only the ranges it is actually displaying, so the cost of the wide notification is
bounded by the visible text rather than by the document.

The classifier is stored in the buffer's property collection and subscribes to that same buffer, so
the subscription is a self-reference that dies with the buffer; `IClassifier` offers no disposal point
to unsubscribe from.

The out-of-process tagger this replaces reported the whole document too, but for a different reason:
each report was a JSON-RPC round trip, and reporting only the requested lines left every unscrolled
line drawn in the default text color until a round trip completed. That failure mode does not exist
in process. Whole-document lexing is a property of the container format and survives the transport
change; the version watermarking and requested-range bookkeeping that made the round trips bearable
did not, and is gone.

## Auto-closing

Typing an opening delimiter closes it ([V01.01.12.07.08]), and a `{` opened in a script section
expands into an indented block on `Enter` ([V01.01.12.07.09]). Two mechanisms carry that, chosen
because the editor offers exactly two shapes and they answer different questions.

**Character pairs ride the editor's brace-completion engine.** The engine already owns everything a
pair needs after it opens — inserting the closer, tracking the span, typing through the closer, and
deleting both halves on one backspace — so Viu contributes only which characters pair, where, and
what `Enter` does between them. Interpolation does **not** fall out of the brace pair and needs a
rule of its own; see "The interpolation scaffold".

Both registrations are `IBraceCompletionContextProvider`s filtered on the `viu` content type, for two
different reasons:

- `ViuBracketBraceCompletionContextProvider` carries `{ }`, `( )`, and `[ ]`. These pairs need no
  gating — they are balanced constructs in template markup, in C#, and in CSS alike — and they
  shipped on an `IBraceCompletionDefaultProvider` for exactly that reason ([V01.01.12.07.08]). They
  moved to a context provider in [V01.01.12.07.09] because a *default* session never calls
  `OnReturn`: `BraceCompletionDefaultSession.PostReturn` guards on `_context != null`, so a pair with
  no context can never expand into a block. Nothing else about their behavior changed — the
  aggregator builds the same `BraceCompletionDefaultSession` either way.
- `ViuQuoteBraceCompletionContextProvider` carries `"` and `'`, which do need gating. The editor's
  aggregator asks the providers registered for a character in order — session, dynamic session,
  context, then default — and takes the first that agrees to start; a default provider always agrees,
  so a pair registered on one can never be declined. Registering the quotes on a context provider and
  **only** there makes its `TryCreateContext` the gate; a second registration on a default provider
  would hand the aggregator an unconditional fallback and defeat it entirely.

Both providers hand out the same stateless `ViuBraceCompletionContext`, which tells the pairs apart
from the session's own `OpeningBrace`.

**Element and comment closing rides a typed-character command handler**, because there is no
"pair" to register: what gets inserted depends on the tag name the user just typed.
`ViuAutoClosingCommandHandler` is an `ICommandHandler<TypeCharCommandArgs>` filtered on the `viu`
content type.

**All the decisions are pure.** `ViuAutoClosingLogic` answers every question from document text and a
caret position alone — no editor type appears in it — and it takes its section attribution from
`ViuSectionScanner` and its void-element list from the repository's shared `DomKnowledgeData`.
`ViuBraceIndentation` computes the Return expansion's two indentation strings from a line of text and
two integers, and is pure for the same reason. The MEF parts, the brace-completion context, and the
command handler are thin adapters: they convert a snapshot into lines, ask, and apply. That is why the
behavior below is unit-tested through the source-linked test project while the composition and the
buffer edits are honestly runtime-verified only.

`ViuSectionScanner` is the single definition of the container grammar in this extension. It owns the
patterns that open and close a section and the `@`-block and tag-delimited state machines;
`ViuLexicalClassifier` drives its per-line passes from the same primitives, so colorization and
auto-closing cannot disagree about where a section starts. Attribution is per line, which is what the
format supports: every section boundary is line-anchored. A line that opens or closes a section
belongs to it, because content may sit on the same line; the column-0 `}` that ends a legacy
`@`-block does not, because it is structure rather than content.

### What pairs, and where

| Typed | Template | `@script` / `<script>` | `<style>` | Between containers |
| --- | --- | --- | --- | --- |
| `{` `(` `[` | pairs | pairs | pairs | pairs |
| `{` **immediately after `{`** | writes the interpolation scaffold `{{|}}` — **does not** pair | pairs (a nested block) | pairs (a nested at-rule) | pairs |
| `}` **with a `}` at the caret** | walks the caret over it | — | — | — |
| `"` | pairs **only in attribute-value position** | pairs | — | — |
| `'` | — | pairs | — | — |
| `>` after an open tag | inserts `</name>` | — | — | — |
| `/` immediately after `<` | completes the nearest unclosed element | — | — | — |
| `-` completing `<!--` | inserts ` -->` | — | — | — |

`Enter` between an auto-completed `{` and `}` expands them into a block **in a script section only**
([V01.01.12.07.09]) — see "Return between paired braces" below.

Attribute-value position means: inside an open tag header, with `=` as the last non-whitespace
character before the caret, and not already inside a quoted value. A tag header spanning several
lines is walked back through. Everything else in a template is text content, where a double quote is
punctuation rather than a delimiter and an apostrophe is part of ordinary prose — which is the whole
reason the single quote is script-only.

The three element behaviors each have exclusions, and they are what keeps the feature from being
intrusive:

- `>` inserts nothing after a **void** element (`<br>`, `<input>`, … — matched case-insensitively
  against the shared WHATWG table), after a tag the user **self-closed** (`<Component />`), when the
  matching end tag **already follows the caret**, when the `>` is **inside an attribute value**, and
  when the caret is not in a tag header at all — template text and interpolation interiors reach a
  `>` from the enclosing tag first, so they never qualify. Framework tags are ordinary elements here:
  `<template>` and `<slot>` auto-close like any other.
- `/` completes only when it is typed **immediately after `<`**, and only when something is still
  open. The ancestor scan starts at the top of the template run and skips void elements,
  self-closed elements, comments, and tags written inside attribute values. In a tag-delimited
  section the container tag is itself an unclosed element, so `</` at the top of a hand-authored
  `<template>` completes `</template>`.
- `-` completes only the third hyphen of `<!--`, and not when a `-` already follows.

**Section awareness is the load-bearing part.** A `>` in `@script` closes a generic argument list or
a lambda arrow, and a `>` in `<style>` is the CSS child combinator; a script or style section that
grew an end tag on every `>` would be unusable. That is why the element behaviors are template-only,
and why the top-level `<style>`/`<script>` opening tags do not auto-close either — their own line is
already attributed to the section they open.

### The interpolation scaffold

**Correction to [V01.01.12.07.08].** That work item recorded that typing `{` twice yields `{{}}`
"with the caret in the middle, which is the authoring path for `{{ Count }}`". It was aspirational
and it was false — it never worked, and it cannot work on the brace-completion path. The editor's
`ShouldStartSession` declines whenever the first non-whitespace character after the caret is a letter
or digit, the pair's own characters, one of `!`/`_`/`@`, or **any registered opening brace** — and
`<` is registered (the live set from a user session is `({["|<*'\u0000` plus a few from other
extensions). So in the ordinary case of authoring an interpolation inside an element,
`<p>{|</p>`, even the *first* brace never pairs, and the second has nothing to build on.

`{` typed immediately after `{` in a **template** therefore writes the scaffold explicitly
([V01.01.12.07.09]), from either caret state and with the same result:

| Before | Typed | After |
| --- | --- | --- |
| `<p>{\|</p>` (first brace declined) | `{` | `<p>{{\|}}</p>` |
| `<p>{\|}` (first brace paired) | `{` | `<p>{{\|}}` |

The second case reuses the closer the first brace's session already inserted; the first supplies
both. A user cannot tell which path they were on, which is the point.

**No inner spaces.** The scaffold writes `{{}}`, not `{{  }}` — matching what the Visual Studio Code
client already produces from its `language-configuration.json` pair, so the two hosts agree. An
interpolation reads perfectly well as `{{ Count }}`; typing that space is one keystroke, deleting two
unwanted ones is two, and the spaces are not part of the syntax.

**Nothing races the scaffold, and that is arranged rather than hoped for.**
`ViuAutoClosingLogic.AllowsBracketPair` declines the second `{`, so the context provider refuses, the
aggregator creates nothing, and the manager holds no pending session. Without that decline the
manager's `PostTypeChar` would validate the caret it finds after the scaffold — the character before
it really is `{` — push a session, and insert a third brace. The session opened by the *first* `{`,
where there was one, is left alone: its tracking points survive and widen around the insertion, and
its over-type path then fails its own validity check because the span's content changed, so it
declines the closing brace and the walk-over below answers instead.

**`}` walks over an existing `}` in a template.** Nothing tracks a hand-written scaffold, so the
editor's own type-through does not apply and closing `{{}}` by typing both braces would otherwise
leave `{{}|}}`. The test is deliberately broader than "inside an interpolation" — just "a `}` is
already at the caret, in a template" — because matching a closer to its opener would mean parsing the
template to decide a keystroke. The cost is that a literal `}}}` in template prose cannot be typed
straight through; it can still be pasted, or typed with an arrow key between the braces. Script and
style sections are untouched, where a live session owns `}` and a walk-over firing outside one would
break ordinary C# and CSS authoring. The two can never both act: a live session's `PreOverType` runs
in the chained brace-completion handler and marks the command handled, so this handler is not reached
at all when the platform has already walked the caret over.

**Backspace is deliberately not special-cased.** Inside the scaffold it deletes one character at a
time, so `{{|}}` backspaces to `{|}}`. Collapsing both sides would mean owning a pair's lifetime —
tracking the span across edits, deciding when it stops being a pair — which is exactly the machinery
the brace-completion engine exists to provide and which is not available for text no session backs.
Recorded as accepted rather than built; revisit if it proves annoying in practice.

### Return between paired braces

Pressing `Enter` with the caret between an auto-completed `{` and `}` produces the three-line block
Visual Studio's C# editor produces ([V01.01.12.07.09]):

```text
    partial void OnSetup() {          partial void OnSetup() {
    |}                          ->        |
                                      }
```

The closing brace moves onto a line of its own at the **opening brace line's** indentation, and the
caret lands on a blank line one level further in. Three things decide the shape, and all three are
pure and unit-tested:

- **Where it applies.** `ViuAutoClosingLogic.AllowsBlockExpansionOnReturn` allows the expansion only
  for a brace opened in a **script** section. A template brace is most often an interpolation —
  `{{ Count }}` is authored by typing `{` twice — and pushing its closer onto its own line would break
  the expression outright. A `<style>` rule genuinely is a block, so expanding there would be
  defensible; it is deliberately left alone so this change carries exactly the specified behavior and
  template and style sections keep the pairing they shipped with. Revisit if CSS authoring asks for it.
- **What indentation it uses.** The `viu` content type has no `ISmartIndentProvider` — nothing in
  Visual Studio registers one for it, and writing one would mean teaching the editor the whole
  container grammar for one keystroke. `ViuBraceIndentation.ComputeBlockExpansion` instead reads the
  opening brace line's leading whitespace and the buffer's own `Tabs/IndentSize` and
  `Tabs/ConvertTabsToSpaces` options. Nesting needs no brace counting: the opening line's own
  indentation already carries the depth.
- **What it never rewrites.** The copied indentation is verbatim, even when it disagrees with the
  current tabs-versus-spaces setting — a tab-indented file opened under a spaces setting keeps its
  tabs, and only the *added* level follows the option. Normalizing it would silently reformat a line
  the user did not touch. An indent size below one is floored at one space, because a zero-width level
  would leave the caret's blank line indistinguishable from the closer's.

The edit itself is a single `ITextEdit` replacing the whitespace between the caret's line start and
the closing brace, which makes it one undo unit. It cannot be merged with the line break the editor
already committed, because `PostReturn` runs *after* that edit: `Ctrl+Z` steps back through the
expansion and then the newline. Every precondition in `ViuBraceCompletionContext.OnReturn` is a
bail-out rather than a repair — a session whose tracking points have gone stale leaves the buffer
exactly as the editor left it.

### What engages the brace-completion engine — the [V01.01.12.07.09] root cause

[V01.01.12.07.08] shipped the pairs and they read as dead in `@script`. The leading hypothesis was
that `DefaultTextViewOptions.BraceCompletionEnabledOptionId` is a per-view option real language
services set for their own views, leaving every Viu pair correct but dormant. **Decompiling the
shipping editor refutes that**, and the refutation is recorded here so nobody re-derives it:

- `Microsoft.VisualStudio.Platform.VSEditor.dll` →
  `Microsoft.VisualStudio.Text.BraceCompletion.Implementation.BraceCompletionEnabledOption` is the
  `EditorOptionDefinition<bool>` behind that key, and its `Default` is **`true`**.
- A scan of every assembly in the Visual Studio 18 installation finds the name
  `BraceCompletionEnabledOptionId` in exactly two places: `Microsoft.VisualStudio.Text.UI.dll`, which
  declares the key, and `Microsoft.VisualStudio.Platform.VSEditor.dll`, which declares the option and
  reads it in `BraceCompletionManager.GetOptions`. **Nothing in the product ever writes it.** The
  per-language "Automatic brace completion" checkbox is not wired to it — Roslyn enforces that inside
  its own `IBraceCompletionSessionProvider`, which a `viu` buffer never reaches.

Both of those facts are true, and the conclusion first drawn from them — that brace completion is
therefore already on for a `viu` view — was **wrong**. Recorded rather than quietly rewritten,
because the way it was wrong is the reusable lesson.

**Both greps searched for the wrong thing.** The writer references the option neither by its key
field nor by an ASCII copy of its name: it passes the option *name* as a C# string literal, and
literals live in the `#US` metadata heap encoded **UTF-16**. A byte grep for `BraceCompletionEnabled`
or for `BraceCompletion/Enabled` cannot match a UTF-16 literal, so
`Microsoft.VisualStudio.Editor.Implementation.dll` never appeared in either result. When a grep for a
writer comes back empty, that is evidence about the grep.

**The writer, found by the opt-in trace and then read in the decompiler**, is
`Microsoft.VisualStudio.Editor.Implementation.SimpleTextViewWindow`:

```text
Init_InitializeWpfTextView()
  -> SetToolsOptions(fontsAndColorsCategory.LanguageService)
       -> IVsTextManager.GetUserPreferences7(null, new[]{ new LANGPREFERENCES4 { guidLang = … } }, null)
       -> AdoptLangPreferences(langPrefs)
            -> if (_canChangeBraceCompletion)
                   _editorOptions.SetOptionValue("BraceCompletion/Enabled", val.fBraceCompletion != 0);
```

`fBraceCompletion` is the legacy per-**language service** preference, backed by the
`ShowBraceCompletion` value under `[$RootKey$\Languages\Language Services\<Lang>]`. Every language
that pairs braces registers it: C#, Visual Basic, Razor, TypeScript, the HTML/JSON/REST Web Tools
languages, and VC all ship `"ShowBraceCompletion"=dword:00000001` in their `.pkgdef`. A `.viu` buffer
has **no language service at all** — deliberately, because that registry key stamps its own content
type and re-breaks colorization (see "File extension ownership") — so `ActualLanguageServiceID` falls
back to the default file type, the preferences come back zeroed, and the option is explicitly written
`false` on the view.

The trace from a user session says exactly that, and nothing else has to be inferred:

```text
view.created  … braceCompletion(effective=False definedOnThisView=True globalValue=True)
              … enabled=False activeSessions=0 openingBraces="({[\"|<*'\u0000`^~_"
```

The aggregator had resolved the Viu pairs correctly all along — `(`, `{`, `[`, `"` and `'` are all in
that set — and `BraceCompletionManager.PreTypeChar` returned on `!Enabled` before it consulted any of
them, which is why no provider ever logged a call. The MEF side was healthy throughout: the hive's
composition cache carries every Viu part with no error, and the deployed assembly's
`[BracePair]`/`[ContentType]` metadata decompiles to what the source declares.

This also settles a question [V01.01.12.07.08] left open: **template and style pairing were equally
broken**, because the option gates the whole engine rather than any one pair. Nobody had noticed.

### Restoring the inherited setting

`ViuBraceCompletionEnablementTextViewCreationListener` **clears** the view-scoped value rather than
writing `true`. That is the whole off-switch story: the option's own default is `true` and nothing in
Visual Studio writes the global scope, so the global value *is* the user's Automatic Brace Completion
choice. `ClearOptionValue` removes the adapter's spurious definition and lets that choice through;
writing `true` would overwrite it and take the off switch away from a user who had deliberately
turned the feature off.

The decision is `ViuBraceCompletionEnablement.ShouldClearViewOverride(definedOnThisView,
effectiveValue, globalValue)` — pure, and unit-tested across all eight states. It clears in exactly
one: locally defined, off, while the global value is on. The other three declines are each real — an
inherited value is already the user's, a locally defined `true` needs no help, and a global `false`
is the off switch.

**Ordering is not a race this part could lose.** The adapter's write happens inside
`Init_InitializeWpfTextView` *before* the WPF view exists, so it is always ahead of any
`ITextViewCreationListener` and one clear at view creation repairs the view the user opens. It is not
the only write, though: `SimpleTextViewWindow.OnUserPreferencesChanged7` calls `AdoptLangPreferences`
again whenever Tools > Options broadcasts a change for that same language-service GUID, which would
re-disable a view already open. An `IEditorOptions.OptionChanged` subscription, dropped on
`ITextView.Closed`, covers that.

**The clear cannot loop**, and the bound comes from the condition rather than from a guard flag.
Clearing raises `OptionChanged` synchronously and re-enters the handler once; by then the local
definition is gone, so `ShouldClearViewOverride` answers `false` and the recursion stops at depth two.
A user toggling the global option raises the same notification and is declined for the same reason —
an inherited value is never cleared.

What actually suppresses a pair is the editor's own start heuristic, and it is worth knowing when
triaging "it did not close":
`BraceCompletionManager.ShouldStartSession` → `ShouldStartBraceSessionWithDifferentOpenCloseCharacters`
refuses to open a session when the first non-whitespace character **after** the caret on that line is
a letter or digit, the pair's own opening or closing character, another registered opening brace, or
one of `!`, `_`, `@`. Typing `(` immediately before an identifier — a cast such as `(ViuRouter?` — is
therefore declined, and declined identically in a `.cs` file. That is C#'s behavior, not a Viu
divergence, and matching C# means keeping it. The genuine divergence was the missing `OnReturn`, which
is what this work item fixes.

### Opt-in editor diagnostics

Everything above about the *engine* was established by decompiling Visual Studio. What no amount of
reading establishes is what happens on a particular machine, in a particular hive, with a particular
set of extensions composed — and brace completion reproduces only when a person types. Setting the
environment variable `VIU_EDITOR_DIAGNOSTICS` to any non-empty value **in the process that launches
`devenv.exe`** turns on a plain-text trace at `%TEMP%\viu-editor-diagnostics.log`, one line per event:

| Category | Written when | Carries |
| --- | --- | --- |
| `view.created` | a `.viu` document view opens | the view buffer's content type and full base chain, the data model's content type, whether the view buffer is the document buffer, the view roles, the Automatic Brace Completion option **as observed** (effective value, whether defined on this view, global value), and the editor's brace-completion manager if it exists yet |
| `brace.reenabled` | the option is inspected at view creation and on every change to it | the trigger, the full before state, whether the view override was cleared, and the full after state — the authoritative before/after pair, because two `ITextViewCreationListener` parts have no defined order and `view.created` may be written on either side of the repair |
| `buffer.changed` | every buffer edit while the view is open | version before and after, the edit tag, and each change as position, deleted text, inserted text |
| `context.bracket` / `context.quote` | the editor asks a Viu provider whether a pair may start | the opening and closing characters, line, column, resolved section, the line's text, the allow/deny answer, and the manager's live `Enabled`, active session count, and registered opening/closing brace characters |
| `typechar.enter` / `typechar.exit` | `ViuAutoClosingCommandHandler` runs | the typed character, whether it is a trigger character, and on exit whether the command was handled — spelled out as `chain=STOPPED` or `chain=CONTINUES`, because a non-chained `ICommandHandler` has no next-handler delegate and `return true` *is* the swallow |
| `context.onreturn` | `Enter` reaches the Viu brace-completion context | the session's opening and closing characters and whether its tracking points survive |

`view.created` plus `buffer.changed` is the pair that separates the two failure shapes a user cannot
tell apart: a closer that is never inserted produces one insertion of the typed character, while a
closer inserted and then withdrawn produces two insertions and a deletion — and whatever ran between
those lines is the culprit.

Three properties make this shippable rather than a debug branch. It is **dormant**: the variable is
read once at type initialization, so with it unset `ViuEditorDiagnostics.IsEnabled` is a static field
read at every call site and no file is ever created — pinned by unit tests, because a trace that was
not opt-in would be a behavior change shipped to every user. It is **total**: the message factory
runs inside a guard and every file operation inside another, so a locked file, a full disk, or a bug
in a message factory costs a log line and never a keystroke. And it is **honest about its one
deviation**: `ViuEditorDiagnosticsDescriptions.DescribeBraceCompletionManager` reflects over the
editor's internal `BraceCompletionManager`, read out of the view's property collection under the
string key the editor stores it as, because its live view of the option and of the registered brace
characters is precisely the missing evidence. That is the only reflection in the repository; it is
diagnostics-only, dormant by default, and lives in a .NET Framework in-process extension that is
never trimmed and never ahead-of-time compiled, so the AOT and trimming constraints that govern the
shipping WebAssembly runtime are not in play. A missing member degrades to `<absent>`.

This trace is what found the root cause: its very first line showed the option defined `false` on the
view against a global `true`, which no reading of the decompiled editor had suggested. It is kept
rather than deleted because the same one-line-per-event shape answers the next question of this kind
too, and because it costs a static field read when it is off.

### Recorded decisions

- **The comment shape is `<!-- | -->`.** Typing the third `-` inserts `-  -->` with the caret between
  the two spaces, so the comment reads `<!-- text -->` the moment the user types. Parking the caret
  hard against the opener instead would produce `<!--text -->`.
- **The typed character is part of the insertion.** The handler reports the command as handled and
  writes the character itself, so the completion is one buffer change inside one transaction: a
  single `Ctrl+Z` removes the end tag and the character that triggered it together, matching the
  editor's own brace completion and Visual Studio's HTML editor.
- **The handler is ordered after the completion and brace-completion handlers.** Both are chained
  handlers that pass the character along, so they see it first and this one still runs; the order
  matters because a handled command stops the chain, and running first would hide the keystroke from
  an open completion session. The two names are written as literals — one of them has no published
  constant, so a package reference for the other would buy nothing, and an unrecognized ordering name
  is simply ignored.
- **The Automatic Brace Completion option is the user's, and Viu only removes what the shell wrote
  over it.** The editor's brace-completion manager reads
  `DefaultTextViewOptions.BraceCompletionEnabledOptionId` and returns before it consults any
  provider, so the option gates every Viu pair in every section. The global scope holds the user's
  choice and Viu never writes it; the only thing Viu touches is the *view-scoped* value the legacy
  editor adapter stamps on from a language service `.viu` does not have, and it clears that value
  rather than replacing it, so a global `false` still wins. See "Restoring the inherited setting".
  Element auto-close has no editor-owned option and ships **always on**; inventing a Viu options page
  for one toggle was rejected. Revisit if it proves intrusive.
- **Over-type and paired backspace come from the editor, not from the context.**
  `OvertypeSession.PreOverType` consults `IBraceCompletionContext.AllowOverType` only to let a
  context *veto* over-typing, and `BraceCompletionDefaultSession.PreBackspace` never asks a context at
  all. `ViuBraceCompletionContext.AllowOverType` therefore returns `true` and adds nothing; it exists
  because the interface requires it. `OnReturn` is the one behavior the editor delegates outright,
  and it is the whole reason the brackets carry a context.
- **Auto-surround is ungated.** Selecting text and typing a quote wraps the selection wherever the
  pair is registered, because the editor answers that from the `[BracePair]` metadata alone and never
  consults the context provider. Accepted rather than worked around: surrounding happens only with an
  active selection, where the alternative — replacing the selection with the typed character — is the
  more destructive outcome.
- **No new package pins were needed.** `Microsoft.VisualStudio.Text.BraceCompletion`,
  `Microsoft.VisualStudio.Commanding`, `TypeCharCommandArgs`, and the undo history registry all live
  in the editor packages this project already references compile-only.

### Out of scope, recorded rather than half-built

- **`=` inserting `=""`.** Auto-quoting an attribute the moment `=` is typed is a separate behavior
  from pairing a quote the user typed, and it interacts with attribute-name completion — a directive
  such as `v-else` takes no value at all. Deferred as its own decision.
- **Element auto-close in Visual Studio Code.** That client gets its character pairs from
  `language-configuration.json` already. Element auto-close there has no declarative form: it needs a
  custom protocol request between the extension and the language server, which is a protocol change
  this work item deliberately does not make. Recorded as a follow-up candidate.
- **An options page for element auto-close.** See the recorded decision above: always on for now.
- **Moving a paired `}` onto its own line on Return** was recorded here by [V01.01.12.07.08] as
  deferred, on the reasoning that giving `{` a context purely to reformat would be a runtime-only,
  untestable addition. [V01.01.12.07.09] **built it** — see "Return between paired braces" above. The
  reasoning was wrong on the second half: the decision about *where* the expansion applies and the
  computation of *what* indentation it uses are both pure, and only the buffer edit is runtime-only.
- **Expanding a `{ }` block on Return in a `<style>` section.** A CSS rule is a block and Visual
  Studio's CSS editor does expand it, so this is a plausible follow-up; it is out of scope here so
  that [V01.01.12.07.09] carries exactly the C#-parity behavior it specifies. Template sections are
  not a candidate: a template brace is usually an interpolation.

## Activation, and where `.vue` stands in Visual Studio

`ViuLanguageClient` is a MEF `[Export(typeof(ILanguageClient))]` filtered on the `viu` content type,
and that filter is the whole of activation: opening a `.viu` buffer starts the server, and nothing
else does. There is no document-type manifest, no solution scan, and no build-property condition —
the in-process client has no equivalent of the out-of-process `AppliesTo` machinery and needs none.

The `viu` content type carries a second base definition,
`CodeRemoteContentDefinition.CodeRemoteContentTypeName` (`code-languageserver-preview`), alongside
`code`. Activation matches on the
content type itself, but completion, hover, outlining, light bulbs, and the Error List entries the
server publishes are contributed by editor parts registered against that base — without it the server
would run and reach no surface. The base derives from `code`, so it does not displace the existing
one.

**Recorded decision (2026-08-04): `.vue` is out of scope for the Visual Studio client, in two
separate halves.**

- **Colorization is out of scope and intended to stay there.** Visual Studio's Web Tools claims
  `.vue` explicitly at wildcard priority `0x33` and contributes its own content type and colorizer.
  The Viu classifier is filtered on the `viu` content type, which no `.vue` buffer carries, so nothing
  collides today. Claiming `.vue` would mean overriding a first-party explicit registration and
  displacing an editing experience the user already has — an intrusion, and a separate product
  decision rather than a side effect of shipping Viu colors.
- **Language-service support is currently unreachable, not refused.** The server's own `.vue`
  admission rules are untouched and fully live: before admitting a `.vue` document it performs a
  deliberately narrow nearest-owning-project check for `Assimalign.Viu.Sdk` or the explicit
  `ViuVisualStudioLanguageServiceEnabled` marker, stopping at the first directory containing a project
  so an unrelated nested Vue project is not claimed by a Viu ancestor; an explicit literal `false`
  marker overrides SDK-name detection; the check repeats for document changes, diagnostics,
  completion, and hover; and a directory holding both eligible and ineligible project files is
  ambiguous without an evaluated project-system query, so the probe fails closed. The Visual Studio
  Code client reaches that gate. The Visual Studio client cannot, because it activates on content type
  `viu` alone and a `.vue` buffer never carries it. Making `.vue` semantics reachable from Visual
  Studio is future work, worth doing only if it is demanded, and would need an activation path that
  coexists with Web Tools rather than replacing it.

## Viu Utilities project context

`Assimalign.Viu.Tooling.UtilityCss` is the single compiler/editor authority. Its contract is frozen
to Tailwind CSS v4.3.3 by
[`compatibility-v4.3.3.json`](../../../../tooling/Assimalign.Viu.Tooling.UtilityCss/conformance/compatibility-v4.3.3.json)
and independently authored
[`golden-vectors-v4.3.3.json`](../../../../tooling/Assimalign.Viu.Tooling.UtilityCss/conformance/golden-vectors-v4.3.3.json).
The manifest enumerates 382 utility roots, 88 variants, 21 theme namespaces, value and modifier
modes, source forms, directives, functions, and canonical ordering. The language service consumes
that registry directly; completion detail and hover display executable compiler output.

For each completion or hover request, the language-server host:

1. finds the nearest owning Viu project;
2. reads exactly one literal `<ViuUtilityCss Include="...">` item;
3. reloads the project stylesheet;
4. builds its recursive relative `@reference` graph;
5. parses the virtual import/source configuration and immutable theme;
6. compiles local and referenced custom utility/variant definitions;
7. resolves the authored candidate through the same built-in and project compilers used by the SDK.

The CSS-first project entry supports:

- `@import "viu-utilities"` with `source(<path>|none)`, `prefix(...)`,
  `theme(inline|static)`, and `important`;
- path and inline `@source` inclusion/exclusion, including brace expansion and numeric ranges;
- normal, `inline`, `static`, `reference`, and `default` `@theme` declarations;
- static, nested, negative, and functional `@utility`;
- selector and block `@custom-variant`, authored `@variant`, and built-in/custom `@apply`;
- `@reference` composition for shared theme, utility, and variant definitions;
- `--value()`, `--modifier()`, `--default()`, `--spacing()`, and `--alpha()`.

`"viu-utilities"` is a Viu compiler sentinel, not a package. The VSIX does not install, load,
bundle, or coordinate with Tailwind CSS or Tailwind CSS IntelliSense. Viu Utilities is an
independent Viu feature compatible with documented Tailwind CSS v4.3.3 behavior; it is not
affiliated with or endorsed by Tailwind Labs.

The pre-`MSBuildWorkspace` project lookup is intentionally conservative. Multiple utility entries,
an MSBuild property or wildcard in `Include`, a missing file, or an unreadable reference falls back
to the built-in registry and default theme. The service does not guess an evaluated path. Custom
static utilities and theme-backed values are offered directly; functional definitions can expose
their root, but completion does not invent project-specific functional values absent from the
theme or authored source.

## Source and update boundaries

Utility IntelliSense activates only in static class attributes and literal class-binding strings.
The container parser supplies only `.viu` or `.vue` `<template>` text (including the legacy `.viu`
`@template` container during its migration window) to that context.
Script and style regions, ordinary `.cs`, arbitrary C# strings, and runtime-built class fragments
are never utility candidate sources. Complete alternatives must appear in template text or be
included through `@source inline(...)`. Code-first utility discovery is a separate deferred
feature.

The `@source` forms above govern SDK build-time candidate discovery. The language server does not
crawl configured source roots or convert inline source entries into completion suggestions; it
uses the loaded configuration to resolve class text authored in the template being edited.

The language server reads CSS-first configuration for editor semantics but does not write bundles
or refresh the browser. In a Debug `dotnet watch` session, the packaged SDK launches one
project-scoped CSS sidecar. It watches component files, the utility entry, explicit utility-source
items, and supported automatically discovered markup; batches regeneration; and lets the .NET
browser-refresh client replace the generated stylesheet links. A CSS file reached only through
`@reference` is re-read on compilation but is not independently a watch trigger unless it is also
an explicit `ViuUtilityCssSource`. This CSS-only update does not remount the Viu application or
discard browser state. Visual Studio's ordinary Hot Reload command does not invoke that watch-list
contract. Template/C# generation remains on the normal .NET build/Hot Reload path and remounts the
affected component on .NET 10 browser WebAssembly.

## Semantic IntelliSense roadmap

Project-aware IntelliSense requires one authoritative `.viu`/`.vue` to C# projection and source map:

1. **Delivered ([V01.01.06.11], #258).** The generator's component-name, script-region,
   generated-context, and source-mapping logic is extracted into the shared
   `Assimalign.Viu.Tooling.SingleFileComponent` library
   (`tooling/Assimalign.Viu.Tooling.SingleFileComponent`, see its `docs/DESIGN.md`); the source
   generator and this language service both consume it, and the two-host conformance test
   (`analyzers/Assimalign.Viu.Generators.Syntax/test/SingleFileComponentProjectionConformanceTests.cs`)
   pins ordinal-identical generated source, hint names, and diagnostics.
2. Have both the source generator and language service consume that projection builder so editor and
   compiler behavior cannot drift. (The extraction landed this for today's surfaces — the shared
   `ScriptBlockAnalyzer` member description and the block-to-file position composition that places
   outline symbol children (`SingleFileComponentDiagnostics.ComposeToFilePosition`, the same
   arithmetic as the emitted `#line` map); the full projected-document
   consumption arrives with steps 3–5.)
3. **Delivered ([V01.01.12.23], #259) — recorded decision: artifact-fed, not `MSBuildWorkspace`.**
   The server never evaluates the project. `ViuProjectAssetsReader` resolves the restore's own
   artifact (`obj\project.assets.json`) with pure `System.Text.Json`: package compile assets
   against the declared `packageFolders` order using NuGet's `.nupkg.metadata` restore-success
   marker; `Microsoft.NETCore.App` reference assemblies from the installed SDK's targeting pack
   (dotnet root pinned by the restore's `runtimeIdentifierGraphPath`, then `DOTNET_ROOT*`, then
   `%ProgramFiles%\dotnet`); every other shared framework (`Assimalign.Viu.App`) from its pinned
   `<name>.Ref` `downloadDependencies` package; project references from built outputs
   (newest wins, missing outputs dropped with a named remedy). No `MSBuildWorkspace`, no BuildHost
   processes, and no muxer/`global.json`/arm64 resolution exposure. The consumer prerequisite is
   exactly: **the project restored once (`dotnet restore`) plus an installed .NET SDK** for the
   `Microsoft.NETCore.App.Ref` targeting pack — the packed-consumer round trip
   (`scripts/Test-LanguageServerSemanticRoundTrip.ps1`, wired into `area-visual-studio.yml`)
   proves that documented prerequisite is sufficient against the published single-file server.
   Degradation is graceful and visible: a per-project status ladder (missing artifacts, partial
   resolution, stale artifacts, active) reports through `window/logMessage` on state-signature
   transitions — never per keystroke, re-warning on re-degradation — while loose files outside any
   Viu project stay silent, and the no-project-state path answers byte-identically to the
   pre-semantic service (both pinned by `LanguageServerSemanticDegradationTests`). Status messages
   carry the absolute project path so two same-named projects stay distinguishable. Two recorded
   decisions in this ladder: a healthy project reports "active" once on first resolution — not only
   on a degraded-to-available transition — so availability is visible in both directions; and an
   unresolvable package compile asset degrades the whole project to syntax-only with a warning
   naming the package, rather than serving a partial compilation whose missing references would
   surface as misleading binder misses (a partial-compilation rung remains a possible refinement).
4. **Delivered ([V01.01.12.23], #259) — SemanticModel backend for increment 1.** The open
   document's live text is projected through the shared library (generator-identical by
   construction), emitted, parsed, and added as an immutable fork of a per-project cached
   `CSharpCompilation` holding sibling `.cs` sources and projected sibling components. Completion
   binds with `SemanticModel.LookupSymbols` (member access binds the receiver first): no
   workspace, no MEF composition, no Features assemblies, zero new packages in the trimmed
   single-file publish. The recorded increment-2 upgrade behind the same engine seam is
   `AdhocWorkspace` + `CompletionService` (Features/Workspaces packages re-entering the central
   catalog at the single Microsoft.CodeAnalysis 5.3.x line, without `PrivateAssets=all`); its gate
   is an integration test against the actual trimmed single-file publish (MEF and
   `Assembly.Location`-dependent code paths are exactly what in-process tests cannot see) plus a
   VSIX size re-measure against the 50MB Marketplace budget (estimated +10–12 MB per compressed
   RID). Known gap, recorded as a non-goal for this increment: sibling `.cs` members produced by
   the **Reactivity source generator** are absent from the editor compilation — only the Syntax
   generator's projection is shared. The later option is running the ref pack's packaged
   generators through `CSharpGeneratorDriver`, with version-skew and load-isolation questions to
   settle then.
5. **Delivered for completion ([V01.01.12.23], #259).** `GeneratedScriptDocumentMapper` maps the
   member and using regions bidirectionally through the emitter's simple-form `#line` directives
   (line-affine, column-identity) and suppresses — never misplaces — positions in scaffold or
   render-body spans. Completion and resolve documentation ride the map today; hover, signature
   help, definitions, references, and diagnostics remain open work on the same map.
6. Integrate the existing template and CSS syntax trees for precise semantic tokens and recoverable
   embedded-language diagnostics.

Parsing remains cancellable and off the Visual Studio UI path: feature requests dispatch
concurrently with `$/cancelRequest` support, semantic engine work is serialized under its own gate,
and every cache re-validates by cheap stats on the next request (no file watchers).

## File extension ownership

Nothing in a Visual Studio installation registers the `.viu` file extension — a scan of all 525
`.pkgdef` files in VS 18 Community finds no entry for it. An extension with no registration falls
through to the **wildcard** (`"*"`) editor factories, which are offered in descending priority:

| Editor factory | Wildcard priority | Outcome for `.viu` |
| --- | --- | --- |
| TextMate | `0x25` | Declines — no `.viu` grammar |
| XML Editor Chooser | `0x21` | Declines — no designer registered |
| **Microsoft XML Editor** | `0x20` | **Claims the document** |
| Source Code (Text) Editor | `0x1e` | Never reached |

`Microsoft.XmlEditor.Sniffer.SniffXmlDocument` accepts any document whose first token is a
well-formed start tag. A `.viu` container opens with `<template>`, so the sniff succeeds on that
first tag and never reads the `@script` block below it. The factory then attaches the XML language
service and sets `guidVSBufferDetectLangSid` to false, which **suppresses later content-type
detection** — the buffer stays XML-derived for its lifetime.

Three consequences follow, and they explain behavior that would otherwise look like extension bugs:

- The `viu` content type never reaches the buffer, so **every** MEF part filtered on it — the
  classifier and `ViuLanguageClient` alike — is never activated. Neither colorization nor semantic
  IntelliSense works while the XML editor owns the file.
- The XML parser reports its own diagnostics against C# and container syntax: `@script {` becomes
  "Invalid token 'Text' at root level of document", `Get<string>` becomes "Tag was not closed", and
  `<style scoped>` becomes "Missing attribute value on attribute 'scoped'". These carry no error
  code, are editor-only, and never affect a build.
- `.vue` is unaffected, because Web Tools claims that extension explicitly at `0x33`, outranking the
  wildcard.

**The fix ships in this VSIX**, as
[`Assimalign.Viu.VisualStudio.pkgdef`](../src/Assimalign.Viu.VisualStudio.pkgdef) (#264): it claims
`.viu` for the text editor at `0x32`, the band Microsoft itself uses for explicitly owned extensions
(`json`, `html`, `css`), with `0x31` on the with-encoding sibling factory — the same one-lower offset
Visual Studio's own registrations use. `0x32` outranks every wildcard factory in the ladder above, so
the text editor claims the file outright. The text editor attaches no language service, so the buffer
then takes its content type from this extension's `FileExtensionToContentTypeDefinition`, and both the
classifier and the language client activate.

Carrying the pkgdef is precisely what the out-of-process model could not do: the asset requires
`VssdkCompatibleExtension`, which mandates `RequiresInProcessHosting`. That is one of the reasons the
extension is now in process (see "Decision"), and with the extension in process the constraint is
simply satisfied — the manifest declares the file as an ordinary
`Microsoft.VisualStudio.VsPackage` asset beside the MEF component. The
`Assimalign.Viu.VisualStudio.Registration` companion that once carried this exact registry claim is
**retired**; it was never published, and its whole payload now ships here.

Two omissions in that `.pkgdef` are deliberate:

- There is **no `[$RootKey$\Languages\File Extensions\.viu]` entry**. That key attaches a legacy
  language service to the buffer, which stamps its own content type and re-breaks precisely what the
  registration fixes.
- **`.vue` is not claimed**, for the reasons in "Activation, and where `.vue` stands in Visual
  Studio".

Do not reach for a TextMate grammar as a workaround either: TextMate's factory outranks XML and would
evict it, but it would stamp its own TextMate-derived content type on the buffer, trading one wrong
content type for another and still leaving both Viu parts inactive.

One user-level override survives the pkgdef and is worth knowing about when triaging a report of "no
colors": `GetDesignerFactory` consults the per-user editor mapping *before* the priority ladder, so a
user who once set Open With → *Set as Default* on `.viu` keeps whatever they chose. If that choice was
the Source Code (Text) Editor everything still works; if it was the XML editor, the pkgdef never gets
a vote. Clearing the override restores the shipped behavior. This is documented as a troubleshooting
note in [`extensions/VisualStudio/README.md`](../../README.md); it is no longer an installation step.

## Packaging

The shared `ViuPublishLanguageServer` target in `build/Targets/Build.LanguageServer.targets`
publishes self-contained, single-file .NET language-server executables for `win-x64` and
`win-arm64`; `Build.ps1` and the extension build both drive that one target, so an in-IDE build can
never package a stale server. The extension selects the executable matching
`RuntimeInformation.ProcessArchitecture`.

Self-contained packaging is the current implementation, **not an invariant**. Recorded decision
(2026-08-02, [V01.01.12.23] #259): the fully shipped product is expected to require a locally
installed .NET SDK and the `Assimalign.Viu.Sdk` package for complete end-to-end functionality —
in particular the Roslyn-workspace semantic features, whose project evaluation cannot be frozen
into the VSIX because it must match the SDK the consumer's project builds with. The boundary is:
baseline features (container parsing, diagnostics, utility IntelliSense) keep working with no
machine prerequisites; semantic features may depend on local SDK state and must degrade gracefully
— never silently — when it is absent. An earlier revision of this section stated machine
independence as a design guarantee; that overstated the intent. The VSIX layout is:

```text
Assimalign.Viu.VisualStudio/
  Assimalign.Viu.VisualStudio.dll
  Assimalign.Viu.VisualStudio.pkgdef
  language-server.json
  LanguageServer/
    win-x64/
      Assimalign.Viu.Tooling.LanguageServer.exe
    win-arm64/
      Assimalign.Viu.Tooling.LanguageServer.exe
```

The server path is resolved relative to the installed extension — derived from the client assembly's
own location, since an in-process part has no host-supplied installation path — and rejected if
configuration tries to escape that directory. The host build validates both executable paths before
packaging, and `Build.ps1` re-verifies the packaged container afterwards, so neither a clean direct
build nor a silently-stopped inclusion rule can emit a VSIX without its language server or its
pkgdef.

Every assembly the extension compiles against is supplied by Visual Studio at run time, so the
package references are compile-only and `IncludeCopyLocalReferencesInVSIXContainer` is false: nothing
from the editor, language-client, or `System.Text.Json` packages enters the container. The manifest
declares two assets — the MEF component and the pkgdef — and takes its identity version from
`$(VersionPrefix)` through a local `GetVsixVersion` target, so the central repository version reaches
the VSIX the same way it reaches every other Viu artifact. The package is Preview and targets
Community/Professional/Enterprise `[17.14,19.0)` on `amd64` and `arm64`, with the Visual Studio core
editor as its only prerequisite — Roslyn's classification types are resolved defensively at run time,
so a managed-language workload is an enhancement rather than a requirement.

The semantic IntelliSense increment ([V01.01.12.23] #259) shipped with **zero new packages**: the
publish already carried `Microsoft.CodeAnalysis` + `Microsoft.CodeAnalysis.CSharp` 5.3.0 with
unrestricted runtime assets, and the SemanticModel backend needs nothing else. The consumerless
`Microsoft.CodeAnalysis.CSharp.Workspaces` 5.0.0 and `Microsoft.CodeAnalysis.Workspaces.MSBuild`
5.0.0 central pins were removed from `build/Targets/Build.References.Packages.targets` rather than
version-aligned; the comment there records that any future Workspaces/Features re-entry (the
CompletionService increment) must land at the single 5.3.x line without `PrivateAssets=all` so the
assemblies flow into this publish, and that the consumerless `Microsoft.Build`/`Microsoft.Build.Locator`
pins are flagged for a separate cleanup item. The trimming and publish stance is unchanged:
`PublishTrimmed` with partial trim mode, single file, compression, and English-only satellite
resources. Measured by the packed-consumer round trip on the two-file scaffold
(`scripts/Test-LanguageServerSemanticRoundTrip.ps1`, 2026-08-02, Debug win-arm64): single-file
payload 17,880,455 bytes (within the pre-semantic ballpark — no new packages entered the
closure); first semantic completion (cold: materializing roughly 170 reference assemblies plus
the first bind) 787 ms; semantic completion after a subsequent edit 41 ms via the cached base
compilation; consumer `dotnet restore` 6.2 s with a warm package cache.

## References

- [Editor classification and colorization](https://learn.microsoft.com/visualstudio/extensibility/walkthrough-highlighting-text)
- [Language Server Protocol support in Visual Studio](https://learn.microsoft.com/visualstudio/extensibility/adding-an-lsp-extension)
- [Registry (pkgdef) file layout](https://learn.microsoft.com/visualstudio/extensibility/internals/registering-verbs-for-file-name-extensions)
- [Visual Studio language configuration](https://learn.microsoft.com/visualstudio/extensibility/language-configuration)
- [Language Server Protocol specification](https://microsoft.github.io/language-server-protocol/)
