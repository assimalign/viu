# ADR-0004: Composition-only component model (no Options API, mixins, or global properties)

- **Status:** Accepted
- **Date:** 2026-07-19 (foundational C#/WASM premise; formally recorded under [V01.01.13.01], #98)
- **Scope:** `Assimalign.Viu.RuntimeCore` — the component instance, setup model, app API, and
  provide/inject.

## Context

Vue 3 supports two authoring styles: the [Options API](https://vuejs.org/guide/typescript/options-api.html)
(`data`/`methods`/`computed` merged onto a `this` context) and the
[Composition API](https://vuejs.org/guide/extras/composition-api-faq.html) (`setup()` returning
reactive state). It also offers [mixins](https://vuejs.org/api/options-composition.html#mixins) for
sharing option fragments and
[`app.config.globalProperties`](https://vuejs.org/api/application.html#app-config-globalproperties)
for injecting ambient members onto every component's `this`.

The Options API and mixins depend on runtime option resolution and `this`-based merging; global
properties inject untyped ambient members. All three fight static typing and add reflection-shaped
runtime machinery — a poor fit for an AOT/trimming target and for C#'s type system.

## Decision

**Viu ships a composition-only component model.**

- No Options API and no mixins: component logic is expressed in a setup function returning reactive
  state (refs, computeds) and handlers.
- No `app.config.globalProperties`: cross-cutting values are supplied through **typed
  provide/inject** (`InjectionKey<T>`, app-level `Provide<T>`) and plugins (`IPlugin<TNode>`).
- `Application<TNode>` (the C# port of `createAppAPI(render)`) and `ApplicationConfiguration`
  deliberately exclude a global-properties bag; `ApplicationConfiguration` carries the error
  handler, warn handler, and performance flag only. This exclusion is called out in
  `Application<TNode>`'s own XML docs, which reference this ADR.

## Consequences

- Composition functions and typed provide/inject replace every Options-API and mixin use case;
  shared behavior is a plain function, and shared state is an injected, typed value.
- Cross-cutting registration (components, directives, app-level provides) flows through the app API
  and plugins, all typed.
- Easier: static typing end to end, trimming safety, and no `this`-merge order ambiguity. Harder:
  there is no drop-in Options API path for Vue authors migrating Options-style components — that is
  the accepted cost of the divergence.

## Alternatives considered

- **Support the Options API and mixins** — rejected: runtime option merging is untyped, reflection-
  shaped, and reintroduces the `this`-merge ambiguity even Vue's own guidance steers larger apps
  away from.
- **Provide `globalProperties`** — rejected: untyped ambient state that the trimmer cannot reason
  about; typed provide/inject covers the same need with compile-time safety.

## References

- [`docs/PLAN.md`](../PLAN.md) — founding decision 5.
- [`Assimalign.Viu.Core/docs/DESIGN.md`](../../libraries/Assimalign.Viu.Core/docs/DESIGN.md)
  (formerly `Assimalign.Viu.RuntimeCore`, renamed in [V01.01.12.21]) and `Application<TNode>` /
  `ApplicationConfiguration`.
- Vue 3: [Composition API FAQ](https://vuejs.org/guide/extras/composition-api-faq.html),
  [provide/inject](https://vuejs.org/guide/components/provide-inject.html).
