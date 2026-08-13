> **Status — parked add-on; non-normative (2026-08-13).** Viu Utilities is no longer part of Viu.
> The SDK, hot-reload, editor, packaging, release-train, and CI integrations described below were
> removed in commits `f952b19`, `7330f74`, and `b3f71f0`. The non-packable engine is parked at
> `tooling/Assimalign.Viu.UtilityCss` pending a fresh add-on design under `libraries/Utilities/`,
> with its own MSBuild integration and potentially its own Visual Studio extension. This document is
> retained unchanged as design history for that redesign; its Tailwind CSS v4.3.3 target belongs to
> the parked add-on, not to Viu's core compatibility contract.

# Viu Utilities — standalone Tailwind CSS v4.3.3 compatibility design

This document is the authoritative design for **Viu Utilities**, Viu's built-in utility-first CSS
feature. It supersedes the Tailwind-v3-shaped parts of the original
**[V01.01.12.10]** scoping work ([#129](https://github.com/assimalign/viu/issues/129)): JSON theme
configuration, leading-`!` as canonical syntax, a reduced first catalog, and deferral of arbitrary,
group, or peer variants are no longer the contract.

Viu Utilities is an independently implemented build-time feature. It targets the documented behavior
of **Tailwind CSS v4.3.3**, but it does not install, load, invoke, bundle, or require Tailwind CSS.
There is no npm package, Node process, Tailwind executable, Oxide scanner, PostCSS plugin, Vite
plugin, Tailwind editor extension, or runtime CSS generator anywhere in the product or its normal
build and test path.

The compatibility pin is exact:

- reference release: [Tailwind CSS v4.3.3](https://github.com/tailwindlabs/tailwindcss/releases/tag/v4.3.3);
- tagged source used only as a behavioral reference:
  [tailwindlabs/tailwindcss at v4.3.3](https://github.com/tailwindlabs/tailwindcss/tree/v4.3.3);
- compatibility changes require a new manifest version, new conformance vectors, and an explicit Viu
  work item. The implementation must never silently follow a moving `latest`.

“Compatible” means that a promised candidate, theme token, variant, directive, or function has the
same documented author-visible meaning at the v4.3.3 boundary. It does not require Tailwind's
internal architecture, JavaScript APIs, plugin ABI, emitted whitespace, or implementation language.
The machine-readable compatibility manifest in
**[V01.01.12.16.01]** ([#249](https://github.com/assimalign/viu/issues/249)) is the exhaustive
definition of the promised surface.

## Authoritative references

The implementation and conformance vectors must cite the narrowest applicable official reference:

- [Tailwind CSS documentation](https://tailwindcss.com/docs)
- [Styling with utility classes](https://tailwindcss.com/docs/styling-with-utility-classes)
- [Detecting classes in source files](https://tailwindcss.com/docs/detecting-classes-in-source-files)
- [Theme variables](https://tailwindcss.com/docs/theme)
- [Hover, focus, and other states](https://tailwindcss.com/docs/hover-focus-and-other-states)
- [Adding custom styles](https://tailwindcss.com/docs/adding-custom-styles)
- [Functions and directives](https://tailwindcss.com/docs/functions-and-directives)
- [Preflight](https://tailwindcss.com/docs/preflight)
- [Tailwind CSS v4.3 release notes](https://tailwindcss.com/blog/tailwindcss-v4-3)
- [CSS Syntax Module Level 3](https://www.w3.org/TR/css-syntax-3/)
- [CSS Cascading and Inheritance Level 5](https://www.w3.org/TR/css-cascade-5/)
- [Roslyn source generators](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [`dotnet watch`](https://learn.microsoft.com/dotnet/core/tools/dotnet-watch)
- [Vue single-file-component specification](https://vuejs.org/api/sfc-spec.html), for the explicit
  tag-based `.vue` compatibility dependency in
  [§8](#8-single-file-components-and-the-vue-compatibility-dependency)

Repository architecture reused by this feature:

- [`Assimalign.Viu.Syntax.Css` design](../libraries/Syntax/Assimalign.Viu.Syntax.Css/docs/DESIGN.md)
- [`Assimalign.Viu.Compiler.Css` design](../tooling/Compiler/Assimalign.Viu.Compiler.Css/docs/DESIGN.md)
- [Viu `.viu` format](../libraries/Syntax/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md)
- [Visual Studio language tooling design](../extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md)

## 1. Product contract

Viu Utilities has four inseparable product surfaces:

1. **The compiler** detects complete utility candidates, resolves them through the project's
   CSS-first theme and registry, and emits a deterministic standalone stylesheet.
2. **The Viu SDK** discovers inputs, supplies them to the pure compiler, writes and fingerprints the
   stylesheet as a static web asset, and injects its link into the host page.
3. **The development loop** regenerates and swaps only that stylesheet during `dotnet watch`, keeping
   the mounted application and component state alive.
4. **The Visual Studio extension pack** supplies completion and hover from the exact parser, theme,
   and registry used by the compiler.

All four surfaces ship from one compatibility implementation. “Compiler support” without matching
IntelliSense, or editor suggestions that do not generate CSS, is incomplete.

The feature is standalone in two senses:

- its engine is a dedicated `Assimalign.Viu.UtilityCss` assembly rather than utility logic
  hidden inside the single-file-component generator or Visual Studio extension; and
- its output is a separate utility stylesheet. Disabling component `@style` bundling does not disable
  utilities, and disabling utilities does not change scoped CSS or CSS Modules.

The runtime contract is deliberately small: a browser receives ordinary CSS through a `<link>`.
No utility parser, registry, theme compiler, file watcher, or hot-reload transport is linked into an
AOT release WebAssembly payload.

## 2. Architecture and dependency direction

### 2.1 The shared engine assembly

Create the inverted-layout library:

```text
tooling/Assimalign.Viu.UtilityCss/
  src/Assimalign.Viu.UtilityCss.csproj
  test/Assimalign.Viu.UtilityCss.Tests.csproj
  conformance/
```

The shipping project targets `$(TargetFrameworkForAnalyzers)` because the same binary must load in a
Roslyn analyzer host. It is build-time tooling, not a member of `Assimalign.Viu.App`, and is not a
runtime framework reference.

`Assimalign.Viu.UtilityCss` owns:

- immutable candidate, variant, theme, source, diagnostic, and generated-rule models;
- the balanced candidate parser and plain-text detector;
- source-directive parsing and source-graph evaluation over content supplied by a host;
- CSS-first theme, custom utility, and custom variant parsing;
- the complete built-in utility and variant registry;
- resolution, ordering, Preflight, layer assembly, and deterministic CSS generation;
- editor metadata derived from the same registry entries;
- the v4.3.3 compatibility version and manifest reader used by conformance tests.

It may reference `Assimalign.Viu.Syntax` and `Assimalign.Viu.Syntax.Css`. The CSS library stays
generic and never learns about utility candidates. The utility assembly does not reference Viu
runtime libraries, the source-generator host, MSBuild, Visual Studio, or Tailwind.

### 2.2 One registry, not synchronized copies

`UtilityCssRegistry` is the single immutable source of truth. A registry entry carries everything
needed by both compilation and editing:

- candidate family and static or functional form;
- accepted named, bare, arbitrary, and CSS-variable values;
- supported CSS data types and ambiguity hints;
- negative, fraction, modifier, prefix, and important capabilities;
- theme namespaces and default lookup behavior;
- declaration construction and stable property/rule order;
- compatible variants and compounds;
- completion label, filtering keys, detail text, and hover projection;
- conformance-manifest identity.

The compiler executes the declaration construction metadata. The language service projects
completion and hover from the same entry. Neither host owns a second list, switch statement, generated
JSON catalog, or hand-maintained editor vocabulary.

Project-defined `@utility` and `@custom-variant` declarations extend an immutable project registry.
That project registry is the input to both generation and editor requests. A completion item is only
valid if the same registry can resolve it.

### 2.3 Hosts

```text
                         MSBuild-discovered inputs
        .viu / host markup / registered .vue regions / utility entry CSS
                                      |
                         content + normalized source identity
                                      v
                  Assimalign.Viu.UtilityCss
        detector -> parser -> theme/directives -> shared registry -> compiler
                |                         |                    |
                |                         |                    +-> deterministic CSS
                |                         +-> diagnostics/editor metadata
                +-> per-source candidates and spans
                     /                                  \
                    /                                    \
 Assimalign.Viu.Generators.Syntax             Assimalign.Viu.LanguageService
 diagnostics + incremental model              completion + hover + diagnostics
                    \                                    /
                     \                                  /
             Assimalign.Viu.Sdk.Browser.Tasks + Browser targets
                    filesystem I/O + StaticWebAsset registration
                                      |
                         <PackageId>.utilities.css
```

The analyzer performs no filesystem or network I/O. Inputs arrive as `AdditionalText` plus analyzer
configuration. The MSBuild host owns discovery and I/O, passes already-read content to the same pure
engine, and writes output outside the analyzer sandbox. The language server owns project/document
snapshots and also calls the pure engine.

### 2.4 Landed foundations retained

The following architecture is already implemented and remains valid:

- **[V01.01.12.11]** ([#154](https://github.com/assimalign/viu/issues/154)) added generic CSS
  construction and deterministic emission to `Assimalign.Viu.Syntax.Css`.
- **[V01.01.12.12]** ([#155](https://github.com/assimalign/viu/issues/155)) established the
  two-host pattern: a pure analyzer-compatible CSS compilation core and an MSBuild task that performs
  physical output.
- **[V01.01.12.12.01]** ([#167](https://github.com/assimalign/viu/issues/167)) made host-page
  link injection idempotent and compression-safe by rewriting and re-registering the static asset
  before compression.
- **[V01.01.12.12.03]** ([#169](https://github.com/assimalign/viu/issues/169)) established
  content-based fingerprinting through `DefineStaticWebAssets`.

The existing `ViuBundleCss` task and `Assimalign.Viu.Compiler.Css` compile `.viu` component styles.
They are not retroactively redefined as the utility engine. Viu Utilities adds a separate pure core,
a separate task entry point in the existing SDK task assembly, and a separate asset while reusing
the landed discovery, no-op write, fingerprint, link-injection, and compression ordering patterns.

## 3. CSS-first authoring contract

There is no `utility.theme.json`, `tailwind.config.js`, or C# theme object. The project utility entry
is CSS, supplied by MSBuild as `@(ViuUtilityCss)` and as an analyzer `AdditionalFile`.

The Viu SDK recognizes a virtual built-in import:

```css
@import "viu-utilities";
```

`"viu-utilities"` is a compiler sentinel, not a package name or filesystem dependency. It expands
Viu-owned compatible theme, Preflight, and utility layers. Viu does not recognize
`@import "tailwindcss"` as an alias because doing so would imply a dependency and blur the product
boundary.

Import modifiers follow the v4 contract where applicable:

```css
@import "viu-utilities" source("../Components") prefix(vu);
```

The engine supports `source(none)`, `important`, and the theme-mode options at their documented CSS
positions. Prefixes behave as the first variant in class syntax, while authors continue to declare
unprefixed theme names. With no explicit theme customization, the independently authored Viu default
theme and complete built-in registry apply.

### 3.1 Theme variables

Top-level `@theme` is the configuration model:

```css
@import "viu-utilities";

@theme {
  --color-brand-500: oklch(0.63 0.19 260);
  --font-display: "Example Sans", sans-serif;
  --breakpoint-3xl: 120rem;
}
```

The parser supports v4.3.3 `@theme` semantics including:

- normal, `inline`, and `static` modes;
- individual override and extension;
- namespace wildcard reset and `--*: initial` full reset;
- references between custom properties;
- prefixed emitted custom properties when `prefix(...)` is configured;
- theme-owned `@keyframes`;
- recoverable, source-located diagnostics.

The namespace registry covers every v4.3.3 namespace, including colors, font families, text sizes,
font weights, tracking, leading, tab size, breakpoints, containers, spacing, radius, box/inset/text
and drop shadows, blur, perspective, zoom, aspect ratio, easing, and animation. The manifest, rather
than this prose list, is the completeness authority.

### 3.2 Sources and safelisting

Source customization is CSS-first:

- import `source(<path>)` establishes a base;
- `source(none)` disables automatic discovery;
- `@source "<path>"` includes a root;
- `@source not "<path>"` excludes a root;
- `@source inline("<expression>")` adds literal candidates;
- `@source not inline("<expression>")` excludes literal candidates;
- inline expressions support the v4 brace-expansion and numeric-range forms.

This is also the only safelist mechanism. There is no JSON `safelist` property.

### 3.3 Directives and functions

The complete CSS-first customization surface is in
**[V01.01.12.17]** ([#160](https://github.com/assimalign/viu/issues/160)):

| Surface | Required behavior |
| --- | --- |
| `@utility` | Static, nested/complex, and functional utilities; theme, bare, literal, arbitrary, negative, modifier, fraction, and default-value forms as declared |
| `@variant` | Apply one, stacked, compound, or comma-separated variants inside authored CSS |
| `@custom-variant` | Shorthand selector form and nested selector/at-rule form with `@slot` |
| `@apply` | Expand resolvable utilities in authored CSS with source-located diagnostics |
| `@reference` | Make theme, custom utilities, and variants available to separately compiled style blocks without duplicating output |
| `--value()` | Resolve declared theme, bare, literal, and arbitrary value modes |
| `--modifier()` | Resolve a slash modifier using the declared modes |
| `--default()` | Supply the value or modifier used by a functional utility with an omitted part |
| `--alpha()` | Produce the compatible color-opacity expression |
| `--spacing()` | Resolve calculations against the configured spacing token |

`@reference` composes separately processed `.viu` and, once
[#250](https://github.com/assimalign/viu/issues/250) lands, `.vue` style blocks with the project
entry. Imports and references are resolved by
MSBuild and supplied to the analyzer; analyzer code never reads them directly.

## 4. Candidate language

The candidate parser is a balanced, escape-aware parser. It must not split naively on every colon,
slash, dash, bracket, or parenthesis.

Representative v4 forms include:

```text
bg-blue-500
hover:bg-blue-500
md:focus:bg-blue-500/50
vu:hover:bg-blue-500
-mt-4
w-1/2
w-[calc(100%-2rem)]
fill-(--brand-color)
text-(color:--brand-color)
[mask-type:luminance]
[&>[data-active]+span]:underline
group-hover/item:block
bg-red-500!
```

The immutable parsed model preserves:

- ordered variants, with a configured prefix occupying the first variant position;
- utility family, base, and optional named or bare value;
- arbitrary value and its optional CSS data-type hint;
- CSS-variable shorthand;
- arbitrary property;
- negative form;
- slash modifier versus fraction;
- canonical trailing important marker;
- source text and spans needed for diagnostics and editor explanations.

Tailwind v4's canonical important syntax is **trailing**: `bg-red-500!`. The parser and editor emit
that form. Legacy `!bg-red-500` may be accepted only as a migration aid and must carry a deprecation
diagnostic; it is never the canonical display or generated suggestion.

Stacked variants apply in v4 left-to-right order. Class order in source does not define conflict
resolution; registry order and CSS cascade-layer order do. A malformed token is recoverable and
normally means “not a registered utility,” although a token in a directive such as `@apply` receives
an actionable diagnostic.

### 4.1 Complete built-in variants

The built-in registry is complete for v4.3.3. It includes, without a “later” bucket:

- responsive minimum, maximum, range, and arbitrary breakpoints;
- named and arbitrary container queries, including minimum/maximum forms;
- interactive, form-state, structural, and validation pseudo-classes;
- pseudo-elements and generated-content targets;
- child and descendant variants;
- direction, dark scheme, print, orientation, motion, contrast, forced-color, pointer, and other
  documented environment/media variants;
- `aria-*`, `data-*`, and `supports-*`;
- `group-*`, `peer-*`, `in-*`, `has-*`, and `not-*`, including named groups/peers, modifiers, and
  supported compounds;
- arbitrary selector and at-rule variants;
- v4.3.3 stacked and compound `@variant` behavior.

An individual family may reject a value, modifier, negative form, fraction, or compound. Capability
comes from registry metadata, not from a global parser assumption.

## 5. Complete utility catalog and default design system

“Complete Tailwind v4 replication” means all documented v4.3.3 built-in utility families, not an
initial subset of spacing, color, typography, sizing, flex/grid, borders, and backgrounds.

The compatibility manifest enumerates every family in these catalog areas:

| Area | Included families |
| --- | --- |
| Layout | Aspect ratio, container behavior, columns, break controls, box decoration and sizing, display, float/clear, isolation, object fit/position, overflow, overscroll, position, physical/logical inset, visibility, and z-index |
| Flexbox and grid | Basis, direction, wrap, flex, grow/shrink, order, grid templates and placement, automatic flow/tracks, gap, alignment, justification, and placement |
| Spacing | Physical and logical margin and padding, including the v4.2 block/inline additions |
| Sizing | Width/height and minimum/maximum forms, size, physical and logical inline/block sizes, viewport/content/fraction/theme/arbitrary modes |
| Typography | Family, size, smoothing, style, weight, stretch, numeric variants, font features, tracking, clamp, leading, lists, alignment, color, decoration, transform, overflow/wrap/indent, tab size, vertical alignment, whitespace, word breaking, hyphenation, and content |
| Backgrounds | Attachment, clip, color, image/gradients, origin, position, repeat, and size |
| Borders and outlines | Radius, width, color, style, outline width/color/style/offset, including logical sides |
| Effects and masks | Box/inset/text shadows, opacity, blend modes, and all documented mask properties |
| Filters | Filter and backdrop-filter families, including blur, brightness, contrast, drop shadow, grayscale, hue rotate, invert, opacity, saturate, and sepia |
| Tables | Collapse, spacing, layout, and caption side |
| Transitions and animation | Property, behavior, duration, timing, delay, animation, and required keyframes |
| Transforms | Backface visibility, perspective and origin, rotate, scale, skew, transform, origin/style, translate, and zoom |
| Interactivity | Accent/caret color, appearance, color scheme, cursor, field sizing, pointer events, resize, scrolling behavior, scrollbar color/width/gutter, scroll margin/padding/snap, touch action, user selection, and will-change |
| SVG and accessibility | Fill, stroke, stroke width, forced-color adjustment, and documented accessibility helpers |

The manifest must specifically freeze additions through v4.1, v4.2, and v4.3, including the v4.2
logical-property and font-feature surfaces and the v4.3 scrollbar, size-container, zoom, tab-size,
stacked/compound `@variant`, and functional-utility default-value behavior.

The default design system includes compatible design-token values, Preflight behavior, registered
custom-property fallbacks, and required keyframes. It preserves the
`theme, base, components, utilities` cascade-layer contract. Viu authors the tables and code in this
repository; it does not import Tailwind's `theme.css`, `preflight.css`, or generated utility modules.

### 5.1 Machine-readable conformance

The repository-owned manifest under the utility library's `conformance/` folder records:

- compatibility version `4.3.3`;
- every utility and variant family;
- every theme namespace;
- every directive, function, source-detection form, and layer-ordering rule;
- supported value, modifier, negative, fraction, prefix, important, arbitrary, and compound modes;
- golden vectors and the authoritative official reference for each behavior.

The frozen contract is
[`compatibility-v4.3.3.json`](../tooling/Assimalign.Viu.UtilityCss/conformance/compatibility-v4.3.3.json);
its independently authored executable expectations are
[`golden-vectors-v4.3.3.json`](../tooling/Assimalign.Viu.UtilityCss/conformance/golden-vectors-v4.3.3.json).
These files are copied only to the test output and are not embedded in or loaded by the shipping
tooling assembly.

The manifest may use JSON as test data, but it is **not user configuration** and is never loaded from
a consuming project. No JSON theme/configuration contract exists.

CI fails when:

- a manifest promise has no registry entry;
- a registry entry lacks compiler and editor metadata;
- a promised mode has no independently authored golden vector;
- a documented catalog family is omitted;
- compiler and language-service resolution differ;
- generated layer or property ordering changes without an approved compatibility update.

Normal tests do not invoke Tailwind as a differential oracle. Expected results are frozen Viu-owned
vectors reviewed against the official v4.3.3 references.

## 6. Plain-text candidate detection

Viu follows the v4 source-detection model: supported source regions are treated as text, and complete
candidate-shaped tokens are offered to the parser. The detector does not evaluate a programming
language or attempt to discover the runtime result of interpolation.

The important separation is:

- **MSBuild discovers files and resolves include/exclude roots.**
- **A container slicer selects supported markup regions.**
- **The utility detector scans those regions as plain text.**
- **The candidate parser and registry decide whether a token generates CSS.**

Default discovery includes:

- `.viu` `<template>` content (including the legacy `@template` container during its migration
  window);
- supported host markup such as `.html` and `.htm`;
- explicitly registered source roots;
- tag-based `.vue` template regions after the parser boundary owned by
  [#250](https://github.com/assimalign/viu/issues/250) is implemented.

Default discovery excludes:

- ordinary `@(Compile)` input and every `.cs` file;
- `.viu` or `.vue` script regions;
- CSS as an automatically scanned candidate source;
- ignored directories, build output, dependencies, binary files, archives, and lock files;
- excluded roots and inline exclusions.

This exclusion is the requested **no code-first C# boundary**. Viu Utilities does not scan C# source,
string constants in `.cs`, attributes, render-method class arguments, or generated C#. A future
code-first feature must be separately designed and cannot widen this source set accidentally.

Runtime concatenation or interpolation cannot manufacture a detectable class. Authors must place
every complete alternative in source text or use `@source inline(...)`. For example, two complete
literal alternatives can be detected; fragments such as `bg-{color}-500` cannot.

Each file produces a sorted, de-duplicated, structurally equatable candidate set with source spans.
A reference-counted project union ensures that:

- adding a second use does not regenerate CSS;
- removing one of several uses does not remove CSS;
- removing the final use does remove stale CSS;
- file deletion, rename, root changes, and overlapping roots cannot leave stale candidates.

## 7. Compilation and Browser SDK static-asset pipeline

The compilation stages are:

1. Parse the project utility entry, imports/references already resolved by MSBuild, theme, source
   directives, custom utilities, and custom variants.
2. Detect and de-duplicate complete source candidates per file.
3. Parse candidates with the shared balanced parser.
4. Resolve each candidate through the immutable project registry and theme.
5. Apply variants left-to-right and construct CSS through `Assimalign.Viu.Syntax.Css`.
6. Assemble compatible theme, base/Preflight, components, and utilities layers in fixed registry
   order.
7. Serialize one byte-stable standalone stylesheet.

Unknown source tokens do not fail a build. Authoring errors in CSS-first directives, invalid
references, or a known family with an incompatible explicit form produce source-located Viu
diagnostics and recover where possible. Cancellation is the only expected control-flow exception.

The Browser SDK adds a `ViuBundleUtilityCss` task entry point through its
`Assimalign.Viu.Sdk.Browser.Tasks` build payload. It calls the same
`Assimalign.Viu.UtilityCss` compiler as the analyzer and writes:

```text
obj/<configuration>/<tfm>/viu/<PackageId>.utilities.css
```

Central targets then:

- skip task execution when MSBuild inputs are unchanged;
- compare generated content and avoid touching the file on a semantic no-op;
- register the stylesheet through `DefineStaticWebAssets`;
- create content-derived fingerprints and endpoints;
- inject one idempotent `<link>` into the host page using the landed compression-safe rewrite order;
- copy the static asset into build and publish output for packaged SDK consumers.

The utility asset remains separate from `<PackageId>.viu.css`, the existing component-style bundle.
Both may be present and both use deterministic content hashes. An external browser application using
`<Project Sdk="Assimalign.Viu.Sdk.Browser">` receives the analyzer, utility core dependency closure,
tasks, targets, defaults, and built-in CSS data from the Browser SDK/base-ref-pack composition with no
project reference into this repository. The host-neutral base SDK does not run this static-web-asset
pipeline or write to `wwwroot`.

Package-consumer verification is distinct from in-repo tests: pack to `_out/packages`, build a
consumer from the installed package boundary, confirm the utility asset and link, then run the WASM
application. AOT publication and trimming checks are separate gates.

## 8. Single-file components and the `.vue` compatibility dependency

### 8.1 Canonical `.viu`

Viu's canonical component format remains `.viu`. Since the hybrid-container pivot
(**[V01.01.06.10]**, [#257](https://github.com/assimalign/viu/issues/257), 2026-08-02) it uses the
documented hybrid container — tag-based `<template>` / `<style>` blocks with the component's C# in
an `@script { }` block, the legacy `@template`/`@style` containers still parsing during a migration
window — and is discovered by the base SDK's `**/*.viu` item; the Browser SDK inherits that item
graph. The generator, component-style compiler, utility scanner, build tasks, hot-reload metadata,
and Visual Studio document support share the same source-located descriptor.

### 8.2 Tag-based `.vue` compatibility

**[V01.01.06.09]** ([#250](https://github.com/assimalign/viu/issues/250)) adds a narrow tag-based
compatibility surface alongside canonical `.viu`:

1. the recoverable parser projects `<template>`, `<script>`, `<style>`, and custom blocks into the
   existing immutable descriptor with source locations;
2. `<script lang="csharp">` and `<script setup lang="csharp">` merge in authored order into the
   generated C# component; JavaScript and other script languages are never executed and receive a
   source-located diagnostic;
3. template code generation, scoped styles, CSS Modules, `v-bind()`, `@reference`, `@apply`,
   source mapping, and utility candidate detection reuse the existing Viu pipeline;
4. both Viu SDKs discover `.vue` only for Viu projects, while the Visual Studio language server
   rechecks the owning project before accepting compatibility documents;
5. template and script edits reset/remount through generated, AOT-safe hot-reload marker types so
   .NET 10 browser WebAssembly executes the updated generated code; style edits swap the stylesheet
   without remounting and preserve component state.

A same-directory, same-base canonical `.viu` file shadows its `.vue` compatibility peer. Path
identity follows the host filesystem: case-insensitive on Windows and ordinal on Unix-like systems.
The implementation does not add a Vue JavaScript runtime or a dependency on Vue or Tailwind.

## 9. `dotnet watch` and stylesheet hot swap

**[V01.01.12.05.01]** ([#248](https://github.com/assimalign/viu/issues/248)) owns the development
hot-swap path. “Hot release” in this design means a development hot update; production release output
remains a static, fingerprinted stylesheet.

The SDK contributes these inputs to the watch graph:

- the project utility entry and every resolved `@reference` stylesheet;
- theme and source-directive changes;
- every supported `.viu`, `.vue`, host-markup, and explicitly included source;
- external CSS-first `source(...)` and `@source` roots through a compiler-produced dependency
  manifest refreshed after each regeneration;
- add, delete, and rename events, not only content writes.

On a distinct candidate-set, theme, custom registry, or directive change:

1. run one incremental utility compilation;
2. write the asset only if its bytes changed;
3. notify the browser once;
4. replace or cache-bust the utility `<link>` in one batched operation;
5. leave the application root and mounted component state intact.

A text edit that leaves the candidate/theme/registry model equal performs no stylesheet write and no
browser update. A watch session that begins with no candidates receives a marked zero-byte bundle so
the static asset and host link already exist when the first utility appears. Removing the final
reference returns to that development tombstone. Ordinary builds and release/AOT publish remove the
tombstone and contain none of the watcher or transport path.

## 10. Visual Studio IntelliSense

**[V01.01.12.07.01]** ([#247](https://github.com/assimalign/viu/issues/247)) extends the existing
architecture:

```text
Assimalign.Viu.VisualStudio
  -> Assimalign.Viu.LanguageServer
       -> Assimalign.Viu.LanguageService
            -> Assimalign.Viu.UtilityCss
```

The thin VSIX still owns only Visual Studio registration, process lifetime, and presentation. The
language service owns document/project snapshots and references the shared utility assembly.

Completion activates only in supported static class attributes and literal class-binding strings. It
does not pollute tags, directives, scripts, arbitrary C# strings, or ordinary CSS property contexts.
It covers:

- complete built-in and project-defined utilities;
- variants and compounds;
- theme-backed names and values;
- arbitrary-value/property/variant shapes;
- CSS-variable shorthand and type hints;
- negative, fraction, modifier, prefix, and canonical trailing-important forms.

Completion detail and hover show the declaration projection, resolved theme value, layer/order
metadata, and variant behavior. Malformed partial input is handled through the same recoverable parser
used by the compiler.

The language server loads the project's CSS entry and sources through its project snapshot. Before a
project snapshot is available it may offer the built-in registry/default theme, clearly omitting
project-defined entries. Incremental document changes invalidate only affected candidates, theme, or
registry projections.

The VSIX packages the language server with the `Assimalign.Viu.UtilityCss` dependency closure.
It does not bundle or coordinate with the Tailwind CSS IntelliSense extension.

## 11. Incrementality, determinism, and quality gates

The minimum cache boundaries are:

| Stage | Equality key | Must not invalidate |
| --- | --- | --- |
| Source discovery | normalized roots, exclusions, and source identities | Candidate parsing for unchanged source content |
| Per-source detection | selected source text and source options | Other files |
| Project candidate union | distinct candidate reference counts | Resolution when a duplicate use is added or removed |
| Theme/directives | immutable parsed CSS-first model | Plain-text detection |
| Project registry | built-in compatibility version plus custom utility/variant model | Detection |
| Candidate resolution | parsed candidate, registry, and theme | Unrelated candidates |
| Stylesheet assembly | distinct resolved rules and layer inputs | Physical write when bytes are unchanged |
| Editor projection | registry/theme/document context | Compiler output |

Determinism requirements:

- ordinal normalized source identities;
- immutable value-equatable models;
- stable registry/property/rule ordering;
- LF-only canonical emission;
- no timestamps, machine paths, random identifiers, locale-sensitive sorting, or source class-order
  effects;
- byte-identical output for identical semantic input.

Required independent gates include:

- parser, detector, registry, resolver, directive, and malformed-recovery unit tests;
- manifest completeness and golden conformance;
- compiler/editor parity tests;
- incremental add/remove/rename/no-op tests;
- Browser SDK static-asset, fingerprint, link, compression, and package-consumer tests;
- `dotnet watch` browser tests proving managed template/script remounts and style-only mounted-state
  preservation;
- output-size and warm-build budgets;
- trimming and AOT publication checks;
- an automated dependency audit proving no Tailwind/Node/PostCSS/Vite package or executable entered
  build, test, VSIX, SDK, or publish output.

## 12. Licensing, attribution, and trademarks

Tailwind CSS v4.3.3's tagged source is distributed under the
[MIT License](https://github.com/tailwindlabs/tailwindcss/blob/v4.3.3/LICENSE). The license permits
reuse subject to its notice conditions; it does not create a runtime or build dependency.

This design chooses an independently authored C# implementation and independently authored
conformance vectors. No Tailwind source file, generated stylesheet, editor catalog, logo, or package
is vendored. If implementation work later copies a substantial source or data portion instead of
using it only as a behavioral reference, that change must identify the copied material, preserve the
required MIT notice in the distributed product, and receive an explicit licensing review.

The [Tailwind brand guidance](https://tailwindcss.com/brand) governs use of the name and marks.
The product name is **Viu Utilities**, never “Viu Tailwind,” “Tailwind for Viu,” or another name that
suggests ownership or endorsement. Compatibility prose uses the Tailwind CSS name only to identify
the reference framework and must carry this statement in public documentation and Marketplace/package
copy:

> Viu Utilities is an independent Viu feature compatible with documented Tailwind CSS v4.3.3
> behavior. It is not affiliated with or endorsed by Tailwind Labs.

Tailwind names and logos are not used as Viu package, assembly, extension, command, namespace, or
asset identifiers.

## 13. Explicit non-goals

The following are outside this feature:

- code-first C# styling, configuration, source scanning, attributes, or registry APIs;
- executable `tailwind.config.js`, legacy `@config`, JavaScript presets, or JavaScript plugins;
- legacy `@plugin` execution or third-party Tailwind plugin ABI compatibility;
- deprecated `theme()` compatibility;
- a JSON author configuration or JSON safelist;
- Tailwind's npm packages, CLI, Oxide/native scanner, browser package, PostCSS/Vite/webpack plugins,
  or editor extension;
- runtime CSS generation in WebAssembly;
- byte-for-byte reproduction of Tailwind's formatting or internal implementation;
- automatic compatibility with versions after v4.3.3;
- treating deprecated leading important syntax as canonical;
- deferring group/peer/arbitrary variants or shipping only a “core utility” subset.

Actual tag-based `.vue` code generation is not listed as a non-goal because the user requested it.
It is the explicit [#250](https://github.com/assimalign/viu/issues/250) dependency described in
[§8](#8-single-file-components-and-the-vue-compatibility-dependency).

## 14. Work-item ownership and definition of done

| Work item | Design ownership |
| --- | --- |
| [V01.01.12.13 — #156](https://github.com/assimalign/viu/issues/156) | Balanced candidate parser; v4 prefix/stacking/negative/fraction/modifier/arbitrary/CSS-variable/trailing-important model; complete built-in variants |
| [V01.01.12.14 — #157](https://github.com/assimalign/viu/issues/157) | CSS-first `@theme`, compatible default theme, namespaces, resets, prefix behavior, diagnostics |
| [V01.01.12.15 — #158](https://github.com/assimalign/viu/issues/158) | Plain-text detector, source directives/graph, per-file spans/sets, project reference counts, no C# scanning |
| [V01.01.12.16 — #159](https://github.com/assimalign/viu/issues/159) | Complete registry, resolver, Preflight/layers/order, deterministic compiler, analyzer and SDK/static-asset pipeline |
| [V01.01.12.17 — #160](https://github.com/assimalign/viu/issues/160) | `@utility`, `@variant`, `@custom-variant`, `@apply`, `@reference`, and CSS-first functions/composition |
| [V01.01.12.07.01 — #247](https://github.com/assimalign/viu/issues/247) | Visual Studio completion/hover/packaging from the shared engine |
| [V01.01.12.05.01 — #248](https://github.com/assimalign/viu/issues/248) | `dotnet watch` item graph, incremental regeneration, one-shot stylesheet swap, style-only state preservation |
| [V01.01.12.16.01 — #249](https://github.com/assimalign/viu/issues/249) | Frozen v4.3.3 manifest, catalog completeness, golden vectors, compiler/editor parity |
| [V01.01.06.09 — #250](https://github.com/assimalign/viu/issues/250) | Tag-based `.vue` parser, C# script contract, shared downstream code generation, SDK/editor/watch activation, documented format deviation |

The feature is complete only when all nine items are satisfied together, including the implemented
and tested `.vue` compatibility dependency in
[§8](#8-single-file-components-and-the-vue-compatibility-dependency). Closing the compiler item
with a partial catalog, closing IntelliSense with a separate list, or closing source detection after
merely scanning raw `.vue` text does not meet this design.
