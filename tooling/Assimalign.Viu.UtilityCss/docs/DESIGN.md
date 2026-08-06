# Viu Utilities engine design

This library is the pure engine boundary defined by
[`docs/UTILITY-CSS-DESIGN.md`](../../../docs/UTILITY-CSS-DESIGN.md). That document is authoritative;
this file records the local constraints that must remain true as the implementation grows.

## Boundaries

- Public models are immutable and structurally equatable so Roslyn incremental steps can cache
  unchanged candidates, themes, registries, and compilations.
- Parsing and compilation accept strings and immutable inputs. They perform no filesystem, process,
  network, environment, MSBuild, Visual Studio, or Roslyn operations.
- The candidate parser owns grammar only. Utility-family capabilities belong to the shared registry,
  and host-specific source slicing belongs outside the parser.
- Compiler and editor projections come from the same registry entry. An editor-only completion list
  or a compiler-only utility switch is a defect.
- Expected author errors are recoverable diagnostics. Cancellation is the only routine exception
  path.
- No implementation code is linked into `Assimalign.Viu.App`; consumers receive ordinary generated
  CSS as a static web asset.

`UtilityCandidateProjectIndex` is the source-union cache boundary. It consumes complete immutable
scanner snapshots, counts exact authored candidate spellings across normalized source identities,
and reports only first-reference additions and final-reference removals. Rename handling is an
explicit remove plus update by the host; occurrence-only edits and duplicate references do not
invalidate candidate resolution.

## Compatibility

The exact reference boundary is Tailwind CSS v4.3.3. Tests pin behavior to official documentation or
tagged source links, while expected outputs remain independently authored Viu vectors. Compatibility
updates require a new manifest version and tracked work item; the engine never follows a moving
latest release.

Code-first C# styling is intentionally excluded. Project authoring is CSS-first, and source
detection treats registered markup regions as plain text.

## CSS-first design system

Work item [V01.01.12.14] (#157) introduces the pure `@theme` ingestion boundary. The behavior is
pinned to Tailwind CSS v4.3.3's tagged
[`theme.ts`](https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/packages/tailwindcss/src/theme.ts)
and is implemented without loading Tailwind or performing file I/O.

The design-system layer supports top-level `@theme` blocks, the `inline`, `static`, `reference`, `default`,
and `prefix(...)` options, exact custom properties, individual resets, namespace wildcard resets,
and the full `--*: initial` reset. It overlays an immutable base theme and projects every documented
v4.3.3 namespace through one generic compiler/editor API: color, font family, font size, font weight,
letter spacing, line height, tab size, breakpoint, container, spacing, radius, the four shadow
families, blur, perspective, zoom, aspect ratio, transition timing, and animation. Compound metadata
properties remain available through the exact-property API without becoming false standalone
tokens. Declarations and diagnostics carry source identity and absolute spans for build and editor
hosts.

`UtilityTheme.Default` carries the complete tagged v4.3.3 token inventory: 288 named colors, system
font stacks, the numeric spacing base, viewport and container thresholds, typography metadata,
radii, shadows, filters, transition defaults, animations, and the spin, ping, pulse, and bounce
keyframes. Numeric spacing is calculated from `--spacing`, while an explicit `--spacing-*` property
overrides that calculation.

`UtilityCssLayerEmitter` is the whole-design-system emission boundary. It writes the canonical
`theme`, `base`, `components`, and `utilities` layer order, emits all non-reference theme properties,
rewrites owned variable references when a prefix is active, emits only built-in keyframes still
referenced by an animation token, and wraps the complete v4.3.3-compatible Viu Preflight rules in
the base layer. It also emits deterministic CSS Properties and Values registrations for every used
transform, outline, line-height, transition, touch-action, gradient, mask, filter, backdrop-filter,
border-style, and shadow composition group. Each internal property is explicitly non-inherited and
carries the v4.3.3 syntax and initial value when one is defined, which prevents a parent composed
value from leaking into a child.
Box, inset, text, and drop shadows keep size and color in separate custom properties so independent
size, color, and fractional-opacity utilities compose in the same element. Candidate resolution
rejects modifiers, internal negative spellings, noncanonical spacing values, and compound variants
that v4.3.3 cannot transform instead of emitting a plausible but unsupported declaration.

The implementation and test vectors are independently authored; the exact
compatibility values and behavior are traced under
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) with Tailwind Labs' MIT license.

The following theme-host behaviors remain outside this pure-library boundary:

- `@theme` discovery inside imported or nested CSS and `@import ... theme(...)` processing. The host
  must pass each resolved stylesheet to the pure parser.
- Parsing project-authored `@keyframes` nested inside `@theme`. The immutable model and emitter
  already support keyframes, and the four default keyframes are complete.
- Utility-usage pruning of normal variables. Authored normal and `static` declarations are both
  emitted by the complete design-system emitter; the `static` flag remains preserved for a later
  candidate-aware pruning host.
- Resolving CSS imports and deciding whether an application disables Preflight. Those are SDK
  project-graph choices rather than theme-model behavior.

## CSS-first directive foundation

