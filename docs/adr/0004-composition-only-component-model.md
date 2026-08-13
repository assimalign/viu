# ADR-0004: Composition-only component model (no Options API, mixins, or global properties)

- **Status:** Accepted (decision stands; framing annotated and stated mechanism outdated as of
  2026-08-02 — see the note below)
- **Date:** 2026-07-19 (foundational C#/WASM premise; formally recorded under [V01.01.13.01], #98)
- **Scope:** `Assimalign.Viu.RuntimeCore` — the component instance, setup model, app API, and
  provide/inject.

> **Superseded framing (2026-08-02).** On 2026-08-02 the user directed that **Viu is a standalone
> framework**; external projects are not normative authorities for Viu's semantics, and
> [`docs/SPECIFICATION.md`](../SPECIFICATION.md) is now the authority — the composition-only
> component model is carried forward in §4's component clauses and §17.1. **The core
> decision recorded here is unaffected**: Viu ships a composition-only component model with no
> options-style authoring, no mixins, and no global-properties bag.
>
> **The stated replacement mechanism, however, no longer exists.** The Decision below routes
> cross-cutting values through "typed provide/inject (`InjectionKey<T>`, app-level `Provide<T>`)" and
> plugins (`IPlugin`), and describes an `ApplicationConfiguration` carrying the error handler, warn
> handler, and performance flag. As of this working tree: `InjectionKey`, `ApplicationConfiguration`,
> `IPlugin`, and its interim `IApplicationPlugin` successor do not exist. Composition uses builder
> `ConfigureApplication(ApplicationOptions)` through the lean `IApplicationBuilder`;
> `ApplicationMiddleware` receives `IApplicationContext` and surrounds the live application
> lifetime; all composition and diagnostics are frozen from those options into the context.
> `IApplicationContext` also exposes read-only `IsRunning` and `Stopping` runtime state, while
> `IApplication` exposes `StartAsync` and `StopAsync` and gains long-running `RunAsync` through an
> extension. Browser implements that contract directly and owns the lower-level mount methods; the
> interim generic and abstract application types and Browser's static facade were removed by
> [V01.01.14.08]. **Component-tree provide/inject was deliberately excluded** — it is a
> recorded decision, not a deferral. The adopted seam is recorded in
> [`COMPONENT-MODEL-PLAN.md`](../COMPONENT-MODEL-PLAN.md) §2a, and the specification states it as
> `[CMP-24]` and `[CMP-33]`: dependencies are explicit through parameters and slots,
> `ComponentContext.Services` plus the ambient reactive scope, and application composition. Reversing the
> replacement mechanism recorded in an accepted ADR requires a **superseding ADR**, not an edit to
> this one; until that ADR is written, `[CMP-24]` is the authority and this paragraph is the notice.
>
> A factual note for a future reader, recorded rather than edited: the scope line names
> `Assimalign.Viu.RuntimeCore`, renamed `Assimalign.Viu.Core` ([V01.01.12.21]). The body is preserved
> as the historical record and is not rewritten (see [README.md](README.md), "Append-only").

## Context

An options-style component model merges `data`, methods, and computed values onto a `this` context.
Mixins share option fragments, while a global-properties bag injects ambient members onto every
component instance. A composition model instead uses a setup closure and explicit typed values.

The Options API and mixins depend on runtime option resolution and `this`-based merging; global
properties inject untyped ambient members. All three fight static typing and add reflection-shaped
runtime machinery — a poor fit for an AOT/trimming target and for C#'s type system.

## Decision

**Viu ships a composition-only component model.**

- No Options API and no mixins: component logic is expressed in a setup function returning reactive
  state (refs, computeds) and handlers.
- No `app.config.globalProperties`: cross-cutting values are supplied through **typed
  provide/inject** (`InjectionKey<T>`, app-level `Provide<T>`) and plugins (`IPlugin`).
- The application lifetime and its configuration deliberately exclude a global-properties bag;
  configuration carries only typed composition and diagnostics. The current options/context API
  preserves that exclusion without a host-generic application base.

## Consequences

- Composition functions and typed provide/inject replace every Options-API and mixin use case;
  shared behavior is a plain function, and shared state is an injected, typed value.
- Cross-cutting registration (components, directives, app-level provides) flows through the app API
  and plugins, all typed.
- Easier: static typing end to end, trimming safety, and no `this`-merge order ambiguity. Harder:
  there is no drop-in path for authors migrating options-style components — that is
  the accepted cost of the divergence.

## Alternatives considered

- **Support the Options API and mixins** — rejected: runtime option merging is untyped, reflection-
  shaped, and reintroduces `this`-merge ambiguity.
- **Provide `globalProperties`** — rejected: untyped ambient state that the trimmer cannot reason
  about; typed provide/inject covers the same need with compile-time safety.

## References

- [`docs/PLAN.md`](../PLAN.md) — founding decision 5.
- [`Assimalign.Viu.Core/docs/OVERVIEW.md`](../../libraries/Runtime/Assimalign.Viu.Core/docs/OVERVIEW.md)
  (formerly `Assimalign.Viu.RuntimeCore`, renamed in [V01.01.12.21]) and the current application
  options/context surface.
- [`COMPONENT-MODEL-PLAN.md`](../COMPONENT-MODEL-PLAN.md) — adopted component-model seams and
  migration disposition.
