# Current-to-target type map

Targets follow the adopted disposition in
[`../../docs/COMPONENT-MODEL-PLAN.md`](../../docs/COMPONENT-MODEL-PLAN.md) §2/§2a.

| Current concept | Target concept | Owner | Reason |
|---|---|---|---|
| `IComponent` tree description | `VirtualNode` | Components | Reserves "component" for authored behavior and closes the render vocabulary. |
| Per-kind component interfaces | Sealed `VirtualNode` variants | Components | Makes kind/shape mismatches unrepresentable. |
| `ITemplateComponent` | `ComponentNode` | Components | Names the value as an immutable invocation in the tree. |
| Template request arguments and slots | `ComponentInvocation` | Components | Explicitly identifies raw parent-created inputs. |
| `IComponentTemplate` | `IComponent` | Components | The authored contract stays in the component model, one instance per mount. |
| Template metadata properties | `ComponentContract` + `ComponentRegistration` | Components | Separates static declaration from the live instance; the runtime reads the contract before activation. |
| `IComponentContext` | public abstract `ComponentContext` + internal `RuntimeComponentContext` | Components / Core | The authoring surface is model vocabulary; the single implementation is engine-internal. |
| Context arguments/slots/attributes | `ComponentBindings` + pure static `Resolve` | Components | Names resolved parameters, slots, and fallthrough; the transformation is unit-testable without a runtime. |
| Component registry / definition resolver | `IComponentFactory` / `ComponentFactory` | Components | Registration-backed resolution is model vocabulary; no runtime constructor discovery. |
| Public hot-reload metadata interface on generated types | `ComponentDevelopmentMetadata` registration ABI | Core | Makes the public seam an explicit generated-code ABI. |
| Static render-helper class, `BlockToken`, and underscore name-binding | `ComponentRenderFrame` parameter on `ComponentRenderer` | Components | The per-mount frame owns the render cache and block assembly; no ambient state and no public static helper class remain — the only name-bound generated-code ABIs left are hot reload (shipping `ComponentHotReload`) and Browser's directive vocabulary. |
| State-to-Components bridge interface and its context cast | `StateStoreDefinition<TStore>.Use(ComponentContext)` via `Services` + ambient registry | State | Conventions attach through the context's seams and never earn a member or a cast. |
| Core template built-ins | Structural control nodes carrying `ComponentInvocation` + internal Core executors | Components / Core | Separates declarative structure (with lazy slots) from mounted algorithms. |
| Transition forwarding wrappers | `TransitionNode` | Components | Avoids copying every virtual-node interface member. |
| ServerRenderer friend access | `ComponentHost.RenderAsync` → `IComponentRenderScope` | Core | Exposes the complete one-shot operation rather than mounted machinery. |
| Testing friend access | `MountedComponentView<TNode>` | Core | Publishes exact cold-path inspection data with stable per-mount view identity. |
| Compiler style-scope identity | — deferred | — | Scoped CSS is deferred; no style-scope state exists anywhere in the model. |
| `Assimalign.Viu.Shared` | Domain-owned types or deletion | — | Removes the miscellaneous ownership bucket; flag enums and name normalization land in Components. |
| Tooling friend contracts | Public projection request/result facade | tooling | Shares stable operations while keeping compiler intermediates internal. |
