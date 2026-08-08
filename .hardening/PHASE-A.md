# Item 3 Phase A — post-component-model reevaluation

Date: 2026-08-08

The full stop is lifted. `[V01.01.15]` landed, `Assimalign.Viu.Shared` and `.redesign` are gone, and
the hardening arc now describes only the public surface that exists. D10 records the rule used for
the reevaluation: execute work whose public shape is already determined; do not invent an API to
make a row look complete.

## Dispositions

| Task | Disposition | Evidence / surviving scope |
|---|---|---|
| T02 | Still relevant as final gate; implementation already present | Every packable public project has PublicAPI files and centrally enabled analyzers. Regenerate after all surface movement and prove RS0016/RS0017/RS0037 clean. |
| T05 | Already satisfied/superseded by D9 | All six former Core decisions are explicitly mapped below; no work remains. |
| T07 | Still relevant, blocked-needs-decision | Deleted D9 targets are gone; remaining prefixes, duplicate facades, constructors, and stuttering factories need an approved replacement vocabulary. |
| T08 | Still relevant, reduced | Whole-word/Boolean clarity violations remain in flags, reactivity, browser diagnostics, state, router matching, syntax/compiler, and server renderer. |
| T09 | Still relevant, split | Testing collections and router guard depth have determined fixes; raw batching needs a public API decision. Former render-frame/context issues were resolved by D9/D5. |
| T10 | Still relevant, blocked-needs-decision | Typed patch metadata and slot stability landed; the remaining `RendererOptions<TNode>` delegate-bag replacement has multiple valid public shapes. |
| T11 | Still relevant | Router navigation, Scheduler, router history, ReactiveEffect, and async Testing wrapper names still violate async/cancellation/lifetime conventions. |
| T12 | Still relevant, reduced | Only surviving parser bases and explicit CSS hierarchy handling remain; old component/application hierarchies are gone. |
| T14 | Still relevant, split | Dead raw-object identity APIs have a determined deletion. Router history/click/result/TestRenderer redesigns need replacement contracts. Renderer creation is an owning lease and stays. |
| T17 | Still relevant | Debugger displays are absent and reactive values still need a fresh non-subscribing `Peek()`. |
| G1 | Still relevant | Components, Reactivity, State, Core, and Browser overlap standalone packages and the framework, but the Ref targeting pack lacks `data/PackageOverrides.txt`. The runtime pack must not carry this targeting-pack file. |
| G3 | Still relevant | `SlotFlags` is a plain three-value classification; rename to `SlotStability` and preserve values. |
| G4 | Still relevant, reduced | `RouteLocation` and `RouteParameters` remain app-visible `IEquatable<T>` values without `==`/`!=`. |
| G5 | Still relevant | Router exposes invariant mutable reference shape for read-only current-route state; add a covariant get-only contract. |
| D6-A | Still relevant, blocked-needs-decision | SDK still imports WebAssembly. Base/browser import and payload ownership have not been selected. |
| D6-B | Still relevant, blocked-needs-decision | Framework still contains Browser and a browser-wasm runtime. Base/browser targeting/runtime/reference topology has not been selected. |

## T05 six-decision disposition

1. `EmptyComponentFactory` was deleted. `ApplicationOptions.Components` uses an empty
   `ComponentFactory`, whose unresolved lookup throws.
2. DI is explicitly optional through nullable `ApplicationOptions.Services`,
   `IApplicationContext.Services`, and `ComponentContext.Services`; the empty provider is gone.
3. Public `ApplicationState` shipped and is exposed through `ApplicationLifetime.State` (`[APP-1]`).
4. The old mounted-context proposal is moot. `ComponentContext.Parent` shipped; scoped-style identity
   remains deliberately deferred.
5. `MountedComponent<TNode>` stays internal. `ComponentHost.RenderAsync` plus
   `IComponentRenderScope` is the public SSR operation/ownership seam.
6. `MountedTemplateNode<TNode>` was deleted. `MountedComponentView<TNode>` and
   `Renderer<TNode>.GetMountedComponentViews()` supply the stable diagnostic seam.

## Refuted/superseded findings

- `RenderHelpers._withHandler` and underscore-helper protections are superseded by D9: the types and
  static ABI no longer exist; frame-based generated code carries mount-local state.
- KeepAlive remains protected by `[BLT-6]`: weak string/enumerable filters and integer/string maximum
  decoding still exist. Suspense remains structural and lazy under `[BLT-11]`.
- `RouterLinkClickEvent` remains public and used across Router, Browser.Router, and Testing under
  `[RTR-1]`/`[RTR-7]`; it is not dead surface.

## Blocking questions

- T07: what coherent replacement vocabulary/factory boundary should replace the surviving prefixed,
  duplicate, and stuttering APIs?
- T09-C: raw batch pair, exception-safe disposable scope, or callback batching?
- T10: named delegates, host-operations interface, or deliberate delegate bag?
- T14-B: what value/options types replace the router/history/click/result Boolean and opaque shapes,
  and should TestRenderer ordering change or become options?
- D6-A: thin dependent Browser SDK or self-contained Browser SDK, and which payload belongs where?
- D6-B: targeting-only or runtime base framework, Browser reference/re-export behavior, and the
  ServerRenderer delivery path?

Phase B will execute T08, G3, T09-A/B, T11, G4, G5, T12, T14-A, T17, G1, and T02 in that order by
work class. All blocked rows remain explicit rather than receiving guessed API designs.
