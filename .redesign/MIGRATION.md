# Provisional migration map

This is a blast-radius inventory, not an authorized refactor plan.

| Current location | Proposed owner |
| --- | --- |
| Core `Reactive/`, `References/`, reactive collections, effects/scopes, watch primitives | Reactivity |
| Historical `IReference` / `IReference<T>` contracts | Reactivity `IReactiveReference` / `IReactiveReference<T>` |
| Current `ReactiveValue` / `ReactiveValue<T>` engine base | Reactivity; retained behind the interfaces |
| Other Reactivity interfaces | Renamed consistently to the `IReactive*` family |
| Core `IComponent`, component metadata, arguments, events, slots, directives | Components |
| Core `VirtualDom/VirtualNode*` public vocabulary | Components `IComponent` tree vocabulary |
| Core `PatchFlags`/dynamic properties/dynamic children public contract | Components `ComponentOptimization` |
| Core renderer, scheduler, hydration, block tracker, renderer-owned node state | Core |
| Core application/builder/context | Core |
| Core custom service container, registrations, lifetimes, provider extensions | Removed |
| Core `DependencyInjection.GetService*` | Removed; application supplies `IServiceProvider` separately |
| Vue-semantic component-tree provide/inject | Components or Core integration; not yet decided |
| `Assimalign.Viu.Store` | Replaced by `Assimalign.Viu.State`; migrate its behavior and consumers |
| `Assimalign.Viu.Core.Generators` reactive-object output | Reactivity generator |
| Syntax generator component bridge | Components contracts |

## Required implementation work after sign-off

1. Create WBS work items and sequence the dependency moves before any rename sweep.
2. Split source generators and update emitted fully-qualified names and snapshots.
3. Restore the Reactivity boundary from the pre-consolidation shape at `470142e`, move the current
   improved engine into it, and keep its run-count and benchmark baselines pinned.
4. Introduce Components and migrate public component/tree contracts.
5. Add Core's internal lowering layer before changing renderer dispatch.
6. Replace application service-container construction with independently supplied component and
   service resolvers.
7. Replace Store with State and define whether one transitional compatibility package is required
   for the package-id and namespace migration.
8. Cascade references through Browser, ServerRenderer, Testing, Router, Store/State, examples,
   framework manifests, SDK packaging, solution files, and CI.
9. Update ADR-0004 and add an ADR for the public vnode/component vocabulary divergence from Vue.
10. Run the full solution, all affected tests, the WASM sample, trimmed publish, reactive benchmark,
    and interop-count gates.
