# Vuecs compiler diagnostics (VUECS catalog)

The `Assimalign.Vue.Syntax.Generators` source generator surfaces every `.viu` compile problem as a
first-class Roslyn diagnostic with a stable `VUECS####` ID, so developers see squiggles at the offending
template line/column in the IDE and precise `file:line:col` entries in `dotnet build` output — never opaque
errors inside generated code. Work item **[V01.01.05.08]** (issue #55).

This catalog is the help-link target for every descriptor (`helpLinkUri`) and the authoritative record of the
mapping. The descriptor IDs are a **public contract once shipped**: never renumber them; deprecate with a
documented replacement instead. Release tracking lives in `AnalyzerReleases.Shipped.md` /
`AnalyzerReleases.Unshipped.md` (enforced by RS2008).

## How the mapping works

The base `Assimalign.Vue.Syntax` cluster defines one uniform, value-equatable `Diagnostic` shape every parser
result carries: a message, a template `SourceLocation`, a `Severity` (`DiagnosticSeverity`, with Roslyn-parity
members `Hidden`/`Information`/`Warning`/`Error`), and a `RawCode` integer projection of the per-language code
enum. The generator is a **mapping over that shape** (`SingleFileComponentDiagnostics`), not a re-derivation:

- **`RawCode` → descriptor ID.** The base deliberately keeps per-language code catalogs (the template
  compiler's upstream-pinned `CompilerErrorCode`, the `.viu` container's Vuecs-defined
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
(`CS1061`, …), not a `VUECS####` one. The generator wraps every expression-bearing render line in a C#
`#line (startLine,startColumn)-(endLine,endColumn) charOffset "file.viu"` span directive
(`RenderBodySourceMapper`), aligning the emitted expression (past its inserted `_ctx.` prefix) to its template
span, so that C# error's `GetMappedLineSpan()` resolves to the `.viu` template line and column. It is the
render-body analogue of the `@script` merge's `#line` map. Non-expression scaffolding, and any second
expression sharing one physical line, fall back to the generated file (`#line default`) — the standard
generated-code practice, since a scaffold error is a generator concern, not a template one.

## Configuration

Every descriptor is `isEnabledByDefault: true` and configurable through standard analyzer conventions —
`.editorconfig` `dotnet_diagnostic.VUECS####.severity = error|warning|suggestion|silent|none`. Warning-tier
diagnostics participate in `TreatWarningsAsErrors`.

## Descriptor catalog

All descriptors carry category `Assimalign.Vue.Syntax.Generators`. The three-way origin split gives each
severity tier its own stable ID.

| Origin | Error | Warning | Information |
| --- | --- | --- | --- |
| `.viu` block container | `VUECS1001` | `VUECS1002` | `VUECS1003` |
| dispatched `@template` parse | `VUECS1101` | `VUECS1102` | `VUECS1103` |
| `@script` C# parse | `VUECS1201` | `VUECS1202` | `VUECS1203` |
| dispatched `@style` CSS parse | `VUECS1301` | `VUECS1302` | `VUECS1303` |

### VUECS1001

Single-file component parse error — a recoverable error reported by the `.viu` block-container parser
(a `SingleFileComponentErrorCode`, e.g. stray top-level content). The scaffold is still emitted.

### VUECS1002

Single-file component parse warning — a warning reported by the `.viu` block-container parser.

### VUECS1003

Single-file component parse information — an informational message (or a `Hidden` diagnostic) from the `.viu`
block-container parser.

### VUECS1101

Single-file component template parse error — a recoverable error from the dispatched `@template` parse
(a `CompilerErrorCode` numerically pinned to `@vue/compiler-core` `ErrorCodes` — parse errors such as an
unterminated interpolation, and transform errors such as `v-if`/`v-for`/`v-slot`/`v-on`/`v-bind` misuse).

### VUECS1102

Single-file component template parse warning — a warning from the dispatched `@template` parse.

### VUECS1103

Single-file component template parse information — an informational message (or `Hidden`) from the dispatched
`@template` parse.

### VUECS1201

Single-file component script parse error — a recoverable C# parse error in the `@script` block, mapped onto the
`.viu` file (the Roslyn code, e.g. `CS1525`, rides on the message).

### VUECS1202

Single-file component script parse warning — a C# parse warning in the `@script` block.

### VUECS1203

Single-file component script parse information — an informational (or `Hidden`) C# parse diagnostic in the
`@script` block.

### VUECS1301

Single-file component style parse error — a recoverable error from the dispatched `@style` CSS parse
([V01.01.06.04]), a Vuecs-defined `CssErrorCode` following CSS Syntax Module Level 3 error recovery
(e.g. an unterminated block, a stray `}`, or a declaration missing its `:`). The CSS parser never throws;
the scaffold is still emitted, and the `CssErrorCode` (2000-based) rides on the message (e.g. `... (CSS code
2006)`).

### VUECS1302

Single-file component style parse warning — a warning from the dispatched `@style` CSS parse.

### VUECS1303

Single-file component style parse information — an informational message (or `Hidden`) from the dispatched
`@style` CSS parse.
