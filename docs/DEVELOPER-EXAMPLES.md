# Developer consumption examples

These examples use the shipping `[V01.01.15]` APIs from the separated Components, Reactivity,
State, Core, and Browser packages. They are written from the application developer's point of view;
the examples therefore consume public contracts rather than runtime implementation details.

## 1. Mental model

| Developer concern | Main API | Lifetime |
| --- | --- | --- |
| Immutable render description | `VirtualNode` and sealed variants | One render |
| Static identity and contract | `ComponentReference`, `ComponentContract`, `ComponentRegistration` | One registration |
| Authored component behavior | `IComponent` | One mounted invocation |
| Mounted inputs and capabilities | `ComponentContext` | One mounted invocation |
| Registration resolution | `IComponentFactory` | Application-owned |
| Reactive values and subscriptions | `Reactive`, `IReactiveReference<T>`, `IReactiveEffectScope` | Explicit or component-owned |
| Shared state | `StateStoreDefinition<TStore>`, `IStateStoreRegistry` | One registry |
| Top-level host lifetime | `IApplication`, `IApplicationContext` | One single-use host application |

`VirtualNode` is the closed public render-tree vocabulary. Core keeps internal mounted bookkeeping
because an immutable description and a live host node have different lifetimes.

There is no component-tree `provide`/`inject` API. A component receives explicit inputs through
arguments and slots, application services through nullable `ComponentContext.Services`, and
application state through that service seam or the ambient active registry.

## 2. Context inside a `.viu` component

For a `.viu` file with a template, the source generator emits the following bridge conceptually:

```csharp
partial class UserCard : ComponentBase, IComponent
{
    partial void OnSetup();

    ComponentRenderer IComponent.Setup(ComponentContext context)
    {
        Context = context;
        // Generated assignments from context.Bindings.Parameters run here.
        OnSetup();
        return frame =>
        {
            // The same generated assignments run again before every render.
            return RenderGeneratedTemplate(frame);
        };
    }
}
```

The developer does not write that bridge. Code inside `@script { }` is merged into the same partial
class, so it can use `ComponentBase`'s protected `Context` member and implement the generated
`partial void OnSetup()` hook.

`ComponentBase.Context` is nullable because construction precedes setup. Generated setup assigns it
immediately before parameter binding and `OnSetup`; script code uses `Context!` after that boundary
or performs its own guard. Do not use it from a field initializer or constructor, and do not declare
another member named `Context` or `OnSetup`.

The mounted context exposes:

- `Bindings.Parameters` for resolved component parameters;
- `Bindings.Slots` for current parent-provided slots;
- `Bindings.FallthroughBindings` for undeclared fallthrough values;
- nullable `Services` for the independently supplied `IServiceProvider`;
- `Lifecycle` for callbacks and the component-lifetime cancellation token;
- `Scope`, `WatchScheduler`, and `Watch(...)` for component-owned reactive work;
- `Parent` for the runtime-provided parent context;
- `Emit(...)` for declared component events; and
- `Expose(...)` for the value assigned to a parent template reference; and
- `Warn(...)` for the application warning channel.

### 2.1 Component-local state

```text
<template>
    <button type="button" @click="Increment">
        Count: {{ Count }}
    </button>
</template>

@script {
    using Assimalign.Viu.Reactivity;

    public Reference<int> Count { get; } = Reactive.Reference(0);

    private void Increment()
    {
        Count.Value++;
    }

    partial void OnSetup()
    {
        OnMounted(
            () => System.Diagnostics.Debug.WriteLine("Counter mounted."));
    }
}
```

