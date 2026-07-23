# Asynchronous and dynamic components

Core implements Vue-shaped dynamic selection and asynchronous loading without weakening the
approved component-factory boundary. Neither feature performs constructor discovery, reflection,
service lookup, or component-tree provide/inject.

## Factory identity remains authoritative

An asynchronous definition is an explicit registration/request facade:

```csharp
private sealed class UserPanelAsynchronousIdentity
{
}

AsynchronousComponentDefinition userPanel =
    AsynchronousComponents.DefineAsynchronousComponent<UserPanelAsynchronousIdentity>(
        async cancellationToken =>
        {
            await moduleLoader.LoadUserPanelAsync(cancellationToken);
            return AsynchronousComponentTarget.From<UserPanel>();
        });

IComponentFactory components = new ComponentFactory(
[
    userPanel.Registration,
    new ComponentRegistration(
        typeof(UserPanel),
        static () => new UserPanel()),
]);

ITemplateComponent request = userPanel.CreateComponent(
    new ComponentArguments(
    [
        new KeyValuePair<string, object?>("userId", userId),
    ]));
```

The wrapper identity is supplied explicitly through `typeof(...)`. The loader returns the registered
type or name of the resolved template; it never returns an activated template. Mounting the wrapper
and mounting its resolved target therefore both go through the application-owned
`IComponentFactory`. The factory remains independent from `IServiceProvider`; an application may
choose to close either registration activator over its service provider, but Core does not do so.
`ComponentFactory` consumes `userPanel.Registration` directly. A custom factory can map
`Registration.ComponentType` or `Registration.Name` to `Registration.Activator`; it is not required
to inherit from or delegate to the built-in factory.

Each definition owns only load coordination:

- the loader is not invoked until the first mount;
- concurrent mounts share one in-flight task;
- a successful target identity is cached for later mounts;
- each mount still receives a fresh wrapper template and fresh resolved template from the factory;
- loader failures can retry or fail through `OnError`, with one-based attempt counts; and
- when every consumer of an in-flight load unmounts, Core cancels the shared loader token and a
  later mount starts a new request.

Loading and error presentations are callbacks rather than reusable `IComponent` instances. Each
invocation creates a fresh immutable tree value, which avoids mounting one render description at
multiple positions. Delay and timeout use a cancelable timer seam; tests replace it with virtual
time.

The wrapper registers its tracked load as a server-prefetch callback. The host-generic server
renderer therefore waits for a successful or handled failed load before serializing the wrapper's
resolved or error tree. This uses the ordinary component lifecycle contract rather than adding an
HTTP, browser, or server-renderer dependency to Core.

The wrapper forwards the latest raw arguments, slots, listeners, directives, key, and template
reference to the resolved template request. The reference stays unset while loading, error, or
empty presentation content is active, then receives the resolved component's exposed surface (or
component context) and clears when that resolved component unmounts.

## Dynamic selection is explicit

Dynamic selection accepts template types, asynchronous definitions, existing component-tree values,
element tags, and explicit registered names:

```csharp
object selection = showEditor
    ? typeof(EditorComponent)
    : DynamicComponents.Named("read-only-view");

return DynamicComponents.DynamicComponent(
    selection,
    arguments: new ComponentArguments(
    [
        new KeyValuePair<string, object?>("document", document),
    ]));
```

A plain string always means an element tag:

```csharp
DynamicComponents.DynamicComponent("section");
```

This is a deliberate consequence of the approved `IComponentFactory` contract. The factory can
create a registered type or name, but it cannot probe registrations. Core will not activate and
discard a component merely to determine whether a string is registered. Developers and generated
code use `DynamicComponents.Named(...)` when a string is intended as a factory lookup. A change in
the selected type, name, element tag, or key changes render identity, so the renderer fully unmounts
the old subtree before mounting the new one.

## Suspense boundary

`ISuspenseBoundary` is the host-neutral dependency-registration handshake. A suspensible wrapper
registers its shared pending task with the nearest boundary and renders only a placeholder until the
target resolves; it does not show its local loading presentation.

`Suspense.CreateComponent(defaultSlot, fallbackSlot)` creates the built-in request. Core mounts the
default branch into a detached host container while it collects asynchronous dependencies, mounts
the fallback into the live container when any remain pending, and moves the completed content into
place as one coordinated reveal. A nested boundary owns its own dependencies; fallback content
registers with the parent boundary rather than the boundary whose fallback it is.

Unmounting still tears down the hidden pending branch, so asynchronous wrapper leases release and
the last consumer cancels the shared loader token. Within a boundary, one shared task is counted
once while every mounted consumer retains it; removing the last consumer releases the dependency
even when another boundary still shares the loader. Loader failures remain owned by the wrapper
and flow through the normal component error pipeline; the boundary only treats completion,
failure, or cancellation as settlement.

Server rendering awaits descendant asynchronous-component server-prefetch work and serializes only
the resolved default branch; it never emits the fallback. Client hydration of every Suspense request
intentionally fails fast with `NotSupportedException` for now. Coordinating server-resolved output
with a client-side pending branch needs an explicit hydration protocol rather than guessing which
branch owns the existing nodes. Applications must client-render the boundary until that protocol
exists.

Current parity limits are boundary timeout and event options, transition choreography across
fallback and reveal, and deferral of mounted/post-render effects from the detached default branch.
Those effects currently run when the hidden branch mounts, before it becomes visible. Suspense does
not use provide/inject or an application service container.

The behavior follows Vue 3.5's
[`apiAsyncComponent.ts`](https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/apiAsyncComponent.ts)
and [`Suspense.ts`](https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/Suspense.ts),
plus dynamic-component
[`resolveAssets.ts`](https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/helpers/resolveAssets.ts),
subject to the explicit factory and string-selector decisions above.
