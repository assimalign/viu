# Adopted type map

| Earlier responsibility | Adopted concept | Owner |
|---|---|---|
| Immutable render value | `VirtualNode` and sealed variants | Components |
| Authored behavior | `IComponent.Setup(ComponentContext)` | Components |
| Raw parent inputs | `ComponentInvocation` on `ComponentNode` | Components |
| Resolved mounted inputs | `ComponentBindings` on `ComponentContext` | Components |
| AOT-safe activation | `ComponentRegistration` and `IComponentFactory` | Components |
| Per-mount render cache/block assembly | `ComponentRenderFrame` | Components |
| Mounted execution and inspection | internal engine plus `MountedComponentView<TNode>` | Core |
| Host mutation contract | `RendererOptions<TNode>` | Core and each host |
| Server component execution | `ComponentHost.RenderAsync` lease | Core |
| Route target | `VirtualNode` with optional component arguments | Router |
| Nested route outlet | `RouterView` registration with explicit depth | Router |
| Navigation anchor | `RouterLink` registration and `ElementNode` | Router |
| Browser click integration | `UseRouter` application middleware | Browser.Router |
| Shared miscellaneous values | domain-owned flags, names, styles, and host data | owning libraries |

The staged compiler fixture calls the public `ComponentRegistration.Define` and generated
registration entry point in one application. It is an integration canary, not a second compiler
implementation.
