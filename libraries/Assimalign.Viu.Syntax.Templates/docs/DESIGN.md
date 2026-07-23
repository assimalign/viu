# Assimalign.Viu.Syntax.Templates — design

Why the template compiler front end is shaped the way it is, and the deliberate C#/WASM divergences from
`vuejs/core` v3.5. This document accumulates per feature; today it covers expression binding and scope
analysis ([V01.01.05.04], issue #51) and render-function code generation ([V01.01.05.05], issue #52).

## Expression binding and scope analysis (`transformExpression`)

The C# port of `@vue/compiler-core`'s
[`transforms/transformExpression.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/transforms/transformExpression.ts)
plus the `BindingTypes` classification from
[`options.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/options.ts). Where Vue
parses expression bodies with `@babel/parser`, Viu parses them with
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
- `VOnTransform` processes the handler expression (inline statements are parsed against the Viu event identifier `__event`, the C# spelling of Vue's `$event`),
  so ref writes inside handlers unwrap.

All of the above are gated on `PrefixIdentifiers`, matching upstream's `!__BROWSER__ && context.prefixIdentifiers`
guards.

### The rewrite contract (compiler ↔ codegen ↔ reactivity)

Vue relies on the render context's JavaScript `Proxy` for automatic ref unwrapping. Viu has no `Proxy`, so the
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
| CSS module accessor (`$style`, named module) | `Style.member` / `<Accessor>.member` (bare accessor class) | n/a (read-only) |
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
  (`BindingMetadata.ReportsUnresolvedIdentifiers`), such an identifier surfaces `XViuUnresolvedIdentifier`
  (code 66 — a Viu-specific extension past the upstream and DOM `__EXTEND_POINT__` sentinels) while still
  emitting the recoverable `_ctx.` fallback. With the default permissive metadata the behavior matches Vue.

### CSS Modules accessors (`$style` and named modules, [V01.01.05.04.01])

Vue exposes a `<style module>` block's class map to templates as the `$style` render-context object (a named
module `<style module="theme">` as `theme`), indexed at runtime: `:class="$style.box"`. Viu generates static
C# with no render-context object to index, so [V01.01.06.06] emits the map as a compile-time nested `const` class
(`internal static class Style { public const string box = "box_<hash>"; }`). This feature wires that class into
expression classification as a **new binding source**, so `$style.box` resolves to `Style.box` (the emitted
const) instead of a phantom component member — the loop [V01.01.06.06] left open. The seam is
`CssModuleAccessors`, a transform-input object (like `BindingMetadata`) the composition-root generator supplies
on `TransformOptions.CssModules`; it is rebuilt from the model's already-value-equatable module-class map, so it
never rides in the cached model and the incremental cache stays `Cached`.

- **`$` spelling.** `$` is not a legal C# identifier character, so before the Roslyn parse the accessor spelling
  is rewritten to a parse identifier — `$style`→`_style` — the same spelling-substitution precedent as
  `$event`→`__event` and `$slots`→`__slots`. The substitution is **length-preserving**, so every offset in the
  expression (and therefore every remapped diagnostic span) is unchanged; classification then maps the parse
  identifier to the accessor class name (`Style`). A named module is referenced by its authored name (`theme`),
  already C#-parseable, and maps to its pascal-cased accessor class (`Theme`) — a divergence from Vue's runtime
  string-indexed access, forced because the accessor is a C# type. A member with a non-identifier name (`.a-b`)
  is reached through its sanitized C# member (`$style.a_b`), the same name the emitter writes as the const.
- **Precedence (shadowing).** A module accessor is resolved before component bindings, so a same-named component
  member is shadowed — matching Vue's render context, which exposes `$style`/named modules over component state.
- **Unknown members are a compile-time error.** The generator supplies the *complete* class map, so an access to
  an undeclared class (`$style.missing`) is decidably wrong and surfaces `XViuUnknownCssModuleMember` (code
  1001, the Viu-specific band) on the exact template coordinate under strict metadata
  (`CssModuleAccessors.ReportsUnknownMembers`, which the generator sets). The access still emits the accessor
  member (recoverable), so the C# compiler is the backstop; with a partial map the check is silent and only the
  C# compiler catches it. Pinned by `ExpressionBindingTests`, `SingleFileComponentCssModuleTests`, and the
  compiled-render projects.
- **Known simplification (collision).** The `$`→`_` substitution means a component member literally named
  `_style` referenced in a template while a `$style` module is in scope would be misclassified as the accessor
  (both spell `_style` post-substitution). This mirrors the documented lambda-shadowing approximation and is
  vanishingly rare given the repo's whole-word naming; a real `$style` reference is always unambiguous.

### Diagnostics and source mapping

Every processed expression is validated by a Roslyn parse (`consumeFullText`). A syntax error is reported as
`X_INVALID_EXPRESSION` (numeric parity with upstream), with the offending Roslyn span remapped from
expression-relative offsets back into template coordinates so [V01.01.05.08] can point at the real file/line/
column. The upstream message prefix (`Error parsing JavaScript expression: `) is preserved verbatim for parity
and the Roslyn diagnostic text is appended. Parsing is recoverable — a diagnostic is reported and the original
node is returned; the transform never throws.

### The global allow-list

`GlobalAllowList` is the C# counterpart of Vue's `isGloballyAllowed` (`@vue/shared` `GLOBALS_ALLOWED`) plus its
literal allow-list. Vue's entries name JavaScript globals (`Math`, `Date`, `JSON`, `parseInt`, …); Viu names
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
  statement is parsed and emitted against the Viu spelling `__event`: `VOnTransform` substitutes
  `$event` → `__event` in the handler content before processing and names the wrapping lambda's parameter
  `__event`, so `@input="save($event)"` emits `__event => { _ctx.save(__event); }`. Template authors keep
  Vue's `$event`; without prefixing the emitted wrapper keeps `$event` for upstream output parity. Pinned
  by `ExpressionBindingTests`.
- **Single-statement call handlers are statement-block lambdas.** Upstream `transformOn` wraps every
  single-statement inline handler as an expression arrow `$event => (expr)`; JavaScript has no `void`, so
  the discarded result is harmless. C# has `void`, and a parenthesized void call binds no delegate, so a
  single-statement handler whose expression is a **call** — a plain invocation `save($event)` or a
  null-conditional invocation `model?.save()`, the only single-statement shape that can be void-typed —
  emits as a statement-block lambda `__event => { save(__event); }` instead. Its `Action<…>` overload
  binds both void and value calls (discarding any result, exactly as upstream's arrow function does).
  Every other single-statement shape (an increment `count++`, an assignment `open = true`, a plain value
  expression) yields a C# value and keeps the expression-lambda form, so it stays as close to upstream as
  C# allows. Only the prefixed (C#-codegen) path diverges; non-prefixed output stays byte-for-byte
  upstream parity. Pinned by `ExpressionBindingTests` and `RenderFunctionEmitterTests`
  ([V01.01.05.05.01], issue #143).
- **Multi-statement handlers get a synthesized statement terminator.** A multi-statement inline handler
  (`@click="foo(); bar()"`) is emitted into `__event => { <body> }`. Upstream wraps it the same way and
  relies on JavaScript's automatic semicolon insertion for the final statement; C# has no ASI, so a body
  whose last statement omits its terminator would emit invalid C#. Under prefixing, `ExpressionProcessor`
  decides with the same statement-list parse that validates the body (`asRawStatements`): if `{ body }`
  does not parse clean but `{ body; }` does, the only fault was the missing terminator, so a `;` is
  synthesized onto the rewritten content (a genuine syntax error is left untouched and still surfaces as
  `X_INVALID_EXPRESSION`). The terminator rides out even when no identifier needs rewriting. An
  author-supplied trailing `;` is never doubled. Only the prefixed path is affected; non-prefixed output
  stays byte-for-byte upstream parity. Pinned by `ExpressionBindingTests` and `RenderFunctionEmitterTests`
  ([V01.01.05.05.02], issue #150).
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
`using static global::Assimalign.Viu.RenderHelpers;`, the C# analogue of upstream's aliased
helper import. Neither this library nor the generator references the runtime assembly; the emitted code
binds **by name**, and the contract below is pinned by `RenderFunctionEmitterTests` and the generator
snapshot tests.

### JavaScript → C# serialization divergences

Every divergence exists because the JavaScript construct has no C# spelling; each is pinned by a
snapshot test in `RenderFunctionEmitterTests`.

| Upstream output | Viu C# output | Why |
| --- | --- | --- |
| `(_openBlock(), _createElementBlock(tag, ...))` | `_createElementBlock(_openBlock(), tag, ...)` | C# has no comma operator. C# guarantees left-to-right argument evaluation, so the block opens before any child argument is evaluated and the block factory closes it — the same open/collect/close sequence. |
| `{ id: x, onClick: h }` props/slots objects | `_createProps(("id", x), ("onClick", _withHandler(h)))` | C# has no object literals with arbitrary keys. Static keys become string literals; dynamic keys emit as (contract-typed string) expressions. |
| `[a, b]` children/argument arrays | `new object?[] { a, b }` | A C# collection expression needs a target type, which an object-typed children argument cannot supply. The runtime normalizes object-typed children exactly as Vue's runtime normalizes unknown children. |
| `["title", "id"]` dynamicProps | `["title", "id"]` (verbatim) | The dynamicProps parameter is `string[]` in the helper contract, so upstream's stringified array is already a valid target-typed C# collection expression. |
| `_cache[0] \|\| (_cache[0] = v)` | `(_cache[0] ??= v)` | JavaScript falsy-guarded assignment becomes null-coalescing assignment. |
| `(setBlockTracking(-1, true), (_cache[0] = v).cacheIndex = 0, setBlockTracking(1), _cache[0])` | `_setCache(0, _setBlockTracking(-1, true), v)` | The comma sequence collapses into a helper: argument order pauses tracking before `v` is created; `_setCache` stamps the cache index, resumes tracking, and returns the value. |
| `[...(cacheExpr)]` | `_spreadCache(cacheExpr)` | No spread operator; the helper clones the cached array. |
| inline handler values (`$event => ...`, method refs) | wrapped in `_withHandler(...)` | A C# lambda or method group has no natural type in an object-typed position; the helper's delegate parameter supplies the target type. `_withModifiers`/`_withKeys` calls are not wrapped — their own contract signatures type the inner lambda. |
| single-statement **call** handler (`$event => (call)`) | `__event => { call; }` (statement-block lambda) | JavaScript has no `void`; C# does, and a parenthesized void call binds no delegate. A call is the only single-statement shape that can be void, so it emits as a statement-block lambda (binds the `Action<…>` overload, discarding any value). Increments/assignments/value expressions yield a value and keep `__event => (expr)`. See the inline-handler divergence note above ([V01.01.05.05.01], issue #143). |
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

The emitted code compiles against `global::Assimalign.Viu.RenderHelpers` (imported via
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
- `_renderList(source, iterator) : TResult[]` — generic overloads whose type inference gives the
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
- `_unref(object?) : object?` / `_isRef(object?) : bool` — bridge to `Assimalign.Viu.Core`
  references (upstream `unref`/`isRef`).
- Viu-defined (no upstream counterpart, per the divergence table): `_createProps(params (string, object?)[] entries)`,
  `_withHandler(handler)` (delegate-typed overloads), `_setCache(int index, BlockToken tracking, object? value)`,
  `_spreadCache(object?)`.

### The DOM render-helper surface (`DomRenderHelpers`, [V01.01.04.09])

The DOM-directive helpers are a **separate facade for layering**: `_vShow`, `_vModelText`, `_vModelCheckbox`,
`_vModelRadio`, `_vModelSelect`, `_vModelDynamic`, the `_withModifiers` / `_withKeys` guard wrappers, and the
DOM built-ins `_Transition` / `_TransitionGroup` are **not** members of
`Assimalign.Viu.Core.RenderHelpers` — their behavior lives in `Assimalign.Viu.Browser`, which the
platform-agnostic runtime-core layer must not reference (keeping runtime-core DOM-free, and keeping a real DOM
directive from ever mis-binding onto a runtime-core marker). They ship instead as
`global::Assimalign.Viu.Browser.DomRenderHelpers`, and the composition-root generator ([V01.01.06.02])
emits a **second** file-level `using static global::Assimalign.Viu.Browser.DomRenderHelpers;` alongside the
runtime-core one whenever a render body is present. A browser `.viu` always has Browser available (it is the
DOM renderer), so both imports are unconditional — DOM-directive templates (`v-show`, `v-model`,
`@click.prevent`, `@keyup.enter`) are now end-to-end compilable. `DomRenderHelpers` references only Browser's
own machinery and Core's `IDirective`, never any `Assimalign.Viu.Syntax.*` assembly; the by-name contract
still flows one way. Pinned by `Assimalign.Viu.Browser.CompiledRenderTests` (a `.viu` using every spelling
below compiles against both facades) and `Assimalign.Viu.Browser.Tests.DomRenderHelpersTests` (facade
mapping + v-show / `.prevent` execution through the in-memory adapter).

What code generation requires of each DOM member, mapped to the runtime machinery it forwards to:

- `_vShow`, `_vModelText`, `_vModelCheckbox`, `_vModelRadio`, `_vModelSelect`, `_vModelDynamic` — the runtime
  directive values, typed `IDirective`, mapped to `VShow.Instance` / `VModelText.Instance` / … . The emitter
  writes each as a `withDirectives` tuple element (`new object?[] { new object?[] { _vShow, exp } }`), which
  `RenderHelpers._withDirectives` binds through its `tuple[0] is IDirective` check.
- `_withModifiers(...)` / `_withKeys(...)` — the `v-on` modifier / key guard wrappers (upstream
  `withModifiers` / `withKeys`), each **returning `Action<BrowserEvent>`** (the dispatchable handler the event
  invoker registry understands) and forwarding to `BrowserEvents.WithModifiers` / `BrowserEvents.WithKeys`. The
  handler parameter is **overloaded**, not a single `Action` — this reconciles the earlier
  `_withModifiers(handler, string[])` pin: the emitter (`VOnTransform`) can write the handler as a value-returning
  inline expression lambda (`__event => (expr)`), a statement-block lambda for a single-statement call
  (`__event => { call; }`, the void-capable shape — see the single-statement call-handler divergence above) or a
  multi-statement body, or a member/method-group reference (`_ctx.save`), and a parenthesized void call cannot
  bind to `Func<BrowserEvent, object?>`. The
  overload set — `Func<BrowserEvent, object?>`, `Action<BrowserEvent>`, `Func<object?>`, `Action` (each with
  `params string[]`) — target-types every one of those, mirroring `_withHandler`'s multi-overload rationale.
  Because both return `Action<BrowserEvent>`, a stacked `@keyup.enter.prevent` nests cleanly as
  `_withKeys(_withModifiers(handler, ["prevent"]), ["enter"])` (the outer call resolves the inner result through
  the `Action<BrowserEvent>` arm). These are **not** wrapped in `_withHandler` — their own signatures type the
  inner lambda.
- `_Transition` / `_TransitionGroup` — named-template markers lowered to `TemplateComponent`
  requests. Browser's component-factory wrapper resolves `Transition`, `TransitionGroup`, and the
  Core `BaseTransition` dependency without service resolution. Core's keyed child-to-host-element
  snapshots support `TransitionGroup`'s Browser-owned FLIP pass.

The generated render method itself is the generator's contract:
`internal static object? Render(<ComponentClass> _ctx, object?[] _cache)` plus
`internal const int RenderCacheSize` (C# arrays cannot grow on assignment, so the runtime sizes the
per-instance cache from the constant). `RenderCacheSize` and `Render` are reserved member names in a
`.viu` component's partial class. `Render` returns `object?`; the runtime normalizes it into a vnode
through `RenderHelpers.NormalizeRoot(object?)` (the C# analogue of upstream `normalizeVNode` over the
render result). The runtime-side implementation of `RenderHelpers` and the end-to-end execution tests
against the renderer landed in [V01.01.03.22] (`Assimalign.Viu.Core.CompiledRenderTests`), which
compiles a generator-emitted render body with Roslyn and drives it through the in-memory renderer —
delivering the integration criterion deferred from [V01.01.05.05].

### Render source mapping ([V01.01.05.08])

`RenderFunctionEmitter.Emit` returns a `SyntaxList<RenderSourceMapping>` alongside the code — the C#
counterpart of the source map Vue 3.5's `generate()` produces under `sourceMap`
([`codegen.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/codegen.ts)), but
targeting Roslyn's `#line` mechanism rather than a browser devtools source map, because Viu diagnostics
travel through the C# compiler. Each dynamic expression `RenderCodeWriter` emits records a mapping: the
absolute offset of its **original template text** inside the emitted output (found by locating
`node.Location.Source` within the rewritten content, so the map points past the inserted `_ctx.` prefix or
`unref(...)` wrapper at the identifier the template author wrote) paired with that node's template
`SourceLocation`. Static string literals, synthesized nodes (the empty-`Source` `Ir.LocationStub`), and any
emission whose original text is not a recognizable substring (a future `_hoisted_N` placeholder) are skipped
— they have no faithful template span to point at. The map is value-equatable, preserving the caching
contract.

This library owns the *correspondence*; the composition root (the generator, [V01.01.06.02]) owns turning it
into `#line` directives, because only it holds the block-content-start position and the `.viu` file path. A
C# `#line (startLine,startColumn)-(endLine,endColumn) charOffset "file"` **span** directive (not the
line-only form, which cannot correct the `_ctx.`-shifted column) aligns physical column `charOffset` on the
following line to the template `(startLine, startColumn)` and maps linearly; that mapping stays in effect
across the rest of the physical line, so the generator brackets each expression-bearing render line
individually — anchored to its first (leftmost) expression — and closes it with `#line default`. The
divergence from the line-only `@script` seam is forced: script content is emitted verbatim (columns already
match), whereas a render expression is rewritten, so its column must be re-aligned. A C# error inside a
template expression (a typo'd member under permissive metadata) therefore resolves to the offending `.viu`
line **and** column, proved through the real compiler in the generator's
`SingleFileComponentTemplateSourceMapTests` exactly as the `@script` `#line` map is proved. Non-expression
scaffolding — and any second expression that happens to share one physical render line — falls back to the
generated file, the standard generated-code practice.

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

## Static caching and stringification (`cacheStatic` / `stringifyStatic`)

The C# port of Vue 3.5's static optimization ([V01.01.05.07], issue #54): `@vue/compiler-core`'s
[`transforms/cacheStatic.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-core/src/transforms/cacheStatic.ts)
(the successor to `hoistStatic`) and `@vue/compiler-dom`'s
[`transforms/stringifyStatic.ts`](https://github.com/vuejs/core/blob/v3.5.13/packages/compiler-dom/src/transforms/stringifyStatic.ts).
`StaticCache.Cache` runs from `Transformer.Transform` — after the traversal, before `CreateRootCodegen` —
only when `TransformOptions.HoistStatic` is set (the composition-root generator sets it; the default
pipeline stays unoptimized, which is the acceptance criteria's debugging opt-out). This matters more on
WASM than upstream (PLAN.md founding decision #4): every vnode not created and every patch visit skipped is
a `JSImport` marshaling round-trip avoided, so a stringified 20-node run replaces 20+ interop calls with one
`insertStaticContent`.

### The constant analysis (`getConstantType`)

`ConstantAnalysis.GetConstantType(node, context)` is the full port of upstream `getConstantType`: an element
is constant only when its generated props, every child, and every `v-bind` expression are constant, and it
carries no patch flag and is not a block. `NOT_CONSTANT` from any of them poisons the ancestor, so refs
(`NEED_PATCH`), runtime directives, dynamic keys (which force a block), and dynamic children/props are never
static — exactly the eligibility the issue pins. Levels are memoized per element in
`TransformContext.ConstantCache`. Because Viu keeps expression bodies opaque, an interpolation or dynamic
`v-bind` value is never above `NOT_CONSTANT`; only static text, static attributes, and compiler-injected
literal constants (`v-if` branch keys, `v-model` modifier objects) reach the higher levels. As upstream
does, a fully static `svg`/`foreignObject`/`math` block is demoted back to a plain vnode as a side effect of
the analysis. The context-free `GetConstantType(node)` overload (parser-stamped levels only) still backs the
patch-flag pass, unchanged.

### Caching and marking

Each fully static (`>= CAN_CACHE`) subtree is marked `PatchFlags.Cached` (`-1`, the v3.5 `CACHED` spelling
of the old `HOISTED`) so the runtime diff skips it, then wrapped in a render-cache slot via `context.Cache`
(reusing the same `_cache[n]` seam and `??=` emission v-once already uses). An element whose subtree is
dynamic but whose props are static gets its props cached the same way. **Clone-on-insert is the runtime's
responsibility**: the compiler emits the `CACHED` marker and the `createStaticVNode` payload; the runtime's
DOM adapter clones a cached/static vnode when inserting it into more than one tree (the runtime-area
contract referenced by `createStaticVNode`/`insertStaticContent`).

### Stringification (`stringifyStatic`)

After the walk, `StaticStringifier.Run` scans a container's children for the largest contiguous run of
cached, stringifiable siblings and — at or above the upstream thresholds — collapses it into a single
`createStaticVNode(html, nodeCount)` whose serialized HTML the runtime applies via one `innerHTML`. The
thresholds are pinned numerically to upstream `StringifyThresholds`:

| Threshold | Value | Meaning |
| --- | --- | --- |
| `NODE_COUNT` | 20 | consecutive stringifiable nodes |
| `ELEMENT_WITH_BINDING_COUNT` | 5 | consecutive elements carrying attribute bindings |

Serialization reuses the compiler's existing HTML knowledge: `escapeHtml` (the `@vue/shared` port: `"`, `&`,
`'`, `<`, `>`) so the string round-trips through `innerHTML` to the same DOM (WHATWG fragment serialization),
`CompilerDomKnowledge.IsVoidTag` to omit end tags for void elements, and `IsKnownHtmlAttribute`/
`IsKnownSvgAttribute` plus the `data-`/`aria-` rule for `isStringifiableAttr`. Table-section tags
(`caption,thead,tr,th,tbody,td,tfoot,colgroup,col`) never stringify (innerHTML would reparent them).

### JavaScript → C# / AOT divergences

Each is deliberate, forced by the no-dynamic-codegen rule or the immutable-AST model, and pinned by
`StaticCacheTests` (the *chosen* behavior, per the deviations rule).

| Upstream behavior | Viu behavior | Why |
| --- | --- | --- |
| `context.cache` (vnode subtrees, per-instance `_cache`) **and** `context.hoist` (props objects, per-type module `_hoisted_N` consts) | both route through the per-instance `_cache` seam | the C# generator model owns the render method and has no module-const scope without a new emitter↔generator field contract; each value is still created once per instance and reused across all re-renders — the interop-reduction goal. `context.Hoist`/`result.Hoists` stay reserved for a future per-type static-field seam. |
| constant interpolations / constant `v-bind` values evaluated with `new Function` | not evaluated; `AnalyzeNode` bails on any non-text/comment/element content and on any binding | dynamic code generation is forbidden (AOT). In the opaque-expression model no interpolation or `v-bind` is ever constant, so a stringifiable cached subtree only ever holds static text, comments, and static attributes — the eval branches are unreachable. |
| whole-children-array cache (`cachedAsArray`) and `TEXT_CALL` caching | omitted; each eligible static sibling caches individually | a fully static text run is a single `TextNode` folded into its element's cached subtree, so `TEXT_CALL` caching is unreachable; skipping `cachedAsArray` avoids reconstructing frozen immutable children arrays and means only upstream's non-`isParentCached` stringify path is needed. |
| `stringifyStatic` runs on every container (element, root, `v-for`/`v-if` bodies) | runs on a template root's children and a plain element's children | those are the two containers with clean immutable write-back; static descendants inside `v-if`/`v-for` bodies are still cached, just not stringified (a per-iteration run rarely reaches 20 nodes anyway). |
| after merging, `context.cached` is spliced and trailing cache indices decremented | merged nodes' cache slots stay reserved-but-unused | `CacheExpression.Index` is immutable; leaving the slots reserved keeps the record immutable and the output deterministic, at the cost of a marginally larger `_cache`. |
| `isKnownMathMLAttr` | MathML attributes are never stringifiable | the Viu shared DOM knowledge has no MathML known-attribute table; MathML static content still caches, it just does not fold into an innerHTML string. |

### Non-goals ([V01.01.05.07])

- Per-component-type static fields (the pre-3.5 `_hoisted_N` module consts). All static optimization routes
  through per-instance `_cache`; a static-field emission seam through the generator is deferred.
- Stringification of `v-for`/`v-if` body runs, and of runs interleaved with `TextCall` text (see above).
- Constant-expression evaluation for stringifying constant interpolations / `:bind="1"` (needs an evaluator
  Viu deliberately does not have).
