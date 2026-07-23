# KeepAlive

`KeepAlive` preserves explicitly registered template subtrees while a dynamic view switches between
them. The cache is owned by one mounted `KeepAlive` instance and the renderer remains host-generic:
inactive host nodes move into a renderer-owned storage container, then move back when reactivated.

```csharp
Reference<string> selected = Reactive.Reference("editor");

ITemplateComponent root = KeepAlive.CreateComponent(
    include: new[] { "Editor", "Preview" },
    exclude: null,
    maximum: 2,
    child: _ => selected.Value == "editor"
        ? ComponentTree.Template<Editor>()
        : ComponentTree.Template<Preview>());

IComponentFactory components = new ComponentFactory(
[
    new ComponentRegistration(
        typeof(Editor),
        static () => new Editor()),
    new ComponentRegistration(
        typeof(Preview),
        static () => new Preview()),
]);
```

Core activates `KeepAlive`, `Suspense`, and `BaseTransition` as reserved framework built-ins, so
applications do not register those templates in their component factory. Only `Editor` and
`Preview` above are application registrations.

The current child is identified by its explicit component key when present, otherwise by its
registered type or registered name. Returning to the same identity reuses the mounted template,
reactive scope, and rendered subtree; its factory activator and `Setup` method do not run again.

## Lifecycle

Templates opt into cache lifecycle through their ordinary component context:

```csharp
public ComponentRenderer Setup(IComponentContext context)
{
    context.Lifecycle.OnActivated(RefreshVisibleDataAsync);
    context.Lifecycle.OnDeactivated(PausePolling);
    context.Lifecycle.OnUnmounted(ReleaseResources);

    return Render;
}
```

`OnActivated` runs after the initial cached mount and after each later reactivation.
`OnDeactivated` runs whenever the active cached subtree moves into storage. Descendant callbacks run
before ancestor callbacks. Cache eviction and wrapper teardown perform a full unmount, including the
normal before-unmount and unmounted callbacks.

Asynchronous activated and deactivated callbacks follow the same observed-task rules as other
lifecycle callbacks: rendering does not await them, failures flow through component error handling,
and the component-lifetime cancellation token remains valid while the template is cached.

## Filtering and eviction

`include` and `exclude` accept:

- a comma-separated component-name string;
- an `IEnumerable<string>`; or
- a `Func<string, bool>` predicate.

String segments use exact ordinal matching and are not trimmed, matching Vue's comma-separated
pattern behavior. An unnamed component cannot satisfy `include`. A matching `exclude` always wins.
Changing either argument prunes inactive entries that no longer match; an active entry remains
mounted but is no longer cached.

A positive `maximum` enables least-recently-used eviction. Zero, a negative value, a missing value,
or an unparseable string means unbounded. Eviction fully unmounts the least-recently-used cached
subtree. The cache and its detached storage container are private to the host-neutral renderer and
do not appear in the application component tree.

The behavior follows Vue 3.5's
[`KeepAlive.ts`](https://github.com/vuejs/core/blob/v3.5.29/packages/runtime-core/src/components/KeepAlive.ts)
while retaining Viu's explicit, AOT-safe component activation boundary.
