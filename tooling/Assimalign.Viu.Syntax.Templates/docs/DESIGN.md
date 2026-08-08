# Assimalign.Viu.Syntax.Templates — design

Why the template compiler front end is shaped the way it is, and the decisions C# and WASM force.
This document accumulates per feature; today it covers expression binding and scope
analysis ([V01.01.05.04], issue #51) and render-function code generation ([V01.01.05.05], issue #52).
The normative statements are `[SFC-6]`–`[SFC-8]` and `[SFC-CG-1]`–`[SFC-OPT-4]` in
[`docs/SPECIFICATION.md`](../../../docs/SPECIFICATION.md).

## Expression binding and scope analysis

Template expressions are C#, so they are parsed with
`Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression` — the compiler's idea of an expression is
exactly the C# compiler's, with no second grammar to keep in sync. Roslyn is a generator-only dependency
added through the central build system (`build/Targets/Build.References.Packages.targets`), never
referenced by any runtime library, so it never reaches the WASM app.

### Pipeline placement and gating

`TransformExpression` is inserted into the node-transform preset immediately before the slot-outlet
transform, which consumes already-rewritten expressions. It runs
only when `TransformOptions.PrefixIdentifiers` is set; the default pipeline leaves every expression body
opaque, so a caller that only needs the structural transform pays nothing and every pre-existing
transform and test is unaffected.

Scope registration and per-directive expression handling live in the owning transforms, because each one
consumes its directive before any later pass could see it:

- `VForTransform` rewrites the iterated **source** in the outer scope, then registers the value/key/index
  aliases before the loop body is traversed and removes them on exit.
- `VSlotTransform.TrackSlotScopes` registers the slot props for the slot children and removes them on exit.
- `VOnTransform` processes the handler expression (inline statements are parsed against the event identifier `__event`, the length-preserving C# spelling of `$event`),
  so reference writes inside handlers unwrap.

All of the above are gated on `PrefixIdentifiers`.

### The rewrite contract (compiler ↔ codegen ↔ reactivity)

There is no runtime proxy to auto-unwrap a reference (`[RCT-8]`), so the
compiler alone decides every access form. Rewriting is always **inline-mode** (the generated render method is a
single closure over the component), and the table below is the compiler-owned contract with code generation
([V01.01.05.05]) and the reactivity area's reference API. It is normative in `[SFC-6]` and pinned by
`ExpressionBindingTests`; a change to `ReactiveValue<T>.Value` requires a matching change here.

| Classification (`BindingType`) | Read | Write (assignment / `++` / `--`) |
| --- | --- | --- |
| template-local (`v-for`/`v-slot` alias in scope) | `name` | `name` |
| allowed global (`GlobalAllowList`) | `name` | `name` |
| `SetupReference` | `_ctx.name.Value` | `_ctx.name.Value` |
| `SetupMaybeReference` | `unref(_ctx.name)` | `_ctx.name.Value` (a write to a maybe-reference binding is only legal when it does hold a reference) |
| `SetupLet` | `unref(_ctx.name)` | `_ctx.name` — deciding at runtime whether to assign the member or its `.Value` is not expressible as a C# expression without a helper; the helper-backed guarded write is deferred to [V01.01.05.05] |
| `SetupConstant` / `SetupReactiveConstant` / `LiteralConstant` | `_ctx.name` | `_ctx.name` |
| `Property` / `PropertyAliased` / `Data` / `Options` | `_ctx.name` (alias resolved for `PropertyAliased`) | same |
| CSS module accessor (`$style`, named module) | `Style.member` / `<Accessor>.member` (bare accessor class) | n/a (read-only) |
| unresolved | `_ctx.name` | `_ctx.name` |

Three decisions in that table are load-bearing:

- **`.Value` in read *and* write positions.** Because `ReactiveValue<T>.Value` is a settable C# property,
  `count += 1`, `count++`, and `count = x` on a reference all rewrite to `count.Value …` with one rule and
  no runtime assignment guard in the emitted code. This is the direct answer to the acceptance
  criterion on compound assignment / increment / inline-handler unwrapping.
- **Every binding routes through `_ctx.`** rather than a per-source prefix split. The generated render
  function is a static method receiving the component instance, so there is no closure to read from; a
  definite reference additionally unwraps with `.Value` because nothing at runtime will do it.
- **Unresolved identifiers can be an error.** An identifier that is neither a template-local, an allowed
  global, nor a known binding has no member to bind to, and no dynamic lookup exists to rescue it
  (`[RCT-8]`). When the component model supplies strict metadata
  (`BindingMetadata.ReportsUnresolvedIdentifiers`), such an identifier surfaces `XViuUnresolvedIdentifier`
  (code 1000, the analysis band) while still
  emitting the recoverable `_ctx.` fallback, so the C# compiler stays the backstop. With the default
  permissive metadata the check is silent.

### CSS Modules accessors (`$style` and named modules, [V01.01.05.04.01])

A `<style module>` block's class map is reachable from templates as `$style` (a named module
`<style module="theme">` as `theme`): `:class="$style.box"`. There is no render-context object to index
at runtime (`[RCT-8]`), so [V01.01.06.06] emits the map as a compile-time nested `const` class
(`internal static class Style { public const string box = "box_<hash>"; }`, `[STY-2]`). This feature wires that class into
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
  already C#-parseable, and maps to its pascal-cased accessor class (`Theme`), because the accessor is a C#
  type rather than a string-indexed object. A member with a non-identifier name (`.a-b`)
  is reached through its sanitized C# member (`$style.a_b`), the same name the emitter writes as the const
  (`[STY-5]`).
- **Precedence (shadowing).** A module accessor is resolved before component bindings, so a same-named component
  member is shadowed (`[STY-3]`).
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
`X_INVALID_EXPRESSION`, with the offending Roslyn span remapped from
expression-relative offsets back into template coordinates so [V01.01.05.08] can point at the real file/line/
column. The catalog message prefix is kept and the Roslyn diagnostic text appended. Parsing is recoverable — a diagnostic is reported and the original
node is returned; the transform never throws.

### The global allow-list

`GlobalAllowList` names the identifiers expression rewriting leaves untouched: the common .NET
base-class surface a template legitimately reaches (`Math`, `Convert`, `String`, `DateTime`,
`Enumerable`, the numeric types, the `System` namespace root, …). Membership is a deliberate contract,
not a convenience: a name on the list can never be shadowed by a component member, so adding one is a
breaking change for any component that already declares that member. C# literal keywords (`true`, `false`,
`null`, `this`) never reach the check because Roslyn tokenizes them as keywords, not identifiers.

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
  `__event`, so `@input="save($event)"` emits `__event => { _ctx.save(__event); }`. Template authors still
  write `$event`; the substitution is **length-preserving**, so every expression offset — and therefore
  every remapped diagnostic span — survives it (`[SFC-7]`). Without prefixing the emitted wrapper keeps
  the authored text unchanged. Pinned by `ExpressionBindingTests`.
