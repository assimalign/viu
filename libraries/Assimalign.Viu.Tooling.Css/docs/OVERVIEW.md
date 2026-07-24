# Assimalign.Viu.Tooling.Css — overview

The shared, build-time CSS composition core for `.viu` `@style` blocks. It compiles a component's
styles (scoped CSS, CSS Modules, and `v-bind()` rewrites) and bundles a project's components into one
deterministic stylesheet. It exists so the two build-time hosts that need this logic — the
`Assimalign.Viu.Generators.Syntax` source generator (which emits the styles as a C# constant) and the
`ViuBundleCss` MSBuild task (which writes the physical file) — run **one** implementation and cannot
drift. Area: `V01.01.12.12`.

This is tooling / composition-root code, not a peer `Assimalign.Viu.Syntax.*` language library — the
reason it is *allowed* to reference several language libraries. The rationale, the RS1035 / no-I/O
constraint, and the byte-stable bundle layout are in [DESIGN.md](DESIGN.md); the broader utility-CSS
plan is [`docs/UTILITY-CSS-DESIGN.md`](../../../docs/UTILITY-CSS-DESIGN.md).

## Public surface

- **`SingleFileComponentStyleCompiler`** — the compilation itself: `Compile(parse, scopeId)` (for the
  generator, which already holds the parse) and `CompileFile(parser, text, path, projectDirectory)`
  (for the task). Returns a `SingleFileComponentStyleCompilation`.
- **`SingleFileComponentStyleBundler`** — composes a project's components into one deterministic
  bundle string (stable ordering, LF-only layout). Pure: the caller performs file I/O and hands in
  the already-read text via `SingleFileComponentStyleInput`.
- **`StyleScopeId`** — the `data-v-<hash>` scope-id derivation both hosts resolve identically.
- **`SingleFileComponentParserFactory`** — the shared `.viu` parser composition (`Create()` /
  `CreateForStyleExtraction()`), so the task can parse only `@style` without loading the template
  compiler.
- **Result and input types** — `SingleFileComponentStyleCompilation`,
  `SingleFileComponentStyleDiagnostic`, `SingleFileComponentStyleInput`,
  `SingleFileComponentStyleModuleClass`, `SingleFileComponentStyleVariableBinding`.

## Boundaries

- References `Assimalign.Viu.Syntax`, `.Syntax.SingleFileComponent`, `.Syntax.Templates`, and
  `.Syntax.Css` — legal because this is a composition root, not a language library (a language
  library may not reference another; a composition root may).
- **No I/O, no reflection, no dynamic codegen.** The core is analyzer-sandbox-safe (netstandard2.0
  analyzer TFM); the `ViuBundleCss` task performs the file I/O outside the sandbox.
- **Deterministic, byte-stable output** (LF, ordinal ordering) — the guarantee that the generated
  constant and the bundled file are byte-identical.
