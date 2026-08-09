# The Viu Specification

## 0. Status, scope, and conformance language

**Status:** Draft — normative for implemented behavior and explicitly adopted migration standards.
**Adopted:** 2026-08-02 (standalone-framework decision).
**Applies to:** every `Assimalign.Viu.*` library, generator, SDK, and extension in this repository.

This document is the normative description of Viu's own semantics. Where an implementation and this
document disagree, one of them is a defect; this document is the arbiter of intent.

**Viu is a standalone C#/.NET WebAssembly UI framework. Its semantics are owned here and by the
repository's conformance tests; external projects do not define them.**

Immediately, the thing that must not be lost from that statement:

> Viu ships a **`.vue` single-file-component compatibility parser** as a product feature
> ([V01.01.06.09], [#250](https://github.com/assimalign/viu/issues/250)). That feature deliberately
> targets the Vue single-file-component container specification, because compatibility with a
> documented external format *is* the feature's requirement. This is a **compatibility target**, in
> the same category as Viu Utilities' Tailwind CSS v4.3.3 target and Viu's WHATWG HTML-serialization
> target — not a claim of semantic derivation. It is specified in [§9](#9-vue-compatibility--a-shipping-feature)
> as a first-class product surface.

### 0.1 Conformance language

The key words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** identify normative
requirements. A statement without one of those words is descriptive: it explains why a requirement
exists, or records an observable consequence of one.

### 0.2 The implemented-behavior rule

**This specification describes implemented behavior only.** Anything not yet implemented appears
solely in [§17 (Non-goals and current limits)](#17-non-goals-and-current-limits) — never in the body
as though it exists. A section that describes a partially implemented capability MUST state its
current limit inline and cross-reference §17. The temporary owner-authorized document-status note
after [DOC-2] identifies the sole migration exception.

### 0.3 Clause identifiers

Normative clauses carry stable identifiers (`EXE-1`, `RND-BLOCK-2`, `SCH-4`, …). Code, tests, and
issues cite them as text — `Specified by [RND-BLOCK-2].` — never as a URL, so the API-reference
generator ([V01.01.13.04]) resolves ids to anchors from one mapping. A clause id is stable once
published: a superseded clause is marked superseded in place rather than renumbered or reused.

### 0.4 Versioning

This document versions with the framework. A change to a normative clause is a specification change
and requires the same review as a behavior change: the clause, the tests that pin it, and the XML
docs that cite it move together.

---

## 1. What Viu is

`[DEF-1]` Viu is a C#/.NET user-interface framework that compiles to WebAssembly through
`Microsoft.NET.Sdk.WebAssembly`, using `System.Runtime.InteropServices.JavaScript`
(`JSImport`/`JSExport`) as its browser boundary.

`[DEF-2]` Viu renders through a **hierarchical virtual-node tree with compiler-informed diffing**.
An application describes its UI as an immutable tree of node descriptions; a build-time compiler
annotates that tree with what can change; the runtime patches only the annotated parts. This is the
architectural idea Viu is built on and the subject of [§6](#6-the-rendering-architecture), the
centerpiece of this specification.

`[DEF-3]` **All template compilation happens at build time.** There is no runtime compiler and no
runtime code generation. Roslyn source generators are the sanctioned metaprogramming mechanism
([§3](#3-execution-model-and-hard-constraints), [§8](#8-the-viu-container-and-the-compilation-pipeline)).

`[DEF-4]` Viu is **AOT- and trimming-safe by construction**. Shipping libraries set
`<IsAotCompatible>true</IsAotCompatible>`; activation is explicit delegate dispatch; nothing is
discovered by reflection at runtime.

`[DEF-5]` Viu is **host-generic**. `Renderer<TNode>` is parameterized on the host node type, while
Core's `IApplication` lifetime contains no host node type. Platform assemblies implement that
lifetime directly and own their mount operations; Browser uses opaque integer DOM handles without
making Browser a dependency of Core.

The principal runtime, compiler, and editor assemblies and their responsibilities:

| Library | Responsibility |
| --- | --- |
| `Assimalign.Viu.Components` | The closed `VirtualNode` vocabulary, authored-component contract, registrations, bindings, render plans, and compiler↔runtime flag values |
| `Assimalign.Viu.Reactivity` | The dependency engine, reference primitives, effects, scopes, watch |
| `Assimalign.Viu.State` | A store convention attached through services and the ambient reactive scope; the registry owns store lifetimes |
| `Assimalign.Viu.Core` | The Application Model, renderer and scheduler engine, hydration, internal built-in executors, and host-facing operations |
| `Assimalign.Viu.Browser` | The browser host adapter: interop bridge, DOM directives, transitions |
| `Assimalign.Viu.ServerRenderer` | HTML serialization and the hydration marker protocol |
| `Assimalign.Viu.Router` / `Assimalign.Viu.Browser.Router` | The DOM-free router core and its browser click/history bridge |
| `Assimalign.Viu.Testing` | The in-memory host and component test wrappers |
| `Assimalign.Viu.Syntax*` | The build-time parser cluster: templates, `.viu`/`.vue` containers, CSS, and HTML |
| `Assimalign.Viu.Compiler.*` | Build-time composition roots for CSS and single-file-component projection |
| `Assimalign.Viu.UtilityCss` | The independently published utility CSS compiler and compatibility engine |
| `Assimalign.Viu.LanguageService` / `.LanguageServer` | Editor semantics and their Language Server Protocol process boundary |

*Authority: `Assimalign.Viu.slnx`; `{libraries,tooling}/*/docs/OVERVIEW.md`; `global.json`.*

---

## 2. Document map and precedence

| Document | Role | Relationship to this specification |
| --- | --- | --- |
| `docs/SPECIFICATION.md` | **Normative.** What Viu is and what it guarantees. | Highest authority for semantics. |
| `tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md` | **Normative for the `.viu` container grammar.** | [§8](#8-the-viu-container-and-the-compilation-pipeline) delegates to it rather than restating it. |
| `docs/UTILITY-CSS-DESIGN.md` | **Normative for Viu Utilities' frozen Tailwind CSS v4.3.3 compatibility contract.** | [§10.4](#104-viu-utilities) delegates to it. |
| `docs/adr/*.md` | Decision records — why a choice was made. Append-only. | Normative for *rationale and constraint*, not for current API shape. A conflict means the ADR needs superseding. |
| `{libraries,tooling}/**/docs/OVERVIEW.md` | What each library is; its public surface. | Non-normative elaboration. MUST NOT contradict this document. |
| `{libraries,tooling}/**/docs/DESIGN.md` | Why each library is shaped that way; local deltas. | Non-normative rationale. |
| `docs/PLAN.md` | Delivery narrative — waves, WBS map, sequencing. | Non-normative. Describes *when*, not *what*. Its "Founding design decisions" section is superseded by this document for anything semantic. |
| `docs/PERFORMANCE-RESEARCH.md` | The external performance-research ledger. | **Explicitly non-normative by construction** ([§18](#18-performance-research-policy)). |

`[DOC-1]` **Precedence.** `SPECIFICATION.md` → `FORMAT.md` / `UTILITY-CSS-DESIGN.md` (within their
declared scopes) → ADRs → library `DESIGN.md` → `PLAN.md`. A lower-precedence document that
contradicts a higher one is wrong and MUST be corrected.

`[DOC-2]` A specification clause MUST be traceable to code or to a normative delegate document. A
claim that cannot be traced does not belong in the body.

---

## 3. Execution model and hard constraints

Every later section depends on this one.

### 3.1 Threading

`[EXE-1]` Each Viu application graph targets **one event loop and is not thread-safe**. Browser
applications use one shared ambient execution state. A request-oriented host that runs independent
graphs concurrently MUST enter a fresh logical execution flow for Core's current component and
scheduler queues, Reactivity's tracking/batching/scope state, and State's setup/active-registry
state. ServerRenderer establishes that boundary for every runtime-tree and compiled render. Values,
scopes, registries, or scheduler jobs MUST NOT be shared across concurrent flows.

`[EXE-2]` Every non-thread-safe public type MUST say so in its XML documentation.

`[EXE-3]` A host MAY dispatch the current logical flow's scheduler flush through a
`SynchronizationContext`. When none is installed the flush falls back to the thread pool and carries
that flow's execution context. A per-flow scheduler gate serializes the fallback continuation with a
synchronous renderer flush; this prevents Viu from racing its own queues and does not authorize a
caller to mutate one graph from several threads. On single-threaded browser WebAssembly dispatch
still lands on the main thread through the JavaScript event loop.

### 3.2 AOT and trimming

`[EXE-4]` Viu MUST NOT use reflection-based serialization, dynamic code generation
(`Reflection.Emit`, compiled expression trees, `DispatchProxy`), or linker-unfriendly activation.
Viu never scans assemblies and never calls `Activator.CreateInstance`.

`[EXE-5]` Component activation is **explicit delegate dispatch**. `IComponentFactory` resolves a
`ComponentReference` to a `ComponentRegistration`; the registration carries the reference, the
static `ComponentContract`, and an explicit `ComponentActivator`. An application MAY close an
activator over a generated resolver, a dependency-injection container, or hand-written composition.

`[EXE-6]` Shipping libraries MUST set `<IsAotCompatible>true</IsAotCompatible>`.

`[EXE-7]` Deviating from [EXE-4] requires explicit confirmation, a documented rationale at the site,
and a test that pins the chosen behavior (`.claude/rules/deviations.md`).

### 3.3 Source generators

`[EXE-8]` Roslyn incremental source generators are the sanctioned metaprogramming mechanism. Two
ship: `Assimalign.Viu.Generators.Reactivity` (reactive property wrappers for `[Reactive]` /
`[ShallowReactive]` partial classes) and `Assimalign.Viu.Generators.Syntax` (`.viu` and `.vue` →
C#).

`[EXE-9]` Generator inputs and outputs MUST be **value-equatable**, because the incremental
generator cache depends on structural equality. This shapes the whole `Assimalign.Viu.Syntax.*`
cluster: immutable records, `SyntaxList<T>`, value-equal descriptors and diagnostics.

`[EXE-10]` Generators run in `netstandard2.0` analyzer hosts with **no file or network I/O**
(`EnforceExtendedAnalyzerRules=true`, RS1035). Work that legally requires writing a file — emitting
a physical stylesheet — is performed by an MSBuild task, not a generator ([§15](#15-packaging-and-the-consumer-surface)).

### 3.4 The interop boundary is the performance budget

`[EXE-11]` **Decision logic lives in .NET; the host side is a dumb applier.** The renderer decides
what changed and emits primitive operations; the JavaScript side applies them without deciding
anything.

`[EXE-12]` Node identity crosses the boundary as an **`int` handle**, never as a marshaled object
proxy. Handle `0` is the reserved "no node" sentinel and the JavaScript side MUST never issue it.
This is what makes an `(opcode, int, string)` command stream flattenable into one interop call; an
object-proxy identity could not be serialized that way.

`[EXE-13]` Patch operations batch into a command buffer applied by **one** interop call per commit
boundary ([§6.6](#66-the-scheduler)).

`[EXE-14]` Host-side handles and event listeners MUST be released deterministically and two-sidedly:
the host registry releases a removed subtree's handles and listeners in the removal call and reports
them back, and .NET purges its listener delegates in the same call.

*Authority: `docs/adr/0001-source-generators-over-reflection.md`;
`docs/adr/0003-batched-interop-dom-operations.md`;
`libraries/Assimalign.Viu.Browser/docs/ADR-0001-interop-marshaling.md`;
`libraries/Assimalign.Viu.Core/src/Scheduling/Scheduler.cs`;
`libraries/Assimalign.Viu.Components/src/Activation/ComponentFactory.cs`;
`libraries/Assimalign.Viu.Components/src/Components/ComponentRegistration.cs`.*

---

## 4. The component model

### 4.1 Four lifetimes

`[CMP-1]` Viu separates four roles that a single object would otherwise conflate:

| Role | Type | Lifetime |
| --- | --- | --- |
| Immutable render description | `VirtualNode` and its sealed variants | One render result; compiler-cached immutable subtrees may retain identity |
| Static identity, contract, and activation | `ComponentReference`, `ComponentContract`, `ComponentRegistration` | Readable for the registration's lifetime, before activation |
| Activated authored behavior | `IComponent` | One instance per mounted component invocation |
| Mounted bookkeeping | Internal Core engine types | Mount through unmount |

`[CMP-2]` `VirtualNode` values are **immutable descriptions**. Mounted host state — host nodes,
anchors, ranges, reactive render effects, parent links, prior-tree state — is owned internally by
Core and MUST NOT be written back onto a `VirtualNode`, an authored `IComponent`, or a static
registration.

The parent-created `ComponentInvocation` (`Arguments`, `Slots`, `Listeners`, `Directives`) and the
mounted `ComponentBindings` (`Parameters`, `Slots`, `FallthroughBindings`) are different lifetimes
and deliberately share no interface. The pure transformation is
`ComponentBindings.Resolve(ComponentContract, ComponentInvocation,
ICollection<ComponentBindingDiagnostic>?)`; the runtime owns per-mount default caching and
initial-warning gating around its diagnostics.

The authored `ComponentContext` is a public abstract Components type with a protected constructor.
Its exact surface is `ComponentBindings Bindings`, `IServiceProvider? Services`,
`ComponentLifecycle Lifecycle`, `IReactiveEffectScope Scope`,
`IReactiveWatchScheduler? WatchScheduler`, `ComponentContext? Parent`,
`Emit(string, params object?[])`, `Expose(object?)`, `Warn(string)`, concrete scoped
`Watch<TValue>(Func<TValue>, Action<TValue,TValue>)`, and protected
`OnWatchError(Exception)`. Core supplies the only runtime implementation. No runtime API accepts a
consumer-derived context, and the context carries no convention-specific or style-scope member.

### 4.2 The tree vocabulary

`[CMP-3]` `VirtualNode` is a closed abstract algebra: its constructor is `private protected`, every
shipping variant is sealed, and a variant fixes its own `VirtualNodeKind`, so kind and runtime type
cannot disagree. The algebra has exactly ten kinds:

| `VirtualNodeKind` | Sealed variant | Describes |
| --- | --- | --- |
| `Element` | `ElementNode` | A qualified host element, bindings, directives, and children |
| `Text` | `TextNode` | A text value |
| `Comment` | `CommentNode` | A comment or empty placeholder |
| `Static` | `StaticNode` | A compiler-trusted static payload |
| `Fragment` | `FragmentNode` | A transparent group of siblings |
| `Component` | `ComponentNode` | A non-activating invocation of registered authored behavior |
| `Teleport` | `TeleportNode` | Content rendered into a host-resolved target |
| `KeepAlive` | `KeepAliveNode` | A component subtree eligible for retained mounted state |
| `Suspense` | `SuspenseNode` | Lazy content and fallback branches coordinated by asynchronous work |
| `Transition` | `TransitionNode` | Lazy content decorated with host-provided transition behavior |

Applications and generated code construct the sealed node types directly. `ComponentNode` carries a
`ComponentReference` and a raw immutable `ComponentInvocation`; it does not activate the component.

### 4.3 Activation

`[CMP-4]` `IComponentFactory` is **only** a registration resolver. `Resolve(ComponentReference)`
returns the matching `ComponentRegistration` or throws when none is registered;
`TryResolve(ComponentReference, out ComponentRegistration?)` probes without activation. The factory
does **not** implement `IServiceProvider`, discover constructors, or activate a component itself.
Each registration exposes exactly `ComponentReference Reference`, `ComponentContract Contract`, and
`ComponentActivator Activator`; its contract is therefore readable before activation.

`[CMP-5]` `IApplicationContext` carries the `IComponentFactory` and nullable `IServiceProvider` as
**independent** values. Services are opt-in. An application MAY supply one object for both roles;
the contracts do not require it.

`[CMP-6]` The built-in `ComponentFactory` stores explicit registrations keyed by
`ComponentReference`. Registering a duplicate reference throws; name identity is ordinal, and an
unregistered reference throws `InvalidOperationException` from `Resolve`. Runtime constructor
discovery is never a fallback [EXE-4].

`[CMP-7]` A `ComponentNode` is a **non-activating** mount request. Its `ComponentReference` uses an
explicit type token or registered name, while its `ComponentInvocation` carries immutable raw
argument, slot, parent-listener, and directive snapshots. Key, mount reference, and `RenderPlan`
belong to the node. Core resolves the reference to a registration at mount time.

`[CMP-8]` `IComponent` has exactly one member: synchronous
`ComponentRenderer Setup(ComponentContext context)`. Core runs setup inside the mounted component's reactive scope before any
asynchronous work can interleave. The renderer receives that mount's `ComponentRenderFrame` and
returns the current immutable `VirtualNode?` subtree; compiler-cached static subtrees MAY retain
identity across renders [SFC-OPT-1].

`[CMP-34]` `ComponentRegistration.Define(string, ComponentContract, ComponentSetup)` creates a
reflection-free code-first registration backed by
`ComponentRenderer ComponentSetup(ComponentContext context)`. It is composition-only: there is no
options-object overload. Hand-built output defaults to `RenderPlan.None` and therefore uses a full
diff unless the author supplies compiler-equivalent plans through the render frame.

### 4.4 Ownership

`[CMP-9]` The **external composition root owns and disposes** the component factory, the service
provider, and the state registry. Core and its application objects *borrow* them and MUST NOT
dispose them.

`[CMP-10]` Core **owns** each activated `IComponent` instance and MUST dispose it on setup failure
or unmount when it implements `IAsyncDisposable` or `IDisposable`; asynchronous disposal takes
precedence when both are implemented.

`[CMP-11]` Viu does not create dependency-injection scopes automatically. A custom activator MAY
bind a scope to the component instance it returns.

### 4.5 Parameters, events, and fallthrough

`[CMP-12]` `ComponentParameter` supports required values, an optional default factory evaluated **at
most once per mounted instance**, and an optional validator. A required-value or validator failure
**warns without discarding** the supplied value.

`[CMP-13]` The declaration name is canonical in `ComponentContext.Bindings.Parameters`. A parent
invocation MAY spell a parameter in camel-case or kebab-case; `ComponentBindings.Resolve` matches
aliases and publishes the value under the canonical declaration name.

`[CMP-14]` `ComponentEvent` MAY validate the complete ordered argument list.
`ComponentContext.Emit` accepts zero or more ordered arguments. A kebab-case emission matches a
camel-case listener.

`[CMP-15]` `ComponentEventListener` receives one immutable `IReadOnlyList<object?>` snapshot for an
emission. Generated or authored adapters MAY project that list into strongly typed handler shapes;
the invocation stores the one listener delegate contract and event delivery itself is synchronous.

`[CMP-16]` A generated listener MAY be wrapped for once-only delivery. Once-state belongs to the
**mounted instance** and survives parent updates. Both an ordinary and a once-only listener MAY run
for one emission.

`[CMP-17]` **Fallthrough.** `ComponentBindings.Resolve` consumes declared listeners as component
events and places undeclared invocation bindings in `FallthroughBindings`. When contract-level
inheritance is enabled and the component renders a single element root, Core merges fallthrough
bindings: classes **space-join**,
style declarations **merge with the parent value winning**, and compatible event delegates
**combine in root-then-parent order**. Declared component-event listeners never enter this
host-event merge.

### 4.6 Slots

`[CMP-18]` Generated slot sets carry a `SlotStability` classification — `Stable`, `Dynamic`, or
`Forwarded` — in the immutable invocation's `SlotStability` property. Core's component-update gate
consumes that classification to **skip** child renders for structurally stable slots while forcing
updates for dynamic and effectively-dynamic forwarded slots.

`[CMP-19]` `ComponentInvocation.SlotStability` defaults to `SlotStability.Stable` for empty or fixed
hand-authored slot sets. A caller whose slot structure can change MUST explicitly supply
`SlotStability.Dynamic`; a caller forwarding its parent's slots MUST supply `SlotStability.Forwarded`.
An over-optimistic classification manifests as a child that silently stops updating.

### 4.7 Lifecycle

`[CMP-20]` `ComponentLifecycle` exposes **named, typed hooks**, not an enum-keyed callback
registry: before-mount, mounted, before-update, updated, before-unmount, unmounted, activated,
deactivated, and `OnServerPrefetch`. Each accepts a synchronous or a `Task`-returning callback.

`[CMP-21]` **Ordinary asynchronous hooks do not delay lifecycle progression.** Core observes the
returned `Task` and routes faults through `OnErrorCaptured` to the application error handler, but
does not await it. `OnServerPrefetch` is the sole awaited hook, and only during server rendering
([§11](#11-server-rendering-and-hydration)), because serialization must wait for its data.

`[CMP-22]` `ComponentLifecycle` exposes the **component-lifetime `CancellationToken`**. It is
cancelled during unmount, *after* before-unmount callbacks start and *before* effect-scope and
subtree teardown.

`[CMP-23]` The application-level error handler is the **terminal sink** for observed render,
lifecycle, watcher, and event faults that no ancestor `OnErrorCaptured` hook stopped.

### 4.8 Explicit component-model seams

`[CMP-24]` Viu has **no hierarchical component-tree dependency API**. Component dependencies are
explicit:

- parameters and slots for parent-to-child data;
- nullable `ComponentContext.Services` for application-composed services;
- the ambient reactive scope for scope-bound conventions; and
- `ComponentReference` values for deliberate registration resolution at mount.

This is a decision, not a deferral (see [§17](#17-non-goals-and-current-limits)). It has visible
consequences elsewhere: `RouterView` takes its nesting depth as an explicit argument
([§12](#12-routing)) precisely because no ambient hierarchical channel exists.

`[CMP-33]` A library MAY attach to the component model only through a designed seam: the host
contract and public operations, `ComponentContext.Services` plus the ambient reactive scope, the
generated-code ABI, or application composition. Capability discovery by casting a
`ComponentContext`, cross-library friend access, and bridge interfaces are prohibited. If an
integration needs one, the seam is missing and the seam — not a shim — MUST be fixed.

#### Application composition and lifetime

`[APP-1]` A runnable `IApplication` is coordinated by Core's public sealed
`ApplicationLifetime` over the promoted `ApplicationState` values **Created → Starting → Running →
Stopping → Stopped**, with `Failed` edges from Starting, Running, and Stopping. Constructing the
lifetime claims its application context exactly once; a second attachment is rejected. `StartAsync`
synchronously claims execution by moving from Created to Starting exactly once, begins the
middleware pipeline as an independently observed asynchronous task, and waits until the host
terminal has mounted and signalled Running. Every later `StartAsync` call throws, including after
stopping or failure. `IApplicationContext.IsRunning` is true only between that mounted signal and
the beginning of stopping. An already-cancelled token is observed only after the claim and therefore
follows the ordinary **Starting → Stopping → Stopped** path without mounting.

`ApplicationLifetime` owns the platform-invariant transitions exposed by `StartExecution`,
`SignalRunning`, `RequestStopping`, `CompleteStopping`, and `Fail`, plus `State`, `HasFailed`,
`Stopping`, `IsStoppingCancellation`, and `Dispose`. `Fail` MUST cancel `Stopping` before it invokes
the one-shot error report, so observers see shutdown requested before failure notification.

`[APP-2]` Application composition and runtime behavior are separate phases. The lean
`IApplicationBuilder` exposes only `ConfigureApplication(Action<ApplicationOptions>)` and `Build()`.
`ApplicationOptions` is the single builder composition surface for the root component, component
factory, nullable opt-in service provider, state registry, directive resolver, and diagnostics. `Build()` snapshots
those borrowed values into a read-only `IApplicationContext`; later option mutation cannot alter the
built context. `Use(ApplicationMiddleware)` decorates the already-built application's live execution
and cannot add or replace composition.

`[APP-3]` Middleware registration freezes when execution begins: `Use` after `StartAsync` or a
lower-level Browser mount operation has started throws. Registrations are ordered entries, not a set;
registering the same middleware instance twice executes it twice.

`[APP-4]` For two middleware registrations, execution is nested in registration order and cleanup is
nested in reverse order:

```text
first before
  second before
    initialize host → resolve mount target → mount or hydrate
    signal Running; wait for IApplicationContext.Stopping
    unmount
  second cleanup
first cleanup
```

The terminal delegate MUST remain pending after mount until `IApplicationContext.Stopping` is
signalled; `StartAsync` returns at the mounted signal without completing that pipeline task. Returning
the terminal immediately after mount would run middleware `finally` blocks while the application was
still live. The `RunAsync` extension spans the full lifetime as **StartAsync → wait for Stopping →
StopAsync**.

`[APP-5]` Cancellation of the `RunAsync` token, or a call to `StopAsync`, signals
`IApplicationContext.Stopping`, begins stopping, unmounts the live tree, and then unwinds middleware
cleanup in reverse registration order. `StopAsync` awaits the pipeline task; its own token cancels only
that caller's wait, never the cleanup. Cleanup surrounding a failing inner delegate still runs through
ordinary asynchronous `try`/`finally` semantics. A pipeline failure after startup moves the
application to Failed, is reported once through `IApplicationContext.ErrorHandler`, and remains on the
pipeline task so `StopAsync` and `RunAsync` surface it. Startup and host failures call
`ApplicationLifetime.Fail`, whose cancel-before-report and one-shot behavior is normative [APP-1];
cancellation requested through Stopping is normal shutdown.

`[APP-6]` The component factory, nullable service provider, state registry, directive resolver, and other
composition dependencies are **borrowed**. Viu never disposes them when an application stops, fails,
or is asynchronously disposed; their external composition root retains ownership [CMP-9].

`[APP-7]` Top-level startup is asynchronous only. `IApplication` exposes `StartAsync` and `StopAsync`;
`RunAsync` is an extension composing those operations, and Viu exposes no synchronous start or run
because blocking asynchronous host initialization is unsafe on single-threaded WebAssembly.
Lower-level mount APIs live in `Assimalign.Viu.Browser` for embedding and tests and explicitly bypass
top-level lifetime middleware. Core carries no generic mount abstraction. Server rendering is a
separate per-render composition and also does not participate in this pipeline [SSR-2].

### 4.9 Attribute-declared parameters and events

`[CMP-26]` A component MAY declare its inputs on the **properties that receive them**: `[Parameter]`
on a settable instance property of a `.viu` / `.vue` `@script` class. The single-file-component source
generator reads the attribute at build time and synthesizes the equivalent `ComponentParameter` into
the static `ComponentContract` carried by the generated `ComponentRegistration`. The attribute is a
**compile-time declaration only** — nothing is discovered by reflection, and the resulting
declaration is the same static value a hand-authored registration supplies [CMP-12]. The attributes
take effect only inside a compiled single-file component's script block; a hand-authored
`IComponent` declares its surface through its registration's contract.

`[CMP-27]` **Name derivation.** The canonical argument or event name is the **camel-case spelling of
the declaring member's name**: the leading run of upper-case letters lower-cases whole, except that a
run longer than one keeps its last letter capitalized when a lower-case letter follows it (`Title` →
`title`, `ModelValue` → `modelValue`, `URL` → `url`, `HTMLContent` → `htmlContent`). The attribute's
`Name` overrides the derivation and MUST be a non-empty constant string literal, which is how a
spelling no C# identifier can produce — `update:modelValue`, `model-value` — is declared. The derived
name is canonical in `ComponentContext.Bindings.Parameters`, and a parent's kebab-case spelling still
resolves to it [CMP-13].

`[CMP-28]` **Requiredness.** A parameter is required when the attribute sets `IsRequired` **or** when
the property carries the C# `required` modifier. A generated registration's explicit activator uses
a parameterless `new T()` [EXE-4], so no object initializer exists to satisfy C#'s own required-member rule: a
`required` declared parameter therefore makes the generated partial emit a `[SetsRequiredMembers]`
parameterless constructor used by its explicit activator delegate. The requirement is Viu's to
enforce ([CMP-12] warns at mount), not C#'s. The scaffold emits no constructor when the script block
declares one of its own.

`[CMP-29]` **Binding.** The generated scaffold assigns each declared property from
`ComponentContext.Bindings.Parameters` **once during setup, before `OnSetup`, and again at the head
of every render pass**. Core replaces the resolved bindings before a child re-renders, so a declared
property always reflects the parent's current value. The property's value at setup time — its
initializer, or the type's default when it has none — is captured **once per mounted instance** as
that parameter's default and restored on any pass where the parent supplies no argument; the capture
is the attribute form's equivalent of `ComponentParameter.DefaultFactory` and shares its at-most-once
evaluation [CMP-12]. A value whose runtime type is incompatible with the property yields that type's
default, with no coercion.

`[CMP-30]` **Events.** A component MAY declare an output event on the method that emits it:
`[Event]` on a non-generic, instance `partial void` method with no body and by-value parameters only.
The generator synthesizes the `ComponentEvent` — whose validator asserts the emitted argument count —
and implements the method as a `ComponentContext.Emit` of the declared name with the method's
parameters as the ordered payload. A method rather than a property is the anchor because an event
carries a payload *signature* rather than a value; the consequence is that the event name is spelled
exactly once, in the attribute, and the component's own call site is strongly typed.

`[CMP-31]` **Explicit authored-contract opt-in.** `ComponentBase` deliberately does not implement
`IComponent`. It is an optional authoring base that stores the protected `ComponentContext`; the
authored or generated partial MUST opt into `IComponent` and provide `Setup` explicitly. This keeps
base-class convenience separate from the authored contract and prevents an incomplete partial from
becoming activatable accidentally.

### 4.10 Root-level lifecycle registration

`[CMP-32]` A compiled single-file component MAY register a lifecycle callback **at the root of its
own class** — `OnMounted(callback)` — instead of through the context —
`Context.Lifecycle.OnMounted(callback)`. Generator-emitted internal glue supplies the protected
forwarding members; `ComponentBase` itself does not declare them [CMP-31], [SFC-CG-4]. The root form
is the **specified equivalent** of the context form: it registers the same callback with the same
`ComponentLifecycle`, so the two forms carry identical timing [CMP-20], identical asynchronous
observation [CMP-21], and identical error routing [CMP-23]. Callbacks registered for one phase run in
**registration order**, and that order is the order the registrations were made regardless of which
form each used, so the two forms MAY be mixed freely within one component.

A component MAY declare its own member with one of these names. Root-level lifecycle names are not
reserved: generated glue MUST leave an authored member authoritative at its call sites, and the
context form remains available. A different signature is an ordinary overload. A collision therefore
degrades to the behavior the component would have had without the root convenience.

*Authority: `libraries/Assimalign.Viu.Components/src/Abstraction/{IComponent,IComponentFactory}.cs`;
`libraries/Assimalign.Viu.Components/src/{ComponentContext,ComponentRenderFrame}.cs`;
`libraries/Assimalign.Viu.Components/src/{Components,Tree,BuiltIns,Activation,Delegates,Optimization}/*.cs`;
`libraries/Assimalign.Viu.Core/src/{Rendering,Internal,Abstraction}/*.cs`;
`libraries/Assimalign.Viu.Core/src/Abstraction/{IApplication,IApplicationBuilder,IApplicationContext}.cs`;
`libraries/Assimalign.Viu.Core/src/Delegates/{ApplicationDelegate,ApplicationMiddleware}.cs`;
`libraries/Assimalign.Viu.Core/src/Application/{ApplicationContext,ApplicationOptions}.cs`;
`libraries/Assimalign.Viu.Core/src/Extensions/ApplicationExtensions.cs`;
`libraries/Assimalign.Viu.Browser/src/{BrowserApplication,BrowserApplicationBuilder}.cs`;
`docs/COMPONENT-MODEL-PLAN.md` §§2, 8, 9.*

---

## 5. Reactivity

### 5.1 The type model

`[RCT-1]` `ReactiveValue` / `ReactiveValue<T>` is the engine base class; it holds the dependency
cell inline as a field. `IReactiveReference`, covariant get-only
`IReactiveReadOnlyReference<T>`, and mutable `IReactiveReference<T>` are the public substitutable
contracts. Every Reactivity-owned public interface is prefixed `IReactive*`.

`[RCT-2]` Hot-path dispatch rule: per-trigger notification, patching, and diffing MUST dispatch
through an **abstract base-class vtable**, not an interface. Interface dispatch is for cold public
API boundaries. First-party references therefore derive from `ReactiveValue<T>` *and* implement
`IReactiveReference<T>`.

`[RCT-3]` The reference primitives are `Reference<T>`, `ShallowReference<T>`, `CustomReference<T>`,
and `Computed<T>`. `Computed<T>` plays a dual role in the dependency graph — it is both a value
others subscribe to and a subscriber to its own sources — and realizes the subscriber half by
**composition** over an internal sealed subscriber rather than by multiple inheritance. Their
constructors, plus `ReactiveEffect` and `EffectScope` construction, are non-public; `Reactive` is
the sanctioned creation facade.

`[RCT-4]` An external `IReactiveReference<T>` implementation is responsible for tracking its own
reads and triggering on changed writes; the interface cannot enforce correct tracking.
`Reactive.CustomReference(...)` is the preferred extension point. Operations needing direct
dependency access (forced triggering, graph inspection) additionally require
`IReactiveTrackedReference`.

### 5.2 The public surface

`[RCT-5]` `Reactive` is the static facade: `Reference`, `ShallowReference`, `CustomReference`,
`Computed`; `Effect`; `EffectScope`, `CurrentScope`, `OnScopeDispose`; `Watch`, `WatchEffect`;
`TriggerReference`; `PauseTracking`, `ResetTracking`, `Batch`; and the inspection
and escape hatches `IsRef`, `Unref`, `ToRef`, `IsReactive`, `IsReadOnly`, collection-specific
`ToRaw`, and `MarkRaw`. Generic identity conversion and raw-object conversion are not part of the
surface; generated objects instead expose a typed `ToRawValues()` view over their backing values.
`ReactiveValue<T>.Peek()` returns a fresh value without subscribing the ambient caller, while a
stale computed still refreshes and tracks its own sources. Tracking state is restored if the read
throws. `ReactiveEffect` implements `IDisposable`; `Dispose()` is exactly the idempotent `Stop()`
operation. `Reactive.CurrentScope` is the only public ambient-scope accessor. `Reactive.Batch()`
returns an idempotent disposable that closes exactly the one nesting level it opened: inner
disposal never flushes while an outer batch remains, outermost disposal flushes queued effects,
and exception unwind through `using` restores effect delivery. The allocation-free raw start/end
pair is internal to the dependency engine.

### 5.3 There is no proxy

`[RCT-6]` **Viu has no object-proxy interception layer.** Reactive objects are `[Reactive]` /
`[ShallowReactive]` partial classes whose per-property reactive wrappers are emitted by a source
generator. Reactive collections are dedicated types — `ReactiveList<T>`,
`ReactiveDictionary<TKey,TValue>`, `ReactiveSet<T>` — that implement the BCL collection interfaces
rather than wrapping BCL types.

`[RCT-7]` Consequence: **there is no implicit deep reactivity.** An author opts in per class or per
collection. This is more predictable and less magical, and it is what makes the model trimming-safe.

`[RCT-8]` Consequence, and a hard contract with [§8](#8-the-viu-container-and-the-compilation-pipeline):
because there is no proxy to auto-unwrap a reference, **the compiler alone decides every access
form**. `.Value` therefore appears in read *and* write positions in generated code, and a change to
`ReactiveValue<T>.Value` requires a matching change to the expression-binding contract [SFC-6].

### 5.4 The dependency engine

`[RCT-9]` The engine is a **version-counter and doubly-linked subscriber-edge** graph.
`SubscriberLink` and `Subscriber` are publicly readable, with mutating members internal:
introspection is allowed, mutation is not.

`[RCT-10]` **An effect scope is a lifetime boundary, not a subscription broadcast.** Nothing updates
because a value changed; a component, computed, watch, or render updates only after *it reads* the
value. Stopping a scope stops the effects it owns.

`[RCT-11]` A computed is deliberately **not owned by an effect scope**: scope ownership exists to
stop side effects, and a computed has none. A computed created inside a scope keeps serving fresh
values after that scope stops; its cleanup is driven by its subscriber count.

`[RCT-12]` A standalone watch runs **synchronously** unless the caller supplies an
`IReactiveWatchScheduler`. Core supplies the application scheduler adapter
(`ApplicationWatchScheduler`), which routes watch callbacks into the scheduler's pre-flush phase
([§6.6](#66-the-scheduler)).

*Authority: `libraries/Assimalign.Viu.Reactivity/src/{Reactive.cs,References/,Effects/,Watch/,Collections/,ReactiveObjects/,Abstraction/,Reactive/}`;
`libraries/Assimalign.Viu.Reactivity/docs/DESIGN.md` (type model, interface naming, engine boundary);
`libraries/Assimalign.Viu.Core/src/Scheduling/ApplicationWatchScheduler.cs`;
`docs/adr/0002-ref-first-reactivity.md` (rationale).*

---

## 6. The rendering architecture

This is the centerpiece. Everything in [§3](#3-execution-model-and-hard-constraints) exists to make
this section's guarantees affordable.

### 6.1 The hierarchical tree

`[RND-1]` A render produces an **immutable tree result**. Rendering never mutates the prior tree;
compiler-cached static subtrees MAY retain reference identity across results [SFC-OPT-1].
`Renderer<TNode>` reconciles the returned tree against the mounted representation of the old one
and emits the minimal host operations that reconcile them.

`[RND-2]` The mounted representation is a parallel hierarchy of internal sealed engine types rooted
per host container. It is **occurrence-based**: every position in a render result owns distinct
mounted bookkeeping and host state, even when multiple positions reference the same compiler-cached
`VirtualNode` description [SFC-OPT-1]. Mounted nodes own host nodes, ranges, anchors, child lists,
block-local mounted dynamic-occurrence lists, directive bindings, transition state, reference jobs,
and links to the prior immutable descriptions. No mounted engine type is part of the authoring
vocabulary.

`[RND-3]` Mounted component bookkeeping additionally owns the activated `IComponent`, its runtime
`ComponentContext`, reactive render effect, per-mount `ComponentRenderFrame`, and mounted subtree. A
frame cache MAY share one immutable description among positions within that activation, but never
shares mounted bookkeeping or a host node. **That state never returns to the immutable authoring
model** [CMP-2].

`[RND-4]` Each optimized mounted block owns an ordered list of mounted dynamic occurrences aligned
one-for-one with its current `RenderPlan.DynamicChildren`. A compatible patch pairs mounted occurrence
`i` with the next description at `DynamicChildren[i]`; a replacement is written back to both the
mounted ownership hierarchy and the block-local list. An incompatible shape takes the full-diff path
and rebuilds the list when the resulting association is unambiguous. When a tracked description is
also present in untracked positions within the same block scope, description identity cannot identify
the tracked subset; the block MUST take the full-diff path and keep its mounted occurrence list
unavailable. `VirtualNode` reference identity is description identity, **not mount
identity**: repeated references remain distinct mounted occurrences, and no representative selected
by description identity may drive patching or teardown.

`[RND-5]` `Renderer<TNode>.Render(node, container, application)` mounts on first call and patches
thereafter. Passing a null `VirtualNode` unmounts the current root and forgets the container. A
mounted container retains the first supplied `IApplicationContext`; supplying a different one throws
`InvalidOperationException`.

`[RND-6]` `MountedComponentView<TNode>` (where `TNode : class`) is the public cold-path view of one
mounted authored component. It exposes exactly `ComponentNode Request`, `IComponent Instance`,
`ComponentContext Context`, `TNode? FirstHostNode`, `TNode? LastHostNode`, and
`bool IsMounted`. Core caches one view per mounted component node, so the view's reference identity
is stable for that node across enumerations for the life of the mount; consumers reacquire views
after a flush instead of retaining engine objects.

### 6.2 The flag vocabulary

`[RND-FLAGS-1]` `PatchFlags`, `ShapeFlags`, and `SlotStability` (owned by
`Assimalign.Viu.Components`) are
**the interface between build-time analysis and runtime patching**. Their bit layout is a frozen
contract between compiled output and the runtime: changing a value silently breaks components
compiled by an earlier Viu, so **values are additive only**. Naming the zero `PatchFlags` value
`None` is additive and does not change the frozen layout.

`[RND-FLAGS-2]` `PatchFlags` positive members are single bits and combine with bitwise OR: `Text`,
`Class`, `Style`, `Properties`, `FullProperties`, `NeedsHydration`, `StableFragment`, `KeyedFragment`,
`UnkeyedFragment`, `NeedPatch`, `DynamicSlots`, `DevelopmentRootFragment`.

`[RND-FLAGS-3]` `Cached` (`-1`) and `Bail` (`-2`) are **whole-value sentinels, never bit
combinations**. Because every negative `int` has most bits set, a naive bitwise test against a
negative value spuriously succeeds. Every positive-bit check MUST therefore be gated on
`flags > 0`; every runtime and generated predicate MUST enforce that gate. `Cached` marks a subtree the diff
skips entirely; `Bail` marks a tree that MUST fall back to a full diff.

`[RND-FLAGS-4]` `ShapeFlags` retains its frozen node/child-shape values for previously compiled
output. The closed `VirtualNode` algebra is authoritative for runtime shape dispatch; the enum's
layout remains stable even where a current runtime path no longer consumes it.

`[RND-FLAGS-5]` `SlotStability` is a plain enumeration, not a bitmask: a slot collection has exactly one
of `Stable`, `Dynamic`, `Forwarded`. `ComponentInvocation.SlotStability` transports that value from
compiled output to Core, defaults to `Stable`, and is consumed by the component-update gate after
`Forwarded` is resolved against the active parent.

`[RND-FLAGS-6]` `PatchFlags.cs` and `SlotStability.cs` are `<Compile Include>`-linked into the
`netstandard2.0` generator projects. **Their file paths are frozen**; moving them requires updating
every linking csproj in the same change. The authoritative paths are
`libraries/Assimalign.Viu.Components/src/{PatchFlags.cs,SlotStability.cs}`.

### 6.3 The block tree

The block tree is the mechanism that turns compile-time knowledge into skipped runtime work.

`[RND-BLOCK-1]` `RenderPlan` carries the compiler→runtime hints on every `VirtualNode`:

- `PatchFlags` — what may change;
- `DynamicBindingIndices` — the element-binding indices that may change, or null when unknown; and
- `DynamicChildren` — the ordered direct dynamic occurrences collected for a block root; repeated
  references are retained rather than deduplicated.

`RenderPlan.None` is the metadata for hand-authored, unoptimized values and requires the normal full
diff.

`[RND-BLOCK-2]` **The three-state rule for `DynamicChildren` is normative:**

| `DynamicChildren` | Meaning | Runtime behavior |
| --- | --- | --- |
| `null` | Not a block | Full child walk |
| non-null, **empty** | An optimized block with no dynamic descendants | **Skip every child visit** |
| non-null, non-empty | An optimized block | Patch the listed descendants directly |

`IsBlock` is defined as `DynamicChildren is not null`. Confusing the null and empty cases is the
single most consequential error a producer of this metadata can make.

`[RND-BLOCK-3]` **Superseded emission form.** Generated render code uses statement-form calls against
its per-mount `ComponentRenderFrame`: `OpenBlock()`, `Track(VirtualNode)`, then `CloseBlock()` to
obtain the immutable direct-descendant snapshot attached to the block root's `RenderPlan`.
A separate expression-sequencing token and its helper family do not exist in this contract. `RenderPlan` copies
its list inputs into read-only snapshots, so attached metadata cannot be mutated. Every `Track` call
appends one occurrence in order, including repeated calls with the same `VirtualNode` reference.

`[RND-BLOCK-4]` **Block patching is attempted only when the old and new block shapes agree.** The
renderer requires both `DynamicChildren` lists to be non-null and **equal in count**, plus an old
block-local mounted occurrence list of the same count whose entry `i` is live and still linked to old
`DynamicChildren[i]`. Association MUST be unambiguous: if a tracked description reference also occurs
outside the tracked list in that block scope, the renderer MUST fall back to a full child diff. If any
condition fails, the renderer MUST full-diff, then rebuild the mounted occurrence list only when the
resulting association is unambiguous. A mismatched or ambiguous block shape is a correctness event,
never a crash.

`[RND-BLOCK-5]` When block patching succeeds, each dynamic descendant is patched **in place, in its
own host parent and by occurrence-list index**, bypassing the parent children diff. If patching
replaces a mounted node (a type change), the renderer MUST thread the replacement through both the
mounted ownership graph and the block-local occurrence list so later moves and unmounts never retain
the removed node.

`[RND-BLOCK-6]` **Block-aware teardown.** Unmounting a block visits its stored mounted dynamic
occurrences, preserving distinct visits when descriptions are aliased. Two cases retain the full
walk, because skipping them would leak: non-positive
patch-flag trees (`None`, `Cached`, or `Bail`), and fragments that are not `StableFragment` — that is,
keyed and unkeyed fragment blocks. No unmodeled once-tracking bit participates in this decision.

`[RND-BLOCK-7]` A child skipped by an optimized teardown MUST still be *released*: its
occurrence-local mounted bookkeeping is released, it is marked unmounted, and its pending reference
job is invalidated. A skipped child that is a `ComponentNode` or `TeleportNode`, carries a
`MountReference`, a node lifecycle hook, a directive binding, or a transition MUST receive a **full
unmount visit** instead of a release, because those carry external effects.

### 6.4 Patch dispatch

`[RND-PATCH-1]` `Patch` decides in this order:

1. no mounted node → **mount**;
2. the same `VirtualNode` instance by reference → **no-op** (the tree did not change here);
3. not the same node type → **unmount the old subtree and mount the new one** at the old node's
   next-sibling anchor, preserving the old owner context;
4. otherwise → dispatch to the per-kind patch routine.

`[RND-PATCH-2]` `IsSameNodeType` requires the same sealed `VirtualNode` variant and equal `Key`, and
then, by variant: equal `QualifiedName` for elements; equal `ComponentReference` for component
invocations; equal format and ordinal content for static ranges. Text, comment, fragment, teleport,
and structural built-in nodes match on concrete type and key unless their executor specifies a
stronger identity rule.

`[RND-PATCH-3]` **Element patching** returns early for a cached element, and otherwise selects one of
four paths:

| Condition | Attributes | Children |
| --- | --- | --- |
| `PatchFlags == Cached` (early return) | none | none — only the transition and `MountReference` update |
| block children patched | flag-selective | skipped, except a `Text` fast path |
| either side is a block but block patching failed | full diff | full diff, forced to `Bail` |
| positive patch flags | flag-selective | only the `Text` fast path |
| otherwise | full diff | full diff under the new flags |

`[RND-PATCH-4]` **Flag-selective binding patching**: `FullProperties` degrades to a full binding diff;
otherwise `Class` patches only `class`, `Style` patches only `style`, and `Properties` patches exactly the
indices listed in `RenderPlan.DynamicBindingIndices`. A null index list is unknown and therefore
falls back to the full binding diff. Non-positive flags patch nothing.

`[RND-PATCH-5]` **Attribute value comparison.** An attribute is written to the host only when its
value changed — except `value`, which is **always** written, because a host may have mutated it out
of band (a user typing into an input).

`[RND-PATCH-6]` **Attribute ordering on mount.** Every attribute except `value` is applied first, in
declaration order; `value` is applied last. A host's interpretation of `value` can depend on
attributes like `type`, so ordering is load-bearing.

`[RND-PATCH-7]` Attribute names beginning with `onVnode` are **node lifecycle hooks**, not host
attributes. They are consumed by the renderer and MUST NOT reach the host attribute layer.

`[RND-PATCH-8]` Text patching writes the host text node only when the ordinal string comparison
differs. Comment patching updates registration only.

### 6.5 Keyed reconciliation

`[RND-KEY-1]` `PatchChildren` selects **positional reconciliation** when the parent's patch flags
are positive and include `UnkeyedFragment` — the compiler's declaration that the children never
reorder. Every other case uses the keyed algorithm.

`[RND-KEY-2]` **Positional reconciliation** patches the common prefix pairwise, unmounts the surplus
old children, and mounts the surplus new children. No key comparison is performed.

`[RND-KEY-3]` **Keyed reconciliation** runs in three passes:

1. **Index the new children.** Build a key→index map over the new list. Duplicate keys are reported
   through the application warning handler. Mixing keyed and unkeyed non-comment children in the
   same list is likewise warned: the diff cannot track them reliably.
2. **Match and patch.** Walk the old children in order. A keyed old child matches the new child at
   its key when that index is unclaimed and `IsSameComponentType` agrees. A keyless old child
   matches the first unclaimed keyless new child of the same type. An old child with no match is
   unmounted. Every matched pair is patched in place, and a match whose target index moves backwards
   sets the `moved` flag.
3. **Place, right to left.** Walk the new list from the end. A position with no matched old child is
   mounted before the anchor formed by the already-placed next sibling. A matched position moves
   only if the tree `moved` **and** the position is not on the **longest increasing subsequence** of
   old indices — the maximal set of nodes that are already in relative order and therefore need no
   host move.

`[RND-KEY-4]` The longest-increasing-subsequence pass exists to make host moves **minimal**, not
merely correct: a naive placement pass would move every node after the first reorder. Positions with
no matched old position (encoded as `0`) are excluded from the subsequence.

`[RND-KEY-5]` When nothing moved, the subsequence pass is skipped entirely.

`[RND-KEY-6]` **Fragment and static ranges move as units.** A mounted node exposes a first and last
host node; moving it walks the host sibling chain from first to last and re-inserts each node before
the anchor. Removing a range walks the same chain. A range therefore never has to be decomposed into
individual node moves by its parent.

### 6.6 The scheduler

The scheduler is where "when does the host see my change" is answered. All of the following is
pinned in one file.

`[SCH-1]` **Coalescing.** Jobs queued during one synchronous turn coalesce into a **single flush**,
posted as a continuation on the current `SynchronizationContext`, or to the thread pool when none is
installed [EXE-3].

`[SCH-2]` **Phase ordering** within a flush is normative:

1. **pre-flush jobs** (watcher callbacks) — before render jobs of the same order key;
2. **render jobs**, in `OrderKey` order, so a **parent updates before its children**;
3. **post-flush callbacks** — the mounted/updated lifecycle phase.

`[SCH-3]` `OrderKey` is derived from the component identifier with the pre-flush bit as the low bit,
so a pre-flush job sorts ahead of a render job with the same identifier. An identifier-less job
sorts **last**, except an identifier-less *pre-flush* job, which sorts **first** — an
instance-less pre-watcher runs ahead of every render.

`[SCH-4]` **Post-flush ordering is a stable sort** by `OrderKey`, then by queue-assigned
`InsertionSequence`. The stability is load-bearing: it is what makes `Mounted` fire **child-first**.
An unstable sort here silently reorders lifecycle callbacks.

`[SCH-5]` **Deduplication.** A job carries a `Queued` flag and is deduplicated by instance while
queued. A job that queues itself while running is deduplicated away **unless** `AllowRecurse` is
set, which clears the flag before the run.

`[SCH-6]` A job queued *during* a flush is inserted into the running flush in order-key order, over
the not-yet-executed span only.

`[SCH-7]` **`InvalidateJob`** removes a not-yet-run job from the queue. A parent-driven component
update runs the child's effect synchronously and MUST cancel the reactive update already queued for
that instance.

`[SCH-8]` **Recursion limit.** One job may execute at most **100** times within a single flush
chain. Exceeding it throws `InvalidOperationException` naming the job — the diagnostic for a
reactive effect that mutates its own dependencies.

`[SCH-9]` **`NextTickAsync()`** returns a task that completes after the current or next flush chain, and
`Task.CompletedTask` when nothing is queued. A post-flush callback that queues more work causes
another cycle to run — sharing the same recursion bookkeeping — **before** `NextTickAsync` resolves.

`[SCH-10]` **The commit boundary fires twice per flush** and MUST be idempotent (a no-op when
nothing is buffered):

1. after the job queue drains and **before** post-flush callbacks, so `mounted`/`updated` hooks that
   read the host (layout, mount references) observe the committed render; and
2. **after** post-flush callbacks, because those hooks — and post-flush directive hooks — can
   themselves write, and their writes must commit in the same flush rather than strand until the
   next one.

A steady-state render flush with no post-flush writes therefore still crosses the boundary exactly
once; the second call finds an empty buffer.

`[SCH-11]` A synchronous render (a direct `Render` or a mount) performs the same drain —
pre-flush, commit, post-flush, commit — so lifecycle hooks fire before the render call returns. It
is a **no-op while a scheduled flush is running**: that flush owns the drain, and nested synchronous
renders are never double-applied.

`[SCH-12]` **On exception**, the flush abandons the remaining queue deterministically: queued flags
are cleared so every job can re-queue, `NextTickAsync` is resolved so awaiters do not hang, and the
exception is rethrown to the host.

### 6.7 Host abstraction

`[RND-HOST-1]` `RendererOptions<TNode>` is the complete host contract. Required operations:
`Insert`, `Remove`, `CreateElement`, `CreateText`, `CreateComment`, `SetText`, `ParentNode`,
`NextSibling`, `PatchAttribute`. Optional operations: `ResolveTeleportTarget`, `Commit`,
`InsertStaticContent`, `CreateHydrationReader`, `ScheduleHydrationTrigger`. A generated static
style-scope identifier is an ordinary element attribute under [STY-1]; no separate host stamping
operation exists.

`[RND-HOST-2]` A capability whose operation is absent is **unavailable, not degraded**. Rendering an
`StaticNode` requires `InsertStaticContent`; hydration requires `CreateHydrationReader`; and a
non-immediate hydration strategy requires `ScheduleHydrationTrigger`. Each throws
`NotSupportedException` when its capability is absent.

`[RND-HOST-3]` **Core contains no host handles and no interop.** A host that uses a value-type
handle reserves its default value for "no node". `Assimalign.Viu.Browser` is one adapter
(`TNode = int`); `Assimalign.Viu.Testing` is another (`TNode = TestNode`). Neither is a dependency
of Core, and a future WebView2 host supplies its own handle type without touching Components,
Reactivity, State, or Core.

`[RND-HOST-4]` `RendererOptions<TNode>.Commit` is **the batch seam**. A buffered host supplies it;
Core queues it per renderer — never as a process-global hook — and drains it at the commit
boundaries of [SCH-10].

### 6.8 The interop budget realized

`[RND-IO-1]` The browser host serializes writes into a **binary command frame** of primitive
operations and applies them in one interop call per commit. Reads force pending writes to commit
first, so a read never observes a stale host.

`[RND-IO-2]` **Events use the invoker pattern**: one delegated host listener per (element, event). A
re-rendered handler is a .NET delegate swap on the invoker — **zero listener registration or removal
interop between renders**.

`[RND-IO-3]` **Static content is stringified aggressively** and inserted through a single
`InsertStaticContent` operation, collapsing many node operations into one.

`[RND-IO-4]` Handle allocation on a buffered host happens in managed code. Selector-resolved
teleport targets and hydration snapshots register **foreign** handles the host did not create; the
allocator MUST advance past the maximum handle in each snapshot so a later managed allocation cannot
collide.

`[RND-IO-5]` Interop-call counts are a **gated budget**, not an aspiration:
`benchmarks/baselines/InteropCounts.json` records the expected counts and a delta fails CI
([§16](#16-conformance-and-how-behavior-is-pinned)).

*Authority: `libraries/Assimalign.Viu.Core/src/Rendering/Renderer{TNode}.cs` (`Patch`, `Mount`,
`PatchElement`, `PatchFragment`, `TryPatchBlockChildren`, `PatchChildren`, `PatchUnkeyedChildren`,
`PatchKeyedChildren`, `GetLongestIncreasingSubsequence`, `MountAttributes`, `PatchAttributes`,
`PatchOptimizedAttributes`, `Move`, `MoveRange`, `Unmount`, `TryUnmountBlockChildren`,
`RequiresUnmountVisit`, `IsSameComponentType`);
`libraries/Assimalign.Viu.Core/src/Rendering/RendererOptions{TNode}.cs`;
`libraries/Assimalign.Viu.Core/src/Internal/Mounted*.cs`;
`libraries/Assimalign.Viu.Core/src/Scheduling/{Scheduler,SchedulerJob}.cs`;
`libraries/Assimalign.Viu.Components/src/{PatchFlags,ShapeFlags,SlotStability}.cs`;
`libraries/Assimalign.Viu.Components/src/Optimization/RenderPlan.cs`;
`libraries/Assimalign.Viu.Browser/docs/{DESIGN.md,ADR-0001-interop-marshaling.md}`;
`libraries/Assimalign.Viu.Components/src/{VirtualNode,ComponentRenderFrame}.cs`;
`docs/COMPONENT-MODEL-PLAN.md` §§2, 9.*

---

## 7. Built-in components

Each built-in is specified with its **current limits** inline.

### 7.1 Teleport

`[BLT-1]` `TeleportNode` is the structural description of content rendered into a different host
container while remaining logically positioned in the virtual tree. It carries `TargetIdentifier`,
`Children`, `IsDisabled`, and `IsDeferred`; the engine-internal executor emits origin anchors at the
logical position and manages the target range separately.

`[BLT-2]` `IsDeferred` postpones **target-side setup** to the current render's post-flush phase, so
a target rendered later in the same tree resolves. A disabled Teleport still mounts its content at
the logical position **immediately**; only target-side setup defers.

`[BLT-3]` Teleport content moves between the logical and target containers when `IsDisabled` changes.
Block dynamic-child patching applies to teleport content, with static host carry-forward.

`[BLT-4]` Target resolution goes through `RendererOptions<TNode>.ResolveTeleportTarget`. A target
already assignable to `TNode` is used directly. An unresolved target warns and skips the target
content.

### 7.2 KeepAlive

`[BLT-5]` `KeepAliveNode` is a structural node carrying one lazy `ComponentInvocation`. Its default
slot supplies content without evaluation at description time; include, exclude, and maximum inputs
ride `Invocation.Arguments`. The engine-internal executor moves inactive keyed component subtrees
into **renderer-owned detached storage**, preserving their component instances and reactive scopes
rather than unmounting them.

`[BLT-6]` It implements component-name include/exclude filtering, reactive cache pruning when the
filter changes, and **child-before-parent** activation callbacks. A **positive** `maximum` enables
least-recently-used eviction, which fully unmounts the evicted entry; zero, a negative value, a
missing value, or an unparseable string means **unbounded**.

### 7.3 Transitions

`[BLT-7]` `TransitionNode` is a structural node carrying one lazy `ComponentInvocation`. Its default
slot supplies the decorated child without evaluation at description time; name, mode, appear, class,
and hook inputs ride `Invocation.Arguments`. Core's internal executor owns the **host-neutral**
insertion/removal choreography — identity, cancellation, mode sequencing, insertion, and deferred
removal — and knows nothing about CSS.

`[BLT-8]` Browser's transition directive vocabulary and group behavior attach through the host seam:
Browser
owns class names, double-animation-frame scheduling, forced reflow, computed or explicit end timing,
and element handles.

`[BLT-9]` Transition execution MAY attach one shared internal transition state to multiple immutable
children and finish a pending enter phase before a host performs layout measurement. The host sees
ordered key-to-first-element snapshots of the **outgoing** tree during before-update and the
**patched incoming** tree during updated; that pair is what a FLIP pass measures against. The
snapshot operation is a designed host seam, not mounted-engine access.

`[BLT-10]` For a persisted transition, Core binds its internal host-neutral state to directive
bindings. The renderer **skips its own insertion/removal transition** for persisted hooks, so the
directive and the renderer never both drive the same phase.

### 7.4 Suspense

`[BLT-11]` `SuspenseNode` is a structural node carrying one lazy `ComponentInvocation`. Its default
slot supplies content and its `fallback` slot supplies the fallback, both unevaluated at description
time. The engine-internal executor implements pending-branch storage, fallback ownership, nested
boundary accounting, and coordinated reveal.

`[BLT-12]` **Limit — Suspense hydration is not implemented.** Hydrating a Suspense boundary throws
`NotSupportedException` with a descriptive message, rather than attempting a partial or incorrect
claim of server-rendered pending/fallback branches. Render the boundary on the client.

`[BLT-13]` **Limit.** Boundary timeout and events, fallback-to-reveal transition choreography, and
delaying mounted/post-render effects from the hidden default branch are not implemented; those
effects run when the detached branch mounts. See [§17](#17-non-goals-and-current-limits).

### 7.5 Asynchronous and dynamic components

`[BLT-14]` Asynchronous component definitions retain **explicit registration resolution and
delegate activation**, deduplicate concurrent loads for the same definition, and integrate with
server prefetch and Suspense. `AsynchronousComponents.Define(...)` is the public definition facade.

`[BLT-15]` Dynamic structure is explicit. A dynamic element constructs an `ElementNode` with a
`QualifiedName`; a dynamic registered component constructs a `ComponentNode` with
`ComponentReference.ForName(name)`. The renderer MUST NOT guess whether a plain string is a tag or a
registration. `DynamicComponents.Resolve(...)` normalizes a selector and
`DynamicComponents.Create(...)` creates its closed-algebra node.

*Authority: `libraries/Assimalign.Viu.Components/src/{BuiltIns,Tree}/*.cs`;
`libraries/Assimalign.Viu.Core/src/{KeepAlive,Suspense,Transitions,AsynchronousComponents,DynamicComponents}/`;
`libraries/Assimalign.Viu.Core/src/Rendering/{Renderer.KeepAlive.cs,Renderer.Suspense.cs,Renderer.Hydration.cs}`;
`libraries/Assimalign.Viu.Browser/docs/DESIGN.md` §Transitions;
`docs/COMPONENT-MODEL-PLAN.md` §§2, 9.*

---

## 8. The `.viu` container and the compilation pipeline

### 8.1 Build-time only

`[SFC-1]` **Build-time compilation is the only path.** There is no runtime `compile(templateString)`
API, and a template not present at build time cannot be rendered. Dynamic, string-sourced templates
are unsupported by design.

`[SFC-2]` Consequence: compilation is compiler-grade tooling. Diagnostics carry template source
locations mapped back to the `.viu` file, so a C# error inside a template expression resolves to the
real `.viu` line **and column** [SFC-8].

### 8.2 The container

`[SFC-3]` A `.viu` file uses a **hybrid container**: tag-based `<template>` and `<style>` blocks,
the component's C# in an `@script { }` block, and custom blocks in `@`-form.

`[SFC-4]` The full grammar is **normative in
[`FORMAT.md`](../tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md)** and is not
restated here. This specification pins only the structural invariants a consumer must know:

- **Column 0 is structural for opening a block.** A top-level line beginning with `@` opens an
  `@`-block; one beginning with `<` opens a tag construct. Nothing opens a block mid-line.
- **An `@`-block closes at the first later line whose first column is `}`.** `@`-block content must
  therefore be indented. Tag blocks have no such requirement — they close at their matching end tag,
  anywhere.
- **The legacy `@template` / `@style` containers still parse** during a migration window, each
  reporting a **Warning**-severity diagnostic (1015 / 1016). The window is temporary.
- **A top-level `<script>` tag is rejected** (`ScriptTagBlockNotSupported`, 1017, Error) and
  contributes no block, so tag-era muscle memory fails loudly rather than silently shipping a script
  that never executes.
- Duplicate rules apply **across container syntaxes**: at most one template and at most one script
  block per file, whichever container each uses; the first wins.

`[SFC-5]` The container parser **slices; it does not parse interiors**. Block content is the exact
raw source, never re-parsed, trimmed, or normalized. Every emitted span satisfies
`Location.Source == source.Substring(Start.Offset, End.Offset - Start.Offset)`. Descriptors and
blocks are immutable records with structural equality — the prerequisite for [EXE-9].

### 8.3 The pipeline

`[SFC-PIPE-1]` The pipeline is: **container parse → template compile → `@script` analysis →
projection → generated scaffold.**

`[SFC-PIPE-2]` `Assimalign.Viu.Compiler.SingleFileComponent` is the **one** projection that both the
source generator and the editor language service run. Equality between the two hosts is a **pinned
contract**, not a convention: `SingleFileComponentProjectionConformanceTests` drives every fixture
through both and asserts ordinal-identical generated source, hint names, and diagnostic sets.

`[SFC-PIPE-3]` The only sanctioned per-host divergence is the `DocumentationMode` seam:
`DocumentationMode.None` for the build (cheaper parse, no use for doc comments),
`DocumentationMode.Parse` for the editor (`///` summaries surface in completion). **Both run the same
split arithmetic**, so every `#line`-mapped coordinate derived from it is identical.

### 8.4 The expression-binding contract

`[SFC-6]` Because there is no proxy [RCT-8], the compiler decides every access form. This table is
normative and is the contract between the compiler, code generation, and the reactivity surface:

| Classification | Read | Write (assignment, `++`, `--`) |
| --- | --- | --- |
| template-local (a `v-for`/`v-slot` alias in scope) | `name` | `name` |
| allowed global | `name` | `name` |
| `SetupReference` | `_ctx.name.Value` | `_ctx.name.Value` |
| `SetupMaybeReference` | `unref(_ctx.name)` | `_ctx.name.Value` |
| `SetupConstant` / `SetupReactiveConstant` / `LiteralConstant` | `_ctx.name` | `_ctx.name` |
| `Property` / `PropertyAliased` / `Data` / `Options` | `_ctx.name` (alias resolved) | same |
| CSS-module accessor | `Style.member` / `<Accessor>.member` | n/a (read-only) |
| unresolved | `_ctx.name` | `_ctx.name` |

`[SFC-7]` **Spelling substitutions.** Template spellings that are not legal C# identifiers are
rewritten before the Roslyn parse: `$event`→`__event`, `$slots`→`__slots`, `$style`→`_style`. Each
substitution is **length-preserving**, so every expression offset — and therefore every remapped
diagnostic span — is unchanged.

### 8.5 The code-generation contract

`[SFC-CG-1]` Generated setup returns a `ComponentRenderer(ComponentRenderFrame)` whose invocation
produces `VirtualNode?`. The frame owns per-mount cache slots and block assembly. Generated render
implementation names are collision-safe internal details; `.viu` authoring reserves no `Render`,
cache, or underscore-prefixed helper members. The frame exposes exactly `Cache`, `OpenBlock`,
`SetBlockTracking`, `Track`, `CloseBlock`, `GetOrAddCache`, `SetCache`, `CacheHandler<TDelegate>`,
and `Memo` — the complete compiled-output surface for block assembly, cached values, stable handler
identity, and memoized subtrees. Generated code MUST NOT assume an unlisted frame member; adding a
member is an additive amendment to this clause in the same change.

`[SFC-CG-2]` Generated render code calls through its frame parameter and qualified APIs; it MUST NOT
use file-level static imports or an underscore name-binding convention. The compiler/runtime ABI has
three tiers:

1. **Public by necessity:** `ComponentRenderFrame`, node constructors, component reference/contract/
   invocation values, normalization APIs, the hidden `ComponentHotReload` registration ABI, and
   Browser directive identity tokens where state or type identity crosses assemblies.
2. **Dissolved helpers:** statement-form emission, direct constructors, loops, closures, and
   collection literals replace helpers that need no shared identity.
3. **Consumer-internal glue:** the source generator emits any residual helper as an internal type in
   the consumer compilation, calling only tier-one public surface. It is not loose source linked by
   the SDK.

`ComponentHotReload.Register` and `ComponentHotReload.ApplyUpdates` are the only remaining runtime
calls bound by generated member names; Browser directive values remain bound by type identity. No
`Assimalign.Viu.Syntax.*` assembly
references a runtime assembly.

`[SFC-CG-3]` A component that declares its surface by attribute ([CMP-26], [CMP-30]) emits one static
`ComponentContract` carried by its `ComponentRegistration`, so the runtime and tooling read
parameters and events before activation. Binding/default bookkeeping is generator-internal and uses
collision-safe emitted names; no such member name is reserved for author code.

`[SFC-CG-4]` A component with a template block is generated as
`partial class <Name> : ComponentBase, IComponent`. `ComponentBase` supplies only the protected
`Context` storage and deliberately does not implement `IComponent` [CMP-31]. Generated setup assigns
that context before `OnSetup`, rebinds declared properties per [CMP-29], and returns the frame-based
renderer; generator-internal glue preserves the root lifecycle authoring form [CMP-32]. Because the
base type is declared by the generated partial, **no other partial declaration may name a different
base class**. A component with no template block stays a plain partial class with neither
[V01.01.06.07].

When development metadata emission is enabled, a generated module initializer calls hidden
`ComponentHotReload.Register` with the component type, stable identifier, and generated
template/script/style marker types. The generator gates emission from configuration; authored
components implement no public hot-reload metadata interface.

`[SFC-CG-5]` **Generated-file identity.** Each emitted component occupies exactly one `AddSource` hint
name, derived from its path alone as
`<relative.directory.>.<BaseName>[.<hash>].SingleFileComponent.g.cs` — the project-relative directory
segments and the file's base name, each sanitized to a C# identifier. Roslyn compares hint names
**case-insensitively** and fails the whole generator run on a duplicate, so the derivation appends a
short stable hash of the exact-cased normalized path whenever the readable form is not unique on its
own: the file lies outside the project directory, sanitizing was lossy (`Foo-Bar` and `Foo_Bar` both
sanitize to `Foo_Bar`), **or another component the same compilation emits resolves to a hint name that
differs from it only by case**. That last group exists wherever path identity is ordinal — every
non-Windows filesystem [VUE-7] — and a shadowed `.vue` peer, which emits nothing, is never counted
into it. The hash reads only the component's own path, so a discriminated name is identical on every
build and in any order MSBuild presents the files; a component that collides with nothing keeps its
readable hint name unchanged [V01.01.06.10.01].

`[SFC-CG-6]` **Superseded directive emission.** The positional runtime-directive tuple and its helper
calls are removed. Generated code constructs `DirectiveInvocation` with the compile-time-known
directive type token and the render value; directive-specific argument and modifier shaping belongs
to qualified Browser APIs or consumer-internal generated glue. Core MUST NOT recover a directive by
reflection.

`[SFC-CG-7]` **Native `v-model` carriers.** On a native control the compiler selects the qualified
Browser directive token from the element and its `type`: text for `input`/`textarea`, checkbox for
`type="checkbox"`, radio for `type="radio"`, select for `select`, and dynamic for a dynamic `:type`
or dynamically keyed `v-bind`. The dynamic token re-resolves per render from the element's current
tag and type. `type="file"` is an error. Each directive reflects the model through
the DOM property that carries it and commits user edits from the event that carries them: `value` +
`input` for text-like inputs and `textarea`, `checked` + `change` for checkbox and radio, and option
`selected` + `change` for `select` — matching the events those controls fire per
[WHATWG HTML](https://html.spec.whatwg.org/multipage/input.html#common-input-element-events).
Modifiers shift them: `.lazy` moves the text-input commit from `input` to `change`, `.trim` trims the
committed value and re-syncs the element on change, and `.number` (implied by `type="number"`)
coerces it numerically.

Because Viu has no `this`-proxy and no reflection, a native `v-model` cannot recover its setter from
the `onUpdate:modelValue` prop the way a component `v-model` does. The `DirectiveInvocation` value
therefore carries a `ModelBinding` holding **both** the current value and the generated write-back
delegate; the `onUpdate:modelValue` prop is still emitted for uniformity but is inert on a native
element, which the DOM patcher skips rather than binding as a listener. The `modelValue` prop is not
emitted at all on a native element.

`[SFC-8]` **Source mapping.** Each expression-bearing render line carries a C# `#line` **span**
directive — `#line (line,column)-(line,column) offset "file"` — anchored to that line's leftmost
expression and closed with `#line default`. The span form is required because a render expression is
rewritten and its column must be re-aligned; the `@script` merge uses the line-only form because its
columns already match. Non-expression scaffolding, and any second expression sharing one physical
render line, falls back to the generated file.

### 8.6 Static optimization

`[SFC-OPT-1]` A fully static subtree is marked `PatchFlags.Cached` and stored in a
`ComponentRenderFrame.Cache` slot, so it is created once per mount and reused across every re-render.
One cached description MAY occupy multiple positions in one render result, including cache access
inside list generation; only the immutable description is shared, and every position mounts
independently [RND-2] [RND-4].
The generated `ComponentContract.RenderCacheSize` carries the exact non-negative slot count,
including zero, and Core constructs each mount's frame from that value. The legacy contract
constructor that predates compiler cache-size metadata alone receives a 64-slot compatibility
fallback; newly generated contracts always supply the exact value.

`[SFC-OPT-2]` Contiguous runs of cached, stringifiable siblings collapse into a single static
insert. The thresholds are **`NODE_COUNT = 20`** consecutive stringifiable nodes and
**`ELEMENT_WITH_BINDING_COUNT = 5`** consecutive elements carrying attribute bindings.

`[SFC-OPT-3]` Table-section tags (`caption`, `thead`, `tr`, `th`, `tbody`, `td`, `tfoot`,
`colgroup`, `col`) **never stringify**, because a raw-HTML insert would reparent them.

`[SFC-OPT-4]` Serialization escapes `"`, `&`, `'`, `<`, `>`, omits end tags for void elements, and
restricts stringifiable attributes to known HTML/SVG attributes plus the `data-`/`aria-` prefixes,
so the string round-trips to the same host tree under WHATWG fragment serialization.

### 8.7 Diagnostics

`[SFC-DIAG-1]` **Parsing is recoverable.** Parsers never throw for bad content; problems are
reported as located diagnostics in a single pass, each with a code, a message, a catalog severity,
and a source location. A `null` source argument throws `ArgumentNullException` — that is API misuse,
not input.

`[SFC-DIAG-2]` A structurally openable block **always opens**, so its content is still sliced and
downstream tooling has something to work with. An unterminated block yields its content to end of
file and still appears in the descriptor.

`[SFC-DIAG-3]` **Parser-produced node algebras are explicit.** Template, HTML, and
single-file-component abstract node roots expose no parameterless construction path outside their
own assembly; external parser extensibility goes through the generic parser registration seam, not
by injecting variants into those trees. CSS record roots remain mechanically derivable, so every
CSS writer and rewriter MUST handle each supported built-in variant explicitly and throw
`InvalidOperationException` for an unsupported node rather than silently dropping or copying it.

### 8.8 Component-usage validation

`[SFC-USE-1]` A template that uses a component is checked against that component's **declared**
parameter surface at build time. A declaration is readable when it is attribute-declared [CMP-26] and
the component is either compiled in the same compilation or visible through Roslyn symbols —
**including from a referenced assembly**, because `[Parameter]` survives into metadata. The projection
publishes a value-equatable *usage manifest* per template and the host joins it against the resolved
catalog, so the per-file projection stays cacheable while the catalog remains a compilation-wide
input. A tag resolves through the same name ladder the runtime factory uses [CMP-6].

`[SFC-USE-2]` **Unknown parameter** (`VIU1401`, **Warning**). An attribute or `:`-bound argument that
matches no declared parameter is reported — unless it is a listener spelling (`onX`, `@x`), a
directive, or a plausible fallthrough attribute: a known HTML or SVG attribute, a hyphenated or
namespaced name (`data-*`, `aria-*`, `xml:*`, any vendor prefix), or one the render pipeline itself
consumes (`key`, `ref`, `class`, `style`, `id`, `is`, `role`, …). The severity is **Warning and not
Error** because fallthrough is a specified feature [CMP-17]: an undeclared attribute is legal and
lands on the component's rendered root, so the diagnostic reports a likely mistake, never an illegal
program.

`[SFC-USE-3]` **Missing required parameter** (`VIU1402`, **Error**). A usage that omits a parameter
declared required is an error. Unlike an undeclared attribute there is no legitimate reading of the
omission: the declaration states that the caller must supply it, and the runtime's mount-time warning
[CMP-12] remains only for the usages the compiler cannot see.

`[SFC-USE-4]` **Incompatible argument** (`VIU1403`, **Error**). A supplied value is reported only
where incompatibility is decidable from the source alone: a **plain attribute** — whose value is
always a string — supplied to a parameter of a value type, and a **non-string literal binding**
supplied to a `string` parameter. Both are errors because neither can be right at run time:
`IComponentArguments.Get<T>` would yield the parameter type's default [CMP-29]. Every other
combination is left alone.

`[SFC-USE-5]` **The limits, and the silence they buy.** Validation is skipped entirely for: a
component the catalog does not resolve — which includes **every component that declares its
parameters imperatively**, because a `Parameters` collection is arbitrary C# no compiler can read, and
that gap is the reason the attribute form exists; a tag that resolves to more than one declaration; a
usage carrying an argument-less `v-bind="…"` spread or a dynamic `:[name]` argument; a bound
expression that is not a C# literal; and a hyphenated attribute name. A false positive is worse than a
false negative here, so every undecidable input produces silence rather than a guess.

*Authority: `tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/FORMAT.md` (**normative**);
`tooling/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md`;
`tooling/Assimalign.Viu.Compiler.SingleFileComponent/{docs/DESIGN.md,src/Internal/{SingleFileComponentProjection,SingleFileComponentSourceEmitter,RenderBodySourceMapper}.cs}`;
`analyzers/Assimalign.Viu.Generators.Syntax/src/{SingleFileComponentGenerator,Internal/ComponentSymbolCatalogReader}.cs`;
`docs/adr/0005-no-runtime-template-compilation.md`.*

---

## 9. `.vue` compatibility — a shipping feature

**This is a product feature, not a legacy reference.** [V01.01.06.09]
([#250](https://github.com/assimalign/viu/issues/250)) lets Vue single-file components compile under
Viu. It targets the Vue single-file-component container specification because compatibility with
that documented external format *is* the requirement — exactly as Viu Utilities targets Tailwind CSS
v4.3.3 ([§10.4](#104-viu-utilities)) and the server renderer targets WHATWG HTML serialization
([§11](#11-server-rendering-and-hydration)).

`[VUE-1]` `VueSingleFileComponentParser` is a **dedicated compatibility parser**. It projects
`<template>`, `<script>`, `<script setup>`, `<style>`, and custom blocks into a
`VueSingleFileComponentDescriptor`. It does not change `SingleFileComponentParser` or the `.viu`
grammar.

`[VUE-2]` The compatibility descriptor is deliberately distinct from the canonical one: the `.vue`
format allows one ordinary `<script>` and one `<script setup>` in the same file, while `.viu` has a
single uniform `@script` slot. Both descriptor types reuse the same immutable block, option,
diagnostic, and source-location values wherever their semantics are identical.

`[VUE-3]` **Both engines share one internal tag scanner** (`SingleFileComponentTagScanner`) —
opening-tag and attribute parsing, the nested-`<template>` boundary, the raw-text closing-tag
search, and malformed-tag recovery. The two containers therefore **cannot drift**. End-tag-shaped
text inside quoted attributes, comments, and nested raw-text elements never closes the root
template.

`[VUE-4]` **Viu does not execute JavaScript and does not implement JavaScript compiler macros.** A
`.vue` script merges into the generated component **only** when it declares the exact
`lang="csharp"` contract. A missing or other language value produces `VIU1206`, and that content is
never executed or merged. Macros such as `defineProps` are not evaluated.

`[VUE-5]` The two legal script slots stay **separate through analysis**, each retaining its own exact
`#line` map, then both contribute partial-class members and template binding metadata.

`[VUE-6]` An inline `.vue` block may begin after its opening tag on the same physical line. Analysis
therefore carries both a start **line and start column**, and pads the first `#line`-mapped emitted
line so compiler spans stay exact.

`[VUE-7]` **Shadowing.** A same-directory, same-base `.viu` file shadows its `.vue` peer: the
generator reports `VIU1004`, canonical `.viu` wins, and only one component is emitted. Path identity
follows the host filesystem — case-insensitive on Windows, ordinal elsewhere. The component-style
bundler applies the identical shadowing rule, so a shadowed peer can never contribute a duplicate or
contradictory stylesheet segment.

`[VUE-8]` The base `Assimalign.Viu.Sdk` targets glob `**/*.viu` and `**/*.vue` into one
`ViuSingleFileComponent`, `AdditionalFiles`, and `Watch` graph; `Assimalign.Viu.Sdk.Browser`
inherits that graph by importing the base SDK. `.vue` is discovered only for projects using either
Viu SDK, and the Visual Studio language server re-checks the owning project before accepting a
compatibility document ([§14](#14-the-tooling-and-editor-contract)).

`[VUE-9]` Everything downstream of the container parse is **shared with `.viu`**: template code
generation, scoped styles, CSS Modules, `v-bind()` in CSS, `@reference`, `@apply`, source mapping,
utility-candidate detection, and hot-reload metadata.

`[VUE-10]` The implementation adds **no Vue JavaScript runtime and no dependency on Vue**.

*Authority: `tooling/Assimalign.Viu.Syntax.SingleFileComponent/src/VueSingleFileComponent*.cs`;
`.../src/Internal/{VueSingleFileComponentParseEngine,SingleFileComponentTagScanner}.cs`;
`tooling/Assimalign.Viu.Syntax.SingleFileComponent/docs/DESIGN.md` §§"Tag-based `.vue`
compatibility", "Generator compatibility contract"; `docs/UTILITY-CSS-DESIGN.md` §8.2;
`sdks/README.md`; `build/Targets/Build.UtilityCss.targets`.*

---

## 10. Styling

### 10.1 Scoped CSS

`[STY-1]` A single-file component with a scoped style computes one stable
`data-v-<path-derived-hash>` identifier. The template transform emits that identifier as an ordinary
empty attribute binding on **every native element** in the interactive virtual-node tree, and the
`ServerMarkup` profile writes the same attribute directly [SSR-COMPILE-3]. This static compiler
stamping requires no `ComponentContract`, `ComponentContext`, `VirtualNode`, renderer-host, or
server-serializer scope field. Hand-authored trees receive no implicit identifier. Runtime root
restamping and reactive style-variable application remain deferred under `[V01.01.06.12]` and
[STY-6].

### 10.2 CSS Modules

`[STY-2]` A `<style module>` block compiles to a **compile-time nested `const` accessor class** —
`internal static class Style { public const string box = "box_<hash>"; }`. A named module compiles to
its pascal-cased accessor class. There is no runtime string-indexed lookup, because there is no
render-context object to index.

`[STY-3]` A module accessor **shadows** a same-named component member: the accessor is resolved
before component bindings.

`[STY-4]` Because the generator supplies the **complete** class map, an access to an undeclared class
is decidably wrong and reports `XViuUnknownCssModuleMember` (1001) at the exact template coordinate.
The access still emits the accessor member, so the C# compiler remains the backstop.

`[STY-5]` A member whose CSS name is not a legal C# identifier is reached through its sanitized
member name (`$style.a-b` → `$style.a_b`), the same name the emitter writes as the const.

### 10.3 `v-bind()` in CSS

`[STY-6]` **Deferred under `[V01.01.06.12]` after completion of the `[V01.01.15]` arc.**
`v-bind()` in a style block compiles to a `CssVariables` binding emitted from the generated setup
path with an explicit `ComponentContext` owner. During the deferral the compiler emits no CSS
variable application, and the Browser host retains the designed single-element
`CssVariables.Bind` directive as the primitive the restored feature builds on.

`[STY-7]` **Deferred with `[STY-6]`.** When restored: after mount, a post-flush watcher tracks the
getter's reactive dependencies and applies each hashed custom property to every current outermost
host element — fragment roots included — reapplying on root-set changes and stopping before
unmount. Restoration requires the component-root host-range seam recorded in
`docs/COMPONENT-MODEL-PLAN.md`; introducing that seam is part of the restoration work item, not
the migration arc.

`[STY-8]` **Deferred with `[STY-6]`.** When restored: a `v-bind()` change updates the host without
re-rendering the component — on a buffered host the properties are written into the command frame
and the owning context queues its renderer-specific commit, reaching the host before `NextTickAsync`
with no render.

### 10.4 Viu Utilities

`[STY-9]` Viu Utilities is a **build-time-only** utility-CSS engine whose compatibility contract is
frozen to **Tailwind CSS v4.3.3** and is **normative in
[`docs/UTILITY-CSS-DESIGN.md`](UTILITY-CSS-DESIGN.md)**. This specification restates only the
boundary:

- One immutable `UtilityCssRegistry` is the single source of truth, shared by the compiler and the
  editor. Completion that does not generate CSS, or generation the editor cannot describe, is a
  defect.
- Output is a **separate** `<PackageId>.utilities.css` static web asset, independent of component
  style bundling in both directions.
- **No runtime CSS generation reaches the WebAssembly payload.** No utility parser, registry, theme
  compiler, file watcher, or hot-reload transport is linked into an AOT release build; the browser
  receives ordinary CSS through a `<link>`.
- There is **no npm, Node, Tailwind executable, PostCSS or bundler plugin, JSON configuration, or
  plugin ABI** dependency; the implementation is independently authored.
- **Viu Utilities is an independent Viu feature compatible with documented Tailwind CSS v4.3.3
  behavior. It is not affiliated with or endorsed by Tailwind Labs.**

*Authority: `docs/UTILITY-CSS-DESIGN.md` (**normative** for §10.4);
`tooling/Assimalign.Viu.Compiler.Css/docs/{OVERVIEW,DESIGN}.md`;
`tooling/Assimalign.Viu.UtilityCss/docs/{OVERVIEW,DESIGN}.md`;
`tooling/Assimalign.Viu.Syntax.Css/docs/DESIGN.md`;
`tooling/Assimalign.Viu.Syntax.Templates/docs/DESIGN.md` §"CSS Modules accessors";
`libraries/Assimalign.Viu.Browser/docs/DESIGN.md` §"Component CSS variables".*

---

## 11. Server rendering and hydration

### 11.1 Server rendering

`[SSR-1]` `ServerRenderer.RenderToStringAsync` renders a configured `ServerRenderApplication` to a string;
`RenderToStreamAsync` writes **completed component subtrees** to a `TextWriter` and awaits the
writer's `FlushAsync`. The host adaptor exposes the same write/flush boundary as
`IServerRenderOutput`, so a `PipeWriter`, response body, or other host destination controls its own
buffering and backpressure without entering ServerRenderer's dependency graph.

`[SSR-2]` `ServerRenderApplication` is a plain per-render composition object carrying an immutable
`IApplicationContext` **without a host node type**. It does not implement `IApplication`, owns no
persistent mounted lifetime, and does not participate in top-level application middleware [APP-7].

`[SSR-3]` ServerRenderer consumes the **same `VirtualNode` algebra** client renderers patch; it does
not maintain a second node model. The server serializer dispatches the ten closed
`VirtualNodeKind` variants [CMP-3].

`[SSR-4]` ServerRenderer obtains a one-shot lease through
`ComponentHost.RenderAsync(ComponentRenderRequest)`. Core resolves the registration, activates a
fresh `IComponent`, creates its live `ComponentContext` and reactive scope, runs synchronous `Setup`
inside that scope, **awaits every `OnServerPrefetch` callback** [CMP-21], and invokes the returned
`ComponentRenderer` once before exposing the tree for serialization.

`[SSR-5]` Client-only before-mount, mounted, update, and unmount callbacks **do not run** during
server rendering. Render cancellation interrupts the prefetch wait. Disposing the render scope
aborts the component lifetime, cancels its token, stops its reactive scope, and disposes the authored
instance without invoking client hooks.

`[SSR-6]` Escaping targets **WHATWG HTML serialization**: `"`, `&`, `'`, `<`, `>` are escaped, and
comment terminators are repeatedly removed from comment content. Attribute serialization skips
renderer metadata, event listeners, forced properties, and child overrides; normalizes class and
style; renders boolean attributes by presence; preserves SVG and custom-element casing; and **drops**
an unsafe dynamic attribute name rather than attempting to escape it. `innerHTML` is the explicit
raw-HTML path; `textContent` and a textarea's `value` are escaped and suppress child serialization.

`[SSR-7]` `SsrContext` carries per-render teleport output and an optional, versioned
`StateStorePayload`. Enabled teleport children belong to another target and are **buffered** until
the render resolves. After traversal, a payload-capable application state registry captures only
materialized stores into `{"version":1,"stores":{"key":state}}`; ServerRenderer stores that payload
on the context and appends one inert `<script type="application/json" data-viu-state>` island. The
island contains normalized JSON with HTML-sensitive characters escaped and never executable script.

`[SSR-COMPILE-1]` `RenderFunctionTargetProfile.ServerMarkup` is the explicit build-time SSR target
on the public Templates compiler facade. It accepts only a transform produced with
`TransformOptions.IsServerRendering`; the default profile remains `VirtualNodeTree`, so selecting
SSR cannot alter Browser code generation.

`[SSR-COMPILE-2]` The server-markup profile coalesces provably serializable native markup into
ordered `SsrRenderState.Push` calls and uses public ServerRenderer helpers for dynamic values. A
static structure with interpolations allocates **zero `VirtualNode` instances**. Components and
unsupported subtrees use a subtree-local virtual-node fallback and rejoin the same render state.

`[SSR-COMPILE-3]` Direct output obeys [SSR-6]: interpolation and attributes escape identically;
class and style use the shared normalizers; model directives emit `value`, `checked`, or `selected`;
show directives append `display:none`; and every native element receives the transformed scope
identifier. Suspense renders its default content, Transition is a markup pass-through, and Teleport
uses the ordinary context target buffer and unchanged marker protocol.

`[SSR-COMPILE-4]` `CompiledServerRender` and `ServerRender.RenderCompiledTo{String,Stream}Async`
create renderer-owned request state, preserve cancellation, component-fallback flushes, teleports,
state capture, and the final flush. Executed differential fixtures MUST byte-match the runtime-tree
serializer, including escaping, normalization, fallback regions, and hydration markers.

### 11.2 The hydration marker protocol

`[SSR-MARKERS-1]` These strings are a **cross-package contract**. Changing one is a breaking change
to the hydration protocol.

| Tree value | Output |
| --- | --- |
| Text | escaped text |
| Comment | `<!--content-->`; empty content is `<!---->` |
| Static | raw content |
| Element | `<tag attributes>children</tag>` |
| Void element | `<tag attributes>` |
| Fragment | `<!--[-->children<!--]-->` |
| Component | the rendered subtree, with no wrapper |
| Deferred component | `<!--lazy hydration idle\|visible\|media query\|interaction-->` + rendered subtree + `<!--lazy hydration end-->` |
| Enabled teleport | `<!--teleport start--><!--teleport end-->` |
| Disabled teleport | `<!--teleport start-->children<!--teleport end-->` |

`[SSR-MARKERS-2]` An enabled teleport's **target buffer** receives its children followed by
`<!--teleport anchor-->`. A disabled teleport renders children in place and contributes only the
target anchor. A missing or non-string target emits the origin anchors and skips target content.

`[SSR-MARKERS-3]` Core's public `HydrationMarkers` is the single owner of every marker in
[SSR-MARKERS-1] and [SSR-MARKERS-2]. Core hydration, ServerRenderer, and Testing MUST consume that
vocabulary rather than duplicating literals or a parallel grammar. This ownership change does not
alter any wire value.

### 11.3 Hydration

`[HYD-1]` Hydration is performed by Core's **generic** `Renderer<TNode>.Hydrate`, which walks the
markers of [SSR-MARKERS-1] through a host-supplied `HydrationNodeReader<TNode>`.

`[HYD-2]` **Hydration is a client-host responsibility.** Browser supplies a reader over one batched
host-tree snapshot per root or teleport target, so every structural, kind, text, and attribute read
after that stays in managed memory. Testing supplies a live-tree reader and an immutable-snapshot
reader. `TestRendererOptions.SnapshotSemantics` selects between them; the same non-positional options
record also carries strict-removal validation wherever Testing creates renderer operations.
ServerRenderer itself stays free of host-node types.

`[HYD-3]` `Hydrate` throws `NotSupportedException` when the host supplies no
`CreateHydrationReader`, and `InvalidOperationException` when the container already holds a mounted
tree.

`[HYD-4]` **Matching server nodes are adopted**, not recreated: the container is not cleared, node
identity is retained, interactive bindings and directive hooks attach to the existing nodes, and
later reactive updates patch the adopted nodes.

`[HYD-5]` **Mismatch recovery is localized to the smallest subtree**: that server range is removed
and the client component mounts in its place. Class and style comparison is **semantic**, not
textual, and `data-allow-mismatch` gates expected divergence.

`[HYD-6]` **Host obligation.** For an enabled Teleport, the surrounding server host MUST splice
`SsrContext.Teleports[target]` into the target element **before** client hydration. That buffer
already carries the trailing `<!--teleport anchor-->` the walker requires.

`[HYD-7]` **Limit.** Suspense hydration throws [BLT-12].

`[HYD-8]` A hydrating Browser application with composed state initializes the bridge, consumes and
removes the single `script[data-viu-state]` island, validates schema version 1, and restores the
payload-capable registry **before mount-target resolution, component setup, or first render**.
Removing the island before the hydration snapshot keeps an island placed inside the mount container
from becoming an extra root sibling. A missing island or incompatible registry fails before
rendering rather than silently hydrating from default state.

`[HYD-LAZY-1]` `ComponentInvocation.HydrationStrategy` is immutable invocation metadata. Immediate
is the default. Idle, visible, media-query, and interaction strategies carry only host-neutral data;
an asynchronous component definition may supply a default that an explicit invocation overrides.

`[HYD-LAZY-2]` ServerRenderer surrounds a deferred component with the fixed markers in
[SSR-MARKERS-1]. During the initial walk Core validates and adopts that complete marker-bounded
range as opaque markup: host nodes retain identity, while component activation, setup, rendering,
effects, and descendant lazy-boundary discovery remain deferred. Nested boundaries therefore
register only after their deferred parent activates; an eager parent discovers its lazy child during
the ordinary walk. An asynchronous definition owns exactly one outer boundary: its resolved target
does not inherit the strategy, and Core waits for either target readiness or the wrapper's terminal
error presentation before walking the adopted subtree.

`[HYD-LAZY-3]` Core requests a trigger through `ScheduleHydrationTrigger` and schedules activation
as a post-flush job. The host delivers a trigger asynchronously after registration, and each trigger
activates at most once. Patching retains the latest dormant invocation, a change to Immediate
activates, and any strategy-data change replaces the registration. Unmount or navigation before
activation cancels the job and disposes every observer/listener.

`[HYD-LAZY-4]` Browser maps idle to `requestIdleCallback` with a timer fallback, visible to
`IntersectionObserver` over every top-level element in the marker range, media-query to `matchMedia`,
and interaction to capture listeners scoped to the marker range. Testing supplies
`TestHydrationTriggers`, whose explicit trigger methods enter the same Core post-flush path without a
DOM or clock.

`[HYD-LAZY-5]` **Interaction decision.** Browser captures only the first configured interaction
inside a dormant boundary, prevents its premature delivery, and replays an equivalent event after
Core activates and schedules the host commit. Cancellation before activation drops the captured
event. Later interactions use the ordinary mounted listeners.

### 11.4 The hosting boundary

`[SSR-8]` **No `Assimalign.Viu.*` library may reference a web framework.** Hosting is a downstream
adapter over a host-agnostic contract. ServerRenderer references Components and Core, and
has no DOM, Browser, WebView2, or JavaScript-interop dependency.

`[SSR-9]` A server host SHOULD create **one server-render application per request** when services or
state are request-scoped. `ServerRenderAdaptor<TContext>` requires a structurally new
`ServerRenderApplication` and `SsrContext` identity for every request and rejects reuse. Its factory,
service provider, component factory, and state registry are borrowed and are never disposed by
ServerRenderer [CMP-9], [APP-6]; the adaptor always disposes the request scope. Both identities are
consumed before root validation because a rejected scope is still disposed and cannot safely
reappear. Each render enters the independent logical execution state required by [EXE-1].

`[SSR-10]` `ComponentHost.RenderAsync(ComponentRenderRequest, CancellationToken = default)` returns an
`IComponentRenderScope` exposing exactly `VirtualNode? Tree` and `ComponentContext Context`.
The operation resolves and activates one registration, runs setup inside the component scope, awaits
server prefetch, and invokes the renderer once. `DisposeAsync` aborts and releases the lease without
client mount, update, or unmount hooks. A nested `ComponentRenderRequest` carries the active parent
scope; Core uses that scope's still-valid `Context` as the nested component's parent.

`[SSR-11]` `ServerRenderAdaptor<TContext>` creates exactly one typed request scope, validates that
its application root is the request root, streams through `IServerRenderOutput`, awaits every flush,
and disposes the scope on success, render/output failure, cancellation, and partial response. It has
no HTTP status, header, route, or framework policy.

`[SSR-12]` Ordinary adaptor failures return `ServerRenderResult` with the failure and whether output
had started, allowing the downstream host to choose its response policy. Request cancellation
propagates as `OperationCanceledException`; it is never converted into an ordinary failure result.

*Authority: `libraries/Assimalign.Viu.ServerRenderer/docs/{OVERVIEW,DESIGN}.md`;
`libraries/Assimalign.Viu.Core/src/Rendering/{Renderer.Hydration.cs,HydrationNodeReader{TNode}.cs,HydrationNodeKind.cs}`;
`libraries/Assimalign.Viu.Core/src/{Abstraction/IComponentRenderScope.cs,Rendering/ComponentHost.cs,Rendering/ComponentRenderRequest.cs}`;
`libraries/Assimalign.Viu.Browser/docs/DESIGN.md` §Hydration;
`libraries/Assimalign.Viu.Testing/docs/OVERVIEW.md`;
`docs/COMPONENT-MODEL-PLAN.md` §8.2.*

---

## 12. Routing

`[RTR-1]` The router **core is host-free**. `RouteMatcher` / `IRouteMatcher`, `RouteRecord`,
`RouteLocation`, `RouteParameters`, `PathMatchingOptions`, and the ranked path parser run in a plain
.NET test host using no other Viu library.

`[RTR-2]` `RouteLocation` and `RouteParameters` have **value equality** with matching null-safe
`==` and `!=` operators, so a navigation layer can compare and snapshot cheaply.
`Router.CurrentRoute` exposes the covariant get-only reactive-reference contract; only Router can
replace the current location.
`RouteParameters` accessors are **boxing-free and reflection-free**
(`GetString`/`TryGetString`, `GetInteger`/`TryGetInteger`, `GetStrings`), with immutable
`With`/`WithMany` builders.

`[RTR-3]` Three histories ship behind the `RouterHistory` factory: **memory** (pure; no
initialization), **web** (HTML5 History API), and **hash**. Web and hash lazily initialize their
browser-history bridge when `Router.ReadyAsync` first needs it; `RouterHistory.InitializeAsync`
remains an optional prewarming call. `UseRouter` awaits readiness with
`IApplicationContext.Stopping` before the host terminal mounts and removes the DOM bridge during
reverse-order application cleanup [APP-4], [APP-5]. History state marshals as a **flat,
primitives-only** payload. Every history is an idempotent, terminal `IDisposable`: after disposal
all other members throw `ObjectDisposedException`. A Router borrows its history; the owner disposes
the Router first and then the history, making environment-listener ownership explicit.
`RouterHistoryNavigationOptions` is a flags value whose `SuppressListeners` bit controls `Go`;
`RouterHistoryEntryOptions` is a readonly value carrying the non-bitwise scroll input to `Push` and
`Replace`. `RouterHistoryState.Replaced` remains an observed output fact, never an operation switch.

`[RTR-4]` `RouterView` and `RouterLink` resolve `Router` from nullable
`ComponentContext.Services`.
**`RouterView` takes its nesting depth as an explicit argument** (default `0`), and a nested layout
passes the next depth explicitly, because Viu has no hierarchical component dependency API [CMP-24].
An in-component guard depth outside the current matched route chain throws
`ArgumentOutOfRangeException`; registration never silently drops a guard because its explicit depth
is invalid. `RouterLinkClickEvent` carries system keys as one `RouterLinkModifiers` flags value;
its individual key properties are computed projections retained for click-contract and test
inspection.

`[RTR-5]` **Guards return their decision; they do not call a continuation.** A `NavigationGuard`
returns a `NavigationGuardResult` — `Allow`, `Abort`, or a redirect — from an awaitable,
cancellable signature. An exhaustive result type lets the compiler check that every path decides,
and lets the pipeline guarantee a guard decides exactly once. The result exposes a
`NavigationGuardOutcomeKind` plus exactly the applicable payload: a failed outcome carries its
`NavigationFailureType`, while a redirected outcome carries a `NavigationRedirectTarget`
discriminated as a location or named route. Callers never infer the outcome from a string or null.

`[RTR-6]` A navigation that does not complete yields a `NavigationFailure` typed `Aborted`,
`Cancelled`, or `Duplicated`, returned from cancellable `PushAsync`/`ReplaceAsync` and passed to
every after-navigation hook. A caller cancellation participates in the same cancellation outcome as
a navigation superseded by a later request. One cancellation token spans an entire redirect chain.
A superseded pop navigation reports `Cancelled` to its after-navigation hooks but cannot compensate
history after a newer navigation owns the pipeline. A guard-redirect chain that exceeds the safety
cap throws `NavigationRedirectException`.

`[RTR-7]` **Boundary.** `Assimalign.Viu.Router` references Components and Reactivity but **not Core
and not Browser** — a boundary the test suite asserts. `Assimalign.Viu.Browser.Router` is the
click-dispatch bridge that maps browser modifier flags onto `RouterLinkModifiers`, and the browser
history edge is gated by `[SupportedOSPlatform("browser")]`.

`[RTR-8]` **Limit.** Lazy route components and scroll behavior are not implemented
([V01.01.08.05]); every route component resolves eagerly. See [§17](#17-non-goals-and-current-limits).

*Authority: `libraries/Assimalign.Viu.Router/docs/{OVERVIEW,DESIGN}.md`;
`libraries/Assimalign.Viu.Browser.Router/docs/{OVERVIEW,DESIGN}.md`.*

---

## 13. State

`[STA-1]` `StateStoreDefinition<TStore>` is reusable metadata with a diagnostic `Identifier` and an
explicit, AOT-safe `StateStoreActivator<TStore>(IStateContext)`. Mutable store instances are always
registry-owned; the identifier is not reflection-backed activation or registry identity.

`[STA-2]` `IStateStoreRegistry` lazily creates **one store instance and one detached `EffectScope`
per definition object**. Resolving the same definition in one registry is a cache hit by reference
identity; different registries produce isolated instances. Removing a definition or disposing the
registry disposes its store and stops its scope. A setup failure stops the newly created scope and
adds no partial entry.

`[STA-3]` The caller's ambient component scope is **never** the store scope's parent. Store lifetime
is registry lifetime, not mount lifetime.

`[STA-4]` **Superseded bridge.** State attaches through the designed convention seam [CMP-33].
`definition.Use(componentContext)` first resolves `IStateStoreRegistry` through nullable
`componentContext.Services`, then uses the ambient active registry, and otherwise throws. It MUST
NOT cast the context, require a friend grant, or introduce a bridge interface. The application-global
path deliberately records **no owner**; otherwise the first component to resolve a global store
would become its owner and setup behavior would depend on mount order. An isolated feature creates
and owns another registry, then passes that registry explicitly to `definition.Use(registry)`.

`[STA-5]` `StateStore<TState>` is **optional**. It offers `Patch`, `Reset`, `Subscribe`, and
`OnAction` over source-generated `[Reactive]` state. The live `State` object is never replaced.
`Reset()` creates fresh factory state and applies it to the live object **in place**. A store built
without a factory/applier supports mutator patches but rejects object patch and reset with
`NotSupportedException`.

`[STA-6]` State copying uses an **explicit typed delegate**; State cannot enumerate a state shape by
reflection under [EXE-4].

`[STA-7]` **Scheduler behavior.** With a scheduler supplied, the store's single deep state watcher
uses pre-flush delivery: several direct writes before the flush deduplicate into **one**
notification, and a grouped `Patch` also yields one because its mutations batch. Without a
scheduler, the synchronous fallback applies: each direct write notifies immediately, while `Patch`
remains a single notification because it wraps its writes in a batch.

`[STA-8]` Actions are observable **only** when their implementation uses the protected action
helper — there is no interception layer [RCT-6]. Asynchronous helpers await the task before running
the after-hook, so it receives the resolved value; faults run error hooks and then propagate.

`[STA-9]` SSR persistence is explicit and reflection-free. A serializable definition registers an
`IStateStoreSerializer<TStore>` (the supplied JSON implementation requires `JsonTypeInfo<TState>`).
Capture includes only stores materialized in that registry and fails actionably when one lacks a
serializer. Restore applies immediately to an existing store or stages state until its first
`GetOrCreate`; either path applies before the caller observes the store. Keys are non-empty,
request-local, ordinal strings, schema version 1 is strict, and normalized JSON is safe for the inert
state island [SSR-7], [EXE-4].

*Authority: `libraries/Assimalign.Viu.State/{src,docs/{OVERVIEW,DESIGN}.md}`;
`docs/COMPONENT-MODEL-PLAN.md` §2a.*

---

## 14. The tooling and editor contract

`[TOOL-1]` Editor support is a **thin editor client** plus a **standalone Language Server Protocol
process**. The client is in process in Visual Studio, because the editor surfaces a Viu palette needs
— content types, classification types, and format definitions — exist only as MEF exports inside the
IDE. Nothing semantic follows it in: the parsers and Roslyn stay behind the protocol boundary, in a
process the IDE does not host. The chain is
`Assimalign.Viu.VisualStudio → Assimalign.Viu.LanguageServer → Assimalign.Viu.LanguageService →
{Assimalign.Viu.Syntax.SingleFileComponent, Assimalign.Viu.Compiler.SingleFileComponent,
Assimalign.Viu.UtilityCss}`.

`[TOOL-2]` **The build/editor equality guarantee.** One projection, conformance-pinned, producing
ordinal-identical generated source, hint names, and diagnostics for both hosts [SFC-PIPE-2]. The
`DocumentationMode` seam is the only sanctioned divergence [SFC-PIPE-3].

`[TOOL-3]` **Classification is split by ownership.** Kinds a C# token pass can emit resolve the
classification types the editor and its managed-language service already register, so script blocks,
interpolation interiors, and binding-expression interiors inherit the user's own C# colors. Template,
markup, and style constructs resolve **Viu-owned classification types**, each registered with a
user-editable format definition carrying a Viu default. Resolution is defensive: a name the host does
not register degrades along a fixed fallback chain rather than dropping the span.

`[TOOL-4]` `@script` completion has two tiers, neither of which loads a Roslyn workspace: a
syntax-only parse of the script block for declared members, and — when the host supplies a restored
project context — an **artifact-fed `CSharpCompilation`** answered through `SemanticModel.LookupSymbols`.

`[TOOL-5]` Diagnostics are **host-neutral by design**: the projection returns a `DiagnosticInfo`
(catalog descriptor + location + message) and each host materializes it at its own edge.

`[TOOL-6]` **`.vue` documents are admitted conservatively.** The language-server host performs a
narrow nearest-owning-project check for `Assimalign.Viu.Sdk`, `Assimalign.Viu.Sdk.Browser`, or an
explicit enablement marker, stops at the first directory containing a project so an unrelated nested
project is not claimed, treats a literal `false` marker as an override, and **fails closed** when
ownership is ambiguous. The check repeats for document changes, diagnostics, completion, and hover.

*Authority: `extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md`;
`tooling/Assimalign.Viu.Compiler.SingleFileComponent/docs/DESIGN.md`.*

---

## 15. Packaging and the consumer surface

`[PKG-1]` Viu has two compositional consumer SDKs, both resolved by NuGet's built-in MSBuild SDK
resolver with no installer or administrative rights. `<Project Sdk="Assimalign.Viu.Sdk">` chains
`Microsoft.NET.Sdk` and is the host-neutral component-library surface: no WebAssembly workload,
browser assets, `wwwroot` bundling, or publish hooks.
`<Project Sdk="Assimalign.Viu.Sdk.Browser">` imports that base, declares an exact-version package
dependency on it, chains `Microsoft.NET.Sdk.WebAssembly`, and adds the browser application payload.
This is a direct package split with no compatibility shim.

`[PKG-2]` The framework is segmented along the same boundary. `Assimalign.Viu.App` is
**targeting-only**: `Assimalign.Viu.App.Ref` carries the Reactivity, Components, State, and Core
reference assemblies, generator closure, `data/FrameworkList.xml`, and
`data/PackageOverrides.txt`; there is no base runtime pack. `Assimalign.Viu.App.Browser.Ref` carries
only the Browser reference and Browser override; the Browser SDK composes that targeting pack with
the base framework and resolves the only Viu runtime pack,
`Assimalign.Viu.App.Browser.Runtime.browser-wasm`, which carries the base-plus-Browser implementation
closure. Each targeting pack's override manifest lists exactly the standalone packages in its
segment at the effective NuGet package version, so framework assets win same-version conflict
resolution. The Browser runtime pack does not carry a targeting-only override manifest.
`Assimalign.Viu.ServerRenderer` remains opt-in and is not a framework segment.

`[PKG-3]` **Generators are delivered as analyzers through the base ref pack** —
`analyzers/dotnet/cs` with `<File Type="Analyzer">` manifest entries — so base and Browser SDK
consumers get `[Reactive]` and `.viu`/`.vue` compilation with **zero wiring**. The base SDK owns the
shared `AdditionalFiles` graph; the Browser SDK inherits it rather than duplicating it.

`[PKG-4]` MSBuild tasks perform the physical writes a generator legally cannot [EXE-10]. The base
SDK extracts `.viu.css` when a component library is packed and carries it with generated
`buildTransitive` registration, but does not itself register browser static assets or write to
`wwwroot`. The Browser SDK consumes that transitive registration as an additional browser static web
asset and owns application bundling: `ViuBundleCss` writes the app's component stylesheet,
`ViuBundleUtilityCss` writes the utility stylesheet, and a link-injection task splices each enabled
app `<link>` into the host page **before** the WebAssembly SDK's compression pipeline so content
negotiation stays intact. Both app links can be opted out of. The Browser SDK also owns the CSS
hot-reload worker and publish-budget hooks.

`[PKG-5]` In-repo projects **dogfood via `ViuProjectReference`**; the two SDKs are the *external
consumer* surfaces. The in-repo build deliberately does not consume either SDK, so the framework can
be developed without a pack/restore cycle in the loop.

*Authority: `sdks/README.md`; `sdks/Assimalign.Viu.Sdk/`;
`sdks/Assimalign.Viu.Sdk.Browser/`; `frameworks/`;
`.claude/rules/build-system.md`; `docs/RELEASING.md`.*

---

## 16. Conformance and how behavior is pinned

`[CONF-1]` **Behavior is pinned by tests in this repository, and the test is the authority.** There
is no external conformance suite and no external reference implementation. A change to a behavior
this document specifies is a specification change: the clause, its tests, and the XML docs that cite
it move together.

`[CONF-2]` The gate set:

| Gate | What it pins |
| --- | --- |
| Per-library `test/` suites | Each library's own contracts |
| The `Assimalign.Viu.Browser.CompiledRenderTests` project | The end-to-end `.viu` → generated C# → renderer canary |
| Generator snapshot tests | Emitted source, hint names, diagnostics |
| `SingleFileComponentProjectionConformanceTests` | Build/editor projection equality [SFC-PIPE-2] |
| `SingleFileComponentProjectionLineMappingTests` | That a `@script` type error maps to the real `.viu` line and column |
| `tooling/Assimalign.Viu.UtilityCss/conformance/` | The frozen Tailwind CSS v4.3.3 manifest and golden CSS vectors |
| `scripts/Test-ApplicationLifetimeConsumer.ps1` + `scripts/fixtures/{ComponentLibraryConsumer,ApplicationLifetimeConsumer}` | A base-SDK component library packs with `.viu.css` but without Browser or a WebAssembly workload; a Browser-SDK app consumes and mounts the packed component, flows its stylesheet, and passes Build, trimmed publish, and AOT publish |
| `scripts/Test-EndToEnd.ps1`, `scripts/Measure-PublishBudget.ps1`, `scripts/Test-StartupBudget.ps1`, and `scripts/budgets/PublishBudgets.json` | The packaged-consumer publish/startup producers, checkers, and reviewed budget definitions, calibrated against measured `EndToEndBrowserApp` baselines with recorded provenance |
| `benchmarks/baselines/InteropCounts.json` | Interop-call counts; a delta fails the gate [RND-IO-5] |
| `.github/workflows/area-*.yml` and `benchmarks.yml` | Live per-area CI plus the interop budget gate |
| `.github/workflows/budget-gates.yml` | Live pull-request trimmed-publish/size and trim-warning checks, with scheduled/on-demand WebAssembly AOT and real-browser `boot-to-interactive` startup lanes |

`[CONF-3]` Unit tests are **DOM-free by default**. The runtime is exercised through
`Assimalign.Viu.Testing`'s in-memory host; real-browser coverage is a separate end-to-end harness.
`TestElement` exposes read-only live views of its properties, listeners, and ordered children, so
tests can inspect host state without mutating renderer-owned storage. `ComponentWrapper` and
`ElementWrapper` name event/value operations `TriggerAsync` and `SetValueAsync`; their returned task
completes only after the deterministic scheduler has drained. `TestRenderer` and
`TestNodeOperations.Create` both accept the same `TestRendererOptions` record, so snapshot and
strict-removal behavior cannot depend on positional Boolean order.

`[CONF-4]` For reactivity and caching semantics a test MUST assert **run counts** (effect runs,
getter invocations), not only final values: caching and dependency-tracking bugs hide behind
correct-looking values.

`[CONF-5]` A build MUST produce **0 warnings, 0 errors** (`.claude/rules/checklist.md`).

---

## 17. Non-goals and current limits

### 17.1 Non-goals — decisions, not deferrals

- **Standalone semantics.** Viu makes no semantic-equivalence guarantee with an external project,
  has no external-precedence rule, and tracks no external project's version as its own contract.
- **No options-style component authoring, no mixins, no global-properties bag.** Component logic is a
  setup function returning reactive state and handlers.
- **No component-tree provide/inject** [CMP-24].
- **No runtime template compilation, no dynamic or string-sourced templates, no `compile()` API**
  [SFC-1].
- **No object-proxy interception, no implicit deep reactivity, no proxied BCL collections** [RCT-6].
- **No reflection-based serialization, no dynamic code generation, no assembly scanning, no
  `Activator.CreateInstance`** [EXE-4].
- **Not thread-safe.** Single-threaded by design [EXE-1].
- **No JavaScript execution and no JavaScript compiler macros** [VUE-4].
- **No web-framework dependency in any `Assimalign.Viu.*` library** [SSR-8].
- **Viu Utilities:** no npm, Node, Tailwind executable, PostCSS/bundler plugin, JSON configuration or
  safelist, plugin ABI, or runtime CSS generation; no automatic compatibility past v4.3.3; not
  affiliated with or endorsed by Tailwind Labs [STY-9].

### 17.2 Current limits — implemented partially or not yet

| Limit | Detail |
| --- | --- |
| Suspense hydration | Throws `NotSupportedException` [BLT-12] |
| Suspense boundary behavior | Timeout and events, fallback-to-reveal choreography, and hidden-branch post-effect delay are absent [BLT-13] |
| Router | Lazy route components and scroll behavior are not implemented [RTR-8] |
| DevTools | Area `V01.01.10` has no library in the tree |
| Static hoisting | Per-component-type static fields are not implemented; **all** static optimization routes through the per-instance render cache [SFC-OPT-1] |
| `v-memo` | Bodies are serialized but not C#-legal end to end |
| Handler caching | The cached member-expression handler pass stays off in the generator |
| Slot/`v-for` destructuring | `v-slot` destructuring and tuple `v-for` aliases emit verbatim and are not valid C# lambda parameters |
| MathML stringification | MathML attributes never stringify; MathML static content caches but does not fold into a raw-HTML insert |
| Server rendering | Compiler-informed server code generation, byte-oriented writer integration, static site generation, and directive-specific server properties are deferred |

---

## 18. Performance research policy

`[PERF-1]` **Viu's semantics are defined by this document alone.** Viu does, however, treat other
rendering frameworks — Vue.js in particular, whose compiler-informed rendering strategy Viu's
architecture independently adopts — as **performance research inputs**. A published optimization
from such a project MAY be evaluated, measured against Viu's benchmark baselines, and adopted **only**
as a Viu design decision recorded here or in an ADR.

`[PERF-2]` No external project's behavior, release, or roadmap constitutes a requirement on Viu, and
**no finding in [`docs/PERFORMANCE-RESEARCH.md`](PERFORMANCE-RESEARCH.md) is normative until it
lands in this specification.**

`[PERF-3]` A finding MUST NOT be adopted without a measured delta against
`benchmarks/Assimalign.Viu.Testing.Benchmarks` and/or `benchmarks/baselines/InteropCounts.json`.

`[PERF-4]` Matching an external project's semantics, API, or behavior is **out of scope** for that
channel and MUST NOT be raised through it.

---

## 19. Prior art and influences

Viu's rendering architecture — hierarchical virtual-node trees with compiler-informed diffing,
block-scoped collection of dynamic descendants, bitmask patch flags shared between compiler and
runtime, and longest-increasing-subsequence move minimization in keyed reconciliation — belongs to a
well-established line of work in virtual-DOM renderers. The compiler-informed block-tree formulation
in particular was **introduced by Vue 3**; longest-increasing-subsequence move minimization in keyed
reconciliation is long-established across that family of renderers. The batched command-buffer
approach to a managed↔host boundary has prior art in Blazor's render batch.

Viu's own keyed algorithm is specified in [§6.5](#65-keyed-reconciliation) and is **not** a
double-ended diff: it has no prefix/suffix synchronization pass, matching instead through a key index
plus a same-type scan for keyless children, then placing right-to-left against the
longest increasing subsequence. Where a Viu algorithm differs from a familiar one, §6 is the
authority, not the resemblance.

`[ART-1]` **Origin is history, never authority.** This section is the single place in the repository
where that history is acknowledged. Member-level documentation states behavior in Viu's own terms
and does not carry provenance ([`.claude/rules/documentation.md`](../.claude/rules/documentation.md)).

`[ART-2]` If any externally authored data table were ever transcribed rather than independently
derived, the attribution is a licensing matter and belongs in a `THIRD-PARTY-NOTICES` file, not in
doc comments. Viu Utilities already follows this pattern
(`tooling/Assimalign.Viu.UtilityCss/docs/THIRD-PARTY-NOTICES.md`).

---

## 20. Terminology

| Term | Meaning in Viu |
| --- | --- |
| **virtual tree** | The immutable `VirtualNode` hierarchy a render produces |
| **authored component** | An `IComponent` — activated behavior, one instance per mounted invocation |
| **component invocation** | A `ComponentNode` plus its raw `ComponentInvocation`; a non-activating request resolved at mount |
| **mounted node** | Internal Core bookkeeping for one virtual-tree value |
| **block** | A `VirtualNode` whose `RenderPlan.DynamicChildren` is non-null [RND-BLOCK-2] |
| **dynamic children** | A block root's collected dynamic descendants |
| **patch flag** | A `PatchFlags` value: what the compiler proved can change |
| **shape flag** | A `ShapeFlags` value: what a node is and what shape its children take |
| **flush** | One scheduler cycle: pre-flush jobs → render jobs → post-flush callbacks [SCH-2] |
| **pre-flush / post-flush** | The watcher phase before render jobs; the lifecycle phase after them |
| **commit boundary** | Where a buffered host applies its batched writes [SCH-10] |
| **effect scope** | A lifetime boundary for reactive effects — not a subscription broadcast [RCT-10] |
| **reference** | A reactive cell: `Reference<T>`, `ShallowReference<T>`, `CustomReference<T>`, `Computed<T>` |
| **projection** | The single `.viu`/`.vue` → C# model both the generator and the editor run [SFC-PIPE-2] |
| **scaffold** | The generated partial-class source a projection emits |
| **candidate** | A complete utility-class token the utility compiler can resolve |
| **handle** | An opaque host-node identity crossing the interop boundary as an `int` [EXE-12] |