`OnMounted` and its siblings are generator-provided root conveniences; `ComponentBase` itself stores
only `Context`. Each convenience registers exactly what the longer
`Context!.Lifecycle.OnMounted(...)` form registers, into the same list in the same order, so a
component may write either form or mix them
([`[CMP-32]`](SPECIFICATION.md#410-root-level-lifecycle-registration)).

Each mount receives a fresh generated component instance, so `Count` is component-local. The
generated render function reads `Count.Value`; that read subscribes the component render effect,
and assigning `Count.Value` schedules the necessary patch.

### 2.2 Parameters, services, and emitted events

```text
<template>
    <article>
        <h2>{{ Title }}</h2>
        <button type="button" @click="SaveAsync">Save</button>
    </article>
</template>

@script {
    using System;
    using System.Threading.Tasks;

    using Assimalign.Viu.Components;

    [Parameter(IsRequired = true)]
    public string Title { get; set; } = string.Empty;

    [Event]
    partial void Saved(string identifier);

    private async Task SaveAsync()
    {
        ISaveClient client =
            (ISaveClient?)Context!.Services?.GetService(typeof(ISaveClient))
            ?? throw new InvalidOperationException(
                "The application did not supply ISaveClient.");

        string identifier = await client.SaveAsync(
            Context!.Lifecycle.CancellationToken);
        Saved(identifier);
    }
}
```

The parent may spell a declared camel-case parameter in camel case or kebab case. Parameter
defaults and validators run in Core, while undeclared values remain available in
`Context!.Bindings.FallthroughBindings`.

`Context!.Emit` supports zero or more arguments and delivers an immutable ordered argument snapshot
through `ComponentEventListener`. The generated `Saved` method is the strongly typed authoring form.

### 2.3 Asynchronous loading with lifecycle hooks

Setup itself remains synchronous. It creates the render closure and registers asynchronous work
with the lifecycle that owns that work:

```text
<template>
    <section>
        <button type="button" @click="RefreshAsync">Refresh</button>

        <p v-if="IsLoading">Loading...</p>
        <ul v-else>
            <li v-for="item in Items" :key="item.Id">{{ item.Title }}</li>
        </ul>
    </section>
</template>

@script {
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Assimalign.Viu.Reactivity;

    public Reference<IReadOnlyList<TodoItem>> Items { get; } =
        Reactive.Reference<IReadOnlyList<TodoItem>>([]);

    public Reference<bool> IsLoading { get; } =
        Reactive.Reference(false);

    private HttpClient Http =>
        (HttpClient?)Context!.Services?.GetService(typeof(HttpClient))
        ?? throw new InvalidOperationException(
            "The application did not supply HttpClient.");

    partial void OnSetup()
    {
        OnMounted(LoadAsync);
    }

    private Task RefreshAsync()
    {
        return LoadAsync(Context!.Lifecycle.CancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading.Value = true;
        try
        {
            using HttpRequestMessage request =
                new(HttpMethod.Get, "/api/todos");
            using HttpResponseMessage response =
                await Http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // TodoPayload is an application-owned, source-generated/AOT-safe decoder.
            Items.Value =
                await TodoPayload.ReadAsync(response, cancellationToken);
        }
        finally
        {
            IsLoading.Value = false;
        }
    }
}
```

`OnMounted(LoadAsync)` selects the `Func<CancellationToken, Task>` overload. Core starts and
observes the task after the initial subtree commits; it does not delay mount while the request is
in flight. Setting either reference schedules a render update.

The `@click` compiler preserves `RefreshAsync` as a task-returning delegate. Browser invokes it and
observes the task. `async void` is intentionally rejected by the source generator because its
failure cannot be observed.

Lifecycle task behavior is:

- ordinary asynchronous lifecycle callbacks start in their named phase and may overlap;
- the component token is canceled during unmount, after before-unmount callbacks start and before
  the component effect scope and subtree are torn down;
- asynchronous failures flow through `OnErrorCaptured` and then the application error handler; and
- `OnServerPrefetch` is the exception to non-blocking lifecycle progression: ServerRenderer awaits
  those callbacks before serializing the component.

When pending content must be replaced by a fallback as one coordinated branch, use the
`<Suspense>` template built-in, which compiles to a Components-owned `SuspenseNode` whose executor
is internal to Core. Server rendering awaits the default branch and
does not serialize the fallback. Suspense client hydration is currently unsupported; a hydration
attempt fails explicitly rather than partially claiming the server DOM. Boundary timeout/events,
fallback-to-reveal transition choreography, and delaying mounted/post-render effects from the
hidden default branch are also not implemented.

### 2.4 Application state in a `.viu` component

Define the store once:

```csharp
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

public sealed class CounterStore
{
    public CounterStore()
    {
        Double = Reactive.Computed(() => Count.Value * 2);
    }

    public Reference<int> Count { get; } = Reactive.Reference(0);

    public Computed<int> Double { get; }

    public void Increment() => Count.Value++;
}

public static class CounterState
{
    public static StateStoreDefinition<CounterStore> Counter { get; } =
        new(
            "counter",
            static context => new CounterStore());
}
```

Consume the application registry from a mounted component:

```text
<template>
    <button type="button" @click="Increment">
        {{ Counter.Count }} / doubled: {{ Counter.Double }}
    </button>
</template>

@script {
    using Assimalign.Viu.State;

    public CounterStore Counter { get; private set; } = null!;

    partial void OnSetup()
    {
        Counter = CounterState.Counter.Use(Context!);
    }

    private void Increment()
    {
        Counter.Increment();
    }
}
```

`Use(Context!)` locates the application's configured `IStateStoreRegistry`. Every component in that
application receives the same `CounterStore` instance. Resolving the store does not itself
subscribe the component; the generated render subscribes when it reads `Count` and `Double`.

## 3. Pure C# components and virtual nodes

### 3.1 An authored component

```csharp
using System;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

public sealed class CounterCard : IComponent
{
    public static ComponentReference Reference { get; } =
        ComponentReference.ForType(typeof(CounterCard));

    public static ComponentContract Contract { get; } = new(
        displayName: nameof(CounterCard),
        parameters:
        [
            new ComponentParameter(
                "step",
                defaultFactory: static () => 1,
                validator: static value => value is int step && step > 0,
                parameterType: typeof(int)),
        ],
        events:
        [
            new ComponentEvent(
                "changed",
                static arguments =>
                    arguments.Count == 1 && arguments[0] is int),
        ]);

    private readonly Reference<int> _count = Reactive.Reference(0);
    private ComponentContext _context = null!;

    public ComponentRenderer Setup(ComponentContext context)
    {
        _context = context;
        context.Lifecycle.OnMounted(LogMounted);
        return Render;
    }

    private VirtualNode Render(ComponentRenderFrame frame)
    {
        return new ElementNode(
            new QualifiedName("button"),
            bindings:
            [
                ElementBinding.Attribute(new QualifiedName("type"), "button"),
                ElementBinding.Event("click", (Action)HandleClick),
            ],
            children: [new TextNode($"Count: {_count.Value}")]);
    }

    private void LogMounted()
    {
        IAuditLog audit =
            (IAuditLog?)_context.Services?.GetService(typeof(IAuditLog))
            ?? throw new InvalidOperationException(
                "The application did not supply IAuditLog.");
        audit.Write("Counter mounted.");
    }

    private void HandleClick()
    {
        int step = (int)_context.Bindings.Parameters["step"]!;
        _count.Value += step;
        _context.Emit("changed", _count.Value);
    }
}
```

`ComponentRegistration` carries `Reference` and `Contract`, so Core can resolve inputs before it
activates `CounterCard`. `Setup` runs once for each mounted invocation. The returned renderer may run
many times and receives that mount's `ComponentRenderFrame`; `_count`, `_context`, and lifecycle
registrations remain on the authored instance.

### 3.2 Creating the parent request

```csharp
VirtualNode counter = new ComponentNode(
    CounterCard.Reference,
    new ComponentInvocation(
        arguments: new Dictionary<string, object?>
        {
            ["step"] = 2,
        },
        listeners: new Dictionary<string, ComponentEventListener>
        {
            ["changed"] = arguments =>
                Console.WriteLine($"New count: {arguments[0]}"),
        }));
```

`ComponentInvocation` is the immutable, raw parent request. Core resolves it against the static
contract into `ComponentBindings` for the mounted context; the two types deliberately share no
interface. A `ComponentEventListener` receives one immutable ordered argument list and returns
`void`; application code starts and observes longer asynchronous work through an owned lifetime.

### 3.3 Elements, fragments, slots, and named components

```csharp
IReadOnlyDictionary<string, ComponentSlot> slots =
    new Dictionary<string, ComponentSlot>
    {
        ["default"] = _ =>
            new ElementNode(
                new QualifiedName("strong"),
                children: [new TextNode("Slot content")]),
    };

VirtualNode tree = new FragmentNode(
[
    new ElementNode(
        new QualifiedName("h1"),
        children: [new TextNode("Dashboard")]),
    new ComponentNode(
        Panel.Reference,
        new ComponentInvocation(slots: slots),
        key: "main-panel"),
]);
```

Dynamic selection stays explicit. A string used as a qualified name creates an element; a
registered component name becomes a `ComponentReference`:

```csharp
VirtualNode dynamicElement = new ElementNode(new QualifiedName(selectedName));
VirtualNode dynamicPanel =
    new ComponentNode(ComponentReference.ForName(selectedName));
```

Hand-built nodes carry `RenderPlan.None` by default and use the correct full diff. Generated `.viu`
templates emit `RenderPlan` values through statement-form calls to
`ComponentRenderFrame.OpenBlock`, `Track`, and `CloseBlock`, allowing compatible block roots to
patch their dynamic descendants without revisiting static siblings.

## 4. Component registrations and application services

`IComponentFactory` resolves `ComponentReference` values to complete `ComponentRegistration`
values. It is not an `IServiceProvider`; application composition chooses both independently.

### 4.1 Explicit activation and a small service provider

```csharp
using System;
using System.Net.Http;

using Assimalign.Viu.Components;

public sealed class ApplicationServices : IServiceProvider, IDisposable
{
    private readonly HttpClient _http = new();
    private readonly AuditLog _audit = new();

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(HttpClient))
        {
            return _http;
        }

        if (serviceType == typeof(IAuditLog))
        {
            return _audit;
        }

        return null;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

ApplicationServices services = new();
ComponentFactory components = new();

components.Register(
    new ComponentRegistration(
        App.Reference,
        App.Contract,
        static _ => new App()));
components.Register(
    new ComponentRegistration(
        CounterCard.Reference,
        CounterCard.Contract,
        static _ => new CounterCard()));
components.Register(
    new ComponentRegistration(
        ServiceConstructedPanel.Reference,
        ServiceConstructedPanel.Contract,
        provider => new ServiceConstructedPanel(
            (IAuditLog?)provider?.GetService(typeof(IAuditLog))
            ?? throw new InvalidOperationException(
                "The application did not supply IAuditLog."))));
```

Each activator is an explicit delegate receiving the nullable application provider, so activation
is trimming- and AOT-safe. Viu does not perform constructor discovery or call
`Activator.CreateInstance`. A per-component dependency-injection scope can be owned by an
`IComponent` wrapper that also implements `IDisposable`; Core disposes that activated instance
after unmount but never creates scopes or disposes the borrowed application provider.

`ComponentRegistration.Define` is the composition-only alternative for a small code-first
component:

```csharp
components.Register(
    ComponentRegistration.Define(
        "StatusBadge",
        new ComponentContract(displayName: "StatusBadge"),
        context => frame => new TextNode("Ready")));
```

### 4.2 Browser application composition

```csharp
using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.State;

using ApplicationServices services = new();
ComponentFactory components = new();

ComponentRegistration appRegistration = new(
    App.Reference,
    App.Contract,
    static _ => new App());
components.Register(appRegistration);
components.Register(
    new ComponentRegistration(
        CounterCard.Reference,
        CounterCard.Contract,
        static _ => new CounterCard()));

using IStateStoreRegistry state = StateStores.CreateRegistry(services);

BrowserApplicationBuilder builder = new();
builder.ConfigureApplication(
    options =>
    {
        options.RootComponent = new ComponentNode(appRegistration.Reference);
        options.Components = components;
        options.Services = services;
        options.State = state;
        options.ErrorHandler =
            (exception, component, information) =>
                Console.Error.WriteLine($"{information}: {exception}");
        options.WarnHandler = Console.Error.WriteLine;
    });

await using IApplication application = builder.Build();
await application.RunAsync();
```

The host borrows the factory, provider, and registry; disposing the application unmounts the tree
but does not dispose those application-owned values. `IApplication` remains single-use,
`ApplicationLifetime.State` exposes the current `ApplicationState` value, and that lifetime owns the common
transition machine. Middleware wraps the complete mounted lifetime.

## 5. Reactivity

### 5.1 References, computed values, effects, and batching

```csharp
using Assimalign.Viu.Reactivity;

IReactiveReference<int> count = Reactive.Reference(1);
Computed<int> doubled = Reactive.Computed(() => count.Value * 2);

int effectRuns = 0;
ReactiveEffect effect = Reactive.Effect(
    () =>
    {
        effectRuns++;
        Console.WriteLine($"{count.Value} -> {doubled.Value}");
    });

Reactive.StartBatch();
try
{
    count.Value = 2;
    count.Value = 3;
}
finally
{
    Reactive.EndBatch();
}

effect.Stop();
```

`IReactiveReference<T>` is the consumer-facing substitution contract. First-party
`Reference<T>`, `ShallowReference<T>`, `CustomReference<T>`, and `Computed<T>` retain the
`ReactiveValue<T>` class hierarchy for engine state and hot-path dispatch.

### 5.2 Watch and cleanup

```csharp
Reference<string> query = Reactive.Reference(string.Empty);

using WatchHandle watch = Reactive.Watch(
    query,
    (value, previousValue, onCleanup) =>
    {
        CancellationTokenSource request = new();
        onCleanup(request.Cancel);
        SearchInBackground(value, request.Token);
    },
    new WatchOptions
    {
        Immediate = true,
    });

query.Value = "reactivity";
query.Value = "rendering";
```

The watch callback itself is synchronous. It may start application-owned background work and use
`onCleanup` to cancel stale work. For task-aware component work, lifecycle callbacks and component
or host event handlers are the preferred boundaries because Core observes their returned tasks.

### 5.3 Effect scopes

```csharp
IReactiveEffectScopeFactory scopes = new ReactiveEffectScopeFactory();
using IReactiveEffectScope scope = scopes.Create(isDetached: true);

Reference<int> count = Reactive.Reference(0);

scope.Run(
    () =>
    {
        Reactive.WatchEffect(
            () => Console.WriteLine($"Scoped count: {count.Value}"));
        Reactive.OnScopeDispose(
            () => Console.WriteLine("Scoped work stopped."));
    });

count.Value++;
scope.Stop();
count.Value++; // No scoped effect runs.
```

An effect scope is ownership, not a broadcast channel. A component or child responds to a state
change only when its render, effect, computed, or watcher reads the corresponding reactive value.
Stopping a scope stops the effects and cleanup callbacks created inside it.

Reactive lists, dictionaries, and sets use the same dependency model:

```csharp
ReactiveList<string> names = new();
ReactiveEffect listEffect =
    Reactive.Effect(() => Console.WriteLine(names.Count));

names.Add("Ada");
listEffect.Stop();
```

## 6. State at three levels

### 6.1 Application/global state

An application composition root owns one `IStateStoreRegistry`, makes it reachable through its
service provider or explicitly passes it to consumers, and resolves static definitions through
that registry:

```csharp
CounterStore first = CounterState.Counter.Use(state);
CounterStore second = CounterState.Counter.Use(state);

Console.WriteLine(ReferenceEquals(first, second)); // True
```

Each definition has one materialized store per registry at a time and remains a cache hit until it
is removed. Each materialized store receives one registry-owned reactive scope. Removing a store
stops its scope; a later use activates it again. Disposing the registry stops every store-owned
effect and cleanup callback.

`StateStores.ActiveRegistry` is the optional fallback used by `definition.Use(context)` when
`context.Services` does not expose `IStateStoreRegistry`. Outside a mounted component, call
`definition.Use(registry)` explicitly. Server and multi-request hosts pass the request-owned
registry rather than relying on ambient state.

### 6.2 Explicit isolated feature state

Create another registry when a route or feature needs isolation. The registry is passed explicitly;
it is not inherited through component-tree injection:

```csharp
using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

public sealed class CheckoutShell : IComponent, IDisposable
{
    private IStateStoreRegistry? _featureState;
    private CheckoutStore? _checkout;

    public ComponentRenderer Setup(ComponentContext context)
    {
        _featureState = StateStores.CreateRegistry(
            context.Services,
            context.WatchScheduler);
        _checkout = CheckoutState.Definition.Use(_featureState);

        return frame => new ComponentNode(
            CheckoutSummary.Reference,
            new ComponentInvocation(
                arguments: new Dictionary<string, object?>
                {
                    ["store"] = _checkout,
                }));
    }

    public void Dispose()
    {
        _featureState?.Dispose();
    }
}

public sealed class CheckoutSummary : IComponent
{
    public static ComponentReference Reference { get; } =
        ComponentReference.ForType(typeof(CheckoutSummary));

    public static ComponentContract Contract { get; } = new(
        displayName: nameof(CheckoutSummary),
        parameters:
        [
            new ComponentParameter(
                "store",
                isRequired: true,
                parameterType: typeof(CheckoutStore)),
        ]);

    public ComponentRenderer Setup(ComponentContext context)
    {
        CheckoutStore store =
            context.Bindings.Parameters["store"] as CheckoutStore
            ?? throw new InvalidOperationException("store is required");

        return frame => new TextNode(
            $"Items: {store.Items.Count}");
    }
}
```

Application composition registers `CheckoutSummary.Reference` with `CheckoutSummary.Contract` and
an explicit activator before this tree can mount; otherwise factory resolution fails rather than
falling back to constructor discovery.

Core disposes `CheckoutShell` after unmount, which disposes its isolated registry. A sibling
`CheckoutShell` creates a different `CheckoutStore`. A child receives the selected store through a
declared parameter or slot; calling `CheckoutState.Definition.Use(childContext)` would instead
resolve the application's global registry.

### 6.3 Component-local state

Use ordinary reactive values when state belongs to exactly one component instance:

```csharp
public sealed class SearchBox : IComponent
{
    private readonly Reference<string> _text =
        Reactive.Reference(string.Empty);

    public ComponentRenderer Setup(ComponentContext context)
    {
        context.Watch(
            () => _text.Value,
            (value, previous) =>
                Console.WriteLine($"Search changed to {value}"));

        return frame => new ElementNode(
            new QualifiedName("input"),
            bindings:
            [
                ElementBinding.Property("value", _text.Value),
            ]);
    }
}
```

Core runs setup inside the component's reactive scope. `context.Watch(...)` explicitly owns the
watch in that scope, so it stops on unmount. A component-local store object may also contain several
references, computed values, and methods; it does not need an `IStateStoreRegistry` unless registry
identity and isolated teardown are useful.

For Pinia-shaped member APIs, State also provides `StateStore<TState>` with typed, reflection-free
`Patch`, `Reset`, `Subscribe`, and `OnAction` support over a source-generated
`IReactiveObject`. Setup-style stores remain the smaller default.

## 7. Boundaries to remember

- Components owns the virtual-node and authored-component vocabulary and depends only on
  Reactivity. State, Core, and Browser point inward toward it.
- `IComponentFactory` resolves component references to registrations; `IServiceProvider` resolves
  opt-in application services. Neither contract implies the other.
- Core never performs reflection-based activation and never owns the supplied application
  provider.
- `ConfigureApplication` composes components, directives, services, state, and diagnostics through
  `ApplicationOptions` before `Build()`. `Use` registers top-level lifetime middleware after
  `Build()`; middleware cannot alter composition, and registration freezes when `StartAsync` begins.
- State replaces the previous Store package. One definition produces one store instance per
  registry.
- Effect scopes stop subscriptions; they do not make descendants subscribe automatically.
- Dynamic selection is explicit: construct an `ElementNode` for an element name or a
  `ComponentNode(ComponentReference.ForName(...))` for a registered component.
- Suspense mount/update behavior is implemented, including fallback and nested-boundary
  coordination. Suspense hydration, boundary timeout/events, fallback-to-reveal transition
  choreography, and hidden-branch post-effect delay are not implemented.
- Component-tree `provide`/`inject` is intentionally absent. Use arguments, slots, explicit state
  registries, and the application-owned service provider.
