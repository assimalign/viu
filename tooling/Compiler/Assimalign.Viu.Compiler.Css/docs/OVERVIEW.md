# Assimalign.Viu.Compiler.Css — overview

The shared, build-time CSS composition core for `.viu` `<style>` blocks (and, during the
[V01.01.06.10] transition window, legacy `@style` blocks). It compiles a component's
ordinary styles, CSS Modules, and `v-bind()` rewrites and bundles a project's components into one
deterministic stylesheet. It exists so the two build-time hosts that need this logic — the
`Assimalign.Viu.Generators.Syntax` source generator (which emits the styles as a C# constant) and the
`ViuBundleCss` MSBuild task (which writes the physical file) — run **one** implementation and cannot
drift. Area: `V01.01.12.12`.

This is tooling / composition-root code, not a peer `Assimalign.Viu.Syntax.*` language library — the
reason it is *allowed* to reference several language libraries. The rationale, the RS1035 / no-I/O
constraint, and the byte-stable component-style bundle layout are in [DESIGN.md](DESIGN.md). The
former utility-CSS integration is retained only as standalone, non-normative add-on history in
[`docs/UTILITY-CSS-DESIGN.md`](../../../../docs/UTILITY-CSS-DESIGN.md); it no longer defines or
participates in this component-CSS pipeline.

## Public surface

- **`SingleFileComponentStyleCompiler`** — the compilation itself: `Compile(parse, localHashSalt)` (for the
  generator, which already holds the parse) and `CompileFile(parser, text, path, projectDirectory)`
  (for the task). Returns a `SingleFileComponentStyleCompilation`.
- **`SingleFileComponentStyleBundler`** — composes a project's components into one deterministic
  bundle string (stable ordering, LF-only layout). Pure: the caller performs file I/O and hands in
  the already-read text via `SingleFileComponentStyleInput`.
- **`CssComponentHash`** — the stable path hash used only to salt module and binding names.
- **`SingleFileComponentParserFactory`** — the shared `.viu` parser composition (`Create()` /
  `CreateForStyleExtraction()`), so the task can parse only style blocks without loading the template
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

Scoped CSS was removed on 2026-09-14 by owner decision [V01.01.06.17]
([#367](https://github.com/assimalign/viu/issues/367)). The compiler carries no scope identifier,
performs no selector scope rewrite, and emits no scope attribute. Parser code 1018 rejects `.viu`
scoped options; parser code 1019 warns for `.vue`, whose style content compiles as an ordinary
global stylesheet. The generator reports these as `VIU1001` Error and `VIU1002` Warning,
respectively. Plain styles, CSS Modules, bundling, library packing, and hot reload remain.
