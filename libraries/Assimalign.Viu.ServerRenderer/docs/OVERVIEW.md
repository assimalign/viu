# Assimalign.Viu.ServerRenderer — overview

`Assimalign.Viu.ServerRenderer` is Viu's host-neutral counterpart to
[`@vue/server-renderer`](https://github.com/vuejs/core/tree/main/packages/server-renderer). It walks
the same unified `IComponent` tree that client renderers patch and emits HTML on a plain .NET host.
It has no DOM, Browser, WebView2, or JavaScript-interop dependency.

## Public surface

- `ServerRenderer.RenderToStringAsync` renders a configured `ServerApplication` to a string.
- `ServerRenderer.RenderToStreamAsync` writes completed template subtrees to a `TextWriter` and awaits
  the writer's backpressure.
- `ServerApplication` carries an `IApplicationContext` without binding it to a host node type. Its
  `IComponentFactory`, `IServiceProvider`, and optional state registry are independently supplied and
  borrowed; their composition root retains ownership.
- `ServerApplicationBuilder` uses Core's host-neutral `ApplicationBuilder` contract.
- `SsrContext` carries per-render teleport output and a free-form state handoff bag.
- `SsrRenderState` is the push surface shared by the runtime walker and future compiler-produced server
  render functions.
- `ServerRender` contains Vue-compatible escaping, attribute, list, slot, teleport, suspense, and
  component helpers.

Internally, `ComponentTreeSerializer` dispatches the seven `ComponentKind` values.
`ServerComponentRenderer` uses Core's shared `MountedComponent` pipeline to create a fresh
`IComponentTemplate`, establish its live `IComponentContext` and effect scope, await
`OnServerPrefetch`, render its subtree, and release its server-only lifetime.

## Using it

Render a primitive tree without application services or template activation:

```csharp
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

IComponent tree = ComponentTree.Element(
    "h1",
    children: [ComponentTree.Text("Hello")]);

string html = await ServerRenderer.RenderToStringAsync(tree);
```

Render a user template through explicit, AOT-safe activation:

```csharp
using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

IComponentFactory components = new ComponentFactory(
[
    new ComponentRegistration(
        typeof(RootTemplate),
        static () => new RootTemplate()),
]);

IServiceProvider services = applicationServices;

ServerApplication application = ServerApplication
    .CreateBuilder(
        ComponentTree.Template<RootTemplate>(),
        components,
        services)
    .Build();

SsrContext context = new();
string html = await ServerRenderer.RenderToStringAsync(application, context);
await ServerRenderer.RenderToStreamAsync(application, Console.Out, context);
```

`IComponentFactory` is only the application-selected template resolver. It does not implement
`IServiceProvider`. Templates access the independently supplied provider through
`IComponentContext.Services`. ServerRenderer does not implement component-tree provide/inject.

## Client hydration contract

ServerRenderer emits the fragment, comment, and Teleport markers consumed by Core's generic
`Renderer<TNode>.Hydrate` path. Hydration remains a client-host responsibility: Browser supplies a
batched DOM snapshot reader, while Testing supplies live-tree and immutable snapshot readers.
ServerRenderer itself stays free of browser and host-node types.

The cross-package test suite exercises the complete boundary rather than comparing marker strings
alone:

1. ServerRenderer serializes the unified component tree.
2. `TestServerMarkup` parses that HTML as a browser would.
3. redesigned Core hydrates the parsed host tree through either Testing reader.
4. matching fragments, template roots, and Teleport ranges retain server-node identity; later
   reactive template updates patch those adopted nodes.

For enabled Teleports, the surrounding server host must splice `SsrContext.Teleports[target]` into
the target element before client hydration. The target buffer already includes the trailing
`<!--teleport anchor-->` required by Core.

## Boundaries

- ServerRenderer references Shared, Components, and Core. Core supplies the lifecycle/runtime seam;
  Components supplies the public tree and template contracts.
- Activation uses explicit `IComponentFactory` delegates. There is no reflection-based activation,
  runtime code generation, or linker-unfriendly service discovery.
- A server application never mounts a live host tree, so `IsMounted` is false and `RootContext` is null.
- Plugins install asynchronously before the first render that observes them.
- A host should create one application per request when services or state are request scoped.
- The supplied factory, service provider, and state registry are borrowed and are never disposed by
  ServerRenderer.

The marker protocol and lifecycle details are documented in [DESIGN.md](DESIGN.md).
