---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# Pre-completion checklist

Run this before declaring any non-trivial change complete. Mark each applicable item ✅ or ❌; if anything
is ❌, fix it before reporting completion, not after. Mark genuinely inapplicable items N/A and move on.

## Build & test
- [ ] `dotnet build Assimalign.Viu.slnx` succeeds with **0 warnings, 0 errors**.
- [ ] Affected `dotnet test` projects pass; new behavior has tests (run counts pinned for reactive/caching
      semantics).
- [ ] For runtime/interop changes, the sample WASM app still builds.

## Structure & naming
- [ ] Public interfaces are in `Abstraction/`; internal types (incl. internal interfaces) in `Internal/`;
      public non-interface types at `src/` root.
- [ ] One public type per file; filename = type name; generics use `{T}` (no `OfT`).
- [ ] Whole words, no abbreviations (acronyms DOM/HTML/CSS/SSR/AOT excepted).
- [ ] File-scoped namespace == assembly name (flat); folders don't leak into namespaces.
- [ ] Explicit, ordered usings (System → third-party → Assimalign); no implicit/global usings.

## Build system
- [ ] Project/package refs use `ViuProjectReference` / `ViuPackageReference` (no raw refs, no inline
      versions); shared settings come from `build/`, not the csproj.
- [ ] Shipping libraries set `IsAotCompatible=true`; tests set `IsPackable=false`.

## Correctness & docs
- [ ] Trimming/WASM-AOT-safe (no reflection serialization, no dynamic codegen); JS handles/listeners
      cleaned up.
- [ ] Public APIs have XML docs; behavior mirroring Vue 3 links the upstream reference.
- [ ] The work item ([V01.01.NN…]) is referenced; scope creep captured via the `viu-work-items` skill.
- [ ] No dangling solution/project references after any rename or move.