Work item [V01.01.12.17] (#160) introduces the immutable syntax boundary for project stylesheets.
The behavior is pinned to the official Tailwind CSS v4.3.3
[`functions and directives`](https://tailwindcss.com/docs/functions-and-directives),
[`custom utilities`](https://tailwindcss.com/docs/adding-custom-styles#adding-custom-utilities), and
[`custom variants`](https://tailwindcss.com/docs/adding-custom-styles#adding-custom-variants)
contracts. The implementation is independently authored and does not load Tailwind.

`UtilityStylesheetParser` currently provides:

- balanced, quote-, escape-, and comment-aware discovery of root `@utility`,
  `@custom-variant`, and `@reference` directives and nested `@variant` and `@apply` directives;
- static, nested, negative, and functional utility-name syntax, including v4.3's requirement that
  a functional `@utility name-*` definition contain `--value()`;
- shorthand custom variants and block custom variants with an `@slot` placeholder;
- stacked, compound, and comma-separated `@variant` parameters as balanced authored syntax;
- quoted reference specifiers retained as composition metadata without resolving a path;
- source-located `--value()`, `--modifier()`, `--default()`, `--spacing()`, and `--alpha()` calls,
  including theme, bare, literal, arbitrary, modifier, fraction, and default argument forms;
- exact source identity and absolute spans on directives, functions, arguments, and diagnostics;
- recoverable malformed-input diagnostics and a deterministic projection of valid directives.

The standalone boundary is enforced during parsing. `@plugin` and legacy JavaScript `@config`
directives are errors, and direct imports or references to the `tailwindcss` package are errors.
Viu does not execute JavaScript, discover Node packages, or delegate any part of generation to
Tailwind.

`UtilityProjectStylesheetCompiler` is the executable transformation layer over that syntax model.
It creates one immutable project registry from local and referenced definitions, resolves
functional candidates left-to-right, omits only declarations whose value mode does not match,
expands built-in and custom `@apply` declarations, transforms built-in and custom `@variant`
branches, substitutes selector and block-form `@slot` variants, calculates `--spacing()` and
`--alpha()`, removes definition directives from authored CSS, and emits used custom utilities in a
separate utilities layer. Negative utilities remain explicit definitions, and a `ratio` value
uses the complete slash-separated fraction instead of treating the right-hand side as a modifier;
a candidate that resolves both a ratio and `--modifier()` is rejected, matching the v4 fraction
contract. Inline spacing is constant-folded when both operands are numeric, numeric alpha values
are converted to percentages, and import-level global important mode applies to generated custom
rules and their `@apply` expansions.

References use `UtilityStylesheetReferenceGraph`, an immutable set of edges whose content and stable
identities were already resolved by the host. Traversal is cycle-safe and deduplicated. Referenced
custom utilities and variants are visible to generation and composition, while referenced ordinary
CSS is never copied into the root output. The compiler performs no path normalization, alias
resolution, package lookup, or file access.

The remaining boundaries are explicit:

- `@theme`, `@source`, CSS `@import`, subpath aliases, and style-block scoping remain parser or SDK
  graph responsibilities. A host passes the resolved `UtilityTheme`, discovered candidates, and
  reference edges to this pure compiler. The browser-ready authored projection strips `@theme`,
  `@source`, and the virtual `@import "viu-utilities"` sentinel while retaining ordinary imports.
- `@apply` composes declaration-producing built-in and custom candidates, including variant
  candidates. Variant rules are rewritten as standards-based nested selectors or at-rules in the
  containing authored rule.
- Numeric, dimension, ratio, percentage, color, and URL arbitrary modes receive focused validation.
  The remaining documented CSS data-type families use balanced safe-value validation rather than
  embedding a complete CSS Values grammar.
- CSS-variable shorthand accepts both the explicit custom-property form
  `example-(integer:--my-value)` and the typed shorthand `example-(integer:my-value)`, which supplies
  the leading `--` in the generated `var(--my-value)`.
- Repeated custom definitions preserve cascade-equivalent declaration order, but the pure compiler
  does not yet collapse duplicate properties into Tailwind's optimized single declaration or
  reproduce Tailwind's internal property-order sort between custom and built-in rules. SDK output
  remains deterministic.
- Variant transformation reuses the executable built-in registry. A relative top-level arbitrary
  variant or unsupported compound at-rule/pseudo-element combination is a recoverable diagnostic.
- Generated variant output intentionally uses standards-based CSS nesting. Host optimization or
  flattening for an older browser target is an SDK emission concern.
- The deprecated v3-compatible `theme()` function remains unsupported; project styles use CSS theme
  variables instead.

`UtilityDirectiveEmitter` remains the canonical directive-only projection for cache keys. It does
not duplicate the project compiler or pretend that a projection is transformed application CSS.

## Line endings are part of the emitted contract

Every emitter normalizes its output to LF regardless of the source stylesheet's line endings —
`UtilityDirectiveEmitter` and `UtilityCssLayerEmitter` both collapse CRLF and lone CR to LF before
returning. That is deliberate: emitted CSS feeds cache keys and content fingerprints, so identical
input must produce byte-identical output on every platform, not merely equivalent text.

The tests pin that contract by asserting emitter output against C# raw string literals, which makes
each literal's line endings part of the expected value. So the test sources themselves must check
out LF — `.gitattributes` pins `tooling/Assimalign.Viu.UtilityCss/test/**/*.cs` to
`eol=lf` ([V01.01.12.24]); without it a Windows checkout under `core.autocrlf=true` turns the
expected values into CRLF and the assertions fail against correct LF output. Keep new expected-CSS
assertions in that directory, and do not relax them to be line-ending agnostic — the canonical LF
output *is* the specified behavior.
