# The Viu Specification

## 0. Status, scope, and conformance language

**Status:** Draft — normative for implemented behavior.
**Adopted:** 2026-08-02 (standalone-framework decision).
**Applies to:** every `Assimalign.Viu.*` library, generator, SDK, and extension in this repository.

This document is the normative description of Viu's own semantics. Where an implementation and this
document disagree, one of them is a defect; this document is the arbiter of intent.

**Viu is a standalone C#/.NET WebAssembly UI framework. It is not a port of, binding to, or
derivative of any JavaScript framework, and no external project's behavior is authoritative for
Viu's semantics.**

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
current limit inline and cross-reference §17.

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
| `Assimalign.Viu.Shared` | The compiler↔runtime flag vocabulary, value normalization, HTML/SVG/MathML knowledge tables |
| `Assimalign.Viu.Components` | The immutable component-tree vocabulary and the activation contract |
| `Assimalign.Viu.Reactivity` | The dependency engine, reference primitives, effects, scopes, watch |
| `Assimalign.Viu.State` | Store definitions and the registry that owns their reactive lifetimes |
| `Assimalign.Viu.Core` | Host-neutral application, renderer, scheduler, hydration, built-in components |
| `Assimalign.Viu.Browser` | The browser host adapter: interop bridge, DOM directives, transitions |
| `Assimalign.Viu.ServerRenderer` | HTML serialization and the hydration marker protocol |
| `Assimalign.Viu.Router` / `.Router.Browser` | The DOM-free router core and its browser click/history bridge |
| `Assimalign.Viu.Testing` | The in-memory host and component test wrappers |
| `Assimalign.Viu.Syntax*` | The build-time parser cluster: templates, `.viu`/`.vue` containers, CSS, HTML, JavaScript |
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

`[EXE-1]` The Viu runtime targets **a single event loop and is not thread-safe**. Ambient `static`
state — the scheduler's queues, the reactivity engine's tracking and batching stack — is a
deliberate consequence of that model, not an oversight.

`[EXE-2]` Every non-thread-safe public type MUST say so in its XML documentation.

`[EXE-3]` A host MAY dispatch the scheduler's flush through a `SynchronizationContext`. When none is
installed the flush falls back to the thread pool; on single-threaded browser WebAssembly that still
lands on the main thread through the JavaScript event loop.

### 3.2 AOT and trimming

`[EXE-4]` Viu MUST NOT use reflection-based serialization, dynamic code generation
(`Reflection.Emit`, compiled expression trees, `DispatchProxy`), or linker-unfriendly activation.
Viu never scans assemblies and never calls `Activator.CreateInstance`.

`[EXE-5]` Component activation is **explicit delegate dispatch**. `IComponentFactory` resolves a
template by registered type or registered name; `ComponentRegistration` carries an explicit
activator delegate. An application MAY close those delegates over a generated resolver, a
dependency-injection container, or hand-written composition.

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
`libraries/Assimalign.Viu.Components/src/Activation/{ComponentFactory,ComponentRegistration}.cs`;
`docs/ARCHITECTURE.md` §"AOT and ownership rules".*

---

## 4. The component model

### 4.1 Three lifetimes

`[CMP-1]` Viu separates three roles that a single object would otherwise conflate:

| Role | Type | Lifetime |
| --- | --- | --- |
| Render description | `IComponent` | Recreated by every render |
| Authored behavior | `IComponentTemplate` | One instance per mounted template request |
| Runtime bookkeeping | Internal Core `MountedRenderNode<TNode>` variants | Mount through unmount |

`[CMP-2]` `IComponent` values are **immutable descriptions**. Mounted host state — host nodes,
anchors, ranges, reactive render effects, parent links, prior-tree state — is owned internally by
Core and MUST NOT be written back onto an `IComponent`.

### 4.2 The tree vocabulary

`[CMP-3]` The component tree has exactly seven kinds, discriminated by `ComponentKind`:

| `ComponentKind` | Interface | Describes |
| --- | --- | --- |
| `Element` | `IElementComponent` | A host element |
| `Template` | `ITemplateComponent` | A user-authored template mount request |
| `Text` | `ITextComponent` | A text value |
| `Comment` | `ICommentComponent` | A comment or empty placeholder |
| `Static` | `IStaticComponent` | A pre-rendered static range |
| `Fragment` | `IFragmentComponent` | A group of siblings |
| `Teleport` | `ITeleportComponent` | Content rendered into a different host container |

`ComponentTree` is the public factory for these values.

### 4.3 Activation

`[CMP-4]` `IComponentFactory` is **only** a component resolver. It declares `Create(Type)` and
`Create(string)`, plus a default-implemented generic `Create<TComponent>()` that forwards to
`Create(Type)`. It does **not** implement or inherit `IServiceProvider`.

`[CMP-5]` `IApplicationContext` carries the `IComponentFactory` and the `IServiceProvider` as
**independent** values. An application MAY supply one object for both roles; the contracts do not
require it.

`[CMP-6]` The built-in `ComponentFactory` resolves a registered name by trying the raw name, then
its camel-case spelling, then the Pascal-case spelling of that — so a `my-widget` request resolves a
`myWidget` or `MyWidget` registration. Name lookup is **ordinal**. A duplicate registered component
type, or a duplicate registered name, throws `ArgumentException` at construction. An unregistered
type or name throws `InvalidOperationException` at resolution: runtime constructor discovery is not
a fallback [EXE-4].

`[CMP-7]` An `ITemplateComponent` is a **non-activating** mount request: it identifies its template
by explicit `Type` or by registered name and carries immutable argument, slot, parent-listener,
directive, key, and optimization snapshots. Core selects the matching `IComponentFactory.Create`
overload at mount time.

`[CMP-8]` `IComponentTemplate.Setup(IComponentContext)` is **synchronous** and returns the render
closure (`ComponentRenderer`), so Core establishes the component's reactive scope deterministically
before any asynchronous work can interleave.

### 4.4 Ownership

`[CMP-9]` The **external composition root owns and disposes** the component factory, the service
provider, and the state registry. Core and its application objects *borrow* them and MUST NOT
dispose them.

`[CMP-10]` Core **owns** each activated template instance and MUST dispose it (when it implements
`IDisposable`) on setup failure or on unmount.

`[CMP-11]` Viu does not create dependency-injection scopes automatically. A custom factory MAY bind
a scope to the template instance it returns.

### 4.5 Parameters, events, and fallthrough

