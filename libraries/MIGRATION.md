# Redesign migration map

The new boundaries are implemented under `.redesign`. Nothing in `libraries/` has been changed;
promotion into the shipping tree requires separate user approval.

| Previous owner or vocabulary | Redesign owner or replacement |
| --- | --- |
| Core reactive references, effects, scopes, watches, and reactive collections | `Assimalign.Viu.Reactivity` |
| Historical `IReference` / `IReference<T>` | `IReactiveReference` / `IReactiveReference<T>` |
| `ReactiveValue` / `ReactiveValue<T>` engine base | Retained in Reactivity behind the public interfaces |
| Core component metadata, arguments, events, slots, and directives | `Assimalign.Viu.Components` |
| Public `VirtualNode` / virtual-DOM vocabulary | Unified `IComponent` tree |
| Patch flags, dynamic properties, and dynamic children on virtual nodes | `ComponentOptimization` on `IComponent` values |
| Core renderer, scheduler, hydration, application, and mounted-node state | `Assimalign.Viu.Core` |
| Browser DOM, events, DOM directives, and transitions | `Assimalign.Viu.Browser` |
| Core custom service container, registrations, lifetimes, and provider extensions | Removed |
| Component factory as service provider | Removed; `IComponentFactory` and `IServiceProvider` are separate |
| Component-tree `provide`/`inject` | Not ported |
| `Assimalign.Viu.Store` | Replaced by `Assimalign.Viu.State` |
| Reactive-object generator output owned by Core | `Assimalign.Viu.Reactivity` runtime contracts; analyzer assembly rename is an atomic promotion step |
| Single-file component bridge targeting the old Core component surface | Components contracts plus Core render helpers |

The requested consolidation commit `80bb967` was reviewed together with its ancestry. The actual
standalone Reactivity baseline is represented by commit `470142e`; the redesign restores that
package boundary while retaining the newer engine behavior and tests.

## Staging graph

All redesign project references resolve inside the staging graph. It includes the four primary
packages plus Browser, ServerRenderer, Testing, Router, Router.Browser, Shared, Syntax, template
compilation, single-file component generation, and CSS tooling.

The staging application model is host-generic:

- `IApplication` carries platform-neutral configuration and lifecycle;
- `IApplication<TNode>` carries the host-specific mount target;
- `Application<TNode>` provides shared mounting and plugin behavior;
- `BrowserApplication : Application<int>` is the current DOM host; and
- a WebView2 application can select its own node-handle type without changing Core abstractions.

## Promotion sequence after approval

1. Freeze the `.redesign` build and test baseline, including Browser compiled-render tests and
   server-render-to-hydrate coverage.
2. Move the staged projects into the corresponding `libraries/Assimalign.Viu.*` locations without
   changing their public contracts.
3. Atomically rename `Assimalign.Viu.Core.Generators` back to
   `Assimalign.Viu.Reactivity.Generators`, retarget every shipping/analyzer/framework reference,
   and update generated snapshots. Do not publish the staged Reactivity package with the
   Core-named analyzer embedded in it.
4. Replace Store package references with State and decide whether a package-id compatibility shim
   is required for external consumers.
5. Update framework manifests, SDK packaging, examples, solution files, CI entries, and
   consumer-facing documentation.
6. Remove obsolete custom dependency-injection and virtual-node sources only after every shipping
   project resolves against the new graph.
7. Run the full solution, affected unit tests, browser sample, trimmed publish, reactive benchmark,
   and interop-count gates.

The promotion must preserve these decisions:

- no component-tree `provide`/`inject`;
- no relationship required between `IComponentFactory` and `IServiceProvider`;
- no reflection activation or dynamic code generation;
- application ownership of factories, service providers, and state registries; and
- host-specific mounting only through `IApplication<TNode>` / `Application<TNode>`.

## Known migration constraint

Suspense mount and update behavior is present. Server rendering emits only the resolved default
branch, and client hydration of a Suspense request fails explicitly. Applications that hydrate
server output must keep Suspense out of the hydrated root until pending-branch hydration
coordination is implemented. Boundary timeout/events, fallback-to-reveal transition choreography,
and hidden-branch post-effect delay also remain outside the current Vue-parity baseline.
