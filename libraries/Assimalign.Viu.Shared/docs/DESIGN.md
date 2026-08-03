# Assimalign.Viu.Shared — design

Why the shared base is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). The
normative statement of the flag vocabulary is
[`docs/SPECIFICATION.md` §6.2](../../../docs/SPECIFICATION.md#62-the-flag-vocabulary).

## The flags are a cross-boundary contract

`PatchFlags`, `ShapeFlags`, and `SlotFlags` are not merely enums — they are the vocabulary the
build-time compiler and the runtime share (PLAN founding decision 1). The compiler *stamps* a node
with the flags describing what can change; the runtime *reads* them to patch only that and to skip
static structure. On WASM this is doubly valuable: every patch visit skipped is a JS-interop
round-trip avoided (see [ADR-0003](../../../docs/adr/0003-batched-interop-dom-operations.md)).

Because two independently built artifacts must agree on every bit, the flag values are a **frozen
numeric contract — additive only**: changing a value silently breaks components compiled by an
earlier Viu, which is why the definition files are compiled into
both sides from one source. `PatchFlags.cs` and `SlotFlags.cs` (and `Internal/DomKnowledgeData.cs`)
are `<Compile Include>` links in the netstandard2.0 `Assimalign.Viu.Syntax.*` generator projects.
There is no second copy to drift. Their paths are frozen; moving one means updating every linking
csproj in the same change.

## Pure data and pure functions only

The library holds only bitmask enums, static lookup tables, and pure normalization functions. It has
no render nodes, no reactivity, no renderer state — those belong to the layers above. This is what lets
everything depend on it without a cycle, and what keeps it trivially AOT/trimming-safe.

## Platform adaptations

- The flag enums are `[Flags]` C# enums; the extension methods (`PatchFlagsExtensions`,
  `ShapeFlagsExtensions`) are the named form of the `flags & X` tests the renderer performs on every
  patch visit, and are the only sanctioned way to test them —
  `PatchFlagsExtensions` additionally gates every positive-bit test on `flags > 0` so the negative
  sentinels (`Cached`, `Bail`) can never spuriously satisfy a bitwise check.
- Normalization works over CLR types but must reproduce the coercions a browser form round-trip
  imposes — the DOM hands values back as strings regardless of the model's type, so
  `LooseEquality`, `NumberCoercion`, and `StyleAndClassNormalization` define truthiness and
  value-matching in those terms rather than in CLR terms. Invariant culture throughout is
  mandatory: a culture-sensitive number would make server output and client hydration disagree.
- Naming spells out whole words except the approved acronyms (DOM/HTML/CSS/SSR/AOT/JSON/WASM), so
  identifiers read `DisplayStringFormatter` rather than a `ToDisplayString`-style abbreviation.

## Non-goals

- No runtime types (render nodes, components, effects) — those are `Core`.
- No platform/DOM interop — `DomKnowledge` is static knowledge only; the live DOM bridge is
  `Assimalign.Viu.Browser`.