- **Single-statement call handlers are statement-block lambdas.** A parenthesized void call binds no
  delegate in C#, so a
  single-statement handler whose expression is a **call** — a plain invocation `save($event)` or a
  null-conditional invocation `model?.save()`, the only single-statement shape that can be void-typed —
  emits as a statement-block lambda `__event => { save(__event); }`. Its `Action<…>` overload
  binds both void and value calls, discarding any result.
  Every other single-statement shape (an increment `count++`, an assignment `open = true`, a plain value
  expression) yields a C# value and keeps the expression-lambda form, so the emitted shape stays uniform
  wherever C#'s void-expression rule permits it. Only the prefixed path makes this choice; the
  non-prefixed path emits the handler text unchanged. Pinned by `ExpressionBindingTests` and `RenderFunctionEmitterTests`
  ([V01.01.05.05.01], issue #143).
- **Multi-statement handlers get a synthesized statement terminator.** A multi-statement inline handler
  (`@click="foo(); bar()"`) is emitted into `__event => { <body> }`. C# has no automatic semicolon
  insertion, so a body whose last statement omits its terminator would emit invalid C#. Under prefixing, `ExpressionProcessor`
  decides with the same statement-list parse that validates the body (`asRawStatements`): if `{ body }`
  does not parse clean but `{ body; }` does, the only fault was the missing terminator, so a `;` is
  synthesized onto the rewritten content (a genuine syntax error is left untouched and still surfaces as
  `X_INVALID_EXPRESSION`). The terminator rides out even when no identifier needs rewriting. An
  author-supplied trailing `;` is never doubled. Only the prefixed path is affected; non-prefixed output
  emits the handler text unchanged. Pinned by `ExpressionBindingTests` and `RenderFunctionEmitterTests`
  ([V01.01.05.05.02], issue #150).
- **`asParams` validation is lenient.** Alias/prop declaration positions are registered for scope but not
  hard-validated as expressions, because C# deconstruction forms (`(a, b)`, `var (a, b)`) and JavaScript-style
  `{ a }` destructures do not all parse as C# expressions; a lenient identifier scan still contributes their
  names to scope.

## Render-function code generation

([V01.01.05.05], issue #52): `RenderFunctionEmitter.Emit(TransformResult)` serializes the transformed
code-generation tree into the statement-form body of a C# render method. The internal
`FrameRenderCodeWriter` emits ordered statements against the method's `ComponentRenderFrame`
parameter and constructs the closed `VirtualNode` algebra directly. Output is deterministic
(ordinal comparisons, invariant-culture numbers, LF newlines) and the result record is
value-equatable, preserving the incremental-generator caching contract.

There is no preamble to emit: the emitter produces a method **body** and nothing more. The composition
root — the source generator ([V01.01.06.02]) — owns the method declaration, its component-instance and
frame parameters, and the enclosing partial class. Neither this library nor the generator references a
runtime assembly; emitted runtime references are fully qualified names in consumer code, so there is no
static-import contract. `[SFC-CG-1]` and `[SFC-CG-2]` are pinned by
`RenderFunctionEmitterTests` and the generator snapshot tests.

### Serialization decisions C# forces

Each row exists because the shape the code-generation tree describes has no direct C# spelling; each is
pinned by a snapshot test in `RenderFunctionEmitterTests`.

| Described shape | Viu C# output | Why |
| --- | --- | --- |
| `(_openBlock(), createNode(tag, ...))` | `frame.OpenBlock();` followed by node construction, `frame.Track(node)`, and `frame.CloseBlock()` | C# has no comma operator. Explicit statements preserve the open/collect/close order and keep block state on the mount-owned frame. |
| `{ id: x, onClick: h }` property/slot objects | `ElementBinding` or `ComponentBinding` entries in an owned collection | C# has no object literals with arbitrary keys. Static keys become string literals; dynamic keys are evaluated as strings before the typed binding is constructed. |
| `[a, b]` children/argument arrays | `new object?[] { a, b }` | A C# collection expression needs a target type, which an object-typed children argument cannot supply. The runtime helper surface accepts object-typed children and normalizes them. |
| `["title", "id"]` dynamic properties | `["title", "id"]` (verbatim) | The dynamic-properties parameter is `string[]` in the helper contract, so the stringified array is already a valid target-typed C# collection expression. |
| `_cache[0] \|\| (_cache[0] = v)` | `frame.GetOrAddCache(0, ...)` | The mount-owned frame provides typed cache identity without an ambient array or falsy-value ambiguity. |
| `(setBlockTracking(-1, true), cache, setBlockTracking(1))` | balanced `frame.SetBlockTracking(-1);` / `frame.SetBlockTracking(1);` statements around the cache write | Statements preserve ordering directly and runtime recovery restores the same frame after a failed render. |
| `[...(cacheExpr)]` | a typed array/list clone emitted at the use site | C# has no spread operator; generated code copies the cached sequence without requiring a mutable global helper ABI. |
| inline handler values (`$event => ...`, method refs) | target-typed `ComponentEventHandler`/`BrowserEventHandler` construction or `frame.CacheHandler(...)` | Explicit runtime delegate types give lambdas and method groups a type in object-valued binding positions; cached handlers are owned by the mount frame. |
| single-statement **call** handler (`$event => (call)`) | `__event => { call; }` (statement-block lambda) | JavaScript has no `void`; C# does, and a parenthesized void call binds no delegate. A call is the only single-statement shape that can be void, so it emits as a statement-block lambda (binds the `Action<…>` overload, discarding any value). Increments/assignments/value expressions yield a value and keep `__event => (expr)`. See the inline-handler divergence note above ([V01.01.05.05.01], issue #143). |
| `$slots` / `$event` | component invocation slot access / `__event` | `$` is not legal in C# identifiers; the event local keeps the [V01.01.05.04] spelling while slots compile into typed lazy `ComponentSlot` closures. |
| `undefined` / `void 0` | `null` | No `undefined` in C#. |
| `{}` (render-slot empty-properties placeholder) | an empty typed binding collection | The generated contract distinguishes component arguments, slots, listeners, and directive modifiers rather than passing an untyped property bag. |
| `{ trim: true }` directive-modifier bag | an `IReadOnlyDictionary<string, bool>` passed to `DirectiveInvocation` | Directive modifiers remain Boolean-valued and cannot be confused with object-valued element/component bindings ([SFC-CG-6], [V01.01.05.03.01]). |
| `const` statements, ` === `/` !== ` (v-memo bodies) | `var`, ` == `/` != ` | Lexical bridging of the JavaScript statement spellings the v-memo transform authors as raw strings. |
| `JSON.stringify(text)` | C# string literal (`SymbolDisplay.FormatLiteral`) | Same role, C# escaping rules. |
| 2-space indent | 4-space indent | Repository C# convention; cosmetic. |

`return`-shape parity is kept: a single text/interpolation root returns the raw string/display string
(the runtime normalizes it), so the generated render method’s return type is
`object?`.

### The generated runtime contract

Generated render methods take a `ComponentRenderFrame` supplied by Core and return `VirtualNode?`.
The contract is public because compiled component assemblies and the runtime must share the same
frame and node identities, but generated calls use ordinary qualified APIs rather than a mutable
ambient helper surface (`[SFC-CG-2]`).

- Blocks emit an ordered `frame.OpenBlock(...)`, descendant construction plus `frame.Track(node)`,
  and `frame.CloseBlock()` sequence. The returned direct-dynamic-descendant snapshot is placed in the
  owning node's `RenderPlan`; statement order supplies sequencing directly.
- Elements, text, comments, static content, fragments, component requests, teleports, and structural
  built-ins construct the corresponding sealed `VirtualNode` variant. A dynamic selector calls
  `DynamicComponents.DynamicComponent` and still returns a node from the same closed algebra.
- Element properties become immutable `ElementBinding` collections. Component inputs become a
  `ComponentInvocation` containing argument, slot, listener, and directive collections; slot laziness
  is preserved by emitted `ComponentSlot` closures.
- `frame.GetOrAddCache`, `SetCache`, `CacheHandler`, and `Memo` own compiler cache slots for one mount.
  Tracking suspension uses balanced `frame.SetBlockTracking(-1)` / `(1)` statements, and failed-render
  recovery is a runtime operation on that same frame.
- Interpolation stringification, class/style normalization, loose equality, and numeric coercion call
  their qualified owning APIs. Lists, property sets, and slot sets dissolve into loops, dictionaries,
  and closures emitted directly into the consumer compilation.

The template compiler remains a runtime-reference-free `netstandard2.0` assembly: these type and member
names occur only in emitted source. `FrameRenderCodeWriter`, generator snapshots, and the compiled-fixture
suite jointly pin the contract.

### Browser directive integration ([V01.01.04.09])

Browser behavior remains Browser-owned. The template compiler recognizes the structural helper tokens in
its intermediate representation, then emits fully qualified Browser types and calls; Core stays free of DOM
knowledge. DOM-directive templates (`v-show`, `v-model`, `@click.prevent`, `@keyup.enter`) therefore compile
end to end without a static facade or an import injected into the consumer. The compiler itself still has no
runtime assembly reference because those qualified names exist only in emitted source. The compiler tests,
Browser tests, and `Assimalign.Viu.Generators.Syntax.CompiledFixtureTests` pin this one-way contract.

What code generation requires of each DOM member, mapped to the runtime machinery it forwards to:

- VShow and the VModel directive tokens lower to `DirectiveInvocation(typeof(VShow), value)` and the
  corresponding qualified Browser directive type. Native `v-model` values are emitted as
  `new ViuModelBinding(exp, value => { exp = value; })`, carrying the current value and reflection-free
  write-back action together inside the invocation.
- `BrowserEvents.WithModifiers(...)` / `BrowserEvents.WithKeys(...)` are the qualified `v-on`
  modifier/key guard calls. Each returns the dispatchable `Action<BrowserEvent>` understood by the event
  invoker registry. The handler parameter is **overloaded**, not a single `Action`: the emitter
  (`VOnTransform`) can write the handler as a value-returning
  inline expression lambda (`__event => (expr)`), a statement-block lambda for a single-statement call
  (`__event => { call; }`, the void-capable shape — see the single-statement call-handler divergence above) or a
  multi-statement body, or a member/method-group reference (`_ctx.save`), and a parenthesized void call cannot
  bind to `Func<BrowserEvent, object?>`. The
  overload set — `Func<BrowserEvent, object?>`, `Action<BrowserEvent>`, `Func<object?>`, `Action` (each with
  `params string[]`) — target-types every one of those without an ambient handler helper.
  Because both return `Action<BrowserEvent>`, a stacked `@keyup.enter.prevent` nests cleanly as
  `BrowserEvents.WithKeys(BrowserEvents.WithModifiers(handler, ["prevent"]), ["enter"])`; the outer call
  resolves the inner result through the `Action<BrowserEvent>` arm.
- Transition lowers to the structural `TransitionNode`; TransitionGroup lowers to a
  `ComponentNode` carrying `ComponentReference.ForName("TransitionGroup")`. Browser's composition
  resolves the named group while Core executes the host-neutral structural transition node.

The generated render method itself is the generator's contract:
`private static global::Assimalign.Viu.Components.VirtualNode? __ViuRender(<ComponentClass> component,
global::Assimalign.Viu.Components.ComponentRenderFrame frame)`. The generator records the emitter's
cache-slot count in `ComponentContract.renderCacheSize`, so Core creates the mount-owned frame at exactly
the compiled size. The method returns the closed-algebra node directly; no root-normalization shim sits
between generated code and the renderer. The compiled-fixture suite builds generator-emitted `.viu` and
compatible `.vue` components against the shipping packages and drives them through the runtime, providing
the end-to-end proof deferred from [V01.01.05.05].

### Render source mapping ([V01.01.05.08])

`RenderFunctionEmitter.Emit` returns a `SyntaxList<RenderSourceMapping>` alongside the code, targeting
Roslyn's `#line` mechanism rather than a browser devtools source map, because a template diagnostic
travels through the C# compiler (`[SFC-8]`). Each dynamic expression `RenderCodeWriter` emits records a mapping: the
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
- **`CacheHandlers` stays off in the generator.** A cached member-expression handler needs a
  variadic-forwarding wrapper, which has no C# spelling yet; `v-once` caching is
  independent and fully emitted.
- **v-slot destructuring and tuple v-for aliases** (`#item="{ label }"`, `(a, b) in pairs`) emit
  verbatim and are not valid C# lambda parameters; they are follow-up expression-binding work.

## Static caching and stringification

The two static optimizations ([V01.01.05.07], issue #54), normative in `[SFC-OPT-1]`–`[SFC-OPT-4]`:
per-instance caching of fully static subtrees, and collapsing contiguous runs of cached, stringifiable
siblings into a single raw-HTML insert.
`StaticCache.Cache` runs from `Transformer.Transform` — after the traversal, before `CreateRootCodegen` —
only when `TransformOptions.HoistStatic` is set (the composition-root generator sets it; the default
pipeline stays unoptimized, which is the acceptance criteria's debugging opt-out). It matters
disproportionately on WASM (PLAN.md founding decision #4): every render node not created and every patch
visit skipped is a `JSImport` marshaling round-trip avoided, so a stringified 20-node run replaces 20+
interop calls with one `insertStaticContent`.

### The constant analysis

`ConstantAnalysis.GetConstantType(node, context)` is the full subtree analysis: an element
is constant only when its generated props, every child, and every `v-bind` expression are constant, and it
carries no patch flag and is not a block. `NOT_CONSTANT` from any of them poisons the ancestor, so refs
(`NEED_PATCH`), runtime directives, dynamic keys (which force a block), and dynamic children/props are never
static — exactly the eligibility the issue pins. Levels are memoized per element in
`TransformContext.ConstantCache`. Because Viu keeps expression bodies opaque, an interpolation or dynamic
`v-bind` value is never above `NOT_CONSTANT`; only static text, static attributes, and compiler-injected
literal constants (`v-if` branch keys, `v-model` modifier objects) reach the higher levels. A fully static
`svg`/`foreignObject`/`math` block is deliberately demoted back to a plain render node as a side effect of
the analysis, because a cached namespace-switching block would be created once and then reused in a
namespace it no longer matches. The context-free `GetConstantType(node)` overload (parser-stamped levels only) still backs the
patch-flag pass, unchanged.

### Caching and marking

Each fully static (`>= CAN_CACHE`) subtree is marked `PatchFlags.Cached` (`-1`, the v3.5 `CACHED` spelling
of the old `HOISTED`) so the runtime diff skips it, then wrapped in a render-cache slot via `context.Cache`
(reusing the same `_cache[n]` seam and `??=` emission v-once already uses). An element whose subtree is
dynamic but whose props are static gets its props cached the same way. **Clone-on-insert is the runtime's
responsibility**: the compiler emits the `CACHED` marker and the `createStaticVNode` payload; the runtime's
DOM adapter clones a cached/static vnode when inserting it into more than one tree (the runtime-area
contract referenced by `createStaticVNode`/`insertStaticContent`).

### Stringification

After the walk, `StaticStringifier.Run` scans a container's children for the largest contiguous run of
cached, stringifiable siblings and — at or above the thresholds — collapses it into a single
`createStaticVNode(html, nodeCount)` whose serialized HTML the runtime applies via one `innerHTML`. The
thresholds are specified values (`[SFC-OPT-2]`) and are pinned by tests; below them the raw-HTML insert
costs more than the nodes it replaces:

| Threshold | Value | Meaning |
| --- | --- | --- |
| `NODE_COUNT` | 20 | consecutive stringifiable nodes |
| `ELEMENT_WITH_BINDING_COUNT` | 5 | consecutive elements carrying attribute bindings |

Serialization reuses the compiler's existing HTML knowledge: `escapeHtml` (`"`, `&`,
`'`, `<`, `>`) so the string round-trips through `innerHTML` to the same DOM (WHATWG fragment serialization),
`CompilerDomKnowledge.IsVoidTag` to omit end tags for void elements, and `IsKnownHtmlAttribute`/
`IsKnownSvgAttribute` plus the `data-`/`aria-` rule for `isStringifiableAttr`. Table-section tags
(`caption,thead,tr,th,tbody,td,tfoot,colgroup,col`) never stringify (innerHTML would reparent them).

### Scope decisions in the optimization passes

Each is deliberate, forced by the no-dynamic-codegen rule or the immutable-AST model, and pinned by
`StaticCacheTests` (the *chosen* behavior, per the deviations rule).

| Alternative considered | Viu behavior | Why |
| --- | --- | --- |
| `context.cache` (vnode subtrees, per-instance `_cache`) **and** `context.hoist` (props objects, per-type module `_hoisted_N` consts) | both route through the per-instance `_cache` seam | the C# generator model owns the render method and has no module-const scope without a new emitter↔generator field contract; each value is still created once per instance and reused across all re-renders — the interop-reduction goal. `context.Hoist`/`result.Hoists` stay reserved for a future per-type static-field seam. |
| constant interpolations / constant `v-bind` values evaluated with `new Function` | not evaluated; `AnalyzeNode` bails on any non-text/comment/element content and on any binding | dynamic code generation is forbidden (AOT). In the opaque-expression model no interpolation or `v-bind` is ever constant, so a stringifiable cached subtree only ever holds static text, comments, and static attributes — the eval branches are unreachable. |
| whole-children-array caching and text-call caching | omitted; each eligible static sibling caches individually | a fully static text run is a single `TextNode` folded into its element's cached subtree, so text-call caching is unreachable; skipping the array cache avoids reconstructing frozen immutable children arrays and leaves only one stringify path to maintain. |
| `stringifyStatic` runs on every container (element, root, `v-for`/`v-if` bodies) | runs on a template root's children and a plain element's children | those are the two containers with clean immutable write-back; static descendants inside `v-if`/`v-for` bodies are still cached, just not stringified (a per-iteration run rarely reaches 20 nodes anyway). |
| after merging, `context.cached` is spliced and trailing cache indices decremented | merged nodes' cache slots stay reserved-but-unused | `CacheExpression.Index` is immutable; leaving the slots reserved keeps the record immutable and the output deterministic, at the cost of a marginally larger `_cache`. |
| `isKnownMathMLAttr` | MathML attributes are never stringifiable | the Viu shared DOM knowledge has no MathML known-attribute table; MathML static content still caches, it just does not fold into an innerHTML string. |

### Non-goals ([V01.01.05.07])

- Per-component-type static fields (the pre-3.5 `_hoisted_N` module consts). All static optimization routes
  through per-instance `_cache`; a static-field emission seam through the generator is deferred.
- Stringification of `v-for`/`v-if` body runs, and of runs interleaved with `TextCall` text (see above).
- Constant-expression evaluation for stringifying constant interpolations / `:bind="1"` (needs an evaluator
  Viu deliberately does not have).