`[CMP-12]` `ComponentParameter` supports required values, an optional default factory evaluated **at
most once per mounted instance**, and an optional validator. A required-value or validator failure
**warns without discarding** the supplied value.

`[CMP-13]` The declaration name is canonical in `IComponentContext.Arguments`. A parent render MAY
spell a parameter in camel-case or kebab-case; both resolve to the canonical declaration name.

`[CMP-14]` `ComponentEvent` MAY validate the complete ordered argument list. `IComponentContext.Emit`
accepts zero or more ordered arguments. A kebab-case emission matches a camel-case listener.

`[CMP-15]` `ComponentEventListener` supports single-payload and all-arguments handler forms, each in
synchronous and `Task`-returning shapes. A single-payload listener receives the first argument, or
`null` for an argument-free emission. Every `Task` returned by a multicast asynchronous listener is
observed through the component error pipeline.

`[CMP-16]` A listener MAY be marked `IsOnce`. Once-state belongs to the **mounted instance** and
survives parent updates. Both an ordinary and a `Once` listener MAY run for a single emission.

`[CMP-17]` **Fallthrough.** Declared `onX` and `onXOnce` listeners are consumed as component events.
Undeclared listeners remain fallthrough attributes. When attribute inheritance is enabled and the
template renders a single element root, Core merges fallthrough properties: classes **space-join**,
style declarations **merge with the parent value winning**, and compatible event delegates
**combine in root-then-parent order**. Declared component-event listeners never enter this
host-event merge.

### 4.6 Slots

`[CMP-18]` `ComponentSlots` carries `SlotFlags` metadata — `Stable`, `Dynamic`, or `Forwarded` —
preserved from compilation into the immutable template request. Core uses that classification to
**skip** child renders for structurally stable slots while forcing updates for dynamic and
effectively-dynamic forwarded slots.

`[CMP-19]` A hand-authored slot collection that cannot prove stability MUST report
`SlotFlags.Dynamic`. An over-optimistic flag manifests as a child that silently stops updating.

### 4.7 Lifecycle

`[CMP-20]` `IComponentLifecycle` exposes **named, typed hooks**, not an enum-keyed callback
registry: before-mount, mounted, before-update, updated, before-unmount, unmounted, activated,
deactivated, and `OnServerPrefetch`. Each accepts a synchronous or a `Task`-returning callback.

