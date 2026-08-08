# Viu component-runtime end-state model

This directory is an isolated, compilable model of the adopted end-state boundaries for Viu's
Components, Reactivity, State, and Core libraries. It is intentionally outside the shipping
solution. Nothing under `libraries/`, `tooling/`, `frameworks/`, or `sdks/` consumes these projects.

**The adopted disposition is
[`../docs/COMPONENT-MODEL-PLAN.md`](../docs/COMPONENT-MODEL-PLAN.md)** (§2/§2a in
particular): Components owns the component model — the closed `VirtualNode` algebra *and* the
authored contract (`IComponent`, abstract `ComponentContext`, contract/invocation/bindings,
registration/factory) — referencing only Reactivity; Core is the Application Model, engine, and
public operations (`ComponentHost`/`IComponentRenderScope`, `MountedComponentView<TNode>`);
conventions such as State attach through `context.Services` and the ambient registry and never earn
a context member. Scoped CSS is deferred — no style-scope identity appears anywhere in the model.

Rendering is frame-based: a `ComponentRenderer` receives its mount's `ComponentRenderFrame` — the
per-mount render cache plus block assembly — so there is no ambient render-helper state and no
public static helper class in the end state. The shipping static render-helper surface, the
ambient block sentinel, and the underscore name-binding convention are superseded; the only
name-bound generated-code application binary interfaces that remain are hot-reload registration
through `ComponentHotReload` and Browser's directive vocabulary. Code-first
components are `ComponentRegistration.Define(name, contract, setup)` — composition-only per
[ADR-0004](../docs/adr/0004-composition-only-component-model.md), with no options-object form;
hand-built subtrees carry `RenderPlan.None` and patch by full diff unless the author supplies
plans.

The model answers four questions:

1. Which project owns each abstraction?
2. Which objects are immutable render descriptions, static definitions, live authored instances,
   and mounted runtime state?
3. How do browser rendering, server rendering, and testing use Core without friend access?
4. Which compiler-facing contracts must be public without exposing the compiler's internal model?

The code is a contract model, not a replacement renderer. Pure data contracts and the one-shot
component runtime are executable; browser interop, persistent reconciliation, hydration, and the
compiler pipeline are deliberately represented only by their public boundaries.

## Suggested reading order

1. [`docs/END-STATE.md`](docs/END-STATE.md) — ownership, lifetimes, and runtime flows.
2. [`docs/PROJECT-GRAPH.md`](docs/PROJECT-GRAPH.md) — project dependencies and allowed directions.
3. [`docs/TYPE-MAP.md`](docs/TYPE-MAP.md) — current concepts mapped to the proposed concepts.
4. [`docs/WALKTHROUGH.md`](docs/WALKTHROUGH.md) — one component from definition through SSR cleanup.
5. `libraries/Assimalign.Viu.Components/src/` — the component model: the closed virtual-tree
   algebra plus the authored-component contract.
6. `libraries/Assimalign.Viu.Core/src/` — the engine operations: `ComponentHost`, render scopes,
   mounted views, and the internal runtime context.
7. `libraries/Assimalign.Viu.ServerRenderer/src/` — operation-level server consumption.
8. `tooling/Assimalign.Viu.Compiler.SingleFileComponent/src/` — the public tooling facade.

## Build

```powershell
dotnet restore .redesign/Assimalign.Viu.Redesign.slnx
dotnet build .redesign/Assimalign.Viu.Redesign.slnx --no-restore -warnaserror
dotnet test .redesign/Assimalign.Viu.Redesign.slnx --no-build --no-restore
```

The local reference resolver consumes every `ViuProjectReference` before the repository resolver
runs. An unresolved name fails the build instead of silently binding to a shipping project with the
same assembly name.

## Non-goals

- This is not an approved specification change.
- It does not alter the current public-surface hardening plan.
- It does not implement the persistent renderer, block patcher, browser interop, or hydration.
- It does not introduce one reactive effect per compiler block. The intended baseline remains one
  effect per mounted authored component plus block-local patching.
- It does not preserve the current public surface through compatibility shims.
