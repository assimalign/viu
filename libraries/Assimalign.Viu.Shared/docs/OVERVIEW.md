# Assimalign.Viu.Shared — overview

The dependency-free base of the framework — the role
[`@vue/shared`](https://github.com/vuejs/core/tree/main/packages/shared) plays for Vue 3. It holds
the bitmask vocabulary the compiler and runtime both speak, the class/style/display normalization
helpers, and the DOM knowledge tables. It references no other `Assimalign.Viu.*` library; everything
else in the framework sits above it. Area: `V01.01.01`.

## What it contains

- **The flag vocabulary** (currency types, `src/` root):
  - **`PatchFlags`** (+ `PatchFlagsExtensions`, `PatchFlagNames`) — the compiler's patch hints
    telling the runtime what on a vnode can change ([`@vue/shared` `patchFlags.ts`](https://github.com/vuejs/core/blob/main/packages/shared/src/patchFlags.ts)).
  - **`ShapeFlags`** (+ `ShapeFlagsExtensions`) — what a vnode *is* (element / component / text /
    slot children shape), as a bitmask ([`shapeFlags.ts`](https://github.com/vuejs/core/blob/main/packages/shared/src/shapeFlags.ts)).
  - **`SlotFlags`** — slot stability classification ([`slotFlags.ts`](https://github.com/vuejs/core/blob/main/packages/shared/src/slotFlags.ts)).
- **Normalization** (`Normalization/`):
  - **`StyleAndClassNormalization`** — `normalizeClass` / `normalizeStyle` (string/array/map forms).
  - **`DisplayStringFormatter`** — `toDisplayString` (interpolation stringification).
  - **`LooseEquality`** — `looseEqual` / `looseIndexOf`.
  - **`NumberCoercion`** — `toNumber` / `looseToNumber`.
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
- Design rationale and the parity contract: [DESIGN.md](DESIGN.md).
