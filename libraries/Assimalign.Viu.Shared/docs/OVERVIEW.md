# Assimalign.Viu.Shared — overview

The dependency-free base of the framework. It holds
the bitmask vocabulary the compiler and runtime both speak, the class/style/display normalization
helpers, and the DOM knowledge tables. It references no other `Assimalign.Viu.*` library; everything
else in the framework sits above it. Area: `V01.01.01`.

## What it contains

- **The flag vocabulary** (currency types, `src/` root):
  - **`PatchFlags`** (+ `PatchFlagsExtensions`, `PatchFlagNames`) — the compiler's patch hints
    telling the runtime what on a node can change (`[RND-FLAGS-1]`–`[RND-FLAGS-3]`).
  - **`ShapeFlags`** (+ `ShapeFlagsExtensions`) — what a node *is* (element / component / text /
    slot children shape), as a bitmask (`[RND-FLAGS-4]`).
  - **`SlotFlags`** — slot stability classification (`[RND-FLAGS-5]`).
- **Normalization** (`Normalization/`):
  - **`StyleAndClassNormalization`** — collapses every accepted `:class` / `:style` binding shape
    (string, nested enumerable, name → truthy dictionary) into the form the host applies.
  - **`DisplayStringFormatter`** — what a `{{ expression }}` interpolation renders to.
  - **`LooseEquality`** — value matching for form bindings, after the coercion an HTML form
    round-trip introduces.
  - **`NumberCoercion`** — prefix and whole-string numeric coercion for DOM string values
    (`v-model.number`).
- **DOM knowledge** (`Dom/DomKnowledge`, backed by `Internal/DomKnowledgeData`) — the HTML, SVG, and
  MathML tag and attribute tables ([V01.01.01.03]) the compiler and DOM runtime consult instead of
  probing a live element.

## Boundaries

- **No `Assimalign.Viu.*` dependencies** — this is the root of the dependency graph. Ships as a
  net10.0 runtime library with `IsAotCompatible=true`.
- The flag definitions are the **contract between the build-time compiler and the runtime**. A few
  source files (`PatchFlags.cs`, `SlotFlags.cs`, `Internal/DomKnowledgeData.cs`) are shared-source
  compiled into the netstandard2.0 `Assimalign.Viu.Syntax.*` generators so both sides use identical
  bit values (their paths are frozen — see
  [`.claude/rules/general-rules.md`](../../../.claude/rules/general-rules.md)).
- Design rationale and the cross-boundary flag contract: [DESIGN.md](DESIGN.md); the normative
  statement is [`docs/SPECIFICATION.md` §6.2](../../../docs/SPECIFICATION.md#62-the-flag-vocabulary).
