# Assimalign.Viu.Core

Core is **the Application Model**: the engine that executes the Components-owned component model
plus the public operations hosts consume. Its public contracts are:

- `ComponentHost.RenderAsync(ComponentRenderRequest)` → `IComponentRenderScope { Tree; Context }`:
  one-shot activate, setup-in-scope, prefetch, render-once, and abort-on-dispose. The host
  constructs one `ComponentRenderFrame` per mount, invokes the renderer with it, and keeps the
  frame on the internal lease so repeated renders reuse the mount's frame — there is no ambient
  render-helper state. Aborting cancels the component-lifetime token before stopping its reactive
  scope, disposes the authored instance, drains observed lifecycle tasks, and then releases the
  lifecycle;
- `ComponentRuntimeOptions.ErrorHandler`: the terminal sink after ancestor `OnErrorCaptured`
  callbacks for lifecycle, watcher, and event faults; with no configured sink, an unhandled fault
  keeps its exception;
- `MountedComponentView<TNode>`: the cold-path testing/diagnostics view (`Request`, `Instance`,
  `Context`, `FirstHostNode`, `LastHostNode`, `IsMounted`) with stable per-mount identity;
- `IVirtualNodeHost<TNode>`: genuine host operation variation;
- `ComponentCompilerServices` + `ComponentDevelopmentMetadata`: the hidden hot-reload
  registration ABI for generated code.

`RuntimeComponentContext` — the single implementation of Components' abstract `ComponentContext` —
is internal and sealed, as are the render lease and the mounted engine types the full
implementation would add (`MountedComponent`, mounted node variants, built-in executors, the
persistent `Renderer<TNode>`). No host is a compile-time friend of Core.
