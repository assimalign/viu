# Viu compiler diagnostics (VIU catalog)

The `Assimalign.Viu.Syntax.Generators` source generator surfaces every `.viu` compile problem as a
first-class Roslyn diagnostic with a stable `VIU####` ID, so developers see squiggles at the offending
template line/column in the IDE and precise `file:line:col` entries in `dotnet build` output — never opaque
errors inside generated code. Work item **[V01.01.05.08]** (issue #55).

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
  compiler's upstream-pinned `CompilerErrorCode`, the `.viu` container's Viu-defined
  `SingleFileComponentErrorCode`, both unbounded). A generator cannot enumerate those into one descriptor each
  without mirroring them, so diagnostics are enveloped by their **origin** (`.viu` block container,
  dispatched `@template` parse, the dispatched `@style` CSS parse, or the Roslyn parse of the `@script` C#)
  and severity, and the per-language `RawCode` rides on the message text (e.g. `... (template compiler code
  25)`, `... (CSS code 2006)`) so the exact catalog code stays visible.
- **Base `Severity` → Roslyn severity.** Vue's error-vs-warning split is pinned on the base `Diagnostic` at
  parse time, not re-derived here. `Error`/`Warning`/`Information` map to the same-tier descriptor; `Hidden`
  collapses into the informational descriptor (surfaced, never dropped).
- **Template `SourceLocation` → Roslyn `Location`.** Block-relative positions are composed with the block's
  content-start position (`ComposeToFilePosition`, the same arithmetic the `@script`/`@template` paths and the
  render `#line` map share) and rebuilt as a `Location` on the `.viu` `AdditionalText`, so the squiggle lands
  on the exact template span.

## Render-body source mapping (`#line`)

Expression-level errors from [V01.01.05.04] are a distinct case: a template expression that is legal to *emit*
but references an unknown member (under permissive binding metadata) compiles to a recoverable `_ctx.Name`
fallback and only fails when the **generated render body** is compiled — as an ordinary C# diagnostic
(`CS1061`, …), not a `VIU####` one. The generator wraps every expression-bearing render line in a C#
`#line (startLine,startColumn)-(endLine,endColumn) charOffset "file.viu"` span directive
(`RenderBodySourceMapper`), aligning the emitted expression (past its inserted `_ctx.` prefix) to its template
span, so that C# error's `GetMappedLineSpan()` resolves to the `.viu` template line and column. It is the
render-body analogue of the `@script` merge's `#line` map. Non-expression scaffolding, and any second
expression sharing one physical line, fall back to the generated file (`#line default`) — the standard
generated-code practice, since a scaffold error is a generator concern, not a template one.

## Configuration

Every descriptor is `isEnabledByDefault: true` and configurable through standard analyzer conventions —
`.editorconfig` `dotnet_diagnostic.VIU####.severity = error|warning|suggestion|silent|none`. Warning-tier
diagnostics participate in `TreatWarningsAsErrors`.

## Descriptor catalog

All descriptors carry category `Assimalign.Viu.Syntax.Generators`. The three-way origin split gives each
severity tier its own stable ID.

| Origin | Error | Warning | Information |
| --- | --- | --- | --- |
| `.viu` block container | `VIU1001` | `VIU1002` | `VIU1003` |
| dispatched `@template` parse | `VIU1101` | `VIU1102` | `VIU1103` |
| `@script` C# parse | `VIU1201` | `VIU1202` | `VIU1203` |
| `@script` generated-member contract | `VIU1204`, `VIU1205` | — | — |
| dispatched `@style` CSS parse | `VIU1301` | `VIU1302` | `VIU1303` |

### VIU1001

Single-file component parse error — a recoverable error reported by the `.viu` block-container parser
(a `SingleFileComponentErrorCode`, e.g. stray top-level content). The scaffold is still emitted.

### VIU1002

Single-file component parse warning — a warning reported by the `.viu` block-container parser.

### VIU1003

Single-file component parse information — an informational message (or a `Hidden` diagnostic) from the `.viu`
block-container parser.

### VIU1101

Single-file component template parse error — a recoverable error from the dispatched `@template` parse
(a `CompilerErrorCode` numerically pinned to `@vue/compiler-core` `ErrorCodes` — parse errors such as an
unterminated interpolation, and transform errors such as `v-if`/`v-for`/`v-slot`/`v-on`/`v-bind` misuse).

### VIU1102

Single-file component template parse warning — a warning from the dispatched `@template` parse.

### VIU1103

Single-file component template parse information — an informational message (or `Hidden`) from the dispatched
`@template` parse.

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

### VIU1301

Single-file component style parse error — a recoverable error from the dispatched `@style` CSS parse
([V01.01.06.04]), a Viu-defined `CssErrorCode` following CSS Syntax Module Level 3 error recovery
(e.g. an unterminated block, a stray `}`, or a declaration missing its `:`). The CSS-Modules and `v-bind()`
rewrites ([V01.01.06.06]) surface here too through the same style-origin envelope — a malformed
`v-bind()` (an unterminated `v-bind(` or an empty `v-bind()`) reports `CssErrorCode` 2008/2009 on the
offending declaration. The CSS parser and rewrites never throw; the scaffold is still emitted, and the
`CssErrorCode` (2000-based) rides on the message (e.g. `... (CSS code 2006)`).

### VIU1302

Single-file component style parse warning — a warning from the dispatched `@style` CSS parse.

### VIU1303

Single-file component style parse information — an informational message (or `Hidden`) from the dispatched
`@style` CSS parse.
