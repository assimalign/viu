# Assimalign.Vue.Syntax.Templates — design

Why the template compiler front end is shaped the way it is, and the deliberate C#/WASM divergences from
`vuejs/core` v3.5. This document accumulates per feature; today it covers expression binding and scope
analysis ([V01.01.05.04], issue #51) and render-function code generation ([V01.01.05.05], issue #52).

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
- `VOnTransform` processes the handler expression (inline statements are parsed against the Vuecs event identifier `__event`, the C# spelling of Vue's `$event`),
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
| `SetupReference` | `_ctx.name.Value` | `_ctx.name.Value` |
| `SetupMaybeReference` | `unref(_ctx.name)` | `_ctx.name.Value` (upstream: a write to a const binding is only legal when it is a ref) |
| `SetupLet` | `unref(_ctx.name)` | `_ctx.name` — upstream emits an `isRef`-guarded write, which a C# expression cannot; the helper-backed guarded write is deferred to [V01.01.05.05] |
| `SetupConstant` / `SetupReactiveConstant` / `LiteralConstant` | `_ctx.name` | `_ctx.name` |
| `Property` / `PropertyAliased` / `Data` / `Options` | `_ctx.name` (alias resolved for `PropertyAliased`) | same |
| unresolved | `_ctx.name` | `_ctx.name` |

Key divergences from Vue's JS contract, all forced by C# semantics:

- **`.Value` in read *and* write positions.** Because `Ref<T>.Value` is a settable C# property, `count += 1`,
  `count++`, and `count = x` on a ref rewrite cleanly to `count.Value …`, replacing Vue's read-time `unref`
  plus an `isRef(…) ? … .value = … : … = …` assignment guard. This is the direct answer to the acceptance
  criterion on compound assignment / increment / inline-handler unwrapping.
- **Every binding routes through `_ctx.`** rather than Vue's `$setup.`/`$props.`/`__props.` split — the
  generated render function is a static method receiving the component instance (upstream's non-inline
  function mode adapted to C#: no proxy exists to auto-unref, so refs additionally unwrap with `.Value`) — a
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
- **Inline-handler event parameter.** `$event` is not a legal C# identifier, so under prefixing an inline
  statement is parsed and emitted against the Vuecs spelling `__event`: `VOnTransform` substitutes
  `$event` → `__event` in the handler content before processing and names the wrapping lambda's parameter
  `__event`, so `@input="save($event)"` emits `__event => (_ctx.save(__event))`. Template authors keep
  Vue's `$event`; without prefixing the emitted wrapper keeps `$event` for upstream output parity. Pinned
  by `ExpressionBindingTests`.
- **`asParams` validation is lenient.** Alias/prop declaration positions are registered for scope but not
  hard-validated as expressions, because C# deconstruction forms (`(a, b)`, `var (a, b)`) and JavaScript-style
  `{ a }` destructures do not all parse as C# expressions; a lenient identifier scan still contributes their
  names to scope.

## Render-function code generation (`generate`)

The C# port of `@vue/compiler-core`'s
[`codegen.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/codegen.ts)
([V01.01.05.05], issue #52): `RenderFunctionEmitter.Emit(TransformResult)` serializes the transformed
code-generation tree into the body of a C# render method — the asset-resolution preamble
(`genAssets`) followed by `return <root block expression>;`. Each `EmitXxx` on the internal
`RenderCodeWriter` mirrors the upstream `genXxx` of the same node kind, and helper invocations use
upstream's aliased spelling — `_` + the `helperNameMap` name (`_openBlock`, `_createElementBlock`,
`_toDisplayString`, …), the same spelling `TransformContext.HelperString` already emits into compound
expressions. Output is deterministic (ordinal comparisons, invariant-culture numbers, LF newlines) and
the result record is value-equatable, preserving the incremental-generator caching contract.

The upstream `mode`/`prefixIdentifiers` preamble split (module imports versus `with (_ctx)` function
mode) has no C# counterpart: the composition root — the source generator ([V01.01.06.02]) — owns the
method declaration and binds every helper name with one file-level
`using static global::Assimalign.Vue.RuntimeCore.RenderHelpers;`, the C# analogue of upstream's aliased
helper import. Neither this library nor the generator references the runtime assembly; the emitted code
binds **by name**, and the contract below is pinned by `RenderFunctionEmitterTests` and the generator
snapshot tests.

### JavaScript → C# serialization divergences

Every divergence exists because the JavaScript construct has no C# spelling; each is pinned by a
snapshot test in `RenderFunctionEmitterTests`.

| Upstream output | Vuecs C# output | Why |
| --- | --- | --- |
| `(_openBlock(), _createElementBlock(tag, ...))` | `_createElementBlock(_openBlock(), tag, ...)` | C# has no comma operator. C# guarantees left-to-right argument evaluation, so the block opens before any child argument is evaluated and the block factory closes it — the same open/collect/close sequence. |
| `{ id: x, onClick: h }` props/slots objects | `_createProps(("id", x), ("onClick", _withHandler(h)))` | C# has no object literals with arbitrary keys. Static keys become string literals; dynamic keys emit as (contract-typed string) expressions. |
| `[a, b]` children/argument arrays | `new object?[] { a, b }` | A C# collection expression needs a target type, which an object-typed children argument cannot supply. The runtime normalizes object-typed children exactly as Vue's runtime normalizes unknown children. |
| `["title", "id"]` dynamicProps | `["title", "id"]` (verbatim) | The dynamicProps parameter is `string[]` in the helper contract, so upstream's stringified array is already a valid target-typed C# collection expression. |
| `_cache[0] \|\| (_cache[0] = v)` | `(_cache[0] ??= v)` | JavaScript falsy-guarded assignment becomes null-coalescing assignment. |
| `(setBlockTracking(-1, true), (_cache[0] = v).cacheIndex = 0, setBlockTracking(1), _cache[0])` | `_setCache(0, _setBlockTracking(-1, true), v)` | The comma sequence collapses into a helper: argument order pauses tracking before `v` is created; `_setCache` stamps the cache index, resumes tracking, and returns the value. |
| `[...(cacheExpr)]` | `_spreadCache(cacheExpr)` | No spread operator; the helper clones the cached array. |
| inline handler values (`$event => ...`, method refs) | wrapped in `_withHandler(...)` | A C# lambda or method group has no natural type in an object-typed position; the helper's delegate parameter supplies the target type. `_withModifiers`/`_withKeys` calls are not wrapped — their own contract signatures type the inner lambda. |
| `$slots` / `$event` | `_ctx.__slots` / `__event` | `$` is not legal in C# identifiers; `__event` is the [V01.01.05.04] event-variable contract and `__slots` follows the same spelling rule. |
| `undefined` / `void 0` | `null` | No `undefined` in C#. |
| `{}` (renderSlot's empty-props placeholder) | `_createProps()` | Same object-literal rule as props. |
| `const` statements, ` === `/` !== ` (v-memo bodies) | `var`, ` == `/` != ` | Lexical bridging of the JavaScript statement spellings the v-memo transform authors as raw strings. |
| `JSON.stringify(text)` | C# string literal (`SymbolDisplay.FormatLiteral`) | Same role, C# escaping rules. |
| 2-space indent | 4-space indent | Repository C# convention; cosmetic. |

`return`-shape parity is kept: a single text/interpolation root returns the raw string/display string
(the runtime normalizes it, upstream parity), so the generated render method's return type is
`object?`.

### The runtime helper name/signature contract

The emitted code compiles against `global::Assimalign.Vue.RuntimeCore.RenderHelpers` (imported via
`using static` by the generator) — a static surface the runtime area provides ([V01.01.03.22], issue
#136). Members carry the upstream-aliased names on purpose (a deliberate, generated-code-only deviation
from the repository C# naming rule; the names ARE the upstream contract). What code generation requires
of each member:

- `_openBlock(bool disableTracking = false) : BlockToken` — opens a block, returns an opaque
  `BlockToken`; every `_createBlock`/`_createElementBlock` takes it as the first argument
  (evaluation-order sequencing).
- `_createElementBlock(BlockToken block, object? tag, object? props = null, object? children = null, int patchFlag = 0, string[]? dynamicProps = null)`
  and `_createBlock(...)` (component form) — close the block (upstream `createElementBlock`/`createBlock`).
  `tag` is `object?` (not `object`): `_resolveDynamicComponent` can return null, which resolves to a
  comment placeholder.
- `_createElementVNode(object? tag, object? props = null, object? children = null, int patchFlag = 0, string[]? dynamicProps = null)`
  and `_createVNode(...)` — plain vnodes (upstream `createElementVNode`/`createVNode`).
- `_createTextVNode(object? text = null, int patchFlag = 0)`, `_createCommentVNode(string? text = "", bool asBlock = false)`,
  `_createStaticVNode(string content, int count)` — text/comment/static vnodes.
- `_toDisplayString(object?) : string` — interpolation stringification.
- `_renderList(source, iterator) : VirtualNode?[]` — generic overloads whose type inference gives the
  emitted `(item)`, `(item, index)` lambdas their parameter types (list/count/object-entry sources).
- `_renderSlot(ComponentSlots? slots, string name, object? props = null, Func<object?[]?>? fallback = null)`.
- `_withCtx(fn) : Slot` — slot-function wrapper (0- and 1-parameter overloads); its delegate parameters
  type the emitted slot lambdas.
- `_resolveComponent(string name)`, `_resolveDirective(string name)`, `_resolveDynamicComponent(object? value)`.
- `_withDirectives(vnode, object?[] directives)` — runtime directive application. Each entry is itself an
  `object?[]` tuple `[directive, value?, argument?, modifiers?]`; the outer array is `object?[]` (of
  `object?[]`), which is the shape the emitter writes (`new object?[] { new object?[] { … } }`) —
  **not** `object?[][]` (an earlier draft of this table said `object?[][]`; reconciled with the runtime
  and the pinned emitter output in [V01.01.03.22]).
- `_mergeProps(...)`, `_normalizeClass(...)`, `_normalizeStyle(...)`, `_normalizeProps(...)`,
  `_guardReactiveProps(...)`, `_toHandlers(...)`, `_camelize(string)`, `_capitalize(string)`,
  `_toHandlerKey(...) : string` — prop-normalization helpers (dynamic keys must be strings).
- `_setBlockTracking(int value, bool inVOnce = false) : BlockToken` — returns a token accepted by `_setCache`.
- Built-in tags as values (runtime-core): `_Fragment`, `_Teleport`, `_Suspense`, `_KeepAlive`,
  `_BaseTransition`. `_Fragment` is fully realized; the component-like built-ins are surface markers
  whose renderer support lands with their own work items.
- `_unref(object?) : object?` / `_isRef(object?) : bool` — bridge to `Assimalign.Vue.Reactivity`
  references (upstream `unref`/`isRef`).
- Vuecs-defined (no upstream counterpart, per the divergence table): `_createProps(params (string, object?)[] entries)`,
  `_withHandler(handler)` (delegate-typed overloads), `_setCache(int index, BlockToken tracking, object? value)`,
  `_spreadCache(object?)`.

**DOM-only helpers are a separate surface (layering).** The DOM directive values `_vShow`, `_vModelText`,
`_vModelCheckbox`, `_vModelRadio`, `_vModelSelect`, `_vModelDynamic`, the `_withModifiers(handler, string[])` /
`_withKeys(handler, string[])` guard wrappers, and the DOM built-ins `_Transition` / `_TransitionGroup`
are **not** members of `Assimalign.Vue.RuntimeCore.RenderHelpers`: their behavior lives in
`Assimalign.Vue.RuntimeDom` (e.g. `VShow.Instance`, `BrowserEvents.WithModifiers`), which the
platform-agnostic runtime-core layer must not reference. A DOM component binds them through a DOM-side
helper surface the DOM generator path adds — a follow-up to [V01.01.03.22]; the current generator emits a
single runtime-core `using static`, so templates using DOM directives are not yet end-to-end compilable.

The generated render method itself is the generator's contract:
`internal static object? Render(<ComponentClass> _ctx, object?[] _cache)` plus
`internal const int RenderCacheSize` (C# arrays cannot grow on assignment, so the runtime sizes the
per-instance cache from the constant). `RenderCacheSize` and `Render` are reserved member names in a
`.viu` component's partial class. `Render` returns `object?`; the runtime normalizes it into a vnode
through `RenderHelpers.NormalizeRoot(object?)` (the C# analogue of upstream `normalizeVNode` over the
render result). The runtime-side implementation of `RenderHelpers` and the end-to-end execution tests
against the renderer landed in [V01.01.03.22] (`Assimalign.Vue.RuntimeCore.CompiledRenderTests`), which
compiles a generator-emitted render body with Roslyn and drives it through the in-memory renderer —
delivering the integration criterion deferred from [V01.01.05.05].

### Known deferrals (non-goals for [V01.01.05.05])

- **`v-memo` bodies are serialized but not C#-legal end to end.** The memo condition parts authored by
  the v-for/v-memo transforms carry JavaScript member accesses (`_cached.key`) and the `_cached`
  parameter contract; C#-izing them belongs with the caching pass work ([V01.01.05.07] era), so v-memo
  templates are excluded from the parse-validity suite.
- **`CacheHandlers` stays off in the generator.** Upstream's cached member-expression handler wraps in
  `(...args) => ...` (JavaScript rest arguments), which has no C# spelling yet; v-once caching is
  independent and fully emitted.
- **v-slot destructuring and tuple v-for aliases** (`#item="{ label }"`, `(a, b) in pairs`) emit
  verbatim and are not valid C# lambda parameters; they are follow-up expression-binding work.
