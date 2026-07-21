# Assimalign.Viu.Shared — design

Why the shared base is shaped the way it is. What it is: see [OVERVIEW.md](OVERVIEW.md). Upstream
counterpart: [`@vue/shared`](https://github.com/vuejs/core/tree/main/packages/shared).

## The flags are a cross-boundary contract

`PatchFlags`, `ShapeFlags`, and `SlotFlags` are not merely enums — they are the vocabulary the
build-time compiler and the runtime share (PLAN founding decision 1). The compiler *stamps* a vnode
with the flags describing what can change; the runtime *reads* them to patch only that and to skip
static structure. On WASM this is doubly valuable: every patch visit skipped is a JS-interop
round-trip avoided (see [ADR-0003](../../../docs/adr/0003-batched-interop-dom-operations.md)).

Because two independently built artifacts must agree on every bit, the flag values are **pinned
numerically to the upstream `@vue/shared` constants**, and the definition files are compiled into
both sides from one source: `PatchFlags.cs` and `SlotFlags.cs` (and `Internal/DomKnowledgeData.cs`)
are `<Compile Include>` links in the netstandard2.0 `Assimalign.Viu.Syntax.*` generator projects.
There is no second copy to drift. Their paths are frozen; moving one means updating every linking
csproj in the same change.

## Pure data and pure functions only

The library holds only bitmask enums, static lookup tables, and pure normalization functions. It has
no vnodes, no reactivity, no renderer state — those belong to the layers above. This is what lets
everything depend on it without a cycle, and what keeps it trivially AOT/trimming-safe.

## Deltas from Vue 3

- The flag enums are `[Flags]` C# enums with the upstream bit values; the extension methods
  (`PatchFlagsExtensions`, `ShapeFlagsExtensions`) provide the `flag & X` tests upstream writes
  inline in JavaScript.
- Normalization ports `normalizeClass`/`normalizeStyle`/`toDisplayString`/`looseEqual`/`toNumber`
  with C# type handling in place of JavaScript's coercions; the *observable* results track upstream.
- Naming spells out whole words except the approved acronyms (DOM/HTML/CSS/SSR/AOT/JSON/WASM), so
  identifiers read `DisplayStringFormatter`, not `ToDisplayString`-style abbreviations, while the XML
  docs name the upstream function each ports.

## Non-goals

- No runtime types (vnodes, components, effects) — those are `Core`.
- No platform/DOM interop — `DomKnowledge` is static knowledge only; the live DOM bridge is
  `Assimalign.Viu.Browser`.
