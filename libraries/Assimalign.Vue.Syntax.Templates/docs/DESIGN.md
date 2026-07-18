# Assimalign.Vue.Syntax.Templates — design

Why the template compiler front end is shaped the way it is, and the deliberate C#/WASM divergences from
`vuejs/core` v3.5. This document accumulates per feature; today it covers expression binding and scope
analysis ([V01.01.05.04], issue #51).

## Expression binding and scope analysis (`transformExpression`)

The C# port of `@vue/compiler-core`'s
[`transforms/transformExpression.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/transforms/transformExpression.ts)
plus the `BindingTypes` classification from
[`options.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/options.ts). Where Vue
parses expression bodies with `@babel/parser`, Vuecs parses them with
`Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression` — a generator-only dependency added through the
central build system (`build/Targets/Build.References.Packages.targets`), never referenced by any runtime
library, so it never reaches the WASM app.

### Pipeline placement and gating

`TransformExpression` is inserted into the node-transform preset immediately before `transformSlotOutlet`,
exactly where upstream's `getBaseTransformPreset` inserts it under `!__BROWSER__ && prefixIdentifiers`. It runs
only when `TransformOptions.PrefixIdentifiers` is set; the default pipeline stays byte-for-byte opaque, matching
Vue's browser build, so every pre-existing transform and test is unaffected.

Scope registration and per-directive expression handling live in the owning transforms, mirroring upstream:

- `VForTransform` rewrites the iterated **source** in the outer scope, then registers the value/key/index
  aliases before the loop body is traversed and removes them on exit.
- `VSlotTransform.TrackSlotScopes` registers the slot props for the slot children and removes them on exit.
- `VOnTransform` processes the handler expression (with `$event`-style locals in scope for inline statements),
  so ref writes inside handlers unwrap.

All of the above are gated on `PrefixIdentifiers`, matching upstream's `!__BROWSER__ && context.prefixIdentifiers`
guards.

### The rewrite contract (compiler ↔ codegen ↔ reactivity)

Vue relies on the render context's JavaScript `Proxy` for automatic ref unwrapping. Vuecs has no `Proxy`, so the
compiler alone decides every access form. Rewriting is always **inline-mode** (the generated render method is a
single closure over the component), and the table below is the compiler-owned contract with code generation
([V01.01.05.05]) and the reactivity area's `Ref<T>` API. It is pinned by `ExpressionBindingTests`; a change to
`Ref<T>.Value` requires a matching change here.

| Classification (`BindingType`) | Read | Write (assignment / `++` / `--`) |
| --- | --- | --- |
| template-local (`v-for`/`v-slot` alias in scope) | `name` | `name` |
| allowed global (`GlobalAllowList`) | `name` | `name` |
| `SetupReference` | `name.Value` | `name.Value` |
| `SetupMaybeReference` | `unref(name)` | `name` |
| `SetupLet` / `SetupConstant` / `SetupReactiveConstant` / `LiteralConstant` | `name` | `name` |
| `Property` / `PropertyAliased` / `Data` / `Options` | `_ctx.name` (alias resolved for `PropertyAliased`) | same |
| unresolved | `_ctx.name` | `_ctx.name` |

Key divergences from Vue's JS contract, all forced by C# semantics:

- **`.Value` in read *and* write positions.** Because `Ref<T>.Value` is a settable C# property, `count += 1`,
  `count++`, and `count = x` on a ref rewrite cleanly to `count.Value …`, replacing Vue's read-time `unref`
  plus an `isRef(…) ? … .value = … : … = …` assignment guard. This is the direct answer to the acceptance
  criterion on compound assignment / increment / inline-handler unwrapping.
- **`_ctx.` member access and bare setup locals** rather than Vue's `$setup.`/`$props.`/`__props.` split — a
  single inline render closure has the component context in `_ctx` and setup state as locals.
- **Unresolved identifiers can be an error.** Vue silently emits `_ctx.name` for any unknown identifier because
  the runtime proxy resolves it. C# cannot: an identifier that is neither a template-local, an allowed global,
  nor a known binding has no member to bind to. When the component model supplies strict metadata
  (`BindingMetadata.ReportsUnresolvedIdentifiers`), such an identifier surfaces `XVuecsUnresolvedIdentifier`
  (code 66 — a Vuecs-specific extension past the upstream and DOM `__EXTEND_POINT__` sentinels) while still
  emitting the recoverable `_ctx.` fallback. With the default permissive metadata the behavior matches Vue.

### Diagnostics and source mapping

Every processed expression is validated by a Roslyn parse (`consumeFullText`). A syntax error is reported as
`X_INVALID_EXPRESSION` (numeric parity with upstream), with the offending Roslyn span remapped from
expression-relative offsets back into template coordinates so [V01.01.05.08] can point at the real file/line/
column. The upstream message prefix (`Error parsing JavaScript expression: `) is preserved verbatim for parity
and the Roslyn diagnostic text is appended. Parsing is recoverable — a diagnostic is reported and the original
node is returned; the transform never throws.

### The global allow-list

`GlobalAllowList` is the C# counterpart of Vue's `isGloballyAllowed` (`@vue/shared` `GLOBALS_ALLOWED`) plus its
literal allow-list. Vue's entries name JavaScript globals (`Math`, `Date`, `JSON`, `parseInt`, …); Vuecs names
the common .NET base-class surface a template legitimately reaches (`Math`, `Convert`, `String`, `DateTime`,
`Enumerable`, the numeric types, the `System` namespace root, …). C# literal keywords (`true`, `false`, `null`,
`this`) never reach the check because Roslyn tokenizes them as keywords, not identifiers.

### Known simplifications and deferrals (non-goals for [V01.01.05.04])

- **Intra-expression lambda/query shadowing is approximated.** Any name declared anywhere inside an expression
  (a lambda parameter, a deconstruction designation, a LINQ range variable) is excluded from rewriting
  everywhere in that expression, rather than tracked per nested scope. This never wrongly rewrites a local; it
  can under-rewrite a same-named outer reference in the rare case an expression both shadows and separately
  reads the same name. Template-scope shadowing — the case the acceptance criteria pin — is exact, driven by the
  `TransformContext` identifier stack.
- **Inline-handler event parameter.** `$event` is not a legal C# identifier, so a handler body that references
  the event variable cannot be parsed under prefixing today. The event-variable identifier is a `v-on` /
  code-generation contract ([V01.01.05.03] / [V01.01.05.05]); handlers that do not reference it (the
  `count++` / `count += 1` / `count = literal` cases the acceptance criteria call out) unwrap correctly now.
- **`asParams` validation is lenient.** Alias/prop declaration positions are registered for scope but not
  hard-validated as expressions, because C# deconstruction forms (`(a, b)`, `var (a, b)`) and JavaScript-style
  `{ a }` destructures do not all parse as C# expressions; a lenient identifier scan still contributes their
  names to scope.
