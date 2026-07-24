# Build-time utility-first CSS engine — design

Scoping deliverable for **[V01.01.12.10] Scope the build-time utility-first CSS engine**
([#129](https://github.com/assimalign/viu/issues/129)). This is a design document plus a proposed
work-item breakdown, **not** an implementation — no engine code ships on this branch.

The engine is an in-house, Tailwind-style utility-first CSS system built entirely at **build time**:
it scans template, `.viu`, and host-page sources for utility-class candidates (Tailwind's
just-in-time content-scanning model), generates only the CSS that the scanned classes actually use,
and emits it as a bundled static web asset. **Zero CSS generation happens in the WebAssembly payload
at runtime** — the utility stylesheet is a build product, exactly like the compiled `.viu` render
functions.

This document is repo-level (per the [documentation rule](../.claude/rules/documentation.md)) because
the engine deliberately spans three homes — `Assimalign.Viu.Syntax.Css` (CSS AST and emission), a
Tooling-owned composition root (extraction, resolution, generation), and the incremental generator /
MSBuild pipeline — and no single library's `docs/DESIGN.md` owns the whole picture.

## Contents

1. [Where the engine sits](#1-where-the-engine-sits)
2. [Engine architecture](#2-engine-architecture)
3. [The utility-class language specification](#3-the-utility-class-language-specification)
4. [Architectural boundaries](#4-architectural-boundaries)
5. [Interplay with the single-file-component style pipeline](#5-interplay-with-the-single-file-component-style-pipeline)
6. [Incrementality](#6-incrementality)
7. [Work-item breakdown](#7-work-item-breakdown)
8. [Open questions](#8-open-questions)
9. [Acceptance-criteria traceability](#9-acceptance-criteria-traceability)

### Reference material

- Tailwind CSS docs — utility-first model, JIT content scanning, theme/variant system:
  <https://tailwindcss.com/docs>
- CSS Syntax Module Level 3 — the `Assimalign.Viu.Syntax.Css` kind-enum and tokenizer pin:
  <https://www.w3.org/TR/css-syntax-3/>
- Roslyn incremental source generators — the caching model the extraction pipeline honors:
  <https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview>
- Vue SFC CSS features (the pipeline this composes with):
  <https://vuejs.org/api/sfc-css-features.html>
- Repo rules: [general-rules](../.claude/rules/general-rules.md),
  [build-system](../.claude/rules/build-system.md), [workflow](../.claude/rules/workflow.md),
  [documentation](../.claude/rules/documentation.md), [deviations](../.claude/rules/deviations.md)
- Landed architecture this builds on:
  [`Assimalign.Viu.Syntax` DESIGN](../libraries/Assimalign.Viu.Syntax/docs/DESIGN.md) (the
  registration seam, [#127](https://github.com/assimalign/viu/issues/127)) and
  [`Assimalign.Viu.Syntax.Css` DESIGN](../libraries/Assimalign.Viu.Syntax.Css/docs/DESIGN.md) (the
  tokenizer/tree/selector parser/scoped rewriter,
  [#60](https://github.com/assimalign/viu/issues/60))

---

## 1. Where the engine sits

Viu already compiles `.viu` single-file components at build time through the
`Assimalign.Viu.Syntax.*` cluster and the `SingleFileComponentGenerator`
([#58](https://github.com/assimalign/viu/issues/58)). The utility-first CSS engine is the **flagship
second consumer** of the same machinery — the role a Tailwind Vite plugin plays alongside
`@vitejs/plugin-vue` in a Vue build. It reuses three landed foundations:

1. **The registration seam** ([`Assimalign.Viu.Syntax`](../libraries/Assimalign.Viu.Syntax/docs/DESIGN.md),
   [#127](https://github.com/assimalign/viu/issues/127)). `AggregateSyntaxParserOptions<T>.RegisterParser(SyntaxSourcePredicate, SyntaxParser)`
   is the seam a composition root uses to attach a parser to a block name, `lang`, or file type
   without the container library referencing it. The utility engine registers its own extraction
   pass here — "language libraries never reference each other; the composition root constructs the
   aggregate options and registers whatever parsers the build embeds — including Viu-owned tooling
   like utility-class style generation" is already written into the base's design as the anticipated
   use.

2. **The CSS AST and emitters** ([`Assimalign.Viu.Syntax.Css`](../libraries/Assimalign.Viu.Syntax.Css/docs/DESIGN.md),
   [#60](https://github.com/assimalign/viu/issues/60)). The two-phase tokenizer (`CssTokenizer`) →
   context-directed rule parser (`CssParseEngine`), the record-graph tree (`CssStylesheetNode`,
   `CssQualifiedRuleNode`, `CssDeclarationNode`, …), the flat selector model, and the deterministic
   canonical serializer behind `CssScopedRewriter` are all reusable. The Css DESIGN already names
   this item as a planned reuse: "Tailwind / utility-class generation — [#129] is a separate consumer
   of this parser. It reuses the tokenizer, tree, and (for its own scoping) `CssScopedRewriter`; it
   does not need changes here" — with one addition scoped below (a programmatic CSS *construction*
   surface; the landed code parses and rewrites CSS but does not yet build rules from scratch).

3. **The incremental generator pipeline and its MSBuild integration**
   (`SingleFileComponentGenerator`, `.props`/`.targets`,
   [#58](https://github.com/assimalign/viu/issues/58)). `.viu` files already flow in as
   `AdditionalFiles`; every pipeline stage is a value-equatable record; the RS1035 content-file
   limitation (a source generator emits C#, not files, and `System.IO` is banned in the analyzer
   sandbox) is already documented, with the extracted CSS surfacing as the generated `ExtractedStyles`
   constant and physical bundling deferred to "an MSBuild-task follow-up." That deferred task is
   **shared** with this engine (see [§2.4](#24-reaching-publish-output-the-bundling-route)).

```
                         AdditionalFiles
   .viu files ──┐        .html host pages ──┐          utility.theme.json ──┐
                │                            │                               │
        ┌───────▼────────────────────────────▼───────────────────────────────▼──────┐
        │              Tooling composition root (build-time, netstandard2.0)          │
        │                                                                             │
        │   AggregateSyntaxParser seam                     Utility engine core        │
        │   ├─ @template → TemplateSyntaxParser  ──►  ┌── candidate extraction ──┐    │
        │   ├─ @style    → CssSyntaxParser            │  (walks class attrs)     │    │
        │   └─ host .html → UtilityClassScanner  ──►  └────────────┬─────────────┘    │
        │                                                          │ UtilityCandidateSet (per file) │
        │                                              ┌───────────▼─────────────┐    │
        │      theme model ───────────────────────────►  utility resolver        │    │
        │  (Assimalign.Viu.Syntax.Css emission) ◄──────┤  candidate → CSS rules   │    │
        │                                              └───────────┬─────────────┘    │
        └──────────────────────────────────────────────────────────┼─────────────────┘
                                                                    │ one bundled stylesheet
                    ┌───────────────────────────────────────────────┼─────────────────┐
      generator host│ AddSource: constants + diagnostics (no I/O)   │  MSBuild task host│ writes .css
                    ▼                                                 ▼   as StaticWebAsset
              generated C#                                     wwwroot/_content/…/viu-utilities.css
```

---

## 2. Engine architecture

### 2.1 Sources scanned

The engine's input is the set of places a utility class name can appear. All three arrive as
compiler `AdditionalFiles`, so the engine never performs its own file discovery or I/O:

| Source | What is scanned | How it is reached |
| --- | --- | --- |
| **`.viu` template class attributes** | Static `class="…"` attribute text on elements, and the static string operands of `:class` / `v-bind:class` bindings | The `@template` block is already dispatched to `TemplateSyntaxParser` by the SFC composition root; the extractor walks the resulting template AST (`ElementNode` → `AttributeNode` where `Name == "class"`, plus `DirectiveNode` class bindings) — see [§2.2](#22-candidate-extraction-through-the-seam) |
| **`.viu` other blocks** | Custom blocks that opt in (a documented predicate, e.g. a `@markup`/raw block a host tool registers) | Registered on the seam by predicate, same as `@style`/`@template` |
| **Host pages** | `class` attributes in static host HTML (`examples/Assimalign.Viu.WebApp/wwwroot/index.html` and equivalents) | Flow in as `AdditionalFiles` (`.html`/`.htm`); the utility scanner registers a `SyntaxSourcePredicate` matching them — nothing else in the cluster claims host HTML, so first-match-wins is uncontended here |

Only **statically determinable** class tokens are extractable — this is a deliberate, Tailwind-parity
constraint (Tailwind scans raw text and cannot see runtime-computed strings). A class produced only by
a C# expression (`:class="dynamic"`) whose value is not a literal is invisible to the scanner; the
documented escape hatch is a **safelist** in the theme config ([§3.4](#34-theme-configuration)). This
divergence and its mitigation are called out in [§3.5](#35-followdiverge-decisions-against-tailwind).

### 2.2 Candidate extraction through the seam

Extraction is modeled as a `SyntaxParser`-shaped pass so it inherits the base cluster's value-equality
and recoverability for free, and it is wired at a **Tooling-owned composition root** — never inside a
language library. Two dispatch paths, reconciled with the seam's **first-matching-registration-wins**
rule (`AggregateSyntaxParser` dispatches each container node to exactly one parser and `break`s):

- **Host pages and standalone class sources** register directly on the aggregate seam. The
  `UtilityClassScanner` (a `SyntaxParser` over raw markup that emits a value-equatable
  `UtilityCandidateSet`) is registered with a predicate matching host-HTML `SyntaxSource`s. This is
  the literal "registers a class-extraction pass over container blocks via
  `AggregateSyntaxParserOptions<T>`" the issue calls for, and it is uncontended because no other
  cluster parser claims host HTML.

- **`.viu` template class attributes** are **not** re-registered against the `@template` block —
  that block is already claimed by `TemplateSyntaxParser`, and a node dispatches to only one parser.
  Instead the extractor **consumes the already-dispatched template AST**: the composition root holds
  the `TemplateSyntaxParserResult` in hand (exactly as `SingleFileComponentGenerator.CompileRenderFunction`
  does today), and a `UtilityCandidateExtractor.FromTemplate(root)` walk pulls class tokens out of it.
  Reusing the one template parse — rather than re-scanning the raw `@template` text a second time — is
  both faithful to the seam (the template got there *through* the seam) and cheaper. See
  [§8](#8-open-questions) OQ-1 for the alternative (a standalone utility-extraction aggregate) and why
  it is rejected for v1.

Both paths converge on the same value-equatable per-source output:

```
UtilityCandidateSet            // record, per scanned file
  ├─ SourcePath : string       // for diagnostics only; not part of equality-relevant generation input
  └─ Candidates : EquatableArray<UtilityCandidate>   // sorted, de-duplicated within the file

UtilityCandidate               // record — the raw token exactly as written, e.g. "md:hover:bg-blue-500/50"
  └─ RawText : string
```

Extraction produces **raw tokens only** — it does not parse variants or resolve CSS. That keeps the
extractor free of any theme or CSS dependency (it roots on `Assimalign.Viu.Syntax` alone) and keeps
the incremental cache key small and stable ([§6](#6-incrementality)).

### 2.3 Resolution and generation

Downstream of extraction, the composition root runs three build-time stages, all in plain
netstandard2.0 code (no I/O, no reflection, no dynamic codegen):

1. **Parse the candidate grammar.** Each `UtilityCandidate.RawText` is split into
   `variants[] + utility-root + optional arbitrary value + optional opacity modifier + optional
   important flag` by `UtilityCandidateParser` ([§3.1](#31-naming-scheme)). Unparseable tokens are
   dropped silently (Tailwind parity: a class that is not a utility is just a normal class name) —
   optionally surfaced as an informational diagnostic behind a verbosity switch.
2. **Resolve to CSS.** `UtilityResolver` maps a parsed candidate to a set of `CssDeclarationNode`s
   using the **compiled utility table** (which utility root maps to which declarations) and the
   **theme model** (color/spacing/breakpoint values). Variants wrap the base rule in the appropriate
   selector suffix (`:hover`, `.dark &`) or at-rule (`@media (min-width: …)`).
3. **Emit one stylesheet.** Rules are de-duplicated (a class used on ten elements emits one rule),
   ordered deterministically (a fixed property/utility ordering plus media-query grouping so output
   is byte-stable for the incremental cache), assembled into a `CssStylesheetNode` via the new
   construction surface, and serialized by the Css canonical serializer.

The resolver output is a single deterministic string: the project's utility stylesheet.

### 2.4 Reaching publish output: the bundling route

A Roslyn source generator emits **C#, not content files**, and `System.IO` is off-limits under
[RS1035](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/rs1035) inside
the analyzer sandbox. The landed scoped-CSS work already hit this exact wall and deferred physical
bundling to "an MSBuild-task follow-up on the [#58] pipeline side." This engine adopts and completes
that same route, and it is **the** shared piece between the two features:

> **The engine core is a plain netstandard2.0 library reused by two hosts.** The incremental
> generator host references it to emit constants and report diagnostics inside the analyzer sandbox
> (no I/O). A separate **MSBuild `Task`** — `Microsoft.Build.Utilities.Task`, which runs outside the
> analyzer sandbox and *may* do I/O — references the same core, re-runs generation over the same
> `AdditionalFiles`, and **writes the stylesheet to publish output as a `StaticWebAsset`**.

Concretely:

- The **generator host** continues to surface build-visible artifacts as C# where useful (e.g. a
  `UtilityStylesheet` constant for tests and IDE inspection, and diagnostics for malformed theme
  config), exactly as `ExtractedStyles`/`ScopeId` are surfaced today. No I/O.
- The **MSBuild bundling task** (`ViuBundleCss`) collects (a) the utility stylesheet and (b) the
  per-component `ExtractedStyles` from scoped CSS, writes them under
  `obj/…/viu/viu-utilities.css` (+ a scoped-styles bundle), and adds them to `@(StaticWebAsset)`
  so the WASM SDK copies them into `wwwroot/_content/<package>/…` and `dotnet publish` fingerprints
  and serves them. MSBuild `Inputs`/`Outputs` incrementality means the task is skipped when no input
  changed. The task links the emitted stylesheet into the host page automatically (an injected
  `<link>` into `index.html`, opt-out), or a project references it explicitly.
- Because both hosts call the **same** deterministic core over the **same** inputs, the C# constant
  and the physical file are always byte-identical — there is no second, divergent generation path.

The **runtime WASM cost is zero**: the stylesheet is a static file the browser loads over a normal
`<link>`; no C# on the WASM side generates or parses CSS. This satisfies #129's "no runtime cost"
criterion the same way the compiled render functions satisfy "no runtime template compilation."

---

## 3. The utility-class language specification

The language **follows Tailwind CSS** structurally so authors carry their muscle memory over, and
**diverges only where a Viu hard constraint forces it** — every divergence is recorded in
[§3.5](#35-followdiverge-decisions-against-tailwind) per the
[deviations rule](../.claude/rules/deviations.md).

### 3.1 Naming scheme

A candidate is `variant* utility modifier?`:

```
[variant ":"]…  utility-root  ["-" value | "-[" arbitrary "]"]  ["/" opacity]  ["!"?]

examples:
  bg-blue-500                 utility-root=bg,   value=blue-500
  md:hover:bg-blue-500/50     variants=[md,hover], root=bg, value=blue-500, opacity=50
  w-[32px]                    root=w,   arbitrary=32px
  grid-cols-[1fr_2fr]         root=grid-cols, arbitrary="1fr 2fr" (underscore→space)
  !mt-4                       important=true, root=mt, value=4
```

- **Leading variants** are colon-separated and order-significant left-to-right (outermost first),
  matching Tailwind. `!` marks `!important` (leading `!` per Tailwind v3; the trailing form is an
  open question, OQ-4).
- The **utility root** is looked up in the compiled utility table; the **value** segment indexes the
  theme scale for that root's property family (`bg` → color scale, `w` → spacing/sizing scale).
- **Opacity modifier** `/<n>` applies to color utilities (`bg-blue-500/50` → `rgb(... / 0.5)`).

### 3.2 Variants and modifiers

v1 ships the high-value variant set; the parser is data-driven off the variant table so adding a
variant is a table entry, not a grammar change:

- **Responsive** — `sm: md: lg: xl: 2xl:` → `@media (min-width: <breakpoint>)`, breakpoints from the
  theme. Mobile-first (min-width), Tailwind parity.
- **State/pseudo-class** — `hover: focus: focus-visible: focus-within: active: disabled: visited:`
  → append the pseudo-class to the selector.
- **Pseudo-element** — `before: after: placeholder: selection:` → append the pseudo-element.
- **Dark mode** — `dark:` → `.dark <selector>` (class strategy) or `@media (prefers-color-scheme: dark)`
  (media strategy), selectable in the theme; class strategy is the default (Tailwind parity).
- **Structural** — `first: last: odd: even:` → structural pseudo-classes.

**Deferred to a later wave** (OQ-4): arbitrary variants (`[&>svg]:`), group/peer variants
(`group-hover:`, `peer-focus:`), and `@apply` composition inside `@style` ([§5.4](#54-apply)). These
need selector machinery beyond a flat table and are explicitly out of v1 scope.

### 3.3 Arbitrary values

`utility-[value]` escapes the theme scale for a one-off value: `bg-[#1da1f2]`, `top-[117px]`,
`grid-cols-[repeat(3,minmax(0,1fr))]`. Tailwind's conventions are followed: underscores become
spaces (`_` → ` `), an escaped underscore (`\_`) stays literal, and the bracket body is emitted into
the declaration verbatim after that transform. Arbitrary **properties** (`[mask-type:luminance]`)
are a candidate for v1 but flagged OQ-4 (they widen the grammar to a full `prop:value` parse). The
bracket body is validated only for balanced delimiters — full CSS-value validation is left to the
existing `CssParseEngine` when the declaration is later serialized, so a malformed arbitrary value
surfaces as a normal `CssError` rather than a bespoke diagnostic.

### 3.4 Theme configuration

Tailwind configures the theme in **`tailwind.config.js` — executable JavaScript**. Viu **cannot**
execute JS config (no `new Function`, no dynamic code, AOT/trimming-safe only — founding decision 6 in
[PLAN.md](PLAN.md)). This is the engine's single largest, principled divergence:

- The theme is a **static, declarative document** supplied as an `AdditionalFile`
  (`utility.theme.json`, or a strongly-shaped subset), parsed at build time with **no I/O beyond the
  `AdditionalText` the compiler already handed us**. It carries `screens` (breakpoints), `colors`,
  `spacing`, `fontSize`, `fontFamily`, `borderRadius`, an `extend` block (merge, not replace), a
  `darkMode` strategy, and a `safelist` (class names always generated even if never scanned — the
  escape hatch for dynamic classes).
- A built-in **default theme** (the Tailwind default scale, transcribed once into a compiled static
  table) applies when no config file is present, so the engine works out of the box.
- The parsed theme is a **value-equatable record**; editing it invalidates exactly the resolve/emit
  stages, not extraction ([§6](#6-incrementality)).
- **Plugins** (Tailwind's `plugin()` JS API) are **not** supported as executable code. The extension
  surface is instead a **source-generated / statically-registered utility contribution** — a project
  adds utilities by supplying a compiled table, never by loading code at build time. Scoped as a
  later wave (OQ-5).

### 3.5 Follow/diverge decisions against Tailwind

| Concern | Decision | Rationale |
| --- | --- | --- |
| Candidate naming (`variant:root-value/opacity`, `!important`) | **Follow** | Author familiarity; the structure is language-agnostic |
| JIT content scanning (generate only used classes) | **Follow** | The whole point; matches the build-time, zero-runtime model |
| Responsive / state / dark / pseudo variants | **Follow** | Table-driven; core value |
| Arbitrary values `utility-[…]`, underscore→space | **Follow** | High author value; bounded grammar |
| **Theme config format** | **Diverge** — static `AdditionalFile` document (JSON/declarative), not `tailwind.config.js` | JS config needs a JS runtime / dynamic evaluation — forbidden by AOT/trimming and the no-`new Function` founding decision |
| **Plugins** (`plugin()` executable API) | **Diverge** — statically-registered/compiled utility tables only | Same reason; executable plugins are dynamic codegen |
| **Preflight** (base reset) | **Follow, opt-in** — ship a minimal transcribed reset, `preflight: false` to disable | Determinism + payload control; a reset is data, not code |
| **`@tailwind` / `@layer` injection directives** | **Diverge** — replaced by the single-bundle emission + MSBuild `<link>` injection | There is no runtime stylesheet to inject into; bundling is an MSBuild concern |
| Dynamically-computed class strings | **Diverge** — invisible to the scanner; use `safelist` | Static scanning cannot see runtime C# values (Tailwind has the same limitation for computed strings) |
| Arbitrary variants (`[&>*]:`), group/peer, `@apply` | **Diverge (defer)** — later wave | Needs selector machinery beyond the flat variant table; keep v1 bounded |
| Full utility catalog | **Diverge (stage)** — core families first (spacing, color, typography, sizing, fl/grid, border, background), rest incremental | Ship value early; catalog breadth is additive table work |

Where behavior mirrors Tailwind, the implementing task must pin it against the
[Tailwind docs](https://tailwindcss.com/docs) in a code comment or test, the same way the cluster
pins Vue parity against vuejs/core.

---

## 4. Architectural boundaries

Pinned so a future session implements without re-deriving them. These are binding on every
implementation task in [§7](#7-work-item-breakdown).

### 4.1 Owning projects and dependency direction

| Concern | Owning project | May reference |
| --- | --- | --- |
| CSS AST, parsing, **construction, and deterministic serialization** | `Assimalign.Viu.Syntax.Css` | `Assimalign.Viu.Syntax` (base) only |
| Utility candidate grammar, variant model, theme model, resolver, generation | **Tooling-owned engine core** (a netstandard2.0 library in the analyzer layer — see OQ-2) | `Assimalign.Viu.Syntax` (base) **and** `Assimalign.Viu.Syntax.Css` (for emission) |
| Candidate extraction scanner | Tooling-owned engine core | `Assimalign.Viu.Syntax` (base) only — no CSS or theme dependency |
| Composition + incremental pipeline wiring, constants, diagnostics | `Assimalign.Viu.Generators.Syntax` (the composition root generator) | the engine core, `Assimalign.Viu.Syntax.Css`, the language parsers — as it already does |
| Physical bundling to publish output | `ViuBundleCss` MSBuild task (Tooling) | the engine core |

**Invariant — no language library references another.** `Assimalign.Viu.Syntax.Css` stays a leaf: it
gains a *construction* surface but never learns what a "utility" is. The utility engine core is
**Tooling / composition-root code**, not a peer language library, precisely so it is *allowed* to
reference `Assimalign.Viu.Syntax.Css` for emission — a peer language library would be forbidden from
doing so. The dependency arrows always point **toward** the base and Css, never sideways between
language libraries. This is the same rule the SFC composition root already honors.

### 4.2 Analyzer-host constraints (netstandard2.0)

Everything that runs at build time — the extraction scanner, candidate parser, theme parser, resolver,
and the CSS construction surface — targets **netstandard2.0** (`$(TargetFrameworkForAnalyzers)`), with
`EnablePreviewFeatures=false` and the linked `Shims/` sources, because the assemblies load inside
Roslyn analyzer hosts alongside the existing cluster. No `net10.0`-only APIs.

### 4.3 No I/O outside `AdditionalFiles`

The engine performs **no file or network I/O** in the analyzer sandbox. Every input — `.viu` files,
host pages, the theme document — arrives as an `AdditionalText`. The **only** component that touches
the filesystem is the `ViuBundleCss` MSBuild task, which runs **outside** the analyzer sandbox where
I/O is permitted (see [§2.4](#24-reaching-publish-output-the-bundling-route)). This is how the engine
respects RS1035 while still landing a physical stylesheet.

### 4.4 No reflection, no dynamic code generation

Utility tables, variant tables, and the default theme are **compiled static data** (hand-authored
tables or source-generated), never reflection-scanned or loaded from executable config. The theme
document is *parsed as data*, never *evaluated as code*. Parsing is recoverable — a malformed theme or
class reports a Viu-defined diagnostic and never throws (the only expected exception is
`OperationCanceledException`), matching the cluster contract. The whole engine is trimming- and
WASM/NativeAOT-safe by construction because none of it ships to the runtime at all.

---

## 5. Interplay with the single-file-component style pipeline

The engine coexists with three landed/in-flight SFC style features. The governing principle: **the
Css library owns rule-level CSS parsing and serialization; each higher feature owns its own
*transform* over that shared AST.**

### 5.1 Shared vs owned CSS parsing

| Layer | Owner | Shared by |
| --- | --- | --- |
| Tokenizer (`CssTokenizer`), rule parser (`CssParseEngine`), tree nodes, selector model | `Assimalign.Viu.Syntax.Css` | scoped CSS, CSS Modules, **utility engine** |
| Canonical deterministic serializer | `Assimalign.Viu.Syntax.Css` | scoped CSS, **utility engine** |
| **Programmatic rule construction** (new, [§7](#7-work-item-breakdown) V01.01.12.11) | `Assimalign.Viu.Syntax.Css` | **utility engine** (scoped CSS never needed it because it rewrites an existing tree) |
| Scoped `[data-v-…]` selector rewrite (`CssScopedRewriter`) | `Assimalign.Viu.Syntax.Css` | scoped CSS; utility engine **only in optional scoped-utility mode** |
| Candidate grammar + resolver | utility engine core | — (owned) |
| Local module-class hashing + `v-bind()`→`var()` rewrite | CSS Modules ([#62](https://github.com/assimalign/viu/issues/62)) | — (owned) |

The utility engine **reuses** the tokenizer/tree/serializer and **adds** a construction surface; it
does not modify or fork the parser.

### 5.2 Composition with scoped CSS ([#60](https://github.com/assimalign/viu/issues/60), landed)

Tailwind utilities are **global by design** — `bg-blue-500` means the same thing everywhere. So the
utility stylesheet is emitted **once, globally, un-scoped** — it is *not* rewritten with any
component's `data-v-<hash>`. A `.viu` that writes `class="p-4 bg-blue-500"` in its template resolves
those classes against the global bundle, while its own `@style scoped` block is independently rewritten
by `CssScopedRewriter` with the component's scope id (the landed `CompileStyles` path is untouched).
The two never collide: scoped selectors carry `[data-v-…]`; utility selectors do not. An **optional
scoped-utility mode** (per-component utilities that *are* rewritten with the component scope id) can
reuse `CssScopedRewriter` unchanged — this is the "for its own scoping, `CssScopedRewriter`" reuse the
Css DESIGN anticipates — but it is off by default and deferred (OQ-6).

### 5.3 Composition with CSS Modules and `v-bind()` ([#62](https://github.com/assimalign/viu/issues/62), in flight)

Designed against #62's **issue contract**, not its in-progress code:

- **CSS Modules** hash class names *locally per component* and expose a typed `$style` accessor.
  Utility classes are **global and stable** (`bg-blue-500`, never hashed), so the two are disjoint and
  compose on one element: `class="bg-blue-500"` (global utility) alongside `:class="$style.card"`
  (module-hashed). They consume the same Css rule parsing but emit into **separate outputs** — the
  global utility bundle vs. the per-component module CSS — so there is no ordering or hashing conflict.
- **`v-bind()` in CSS** compiles to component-scoped `var(--<hash>)` custom properties driven by the
  `UseCssVars` runtime helper. This is **orthogonal** to utilities: utilities emit static declarations;
  `v-bind()` emits custom-property references inside `@style` blocks. They do not interact, except that
  an author could theoretically point a utility's arbitrary value at a custom property
  (`bg-[var(--brand)]`) — which just works, because the arbitrary value is emitted verbatim.
- Hash determinism: where the utility engine ever needs a component-scoped hash (scoped-utility mode,
  OQ-6), it uses the **same FNV-1a-over-project-relative-path scheme** as `StyleScopeId`
  ([#60](https://github.com/assimalign/viu/issues/60)), so all three features agree on hashing.

### 5.4 `@apply`

`@apply` (inlining utilities into an authored `@style` rule) is the **one** place the utility engine
and the SFC style pipeline directly intersect — it would require the `@style` compilation path to call
the utility resolver mid-parse. It is **deferred** (OQ-4 / a later-wave task) to keep v1's boundaries
clean: v1 utilities live only in `class` attributes, not inside `@style`.

---

## 6. Incrementality

Designed to slot into the existing `IIncrementalGenerator` pipeline, whose contract is *identical input
yields an equal, equally-hashed result* — every stage is a value-equatable record, and no
`AdditionalText`/`SourceText`/`Compilation`/`ISymbol` is captured past the read boundary.

### 6.1 Per-stage cache keys

| Stage | Input → Output | Value-equatable key | Recompute trigger |
| --- | --- | --- | --- |
| 1. Read | `AdditionalText` → `SingleFileComponentFile` / host-page text record | file text + path (exists today) | that file's bytes change |
| 2. Parse | text → template/CSS parse result | the parse record (exists today) | that file's bytes change |
| 3. **Extract** | parse result / host text → `UtilityCandidateSet` | `EquatableArray<UtilityCandidate>` (sorted, de-duped **per file**) | that file's *set of classes* changes — **not** every keystroke |
| 4. **Collect** | all `UtilityCandidateSet` → global `EquatableArray<UtilityCandidate>` (sorted, distinct) | the union set | any file's candidate *set* changes |
| 5. **Theme** | theme `AdditionalText` → `UtilityTheme` record | the parsed theme record | theme file changes |
| 6. **Resolve + emit** | `(global candidates, theme)` → stylesheet string | tuple of the two records above | global set or theme changes |

The decisive property is at **stage 3→4**: because each `UtilityCandidateSet` is sorted and
de-duplicated, adding a *tenth* `bg-blue-500` to a template, or adding a class already used in another
file, produces an **equal** `UtilityCandidateSet` (or an equal global union) — so stages 4–6 **do not
re-run**. Regeneration happens only when the *distinct set* of utilities in the project actually
changes. Editing a component's C# logic, or its `@style` block, never touches the utility pipeline at
all (different stages, different keys).

The `Collect()` at stage 4 is the pipeline's one deliberate fan-in (all files → one set). That is
inherent to "one global stylesheet," and it is cheap: a string-set union over already-small per-file
sets, keyed so it is skipped when no per-file set changed.

### 6.2 Expected build-time impact

- **Extraction** is O(class-attribute characters) per file, run only for changed files — comparable to
  the existing template walk, and strictly less than the render-function compile already in the
  pipeline.
- **Resolution + emit** is O(distinct candidates) — for a typical app (a few hundred distinct
  utilities) this is sub-millisecond string work; the dominant cost is serialization, which is linear.
- **Warm incremental edits** that don't change the class set cost **nothing** past stage 3 (cache
  hit). Edits that add a genuinely new utility re-run only stages 4–6 over the whole (small) set.
- The **`ViuBundleCss`** task is gated by MSBuild `Inputs`/`Outputs`, so a no-op build does no CSS
  file work.
- A **budget** to hold the line (validated by the [#95](https://github.com/assimalign/viu/issues/95)
  size/AOT gates, extended per V01.01.12.16): full generation for the reference sample stays within a
  low-tens-of-milliseconds share of the build, and the runtime WASM payload gains **zero** bytes of
  CSS-generation code (only the static `.css` asset, which is not in the WASM module).

---

## 7. Work-item breakdown

Proposed as **sibling features under area [V01.01.12] Framework - Tooling**
([#89](https://github.com/assimalign/viu/issues/89)) — the parent area of this scoping feature
[V01.01.12.10]. The existing Tooling features run `.01`–`.10`
(`.08` = [#104](https://github.com/assimalign/viu/issues/104),
`.09` = [#125](https://github.com/assimalign/viu/issues/125),
`.10` = [#129](https://github.com/assimalign/viu/issues/129), this item), so the next free feature
codes are **`.11`+**. Tasks under a feature take `<feature>.NN`. Codes and metadata are **proposals**
for the orchestrator to validate against the live board before filing via the
[`viu-work-items` skill](../.claude/skills/viu-work-items/SKILL.md); the skill computes the next
free code at creation time.

**Wave/priority rationale.** Utility CSS is DX/ecosystem polish, off the W01–W03 rendering/compiler
critical path. The **shared bundling task and the Css construction surface are W04 / P004** because
scoped CSS (W04) also wants bundling and they unblock everything else. The engine proper lands **W05**
(DX), with `@apply` and scoped-utility polish in **W06**.

**Dependency spine.** `.11` (Css construction) and `.12` (bundling) → `.13` (grammar) → `.14` (theme)
+ `.15` (extraction) → `.16` (resolver + pipeline, the capstone) → `.17` (`@apply`, later).

### Feature index

| Code | Title | Wave | Priority | Depends on |
| --- | --- | --- | --- | --- |
| `V01.01.12.11` | Implement the CSS construction and deterministic emission surface in Assimalign.Viu.Syntax.Css | W04 | P004 | #60 |
| `V01.01.12.12` | Implement the ViuBundleCss MSBuild task for static-web-asset CSS bundling | W04 | P004 | #58, #60 |
| `V01.01.12.13` | Implement the utility-class candidate grammar and variant model | W05 | P005 | #127, `.11` |
| `V01.01.12.14` | Implement the AOT-safe utility theme configuration model | W05 | P005 | `.13` |
| `V01.01.12.15` | Implement the build-time utility candidate extraction pass | W05 | P005 | #127, #51, `.13` |
| `V01.01.12.16` | Implement the utility-to-CSS resolver and incremental generation pipeline | W05 | P005 | `.11`, `.12`, `.13`, `.14`, `.15` |
| `V01.01.12.17` | Implement `@apply` and utility composition inside `@style` blocks | W06 | P006 | `.16`, #62 |

Each feature body below is **file-ready** (repo issue-body standard: `## Summary` →
`## Acceptance Criteria` → `### Standards and Compliance` → `### Architectural boundaries`, per
[#62](https://github.com/assimalign/viu/issues/62)/[#129](https://github.com/assimalign/viu/issues/129)).
Representative child tasks are listed for the two largest features (`.15`, `.16`).

---

### [V01.01.12.11] Implement the CSS construction and deterministic emission surface in Assimalign.Viu.Syntax.Css

**Wave** W04 · **Priority** P004 · **Parent** [V01.01.12] (#89) · **Depends on** #60

```markdown
## Summary

Add a programmatic CSS *construction* surface to `Assimalign.Viu.Syntax.Css`: build
`CssStylesheetNode`/`CssQualifiedRuleNode`/`CssDeclarationNode`/`CssAtRuleNode` graphs from code (not
only by parsing text) and serialize them with the existing deterministic canonical serializer. The
scoped-CSS work ([V01.01.06.04], #60) rewrites an already-parsed tree, so it never needed to *create*
rules; the utility-first CSS engine ([V01.01.12.16]) generates rules from scratch and needs this. The
surface stays language-agnostic — it knows nothing about utilities — so the Css library remains a leaf
in the cluster.

## Acceptance Criteria

- A builder/factory surface constructs qualified rules, declarations, and conditional-group at-rules
  (`@media`) as the existing record-graph node types, with the base cluster's exact-slice
  `SourceLocation` invariant either satisfied or explicitly marked synthetic for generated nodes (a
  documented, tested divergence — generated nodes have no source span).
- Constructed graphs serialize through the same canonical serializer path used by `CssScopedRewriter`,
  producing byte-identical output for identical input (the incremental-cache contract).
- Declarations and rules compare by value; two graphs built from the same inputs are equal and
  equally hashed.
- Serialization order is deterministic and documented (property order within a rule; rule/media order
  across a stylesheet).
- Tests pin construction + serialization against fixed expected CSS text, including nested `@media`.

### Standards and Compliance

- CSS Syntax Module Level 3: https://www.w3.org/TR/css-syntax-3/
- Reuses the deterministic serializer documented in
  `libraries/Assimalign.Viu.Syntax.Css/docs/DESIGN.md`.

### Architectural boundaries

- Project: `Assimalign.Viu.Syntax.Css` (netstandard2.0, analyzer-host-safe). References only
  `Assimalign.Viu.Syntax`. No I/O, no reflection, no dynamic codegen; recoverable, never throws except
  `OperationCanceledException`.
- The surface must not reference any other language library, and must not introduce any
  utility/Tailwind concept into Css — it is generic CSS construction.
```

---

### [V01.01.12.12] Implement the ViuBundleCss MSBuild task for static-web-asset CSS bundling

**Wave** W04 · **Priority** P004 · **Parent** [V01.01.12] (#89) · **Depends on** #58, #60
· *Shared bundling route with scoped CSS — discharges the deferred bundling non-goal in
[V01.01.06.04].*

```markdown
## Summary

Implement the MSBuild task that writes generated CSS into `dotnet publish` output as bundled
`StaticWebAsset`s, resolving the RS1035 limitation that a Roslyn source generator emits C# and cannot
perform `System.IO`. `Assimalign.Viu.Syntax.Css`'s DESIGN records this as an "MSBuild-task follow-up on
the [V01.01.06.02] pipeline side"; this task is that follow-up and is **shared** by scoped CSS
([V01.01.06.04]) and the utility-first CSS engine ([V01.01.12.16]). The task runs outside the analyzer
sandbox (I/O permitted), reuses the same netstandard2.0 generation core the incremental generator uses
(so the physical file and the generated constant are byte-identical), collects the utility stylesheet
plus per-component scoped `ExtractedStyles`, writes them under `obj/…/viu/`, and registers them as
static web assets so the WASM SDK serves and fingerprints them. Zero runtime CSS work.

## Acceptance Criteria

- A `Microsoft.Build.Utilities.Task` (`ViuBundleCss`) collects (a) the project's utility stylesheet
  and (b) per-component scoped `ExtractedStyles`, and writes bundle file(s) under the intermediate
  output directory.
- The bundle(s) are added to `@(StaticWebAsset)` so `dotnet build`/`publish` copy them to
  `wwwroot/_content/<package>/…` with the standard fingerprinting; a WASM sample loads the bundle via
  `<link>` and renders styled with **no runtime CSS generation**.
- The task honors MSBuild `Inputs`/`Outputs` incrementality: a build with no changed `.viu`/host/theme
  input performs no CSS file work.
- Host-page `<link>` injection into `index.html` is automatic with an opt-out property; an explicit
  reference path is documented.
- The task and its targets/props live in `build/` (central build system), wired by-name; no per-project
  csproj duplication.
- Tests/CI verify the emitted asset exists, is byte-identical to the generator's constant, and is
  skipped on a no-op rebuild.

### Standards and Compliance

- RS1035 (banned analyzer APIs):
  https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/rs1035
- ASP.NET Core / Blazor static web assets model (the SDK mechanism reused).
- Reuses the emission documented in `libraries/Assimalign.Viu.Syntax.Css/docs/DESIGN.md` and the
  generator pipeline in [V01.01.06.02] (#58).

### Architectural boundaries

- Owned by Tooling ([V01.01.12]); the task project targets the MSBuild-task TFM and references the
  netstandard2.0 engine core, never the runtime. I/O is confined to this task (outside the analyzer
  sandbox) — the generator itself still performs no I/O.
- No reflection-based serialization, no dynamic codegen. Deterministic output only.
- Shared contract: scoped CSS and utility CSS both feed this one task; it must not embed
  utility-specific logic (it bundles opaque CSS strings the core produced).
```

---

### [V01.01.12.13] Implement the utility-class candidate grammar and variant model

**Wave** W05 · **Priority** P005 · **Parent** [V01.01.12] (#89) · **Depends on** #127, `.11`

```markdown
## Summary

Implement the parser that turns a raw utility token (`md:hover:bg-blue-500/50`, `w-[32px]`, `!mt-4`)
into a value-equatable structured candidate — `variants[] + utility-root + value|arbitrary + opacity +
important` — plus the data-driven variant model (responsive/state/pseudo/dark/structural). Follows
Tailwind's candidate structure (see `docs/UTILITY-CSS-DESIGN.md` §3). Grammar only: no theme lookup, no
CSS emission. Unparseable tokens are simply "not a utility" (dropped), matching Tailwind. This is the
build-time core the extractor ([V01.01.12.15]) and resolver ([V01.01.12.16]) both build on.

## Acceptance Criteria

- A `UtilityCandidateParser` parses variants (colon-separated, order-significant), utility root, value
  or arbitrary `[…]` value (underscore→space, escaped underscore literal, balanced-bracket check),
  `/opacity` modifier, and leading `!important`, into a value-equatable record.
- The variant model is table-driven: responsive (`sm/md/lg/xl/2xl`), state (`hover/focus/…`),
  pseudo-element (`before/after/…`), `dark`, structural (`first/last/odd/even`). Adding a variant is a
  table entry, not a grammar change.
- Deferred variants (arbitrary `[&…]`, group/peer) are explicitly rejected/ignored with a documented
  reason, not silently mis-parsed.
- Parsing is recoverable and never throws (except `OperationCanceledException`); malformed tokens do
  not fail the build.
- Tests pin representative tokens (including edge cases: escaped underscore, opacity on non-color,
  stacked variants) against expected parses.

### Standards and Compliance

- Tailwind utility/variant syntax: https://tailwindcss.com/docs (pin representative cases in tests).
- Value-equality/recoverability contract: `libraries/Assimalign.Viu.Syntax/docs/DESIGN.md`.

### Architectural boundaries

- Project: the Tooling-owned engine core (netstandard2.0 analyzer-host library; exact packaging per
  `docs/UTILITY-CSS-DESIGN.md` OQ-2). References `Assimalign.Viu.Syntax` (base) only at this layer — no
  CSS or theme dependency in the grammar itself.
- Static tables only; no reflection, no dynamic codegen, no I/O.
```

---

### [V01.01.12.14] Implement the AOT-safe utility theme configuration model

**Wave** W05 · **Priority** P005 · **Parent** [V01.01.12] (#89) · **Depends on** `.13`

```markdown
## Summary

Implement the theme model that supplies the scales the resolver indexes (screens, colors, spacing,
fontSize, fontFamily, borderRadius, an `extend` merge block, `darkMode` strategy, and a `safelist`).
Viu cannot execute a `tailwind.config.js` (no `new Function`, AOT/trimming-safe only), so the theme
is a **static declarative `AdditionalFile`** parsed as data — the engine's central, principled
divergence from Tailwind (see `docs/UTILITY-CSS-DESIGN.md` §3.4/§3.5). A built-in default theme
(Tailwind's default scale, transcribed once as a compiled table) applies when no config is present.

## Acceptance Criteria

- The theme is parsed from an `AdditionalText` (no I/O beyond the additional file) into a
  value-equatable `UtilityTheme` record; a malformed theme reports a Viu diagnostic and falls back to
  the default (never throws, never fails hard).
- A compiled default theme yields working output with no config file present.
- `extend` merges onto the default (does not replace); `darkMode` selects class vs. media strategy;
  `safelist` entries are always generated even when never scanned (the dynamic-class escape hatch).
- The theme record's value-equality invalidates only the resolve/emit stages, not extraction (pin with
  an incremental test).
- No executable config, no reflection: the parsed theme is data only.

### Standards and Compliance

- Tailwind theme configuration: https://tailwindcss.com/docs/theme (structure followed; JS execution
  deliberately not).
- AOT/trimming founding decision: `docs/PLAN.md`; no-dynamic-codegen rule:
  `.claude/rules/general-rules.md`.

### Architectural boundaries

- Project: the Tooling-owned engine core (netstandard2.0). References `Assimalign.Viu.Syntax` (base).
- Parsed as data, never evaluated as code. Static default tables only. No I/O outside `AdditionalFiles`.
```

---

### [V01.01.12.15] Implement the build-time utility candidate extraction pass

**Wave** W05 · **Priority** P005 · **Parent** [V01.01.12] (#89) · **Depends on** #127, #51, `.13`

```markdown
## Summary

Implement the JIT content scanner: extract utility-class candidates from template class attributes,
`.viu` blocks, and host pages, producing a value-equatable per-file `UtilityCandidateSet`. Wired at a
Tooling-owned composition root on the [V01.01.05.09] (#127) registration seam — host pages register a
`SyntaxParser` scanner directly; `.viu` template class attributes are read from the already-dispatched
`TemplateSyntaxParser` AST (a single `@template` node dispatches to one parser, so extraction consumes
that parse rather than re-registering — see `docs/UTILITY-CSS-DESIGN.md` §2.2). Only statically
determinable classes are extractable (Tailwind parity); dynamic classes use the theme `safelist`.

## Acceptance Criteria

- Static `class="…"` attribute tokens and the literal-string operands of `:class`/`v-bind:class` are
  extracted from parsed `.viu` templates (walk `ElementNode`→`AttributeNode`/`DirectiveNode`).
- Host HTML pages supplied as `AdditionalFiles` are scanned via a `SyntaxParser` registered on the
  aggregate seam by predicate.
- Output is a per-file `UtilityCandidateSet` record with a sorted, de-duplicated
  `EquatableArray<UtilityCandidate>`; identical class sets yield equal, equally-hashed results
  (incremental-cache contract) — pin that adding a duplicate/existing class does not invalidate
  downstream stages.
- The extractor depends only on the base and the template AST — no CSS, theme, or resolver dependency.
- Tests cover template extraction, host-page extraction, de-duplication, and cache stability.

### Standards and Compliance

- Tailwind JIT content scanning: https://tailwindcss.com/docs/content-configuration.
- Registration seam + value-equality: `libraries/Assimalign.Viu.Syntax/docs/DESIGN.md`; template AST:
  [V01.01.05.01] (#48)/[V01.01.05.04] (#51).

### Architectural boundaries

- Project: the Tooling-owned engine core / composition root. The scanner is a `SyntaxParser` rooting on
  `Assimalign.Viu.Syntax`; registration happens at the composition root, never in a language library.
- No I/O outside `AdditionalFiles`; no reflection; value-equatable results only.

### Child tasks (proposed)
- [V01.01.12.15.01] Extract candidates from parsed `.viu` template class attributes and `:class`
  literal operands.
- [V01.01.12.15.02] Register and implement the host-page (`.html`) class scanner on the aggregate seam.
- [V01.01.12.15.03] Per-file `UtilityCandidateSet` value-equality + incremental-cache tests.
```

---

### [V01.01.12.16] Implement the utility-to-CSS resolver and incremental generation pipeline

**Wave** W05 · **Priority** P005 · **Parent** [V01.01.12] (#89) · **Depends on** `.11`, `.12`, `.13`,
`.14`, `.15` — *capstone*

```markdown
## Summary

Implement the resolver that maps parsed candidates ([V01.01.12.13]) to CSS declarations using the
compiled utility tables and theme ([V01.01.12.14]), builds rules via the Css construction surface
([V01.01.12.11]), de-duplicates and deterministically orders them into one stylesheet, and wires the
whole thing into the `IIncrementalGenerator` pipeline with a `Collect()` fan-in over per-file candidate
sets ([V01.01.12.15]). Emits the stylesheet as a build-visible constant/diagnostic in the generator and
hands it to the `ViuBundleCss` task ([V01.01.12.12]) for physical static-web-asset emission. Ships the
core utility families first (spacing, color, typography, sizing, flex/grid, border, background), with
the catalog extensible by table. Zero runtime CSS work.

## Acceptance Criteria

- Candidates resolve to CSS declarations via compiled utility tables + theme scales; variants wrap the
  base rule in the correct selector suffix or `@media`; arbitrary values and opacity modifiers emit
  correctly.
- Output is one deterministic, de-duplicated, ordered stylesheet (a class used N times emits one rule);
  byte-stable for unchanged input.
- Pipeline stages are value-equatable; a warm edit that does not change the project's distinct class
  set performs no work past extraction (pin with incremental tests); the `Collect()` fan-in recomputes
  only when the global set changes.
- The generator surfaces the stylesheet (constant + diagnostics) with no I/O; `ViuBundleCss` writes
  the physical asset; a WASM sample renders styled from the bundle with no runtime CSS generation.
- Core utility families are covered with tests pinning representative classes against expected CSS
  (Tailwind-referenced); the build-time cost stays within the stated budget (validated by the
  [V01.01.12.06] (#95) size/AOT gates).

### Standards and Compliance

- Tailwind utility semantics + default scale: https://tailwindcss.com/docs (pin per family in tests).
- Roslyn incremental generators:
  https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview.
- CSS Syntax L3: https://www.w3.org/TR/css-syntax-3/.

### Architectural boundaries

- Project: resolver/generation in the Tooling-owned engine core; pipeline wiring in
  `Assimalign.Viu.Generators.Syntax` (composition root). References `Assimalign.Viu.Syntax.Css` for
  emission — allowed because it is the composition root, not a peer language library. Css never
  references back.
- netstandard2.0; no I/O in the analyzer (bundling is [V01.01.12.12]'s task); no reflection; no dynamic
  codegen; trimming/WASM/NativeAOT-safe (nothing ships to the runtime).

### Child tasks (proposed)
- [V01.01.12.16.01] Compiled core-utility tables (spacing, color, typography, sizing, flex/grid,
  border, background).
- [V01.01.12.16.02] Variant application (selector-suffix and `@media` wrapping) over resolved rules.
- [V01.01.12.16.03] De-duplication + deterministic ordering + single-stylesheet assembly.
- [V01.01.12.16.04] Incremental-generator wiring (`Collect()` fan-in, per-stage cache keys) + cache
  tests.
- [V01.01.12.16.05] End-to-end WASM sample + build-time budget check against [V01.01.12.06] (#95).
```

---

### [V01.01.12.17] Implement `@apply` and utility composition inside `@style` blocks

**Wave** W06 · **Priority** P006 · **Parent** [V01.01.12] (#89) · **Depends on** `.16`, #62

```markdown
## Summary

Add `@apply` support so an authored `@style` rule can inline utilities (`@apply p-4 bg-blue-500;`),
the one place the utility engine and the SFC style pipeline directly intersect. Deferred out of the v1
engine to keep boundaries clean; needs the `@style` compilation path ([V01.01.06.04]/[V01.01.06.06]) to
call the utility resolver ([V01.01.12.16]) mid-compile. Also the home for other deferred niceties
(arbitrary variants, group/peer) if pulled in.

## Acceptance Criteria

- `@apply <utilities>;` inside a `@style` block expands to the resolved declarations inline, composing
  with `scoped` rewriting and CSS Modules hashing on the same block (deterministic, tested).
- Unknown utilities in `@apply` report a Viu diagnostic on the `.viu` coordinate.
- Composition order with scoped/module transforms is defined and tested; hashes remain deterministic
  and consistent with the [V01.01.06.04] scheme.
- Trimming/WASM/NativeAOT-safe; no runtime CSS work.

### Standards and Compliance

- Tailwind `@apply`: https://tailwindcss.com/docs/functions-and-directives#apply.
- Composes with Vue SFC CSS features: https://vuejs.org/api/sfc-css-features.html ([V01.01.06.06], #62).

### Architectural boundaries

- CSS-side expansion reuses `Assimalign.Viu.Syntax.Css` + the utility resolver at the composition root;
  the `@style` pipeline calls the resolver — the resolver never calls the `@style` pipeline.
- netstandard2.0; no I/O outside `AdditionalFiles`; no reflection; no dynamic codegen.
```

---

## 8. Open questions

Listed explicitly rather than resolved by assumption; each names a recommended default so
implementation is not blocked.

- **OQ-1 — `.viu` template extraction path.** Primary design consumes the already-dispatched template
  AST rather than registering a competing scanner on the `@template` block (first-match-wins gives a
  block to one parser). *Alternative*: a standalone utility-extraction aggregate that slices the `.viu`
  and registers its own template-block scanner — literal to the seam-registration language but
  double-parses the template. **Recommendation:** consume the dispatched AST (cheaper, still seam-faithful).
  Validate no ordering assumption is violated when both the SFC generator and a future utility generator
  run over the same compilation.

- **OQ-2 — Engine-core packaging.** Fold the netstandard2.0 engine core into
  `Assimalign.Viu.Generators.Syntax`, or ship it as a dedicated analyzer-layer library the generator
  *and* the `ViuBundleCss` task both reference? **Recommendation:** a dedicated library — the MSBuild
  task must reuse the core outside the analyzer sandbox, and a separate library gives clean test
  isolation and a single generation implementation. Confirm the exact assembly name/location with the
  layout owner (it is Tooling/composition code, deliberately **not** a peer `Assimalign.Viu.Syntax.*`
  language library, so it may reference Css).

- **OQ-3 — Theme document format.** JSON (`utility.theme.json`) vs. a strongly-shaped C#
  `[UtilityTheme]` partial (source-generated) vs. a bespoke declarative file. **Recommendation:** JSON
  `AdditionalFile` for v1 (no new grammar, editor-friendly, trivially value-equatable); revisit a typed
  C# form if compile-time validation of theme references becomes valuable.

- **OQ-4 — v1 language scope.** Arbitrary variants (`[&>*]:`), group/peer variants, arbitrary
  *properties* (`[mask-type:luminance]`), trailing `!`, and `@apply` are deferred. Confirm this v1/v2
  line; each is additive but widens the grammar/selector machinery.

- **OQ-5 — Extension model.** Tailwind plugins are executable JS — forbidden. The proposed replacement
  is statically-registered/compiled utility tables (a project contributes a table, never code).
  Confirm this is the desired extension story and whether it needs a source generator of its own.

- **OQ-6 — Scoped-utility mode.** Default is global utilities (Tailwind parity). Is per-component
  scoped-utility emission (reusing `CssScopedRewriter`) wanted at all? **Recommendation:** defer;
  leave the reuse seam noted but unbuilt.

- **OQ-7 — Preflight.** Ship a minimal base reset opt-in by default, or off by default? Payload and
  determinism argue for a small, versioned, opt-out reset. Confirm the default.

- **OQ-8 — Wave/priority placement.** Proposed W04 (shared bundling + Css construction) and W05 (engine)
  / W06 (`@apply`). Validate against the live board and the scoping item's own wave/priority before
  filing.

---

## 9. Acceptance-criteria traceability

Mapping each [#129](https://github.com/assimalign/viu/issues/129) acceptance criterion to where this
document satisfies it.

| #129 criterion | Satisfied in |
| --- | --- |
| Design doc records engine architecture: sources scanned, extraction through the seam at a composition root, generated CSS to publish output as bundled static web assets, no runtime cost | [§2](#2-engine-architecture) (all sub-sections), esp. [§2.1](#21-sources-scanned)/[§2.2](#22-candidate-extraction-through-the-seam)/[§2.4](#24-reaching-publish-output-the-bundling-route) |
| Utility-class language specified — naming, variants/modifiers, arbitrary values, theme config — with explicit follow/diverge decisions vs Tailwind | [§3](#3-the-utility-class-language-specification), decisions table [§3.5](#35-followdiverge-decisions-against-tailwind) |
| Architectural boundaries pinned — owning projects + dependency direction (Css for AST/emission; Tooling composition root; no language library references another), netstandard2.0, no I/O outside `AdditionalFiles`, no reflection/dynamic codegen | [§4](#4-architectural-boundaries) |
| Interplay with SFC style pipeline resolved — compose with scoped CSS (#60) and CSS Modules/`v-bind()` (#62), which rule-level CSS parsing is shared vs owned | [§5](#5-interplay-with-the-single-file-component-style-pipeline) |
| Incrementality designed — per-file value-equatable extraction, cache keys, build-time impact | [§6](#6-incrementality) |
| Feature/task breakdown with WBS, waves, priorities, dependency links, standalone bodies (filed on Project #15 by the orchestrator) | [§7](#7-work-item-breakdown) — file-ready bodies incl. the shared MSBuild bundling task ([V01.01.12.12]) |
| Honest open questions | [§8](#8-open-questions) |

The final #129 criterion — *filing* the breakdown on Project #15 via the `viu-work-items` skill — is
handled by the orchestrating session at review time; [§7](#7-work-item-breakdown) is the complete,
file-ready input for it.
