# ADR-0001: Roslyn source generators over reflection and dynamic code generation

- **Status:** Accepted
- **Date:** 2026-07-19 (foundational C#/WASM premise; formally recorded under [V01.01.13.01], #98)
- **Scope:** Repo-wide — every `Assimalign.Viu.*` runtime library and every build-time generator.

## Context

Viu targets the browser through the .NET WebAssembly build tools, which is AOT and trimming
territory (mono-wasm today, NativeAOT-shaped constraints throughout). Two pillars of Vue 3 are
unavailable or unsafe there:

- Vue models `reactive(obj)` with a JavaScript [`Proxy`](https://vuejs.org/guide/extras/reactivity-in-depth.html),
  which has no AOT-safe .NET equivalent (a `DispatchProxy`/`Reflection.Emit` analogue is dynamic
  codegen).
- Vue's full build compiles templates at runtime with `new Function` (see
  [ADR-0005](0005-no-runtime-template-compilation.md)) — impossible in WASM.

Reflection-based serialization, `Reflection.Emit`, compiled expression trees, and linker-unfriendly
activation (`Activator.CreateInstance` over discovered types) are all trimming-hostile: the linker
cannot prove what stays reachable, so it either bloats output or breaks at runtime.

## Decision

**Roslyn source generators are the sanctioned mechanism for everything Vue achieves with `Proxy` or
runtime `new Function`.** Concretely, repo-wide:

- No reflection-based serialization, no dynamic code generation, no linker-unfriendly activation
  paths. Shipping libraries set `<IsAotCompatible>true</IsAotCompatible>`.
- Compile-time metaprogramming is a Roslyn incremental source generator. Component and reactive
  metadata are emitted at build time, never discovered by reflection at runtime; component
  factories are source-generated, never reflection-activated.

Deviating from AOT/trimming safety requires explicit confirmation and a documented, test-pinned
exception (see [`.claude/rules/deviations.md`](../../.claude/rules/deviations.md)).

## Consequences

- `reactive()` becomes `[Reactive]`/`[ShallowReactive]` partial classes whose property wrappers are
  emitted by `Assimalign.Viu.Reactivity.Generators` (see [ADR-0002](0002-ref-first-reactivity.md)).
- `.viu` single-file components and templates compile through `Assimalign.Viu.Generators.Syntax`
  (see [ADR-0005](0005-no-runtime-template-compilation.md)).
- Generator inputs and outputs must be **value-equatable** so the incremental-generator cache holds
  — this shapes the whole `Assimalign.Viu.Syntax.*` cluster (immutable records, `SyntaxList<T>`; see
  [`Assimalign.Viu.Syntax/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Syntax/docs/DESIGN.md)).
- Generators run in netstandard2.0 analyzer hosts with no file/network I/O and are delivered to SDK
  consumers through the `Assimalign.Viu.App.Ref` targeting pack's `analyzers/dotnet/cs/` (see
  [`docs/PLAN.md`](../PLAN.md) founding decision 8).
- Each area publishes a representative WASM consumer with trimming validation; size and startup
  budgets gate CI from W03.

## Alternatives considered

- **Reflection / dynamic codegen** (`Reflection.Emit`, compiled expression trees, `DispatchProxy`) —
  the most direct port of Vue's `Proxy`, rejected outright: forbidden by the AOT/trimming constraint.
- **MSBuild tasks for all codegen** — rejected for the reactive and template generators because it
  forfeits IDE integration and incrementality. The generators stay Roslyn incremental generators;
  MSBuild tasks are used only where a generator legally cannot act — emitting a *content* file
  (RS1035), as the `ViuBundleCss` task does (`docs/PLAN.md` founding decision 8).

## References

- [`docs/PLAN.md`](../PLAN.md) — founding decisions 2, 3, 6, and 8.
- [`.claude/rules/general-rules.md`](../../.claude/rules/general-rules.md) — the AOT/trimming hard
  constraints.
- Vue 3: [reactivity in depth](https://vuejs.org/guide/extras/reactivity-in-depth.html),
  [rendering mechanism](https://vuejs.org/guide/extras/rendering-mechanism.html).