`[CMP-21]` **Ordinary asynchronous hooks do not delay lifecycle progression.** Core observes the
returned `Task` and routes faults through `OnErrorCaptured` to the application error handler, but
does not await it. `OnServerPrefetch` is the sole awaited hook, and only during server rendering
([§11](#11-server-rendering-and-hydration)), because serialization must wait for its data.

`[CMP-22]` `IComponentLifecycle` exposes the **component-lifetime `CancellationToken`**. It is
cancelled during unmount, *after* before-unmount callbacks start and *before* effect-scope and
subtree teardown.

`[CMP-23]` The application-level error handler is the **terminal sink** for observed render,
lifecycle, watcher, and event faults that no ancestor `OnErrorCaptured` hook stopped.

### 4.8 No component-tree provide/inject

`[CMP-24]` Viu has **no hierarchical component-tree dependency API**. Component dependencies are
explicit:

- parameters and slots for parent-to-child data;
- `IComponentContext.Services` for application services;
- State definitions and explicit registries for shared state; and
- `IComponentContext.Components` for deliberate component resolution.

This is a decision, not a deferral (see [§17](#17-non-goals-and-current-limits)). It has visible
consequences elsewhere: `RouterView` takes its nesting depth as an explicit argument
([§12](#12-routing)) precisely because no ambient hierarchical channel exists.

#### Application composition and lifetime

`[APP-1]` A runnable `IApplication` has the internal single-use state machine **Created → Starting →
Running → Stopping → Stopped**, with failure edges from Starting, Running, and Stopping. `StartAsync`
synchronously claims the application by moving it from Created to Starting exactly once, begins the
middleware pipeline as an independently observed asynchronous task, and waits until the host terminal
has mounted and signalled Running. Every later `StartAsync` call throws, including after stopping or
failure. `IApplicationContext.IsRunning` is true only between that mounted signal and the beginning of
stopping. An already-cancelled token is observed only after the claim and therefore follows the
ordinary **Starting → Stopping → Stopped** path without mounting.

`[APP-2]` Application composition and runtime behavior are separate phases. The lean
`IApplicationBuilder` exposes only `ConfigureApplication(Action<ApplicationOptions>)` and `Build()`.
`ApplicationOptions` is the single builder composition surface for the root component, component
factory, service provider, state registry, directive resolver, and diagnostics. `Build()` snapshots
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
pipeline task so `StopAsync` and `RunAsync` surface it. Startup and host failures follow the same
reporting path; cancellation requested through Stopping is normal shutdown [APP-1].

`[APP-6]` The component factory, service provider, state registry, directive resolver, and other
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
the generated partial's `IComponentTemplate.Parameters`. The attribute is a **compile-time
declaration only** — nothing is discovered by reflection, and the resulting declaration is the same
static value an imperative one produces [CMP-12]. The attributes take effect only inside a compiled
single-file component's script block; a hand-authored `IComponentTemplate` declares its surface
imperatively.

`[CMP-27]` **Name derivation.** The canonical argument or event name is the **camel-case spelling of
the declaring member's name**: the leading run of upper-case letters lower-cases whole, except that a
run longer than one keeps its last letter capitalized when a lower-case letter follows it (`Title` →
`title`, `ModelValue` → `modelValue`, `URL` → `url`, `HTMLContent` → `htmlContent`). The attribute's
`Name` overrides the derivation and MUST be a non-empty constant string literal, which is how a
spelling no C# identifier can produce — `update:modelValue`, `model-value` — is declared. The derived
name is canonical in `IComponentContext.Arguments`, and a parent's kebab-case spelling still resolves
to it [CMP-13].

`[CMP-28]` **Requiredness.** A parameter is required when the attribute sets `IsRequired` **or** when
the property carries the C# `required` modifier. Viu activates a template through a parameterless
`new T()` [EXE-4], so no object initializer exists to satisfy C#'s own required-member rule: a
`required` declared parameter therefore makes the generated partial emit a `[SetsRequiredMembers]`
parameterless constructor. The requirement is Viu's to enforce ([CMP-12] warns at mount), not C#'s.
The scaffold emits no constructor when the script block declares one of its own.

`[CMP-29]` **Binding.** The generated scaffold assigns each declared property from
`IComponentContext.Arguments` **once during setup, before `OnSetup`, and again at the head of every
render pass**. Core replaces the argument snapshot before a child re-renders, so a declared property
always reflects the parent's current value. The property's value at setup time — its initializer, or
the type's default when it has none — is captured **once per mounted instance** as that parameter's
default and restored on any pass where the parent supplies no argument; the capture is the attribute
form's equivalent of `ComponentParameter.DefaultFactory` and shares its at-most-once evaluation
[CMP-12]. Values are read through the typed `IComponentArguments.Get<T>`: an argument whose runtime
value is not of the property's type yields that type's default, with no coercion.

`[CMP-30]` **Events.** A component MAY declare an output event on the method that emits it:
`[Event]` on a non-generic, instance `partial void` method with no body and by-value parameters only.
The generator synthesizes the `ComponentEvent` — whose validator asserts the emitted argument count —
and implements the method as an `IComponentContext.Emit` of the declared name with the method's
parameters as the ordered payload. A method rather than a property is the anchor because an event
carries a payload *signature* rather than a value; the consequence is that the event name is spelled
exactly once, in the attribute, and the component's own call site is strongly typed.

`[CMP-31]` **Coexistence.** The imperative and attribute forms are exclusive **per kind**: a
component that declares `[Parameter]` properties MUST NOT also declare a `Parameters` member, and one
that declares `[Event]` methods MUST NOT also declare an `Events` member. Either mix is a build
error. Parameters and events stay independent, so an imperative `Parameters` collection and
attribute-declared events coexist. The rule has two reasons: the generated declaration is an
*explicit* interface implementation and would silently shadow an authored collection, and an
attribute-declared surface is usable as a build-time contract only when it is complete.

### 4.10 Root-level lifecycle registration

`[CMP-32]` A component MAY register a lifecycle callback **at the root of its own class** —
`OnMounted(callback)` — instead of through the context — `Context.Lifecycle.OnMounted(callback)`.
`ComponentTemplateBase` declares one **protected** pass-through per `IComponentLifecycle` registration
method, with the identical name and signature, and the compiled single-file component derives from it
[SFC-CG-4]. The root form is the **specified equivalent** of the context form: it registers the same
callback with the same registrar, so the two forms carry identical timing [CMP-20], identical
asynchronous observation [CMP-21], and identical error routing [CMP-23]. Callbacks registered for one
phase run in **registration order**, and that order is the order the registrations were made
regardless of which form each used, so the two forms MAY be mixed freely within one component. The
root form adds no state: there is exactly one registrar per mounted component, reached through
`IComponentContext.Lifecycle` either way.

A component MAY declare its own member with one of these names. An identical signature **hides** the
inherited pass-through under ordinary C# member-hiding rules — the authored member wins at every call
site inside the component, C# reports the hiding as a warning rather than an error, no registration
happens through it, and the hidden hook stays reachable through the context form. A different
signature is an ordinary overload and hides nothing. The pass-throughs are therefore *not* reserved
names in the sense of [SFC-CG-1]: a collision degrades to the behavior the component would have had
without them.

*Authority: `libraries/Assimalign.Viu.Components/src/Abstraction/*.cs` (21 interfaces);
`libraries/Assimalign.Viu.Components/src/{Tree,Metadata,Slots,Activation}/*.cs`
(`Metadata/{ParameterAttribute,EventAttribute}.cs` for [CMP-26]-[CMP-31];
`ComponentTemplateBase.cs` for [CMP-32]);
`libraries/Assimalign.Viu.Core/src/Internal/{ComponentContext,ComponentLifecycle,MountedComponent}.cs`;
`libraries/Assimalign.Viu.Core/src/Abstraction/{IApplication,IApplicationBuilder,IApplicationContext}.cs`;
`libraries/Assimalign.Viu.Core/src/Delegates/{ApplicationDelegate,ApplicationMiddleware}.cs`;
`libraries/Assimalign.Viu.Core/src/Application/{ApplicationContext,ApplicationOptions}.cs`;
`libraries/Assimalign.Viu.Core/src/Extensions/ApplicationExtensions.cs`;
`libraries/Assimalign.Viu.Browser/src/{BrowserApplication,BrowserApplicationBuilder}.cs`;
`libraries/Assimalign.Viu.Components/docs/OVERVIEW.md`; `docs/ARCHITECTURE.md`;
`libraries/Assimalign.Viu.Core/docs/OVERVIEW.md`.*

---

## 5. Reactivity

### 5.1 The type model

`[RCT-1]` `ReactiveValue` / `ReactiveValue<T>` is the engine base class; it holds the dependency
cell inline as a field. `IReactiveReference` and `IReactiveReference<T>` are the public,
substitutable contracts. Every Reactivity-owned public interface is prefixed `IReactive*`.

`[RCT-2]` Hot-path dispatch rule: per-trigger notification, patching, and diffing MUST dispatch
through an **abstract base-class vtable**, not an interface. Interface dispatch is for cold public
API boundaries. First-party references therefore derive from `ReactiveValue<T>` *and* implement
`IReactiveReference<T>`.

`[RCT-3]` The reference primitives are `Reference<T>`, `ShallowReference<T>`, `CustomReference<T>`,
and `Computed<T>`. `Computed<T>` plays a dual role in the dependency graph — it is both a value
others subscribe to and a subscriber to its own sources — and realizes the subscriber half by
**composition** over an internal sealed subscriber rather than by multiple inheritance.

`[RCT-4]` An external `IReactiveReference<T>` implementation is responsible for tracking its own
reads and triggering on changed writes; the interface cannot enforce correct tracking.
`Reactive.CustomReference(...)` is the preferred extension point. Operations needing direct
dependency access (forced triggering, graph inspection) additionally require
`IReactiveTrackedReference`.

### 5.2 The public surface

`[RCT-5]` `Reactive` is the static facade: `Reference`, `ShallowReference`, `CustomReference`,
`Computed`; `Effect`; `EffectScope`, `CurrentScope`, `OnScopeDispose`; `Watch`, `WatchEffect`;
`TriggerReference`; `PauseTracking`, `ResetTracking`, `StartBatch`, `EndBatch`; and the inspection
and escape hatches `IsRef`, `Unref`, `ToRef`, `IsReactive`, `IsReadonly`, `ToRaw`, `MarkRaw`.

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

`[RND-1]` A render produces a **fresh immutable tree**. Rendering never mutates the prior tree.
`Renderer<TNode>` reconciles the new tree against the mounted representation of the old one and
emits the minimal host operations that reconcile them.

`[RND-2]` The mounted representation is a parallel hierarchy of internal sealed
`MountedRenderNode<TNode>` variants — `MountedElementNode`, `MountedFragmentNode`,
`MountedLeafNode` (text and comment), `MountedStaticNode`, `MountedTeleportNode`,
`MountedTemplateNode` — rooted at a `MountedTree<TNode>` per host container. These own host nodes,
ranges, anchors, child lists, directive bindings, transition hooks, and reference jobs.

`[RND-3]` `MountedTemplateNode<TNode>` additionally owns the activated `IComponentTemplate`, its
`IComponentContext`, its reactive render effect, and its mounted subtree. **That state never returns
to the immutable authoring model** [CMP-2].

`[RND-4]` `MountedTree<TNode>.Components` maps each `IComponent` to its mounted node by reference
identity. This map is what makes block patching ([§6.3](#63-the-block-tree)) possible: a block root
can look up the mounted node for a dynamic descendant without walking the tree to find it.

`[RND-5]` `Renderer<TNode>.Render(component, container, application)` mounts on first call and
patches thereafter. Passing `null` unmounts the current root and forgets the container. A mounted
container retains the first supplied `IApplicationContext`; supplying a different one throws
`InvalidOperationException`.

### 6.2 The flag vocabulary

`[RND-FLAGS-1]` `PatchFlags`, `ShapeFlags`, and `SlotFlags` (owned by `Assimalign.Viu.Shared`) are
**the interface between build-time analysis and runtime patching**. Their bit layout is a frozen
contract between compiled output and the runtime: changing a value silently breaks components
compiled by an earlier Viu, so **values are additive only**.

`[RND-FLAGS-2]` `PatchFlags` positive members are single bits and combine with bitwise OR: `Text`,
`Class`, `Style`, `Props`, `FullProps`, `NeedHydration`, `StableFragment`, `KeyedFragment`,
`UnkeyedFragment`, `NeedPatch`, `DynamicSlots`, `DevRootFragment`.

`[RND-FLAGS-3]` `Cached` (`-1`) and `Bail` (`-2`) are **whole-value sentinels, never bit
combinations**. Because every negative `int` has most bits set, a naive bitwise test against a
negative value spuriously succeeds. Every positive-bit check MUST therefore be gated on
`flags > 0`; the predicates in `PatchFlagsExtensions` do this. `Cached` marks a subtree the diff
skips entirely; `Bail` marks a tree that MUST fall back to a full diff.

`[RND-FLAGS-4]` `ShapeFlags` encodes what a node is and what shape its children take, so the runtime
branches on bitwise checks rather than type tests.

`[RND-FLAGS-5]` `SlotFlags` is a plain enumeration, not a bitmask: a slot collection has exactly one
of `Stable`, `Dynamic`, `Forwarded`.

`[RND-FLAGS-6]` `PatchFlags.cs` and `SlotFlags.cs` are `<Compile Include>`-linked into the
`netstandard2.0` generator projects. **Their file paths are frozen**; moving them requires updating
every linking csproj in the same change.

### 6.3 The block tree

The block tree is the mechanism that turns compile-time knowledge into skipped runtime work.

`[RND-BLOCK-1]` `ComponentOptimization` carries the compiler→runtime hints on every tree value:

- `PatchFlags` — what may change;
- `DynamicProperties` — the property names that may change when `PatchFlags.Props` is set;
- `DynamicChildren` — the dynamic descendants collected for a block root;
- `HasOnce` — whether suspended block tracking (`v-once`) occurred inside the block.

`ComponentOptimization.None` is the metadata for hand-authored, unoptimized values.

`[RND-BLOCK-2]` **The three-state rule for `DynamicChildren` is normative:**

| `DynamicChildren` | Meaning | Runtime behavior |
| --- | --- | --- |
| `null` | Not a block | Full child walk |
| non-null, **empty** | An optimized block with no dynamic descendants | **Skip every child visit** |
| non-null, non-empty | An optimized block | Patch the listed descendants directly |

`IsBlock` is defined as `DynamicChildren is not null`. Confusing the null and empty cases is the
single most consequential error a producer of this metadata can make.

`[RND-BLOCK-3]` Generated render code **opens a block, collects its dynamic descendants, and
attaches the immutable snapshot to the block root**. `ComponentOptimization` copies both list
arguments defensively into read-only snapshots at construction, so the metadata cannot be mutated
after it is attached.

`[RND-BLOCK-4]` **Block patching is attempted only when the old and new block shapes agree.** The
renderer requires: both `DynamicChildren` lists non-null, **equal in count**, and every old dynamic
child still registered in the mounted-tree map. If any condition fails, the renderer MUST fall back
to a full child diff. A mismatched block shape is a correctness event, never a crash.

`[RND-BLOCK-5]` When block patching succeeds, each dynamic descendant is patched **in place, in its
own host parent**, bypassing the parent children diff. If patching replaces a mounted node (a type
change), the renderer MUST thread the replacement through the mounted ownership graph so later
moves and unmounts never retain the removed node.

`[RND-BLOCK-6]` **Block-aware teardown.** Unmounting a block visits only its collected dynamic
descendants. Three cases retain the full walk, because skipping them would leak: `HasOnce` blocks;
bailed (non-positive patch-flag) trees; and fragments that are not `StableFragment` — that is,
keyed and unkeyed fragment blocks.

`[RND-BLOCK-7]` A child skipped by an optimized teardown MUST still be *released*: unregistered from
the mounted-tree map, marked unmounted, and its pending reference job invalidated. A skipped child
that carries a template, a teleport, a template reference, a node lifecycle hook, a directive
binding, or a transition MUST receive a **full unmount visit** instead of a release, because those
carry external effects.

### 6.4 Patch dispatch

`[RND-PATCH-1]` `Patch` decides in this order:

1. no mounted node → **mount**;
2. the same `IComponent` instance by reference → **no-op** (the tree did not change here);
3. not the same component type → **unmount the old subtree and mount the new one** at the old node's
   next-sibling anchor, preserving the old owner context;
4. otherwise → dispatch to the per-kind patch routine.

`[RND-PATCH-2]` `IsSameComponentType` requires equal `ComponentKind` and equal `Key`, and then, by
kind: equal ordinal `Tag` for elements; equal template identity for templates (by `TemplateType`
when either side declares one, otherwise by ordinal `TemplateName`); equal ordinal `Content` for
static ranges. Text, comment, fragment, and teleport kinds match on kind and key alone.

`[RND-PATCH-3]` **Element patching** returns early for a cached element, and otherwise selects one of
four paths:

| Condition | Attributes | Children |
| --- | --- | --- |
| `PatchFlags == Cached` (early return) | none | none — only the transition and template reference update |
| block children patched | flag-selective | skipped, except a `Text` fast path |
| either side is a block but block patching failed | full diff | full diff, forced to `Bail` |
| positive patch flags | flag-selective | only the `Text` fast path |
| otherwise | full diff | full diff under the new flags |

`[RND-PATCH-4]` **Flag-selective attribute patching**: `FullProps` degrades to a full attribute
diff; otherwise `Class` patches only `class`, `Style` patches only `style`, and `Props` patches
exactly the names listed in `DynamicProperties`. Non-positive flags patch nothing.

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
no old counterpart (encoded as `0`) are excluded from the subsequence.

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

`[SCH-9]` **`NextTick()`** returns a task that completes after the current or next flush chain, and
`Task.CompletedTask` when nothing is queued. A post-flush callback that queues more work causes
another cycle to run — sharing the same recursion bookkeeping — **before** `NextTick` resolves.

`[SCH-10]` **The commit boundary fires twice per flush** and MUST be idempotent (a no-op when
nothing is buffered):

1. after the job queue drains and **before** post-flush callbacks, so `mounted`/`updated` hooks that
   read the host (layout, template references) observe the committed render; and
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
are cleared so every job can re-queue, `NextTick` is resolved so awaiters do not hang, and the
exception is rethrown to the host.

### 6.7 Host abstraction

`[RND-HOST-1]` `RendererOptions<TNode>` is the complete host contract. Required operations:
`Insert`, `Remove`, `CreateElement`, `CreateText`, `CreateComment`, `SetText`, `ParentNode`,
`NextSibling`, `PatchAttribute`. Optional operations: `SetScopeIdentifier`,
`ResolveTeleportTarget`, `Commit`, `InsertStaticContent`, `CreateHydrationReader`.

`[RND-HOST-2]` A capability whose operation is absent is **unavailable, not degraded**. Rendering an
`IStaticComponent` requires `InsertStaticContent`; hydration requires `CreateHydrationReader` and
throws `NotSupportedException` without it.

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
`libraries/Assimalign.Viu.Shared/src/{PatchFlags,ShapeFlags,SlotFlags}.cs`;
`libraries/Assimalign.Viu.Components/src/Optimization/ComponentOptimization.cs`;
`libraries/Assimalign.Viu.Browser/docs/{DESIGN.md,ADR-0001-interop-marshaling.md}`;
`libraries/Assimalign.Viu.Core/docs/OVERVIEW.md`; `docs/ARCHITECTURE.md` §"Block-tree updates".*

---

## 7. Built-in components

Each built-in is specified with its **current limits** inline.

### 7.1 Teleport

`[BLT-1]` `ITeleportComponent` renders its children into a different host container while remaining
logically positioned in the tree. The renderer emits origin anchors at the logical position and
manages the target range separately.

`[BLT-2]` `IsDeferred` postpones **target-side setup** to the current render's post-flush phase, so
a target rendered later in the same tree resolves. A disabled Teleport still mounts its content at
the logical position **immediately**; only target-side setup defers.

`[BLT-3]` Teleport content moves between the logical and target containers when `Disabled` changes.
Block dynamic-child patching applies to teleport content, with static host carry-forward.

`[BLT-4]` Target resolution goes through `RendererOptions<TNode>.ResolveTeleportTarget`. A target
already assignable to `TNode` is used directly. An unresolved target warns and skips the target
content.

### 7.2 KeepAlive

`[BLT-5]` `KeepAlive` moves inactive keyed template subtrees into **renderer-owned detached
storage**, preserving their component instances and reactive scopes rather than unmounting them.

`[BLT-6]` It implements component-name include/exclude filtering, reactive cache pruning when the
filter changes, and **child-before-parent** activation callbacks. A **positive** `maximum` enables
least-recently-used eviction, which fully unmounts the evicted entry; zero, a negative value, a
missing value, or an unparseable string means **unbounded**.

### 7.3 Transitions

`[BLT-7]` `BaseTransition` is the **host-neutral** insertion/removal choreography. Core owns
transition identity, cancellation, mode sequencing, insertion, and deferred removal. It knows
nothing about CSS.

`[BLT-8]` The browser `Transition` and `TransitionGroup` are host components layered on it: Browser
owns class names, double-animation-frame scheduling, forced reflow, computed or explicit end timing,
and element handles.

`[BLT-9]` `ComponentTransitionScope` attaches one shared transition state to multiple immutable
children and can finish a pending enter phase before a host performs layout measurement.
`ComponentHost.GetKeyedChildElements<TNode>` exposes an ordered key→first-host-element snapshot; it
observes the **outgoing** tree during before-update and the **patched incoming** tree during
updated. That pair of snapshots is what a host's FLIP pass measures against.

`[BLT-10]` For a persisted transition, Core binds a host-neutral `ComponentTransition` to directive
bindings. The renderer **skips its own insertion/removal transition** for persisted hooks, so the
directive and the renderer never both drive the same phase.

### 7.4 Suspense

`[BLT-11]` `Suspense` implements pending-branch storage, fallback ownership, nested boundary
accounting, and coordinated reveal.

`[BLT-12]` **Limit — Suspense hydration is not implemented.** Hydrating a Suspense boundary throws
`NotSupportedException` with a descriptive message, rather than attempting a partial or incorrect
claim of server-rendered pending/fallback branches. Render the boundary on the client.

`[BLT-13]` **Limit.** Boundary timeout and events, fallback-to-reveal transition choreography, and
delaying mounted/post-render effects from the hidden default branch are not implemented; those
effects run when the detached branch mounts. See [§17](#17-non-goals-and-current-limits).

### 7.5 Asynchronous and dynamic components

`[BLT-14]` Asynchronous component definitions retain **explicit `IComponentFactory` activation**,
deduplicate concurrent loads for the same definition, and integrate with server prefetch and
Suspense.

`[BLT-15]` A **plain dynamic string resolves to an element tag**, not a registered component,
because `IComponentFactory` deliberately has no registration-probe API [CMP-4]. Use
`DynamicComponents.Named(name)` to select a registered component name explicitly. This is a
correctness-preserving choice: probing would require the factory to answer "do you have this?",
which no arbitrary resolver can be required to answer.

*Authority: `libraries/Assimalign.Viu.Core/src/{KeepAlive,Suspense,Transitions,AsynchronousComponents,DynamicComponents}/`;
`libraries/Assimalign.Viu.Core/src/Rendering/{Renderer.KeepAlive.cs,Renderer.Suspense.cs,Renderer.Hydration.cs}`;
`libraries/Assimalign.Viu.Core/docs/{OVERVIEW.md,KEEP-ALIVE.md,ASYNCHRONOUS-AND-DYNAMIC-COMPONENTS.md}`;
`libraries/Assimalign.Viu.Browser/docs/DESIGN.md` §Transitions; `docs/ARCHITECTURE.md` §"Runtime capabilities".*

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

`[SFC-CG-1]` The generated render member is
`internal static object? Render(<ComponentClass> _ctx, object?[] _cache)`, accompanied by
`internal const int RenderCacheSize`. **`Render` and `RenderCacheSize` are reserved member names** in
a `.viu` component's partial class.

`[SFC-CG-2]` Generated code binds **by name** against `global::Assimalign.Viu.RenderHelpers` through
a file-level `using static`, plus `global::Assimalign.Viu.Browser.DomRenderHelpers` for DOM
directives. **No `Assimalign.Viu.Syntax.*` assembly references any runtime assembly**; the
name-binding contract flows one way.

`[SFC-CG-3]` A component that declares its surface by attribute ([CMP-26], [CMP-30]) additionally
reserves the generated members `__ViuDeclaredParameters`, `__ViuDeclaredEvents`,
`__ViuBindParameters`, `__viuParameterDefaultsCaptured`, and `__viuParameterDefault_<Property>` in its
partial class. The two declaration collections are emitted as **explicit** `IComponentTemplate`
implementations, so they can never collide with an authored member of the same name — which is also
why declaring both forms is an error [CMP-31].

`[SFC-CG-4]` A component with a template block is generated as
`partial class <Name> : ComponentTemplateBase, IComponentTemplate`. The base class supplies the
`Context` property the generated `Setup` assigns once per mount before `OnSetup` runs, and the
protected root-level lifecycle registration surface [CMP-32]. `IComponentTemplate` stays on the
partial itself because the scaffold's declaration members are *explicit* interface implementations
[SFC-CG-3], which C# permits only on a type that lists the interface. Because the base type is
declared by the generated partial, **no other partial declaration of the component may name a
different base class**. A component with no template block stays a plain partial class with neither
[V01.01.06.07].

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

`[SFC-CG-6]` **The runtime-directive tuple.** `_withDirectives` receives one `object?[]` per
directive, positional: `[0]` the directive reference, `[1]` its bound value, `[2]` its string
argument or null, `[3]` its modifier bag. Slot `[3]` is `IReadOnlyDictionary<string, bool>` and is
built by `_createModifiers`, **not** by the `_createProps` property helper — the two bags differ in
value type (`bool` versus `object?`), and a property bag in the modifier slot type-checks yet reads
back as *no modifiers at all*. Core therefore rejects any other non-null shape in slot `[3]` rather
than degrading silently. Slots `[2]` and `[3]` are emitted only when the directive has an argument or
modifiers; an absent leading slot is filled with `null` so the positions never shift.

`[SFC-CG-7]` **Native `v-model` carriers.** On a native control the compiler selects the runtime
directive from the element and its `type` — `input`/`textarea` → `_vModelText`, `type="checkbox"` →
`_vModelCheckbox`, `type="radio"` → `_vModelRadio`, `select` → `_vModelSelect`, and a dynamic
`:type` (or a dynamically keyed `v-bind`) → `_vModelDynamic`, which re-resolves per render from the
element's current tag and type. `type="file"` is an error. Each directive reflects the model through
the DOM property that carries it and commits user edits from the event that carries them: `value` +
`input` for text-like inputs and `textarea`, `checked` + `change` for checkbox and radio, and option
`selected` + `change` for `select` — matching the events those controls fire per
[WHATWG HTML](https://html.spec.whatwg.org/multipage/input.html#common-input-element-events).
Modifiers shift them: `.lazy` moves the text-input commit from `input` to `change`, `.trim` trims the
committed value and re-syncs the element on change, and `.number` (implied by `type="number"`)
coerces it numerically.

Because Viu has no `this`-proxy and no reflection, a native `v-model` cannot recover its setter from
the `onUpdate:modelValue` prop the way a component `v-model` does. Slot `[1]` therefore carries a
`ViuModelBinding` holding **both** the current value and the generated write-back delegate; the
`onUpdate:modelValue` prop is still emitted for uniformity but is inert on a native element, which
the DOM patcher skips rather than binding as a listener. The `modelValue` prop is not emitted at all
on a native element.

`[SFC-8]` **Source mapping.** Each expression-bearing render line carries a C# `#line` **span**
directive — `#line (line,column)-(line,column) offset "file"` — anchored to that line's leftmost
expression and closed with `#line default`. The span form is required because a render expression is
rewritten and its column must be re-aligned; the `@script` merge uses the line-only form because its
columns already match. Non-expression scaffolding, and any second expression sharing one physical
render line, falls back to the generated file.

### 8.6 Static optimization

`[SFC-OPT-1]` A fully static subtree is marked `PatchFlags.Cached` and wrapped in a per-instance
render-cache slot, so it is created once per instance and reused across every re-render.

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

`[VUE-8]` The SDK targets glob `**/*.viu` and `**/*.vue` into one `ViuSingleFileComponent`,
`AdditionalFiles`, and `Watch` graph. `.vue` is discovered only for Viu projects, and the Visual
Studio language server re-checks the owning project before accepting a compatibility document
([§14](#14-the-tooling-and-editor-contract)).

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

`[STY-1]` A generated template exposes its style scope through `IComponentTemplate.ScopeIdentifier`:
a `data-v-<hash>` identifier derived by `StyleScopeId` as an FNV-1a hash over the **project-relative**
component path. Both build-time hosts — the source generator and the bundling MSBuild task — MUST
derive the identical id, or the scoped CSS will not match the stamped elements. The compiler rewrites
the block's selectors to that scope; the renderer stamps the identifier on host elements through the
optional `RendererOptions<TNode>.SetScopeIdentifier` operation.

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

`[STY-6]` `v-bind()` in a style block compiles to `CssVariables.UseCssVariables(context, getter)`
emitted from the generated setup path, taking an **explicit `IComponentContext`** so ownership is
unambiguous.

`[STY-7]` After mount, a post-flush watcher tracks the getter's reactive dependencies and applies
each hashed custom property to **every current outermost host element** reported by `ComponentHost`
— fragment roots included. The updated hook reapplies when a component changes its element or
fragment roots; before unmount the component stops the watcher.

`[STY-8]` **A `v-bind()` change updates the host without re-rendering the component.** On a buffered
host the properties are written into the command frame and the owning context queues its
renderer-specific commit, so the change reaches the host before `NextTick` even though no render
occurred.

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
`RenderToStreamAsync` writes **completed template subtrees** to a `TextWriter` and awaits the
writer's `FlushAsync`, so the destination controls backpressure.

`[SSR-2]` `ServerRenderApplication` is a plain per-render composition object carrying an immutable
`IApplicationContext` **without a host node type**. It does not implement `IApplication`, owns no
persistent mounted lifetime, and does not participate in top-level application middleware [APP-7].

`[SSR-3]` ServerRenderer consumes the **same `IComponent` tree** client renderers patch; it does not
maintain a second node model. `ComponentTreeSerializer` dispatches the seven `ComponentKind` values
[CMP-3].

`[SSR-4]` `ServerComponentRenderer` reuses Core's `MountedComponent` pipeline: activate a fresh
template, create the live `IComponentContext` and reactive effect scope, run synchronous `Setup`,
**await every `OnServerPrefetch` callback** [CMP-21], invoke the renderer once, serialize the
subtree, then stop the scope, cancel the component token, and dispose the mount-owned template.

`[SSR-5]` Client-only before-mount, mounted, update, and unmount callbacks **do not run** during
server rendering. Render cancellation interrupts the prefetch wait and cancels the component token
during cleanup.

`[SSR-6]` Escaping targets **WHATWG HTML serialization**: `"`, `&`, `'`, `<`, `>` are escaped, and
comment terminators are repeatedly removed from comment content. Attribute serialization skips
renderer metadata, event listeners, forced properties, and child overrides; normalizes class and
style; renders boolean attributes by presence; preserves SVG and custom-element casing; and **drops**
an unsafe dynamic attribute name rather than attempting to escape it. `innerHTML` is the explicit
raw-HTML path; `textContent` and a textarea's `value` are escaped and suppress child serialization.

`[SSR-7]` `SsrContext` carries per-render teleport output and a free-form state handoff bag. Enabled
teleport children belong to another target and are **buffered** until the render resolves.

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
| Template | the rendered subtree, with no wrapper |
| Enabled teleport | `<!--teleport start--><!--teleport end-->` |
| Disabled teleport | `<!--teleport start-->children<!--teleport end-->` |

`[SSR-MARKERS-2]` An enabled teleport's **target buffer** receives its children followed by
`<!--teleport anchor-->`. A disabled teleport renders children in place and contributes only the
target anchor. A missing or non-string target emits the origin anchors and skips target content.

### 11.3 Hydration

`[HYD-1]` Hydration is performed by Core's **generic** `Renderer<TNode>.Hydrate`, which walks the
markers of [SSR-MARKERS-1] through a host-supplied `HydrationNodeReader<TNode>`.

`[HYD-2]` **Hydration is a client-host responsibility.** Browser supplies a reader over one batched
host-tree snapshot per root or teleport target, so every structural, kind, text, and attribute read
after that stays in managed memory. Testing supplies a live-tree reader and an immutable-snapshot
reader. ServerRenderer itself stays free of host-node types.

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

### 11.4 The hosting boundary

`[SSR-8]` **No `Assimalign.Viu.*` library may reference a web framework.** Hosting is a downstream
adapter over a host-agnostic contract. ServerRenderer references Shared, Components, and Core, and
has no DOM, Browser, WebView2, or JavaScript-interop dependency.

`[SSR-9]` A server host SHOULD create **one server-render application per request** when services or
state are request-scoped. The supplied factory, service provider, and state registry are borrowed
and are never disposed by ServerRenderer [CMP-9], [APP-6].

*Authority: `libraries/Assimalign.Viu.ServerRenderer/docs/{OVERVIEW,DESIGN}.md`;
`libraries/Assimalign.Viu.Core/src/Rendering/{Renderer.Hydration.cs,HydrationNodeReader{TNode}.cs,HydrationNodeKind.cs}`;
`libraries/Assimalign.Viu.Browser/docs/DESIGN.md` §Hydration;
`libraries/Assimalign.Viu.Testing/docs/OVERVIEW.md`.*

---

## 12. Routing

`[RTR-1]` The router **core is host-free**. `RouteMatcher` / `IRouteMatcher`, `RouteRecord`,
`RouteLocation`, `RouteParameters`, `PathMatchingOptions`, and the ranked path parser run in a plain
.NET test host using no other Viu library.

`[RTR-2]` `RouteLocation` has **value equality**, so a navigation layer can compare and snapshot
cheaply. `RouteParameters` accessors are **boxing-free and reflection-free**
(`GetString`/`TryGetString`, `GetInteger`/`TryGetInteger`, `GetStrings`), with immutable
`With`/`WithMany` builders.

`[RTR-3]` Three histories ship behind the `RouterHistory` factory: **memory** (pure; no
initialization), **web** (HTML5 History API), and **hash**. Web and hash lazily initialize their
browser-history bridge when `Router.ReadyAsync` first needs it; `RouterHistory.InitializeAsync`
remains an optional prewarming call. `UseRouter` awaits readiness with
`IApplicationContext.Stopping` before the host terminal mounts and removes the DOM bridge during
reverse-order application cleanup [APP-4], [APP-5]. History state marshals as a **flat,
primitives-only** payload.

`[RTR-4]` `RouterView` and `RouterLink` resolve `Router` from `IComponentContext.Services`.
**`RouterView` takes its nesting depth as an explicit argument** (default `0`), and a nested layout
passes the next depth explicitly, because Viu has no hierarchical component dependency API [CMP-24].

`[RTR-5]` **Guards return their decision; they do not call a continuation.** A `NavigationGuard`
returns a `NavigationGuardResult` — `Allow`, `Abort`, or a redirect — from an awaitable,
cancellable signature. An exhaustive result type lets the compiler check that every path decides,
and lets the pipeline guarantee a guard decides exactly once.

`[RTR-6]` A navigation that does not complete yields a `NavigationFailure` typed `Aborted`,
`Cancelled`, or `Duplicated`, returned from `Push`/`Replace` and passed to every after-navigation
hook. A guard-redirect chain that exceeds the safety cap throws `NavigationRedirectException`.

`[RTR-7]` **Boundary.** `Assimalign.Viu.Router` references Components and Reactivity but **not Core
and not Browser** — a boundary the test suite asserts. `Assimalign.Viu.Router.Browser` is the
click-dispatch bridge, and the browser history edge is gated by `[SupportedOSPlatform("browser")]`.

`[RTR-8]` **Limit.** Lazy route components and scroll behavior are not implemented
([V01.01.08.05]); every route component resolves eagerly. See [§17](#17-non-goals-and-current-limits).

*Authority: `libraries/Assimalign.Viu.Router/docs/{OVERVIEW,DESIGN}.md`;
`libraries/Assimalign.Viu.Router.Browser/docs/{OVERVIEW,DESIGN}.md`.*

---

## 13. State

`[STA-1]` `StateStoreDefinition<TStore>` is an **explicit, AOT-safe setup delegate**. A definition is
reusable metadata identified by `Key`; mutable state is always registry-owned.

`[STA-2]` `StateStoreRegistry` creates **one detached root reactive scope** at construction and one
**child scope plus instance per definition key**. Resolving the same definition in one registry is a
cache hit by reference identity; different registries produce isolated instances. Disposing the
registry stops the root scope, which cascades through every child scope. A setup failure stops the
newly created child scope and adds no partial entry. Resolving a different definition under an owned
key raises `DuplicateStateStoreKeyException`.

`[STA-3]` The caller's ambient component scope is **never** the store scope's parent. Store lifetime
is registry lifetime, not mount lifetime.

`[STA-4]` Core's mounted context implements the **State-owned** `IStateStoreContext` capability, so
`definition.Use(componentContext)` locates the application registry **without making Components
depend on State**. The application-global path deliberately records **no owner**; otherwise the
first component to resolve a global store would become its owner and setup behavior would depend on
mount order. A caller creating an isolated feature registry MAY pass an owner explicitly.

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

*Authority: `libraries/Assimalign.Viu.State/{src,docs/{OVERVIEW,DESIGN}.md}`;
`docs/ARCHITECTURE.md` §State.*

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
narrow nearest-owning-project check for the Viu SDK or an explicit enablement marker, stops at the
first directory containing a project so an unrelated nested project is not claimed, treats a literal
`false` marker as an override, and **fails closed** when ownership is ambiguous. The check repeats
for document changes, diagnostics, completion, and hover.

*Authority: `extensions/VisualStudio/Assimalign.Viu.VisualStudio/docs/DESIGN.md`;
`tooling/Assimalign.Viu.Compiler.SingleFileComponent/docs/DESIGN.md`.*

---

## 15. Packaging and the consumer surface

`[PKG-1]` A consumer project is `<Project Sdk="Assimalign.Viu.Sdk">`. The SDK chains
`Microsoft.NET.Sdk.WebAssembly` and is resolved by NuGet's built-in MSBuild SDK resolver — no
installer, no admin rights.

`[PKG-2]` The framework ships as the **`Assimalign.Viu.App` shared framework**: a
`KnownFrameworkReference` registration resolving to the `Assimalign.Viu.App.Ref` targeting pack
(compile references + `data/FrameworkList.xml`) and per-RID `Assimalign.Viu.App.Runtime.<rid>`
runtime packs (`browser-wasm` today).

`[PKG-3]` **Generators are delivered as analyzers through the ref pack** — `analyzers/dotnet/cs`
with `<File Type="Analyzer">` manifest entries — so an SDK consumer gets `[Reactive]` and
`.viu`/`.vue` compilation with **zero wiring**.

`[PKG-4]` MSBuild tasks perform the physical writes a generator legally cannot [EXE-10]:
`ViuBundleCss` writes the component stylesheet, `ViuBundleUtilityCss` writes the utility stylesheet,
and a link-injection task splices the `<link>` into the host page **before** the SDK's compression
pipeline so content negotiation stays intact. Both links can be opted out of.

`[PKG-5]` In-repo projects **dogfood via `ViuProjectReference`**; the SDK is the *external consumer*
surface. The in-repo build deliberately does not consume the SDK, so the framework can be developed
without a pack/restore cycle in the loop.

*Authority: `sdks/README.md`; `sdks/Assimalign.Viu.Sdk/`; `frameworks/`;
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
| `scripts/Measure-PublishBudget.ps1` + `scripts/budgets/PublishBudgets.json` | WASM publish size and startup budgets |
| `benchmarks/baselines/InteropCounts.json` | Interop-call counts; a delta fails the gate [RND-IO-5] |
| `.github/workflows/area-*.yml`, `budget-gates.yml`, `benchmarks.yml` | Per-area CI |

`[CONF-3]` Unit tests are **DOM-free by default**. The runtime is exercised through
`Assimalign.Viu.Testing`'s in-memory host; real-browser coverage is a separate end-to-end harness.

`[CONF-4]` For reactivity and caching semantics a test MUST assert **run counts** (effect runs,
getter invocations), not only final values: caching and dependency-tracking bugs hide behind
correct-looking values.

`[CONF-5]` A build MUST produce **0 warnings, 0 errors** (`.claude/rules/checklist.md`).

---

## 17. Non-goals and current limits

### 17.1 Non-goals — decisions, not deferrals

- **Not a port.** Viu makes no parity guarantee with any external project, has no "upstream wins"
  rule, and tracks no external project's version.
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

`[PERF-4]` Semantic, API, and behavioral parity with an external project is **out of scope** for that
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
| **component tree** | The immutable `IComponent` hierarchy a render produces |
| **template** | An `IComponentTemplate` — authored component behavior, one instance per mount |
| **template request** | An `ITemplateComponent` — a non-activating description of a template to mount |
| **mounted node** | An internal `MountedRenderNode<TNode>` — the runtime bookkeeping for one tree value |
| **block** | A tree value whose `ComponentOptimization.DynamicChildren` is non-null [RND-BLOCK-2] |
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
