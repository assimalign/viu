# Viu compiler diagnostics (VIU catalog)

The `Assimalign.Viu.Generators.Syntax` source generator surfaces every `.viu` or compatible `.vue`
compile problem as a first-class Roslyn diagnostic with a stable `VIU####` ID, so developers see
squiggles at the offending template line/column in the IDE and precise `file:line:col` entries in
`dotnet build` output — never opaque errors inside generated code. Work item **[V01.01.05.08]**
(issue #55), extended for tag-based sources by **[V01.01.06.09]**.

This catalog is the help-link target for every descriptor (`helpLinkUri`) and the authoritative record of the
mapping. The descriptor IDs are a **public contract once shipped**: never renumber them; deprecate with a
documented replacement instead. Release tracking lives in `AnalyzerReleases.Shipped.md` /
`AnalyzerReleases.Unshipped.md` (enforced by RS2008).

## How the mapping works

The base `Assimalign.Viu.Syntax` cluster defines one uniform, value-equatable `Diagnostic` shape every parser
result carries: a message, a template `SourceLocation`, a `Severity` (`DiagnosticSeverity`, with Roslyn-parity
members `Hidden`/`Information`/`Warning`/`Error`), and a `RawCode` integer projection of the per-language code
enum. The generator is a **mapping over that shape** (`SingleFileComponentDiagnostics`), not a re-derivation:

- **`RawCode` → descriptor ID.** The base deliberately keeps per-language code catalogs (the template
  compiler's `CompilerErrorCode`, whose numbering is frozen for `.vue` compatibility, the `.viu` container's Viu-defined
  `SingleFileComponentErrorCode`, both unbounded). A generator cannot enumerate those into one descriptor each
  without mirroring them, so diagnostics are enveloped by their **origin** (single-file-component
  container, dispatched template parse, dispatched style CSS parse, or Roslyn parse of the C# script)
  and severity, and the per-language `RawCode` rides on the message text (e.g. `... (template compiler code
  25)`, `... (CSS code 2006)`) so the exact catalog code stays visible.
- **Base `Severity` → Roslyn severity.** The error-vs-warning split is decided on the base `Diagnostic` at
  parse time, not re-derived here. `Error`/`Warning`/`Information` map to the same-tier descriptor; `Hidden`
  collapses into the informational descriptor (surfaced, never dropped).
- **Template `SourceLocation` → Roslyn `Location`.** Block-relative positions are composed with the block's
  content-start position (`ComposeToFilePosition`, the same arithmetic the `@script`/template paths and the
  render `#line` map share) and rebuilt as a `Location` on the originating `AdditionalText`, so the
  squiggle lands on the exact template span.

## Render-body source mapping (`#line`)

Expression-level errors from [V01.01.05.04] are a distinct case: a template expression that is legal to *emit*
but references an unknown member (under permissive binding metadata) compiles to a recoverable `_ctx.Name`
fallback and only fails when the **generated render body** is compiled — as an ordinary C# diagnostic
(`CS1061`, …), not a `VIU####` one. The generator wraps every expression-bearing render line in a C#
`#line (startLine,startColumn)-(endLine,endColumn) charOffset "file.viu"` span directive
(`RenderBodySourceMapper`), aligning the emitted expression (past its inserted `_ctx.` prefix) to its template
span, so that C# error's `GetMappedLineSpan()` resolves to the originating template line and column. It
is the render-body analogue of the C# script merge's `#line` map. Non-expression scaffolding, and any second
expression sharing one physical line, fall back to the generated file (`#line default`) — the standard
generated-code practice, since a scaffold error is a generator concern, not a template one.

## Configuration

Every descriptor is `isEnabledByDefault: true` and configurable through standard analyzer conventions —
`.editorconfig` `dotnet_diagnostic.VIU####.severity = error|warning|suggestion|silent|none`. Warning-tier
diagnostics participate in `TreatWarningsAsErrors`.

## Descriptor catalog

All descriptors carry category `Assimalign.Viu.Generators.Syntax`. The three-way origin split gives each
severity tier its own stable ID.

| Origin | Error | Warning | Information |
| --- | --- | --- | --- |
| `.viu` or `.vue` block container | `VIU1001` | `VIU1002` | `VIU1003` |
| conflicting `.viu` and `.vue` sources | `VIU1004` | — | — |
| dispatched template parse | `VIU1101` | `VIU1102` | `VIU1103` |
| `@script` C# parse | `VIU1201` | `VIU1202` | `VIU1203` |
| script generated-member and compatibility contract | `VIU1204`–`VIU1206` | — | — |
| attribute-declared component surface | `VIU1207`–`VIU1209` | — | — |
| dispatched style CSS parse | `VIU1301` | `VIU1302` | `VIU1303` |
| component-usage validation | `VIU1402`–`VIU1403` | `VIU1401` | — |

### VIU1001

Single-file component parse error — a recoverable error reported by the `.viu` block-container parser
(a `SingleFileComponentErrorCode`, e.g. stray top-level content). The scaffold is still emitted.

Since the [V01.01.06.10] hybrid-container pivot this includes `ScriptTagBlockNotSupported`
(`SingleFileComponentErrorCode` 1017): a top-level `<script>` tag in a `.viu` file is rejected — its
content is never compiled or executed — because a component's C# lives in `@script { }` only. The
parser message rides verbatim: *"A top-level '&lt;script&gt;' tag is not supported in a .viu file and
its content is never compiled or executed. Declare the component's C# with '@script { }'."*

### VIU1002

Single-file component parse warning — a warning reported by the `.viu` block-container parser.

The legacy-container migration diagnostics of the [V01.01.06.10] hybrid-container pivot surface here
(the parser messages ride verbatim):

- `LegacyTemplateBlockSyntax` (`SingleFileComponentErrorCode` 1015) — a legacy `@template { }`
  container parsed; rewrite it as `<template>…</template>`.
- `LegacyStyleBlockSyntax` (`SingleFileComponentErrorCode` 1016) — a legacy `@style … { }` container
  parsed; rewrite it as `<style …>…</style>` (block options become tag attributes).

Both are Warning severity: the legacy blocks still slice and compile during the transition window, so
a legacy component builds with warnings instead of breaking.

### VIU1003

Single-file component parse information — an informational message (or a `Hidden` diagnostic) from the `.viu`
block-container parser.

### VIU1004

Conflicting component formats — a compatibility `.vue` file has a same-directory, same-base canonical
`.viu` file. The `.viu` source wins deterministically; the `.vue` file reports this error and emits no
second partial class.

### VIU1101

Single-file component template parse error — a recoverable error from the dispatched template parse
(a `CompilerErrorCode` whose numbering is aligned with the `.vue` container format's published
`@vue/compiler-core` `ErrorCodes`, so a component ported into Viu reports the code its author expects —
parse errors such as an
unterminated interpolation, and transform errors such as `v-if`/`v-for`/`v-slot`/`v-on`/`v-bind` misuse).

### VIU1102

Single-file component template parse warning — a warning from the dispatched template parse.

### VIU1103

Single-file component template parse information — an informational message (or `Hidden`) from the dispatched
template parse.

### VIU1201

Single-file component script parse error — a recoverable C# parse error in the `@script` block, mapped onto the
`.viu` file (the Roslyn code, e.g. `CS1525`, rides on the message).

### VIU1202

Single-file component script parse warning — a C# parse warning in the `@script` block.

### VIU1203

Single-file component script parse information — an informational (or `Hidden`) C# parse diagnostic in the
`@script` block.

### VIU1204

Single-file component generated-member conflict — the `@script` block declares `Context`, which is supplied
by the generated `IComponentTemplate` bridge, or declares `OnSetup` in any shape other than the supported
`partial void OnSetup()` implementation. The diagnostic is mapped to the conflicting identifier.

### VIU1205

Unobservable asynchronous callback — an `async void` method in the `@script` block cannot return its task to
Core's lifecycle or event dispatcher. Return `Task` so Core can observe failures and apply component-lifetime
cancellation and error-routing policy.

### VIU1206

Unsupported compatibility script language — every tag-based `<script>` block must explicitly declare
`lang="csharp"`. Missing or different language attributes are rejected and their content is never
merged or executed.

### VIU1207

Conflicting component parameter or event declaration — the component declares the same kind of surface
twice: once with `[Parameter]`/`[Event]` attributes and once with its own `Parameters`/`Events` member.
The generated declaration is an explicit interface implementation, so it would silently win over the
authored collection; the mix is rejected instead of resolved by an invisible precedence rule
(`[CMP-31]`). The rule is **per kind** — a component may keep an imperative `Parameters` collection while
declaring its events by attribute.

### VIU1208

Duplicate component parameter or event declaration — two attributed members resolve to the same canonical
name, either because their derived names collide or because an explicit `Name` repeats one. Core's
parameter/event alias table rejects a duplicate at mount, so the duplicate is a build error.

### VIU1209

Unsupported component parameter or event declaration — the attribute is on a member shape the generated
scaffold cannot implement, or carries an argument it cannot read at build time:

- `[Parameter]` on a static property, or on a property with no `set` accessor (the scaffold assigns the
  supplied argument to it before every render).
- `[Event]` on anything other than a non-generic, instance `partial void` method with no body, or on a
  method with a by-reference parameter.
- a `Name` argument that is not a non-empty constant string literal, or an `IsRequired` argument that is
  not the literal `true`/`false`. The declaration is emitted at build time, so it cannot depend on a value
  only a semantic model or the runtime could resolve.

### VIU1301

Single-file component style parse error — a recoverable error from the dispatched style CSS parse
([V01.01.06.04]), a Viu-defined `CssErrorCode` following CSS Syntax Module Level 3 error recovery
(e.g. an unterminated block, a stray `}`, or a declaration missing its `:`). The CSS-Modules and `v-bind()`
rewrites ([V01.01.06.06]) surface here too through the same style-origin envelope — a malformed
`v-bind()` (an unterminated `v-bind(` or an empty `v-bind()`) reports `CssErrorCode` 2008/2009 on the
offending declaration. The CSS parser and rewrites never throw; the scaffold is still emitted, and the
`CssErrorCode` (2000-based) rides on the message (e.g. `... (CSS code 2006)`).

### VIU1302

Single-file component style parse warning — a warning from the dispatched style CSS parse.

### VIU1303

Single-file component style parse information — an informational message (or `Hidden`) from the dispatched
style CSS parse.

### VIU1401

Component declares no such parameter — a component usage supplies an attribute (or a `:`-bound
argument) that matches none of the component's declared parameters (`[SFC-USE-2]`).

**Warning, deliberately not an error.** Fallthrough is a specified feature (`[CMP-17]`): an
undeclared attribute on a component is legal and lands on the component's rendered root. The
diagnostic therefore reports a *likely* mistake, never an illegal program.

The check stays silent for listener spellings (`onX`, `@x`), directives, and plausible fallthrough
attributes — a known HTML or SVG attribute, a hyphenated or namespaced name (`data-*`, `aria-*`,
`xml:*`, any vendor prefix), and the render pipeline's own (`key`, `ref`, `class`, `style`, `id`,
`is`, `role`, …).

### VIU1402

Required component parameter is not supplied — a usage omits a parameter the component declares
required, either through `[Parameter(IsRequired = true)]` or the C# `required` modifier
(`[SFC-USE-3]`). Unlike an undeclared attribute there is no legitimate reading of the omission, so
this is an error; the runtime's mount-time warning (`[CMP-12]`) remains for the usages the compiler
cannot see.

### VIU1403

Component argument type is incompatible — the supplied value's type cannot be the declared
parameter's type (`[SFC-USE-4]`). Only the two decidable directions are reported:

- a **plain attribute** (`rating="3"`), whose value is always a string, supplied to a parameter of a
  value type — bind it instead (`:rating="3"`); and
- a **non-string literal binding** (`:title="3"`) supplied to a `string` parameter.

Both are errors because neither can work at run time: `IComponentArguments.Get<T>` yields the
parameter type's default when the supplied value is not of the declared type (`[CMP-29]`).

## What component-usage validation deliberately does not see

`VIU1401`–`VIU1403` require a component's parameter surface to be **statically readable**, which
means attribute-declared (`[CMP-26]`). A component that builds its `Parameters` collection
imperatively carries nothing a compiler can read — the collection is arbitrary C# — so its usages are
never validated. That is not a gap to be closed later; it is the reason the attribute form exists.

Validation is likewise skipped, in full, for a tag that resolves to more than one declaration, a
usage carrying an argument-less `v-bind="…"` spread or a dynamic `:[name]` argument, a bound
expression that is not a C# literal, and a hyphenated attribute name. A false positive is worse than
a false negative here, so every undecidable input produces silence (`[SFC-USE-5]`).
