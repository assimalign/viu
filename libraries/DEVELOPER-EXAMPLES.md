# Developer consumption examples

These examples describe the APIs implemented in `.redesign`. They are written from the
application developer's point of view and use the separated Components, Reactivity, State, Core,
and Browser packages.

## 1. Mental model

| Developer concern | Main API | Lifetime |
| --- | --- | --- |
| Immutable render description | `IComponent` / `ComponentTree` | One render |
| Authored component behavior | `IComponentTemplate` | One mounted template |
| Mounted component inputs and capabilities | `IComponentContext` | One mounted template |
| Component activation | `IComponentFactory` | Application-owned |
| Reactive values and subscriptions | `Reactive`, `IReactiveReference<T>`, `IReactiveEffectScope` | Explicit or component-owned |
| Shared state | `StateStoreDefinition<TStore>`, `IStateStoreRegistry` | One registry |
| Host lifecycle and mounting | `IApplication<TNode>`, `Application<TNode>` | One host application |

`IComponent` is the one public render-tree vocabulary. Core still keeps internal mounted-node
bookkeeping because an immutable render description and a live host node have different lifetimes.

There is no component-tree `provide`/`inject` API. A component receives explicit inputs through
arguments and slots, application services through `IComponentContext.Services`, and application
state through the State-owned context capability.

## 2. Context inside a `.viu` component

For a `.viu` file with a template, the source generator emits the following bridge conceptually:

```csharp
partial class UserCard : IComponentTemplate
{
    private IComponentContext Context { get; set; } = null!;

    partial void OnSetup();

    ComponentRenderer IComponentTemplate.Setup(IComponentContext context)
    {
        Context = context;
        OnSetup();
        return () => RenderGeneratedTemplate();
    }
}
```

The developer does not write that bridge. Code inside `@script { }` is merged into the same partial
class, so it can use the generated private `Context` member and implement the generated
`partial void OnSetup()` hook.

`Context` is assigned immediately before `OnSetup` runs. Do not use it from a field initializer or
constructor, and do not declare another member named `Context` or `OnSetup`.

The mounted context exposes:

- `Arguments` for declared component parameters;
- `Slots` for current parent-provided slots;
- `Attributes` for undeclared fallthrough attributes;
- `Components` for application-selected component resolution;
- `Services` for the independently supplied `IServiceProvider`;
- `Lifecycle` for callbacks and the component-lifetime cancellation token;
- `Emit(...)` for declared component events; and
- `Expose(...)` for the value assigned to a parent template reference.

### 2.1 Component-local state

```text
@template {
    <button type="button" @click="Increment">
        Count: {{ Count }}
    </button>
}

@script {
    using Assimalign.Viu.Reactivity;

    public Reference<int> Count { get; } = Reactive.Reference(0);

    private void Increment()
    {
        Count.Value++;
    }

    partial void OnSetup()
    {
        Context.Lifecycle.OnMounted(
            () => System.Diagnostics.Debug.WriteLine("Counter mounted."));
    }
}
```

Each mount receives a fresh generated component instance, so `Count` is component-local. The
generated render function reads `Count.Value`; that read subscribes the component render effect,
and assigning `Count.Value` schedules the necessary patch.

### 2.2 Parameters, services, and emitted events

```text
@template {
    <article>
        <h2>{{ Title }}</h2>
        <button type="button" @click="SaveAsync">Save</button>
    </article>
}

@script {
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Assimalign.Viu.Components;

    public IReadOnlyList<IComponentParameter> Parameters { get; } =
    [
        new ComponentParameter("title", isRequired: true),
    ];

    public IReadOnlyList<IComponentEvent> Events { get; } =
    [
        new ComponentEvent("saved"),
    ];

    public string Title =>
        Context.Arguments.Get<string>("title")
        ?? throw new InvalidOperationException("title is required");

    private async Task SaveAsync()
    {
        ISaveClient client =
            (ISaveClient?)Context.Services.GetService(typeof(ISaveClient))
            ?? throw new InvalidOperationException(
                "The application did not supply ISaveClient.");

        string identifier = await client.SaveAsync(
            Context.Lifecycle.CancellationToken);
        Context.Emit("saved", identifier);
    }
}
```

The parent may spell a declared camel-case parameter in camel case or kebab case. Parameter
defaults and validators run in Core, while undeclared values remain available in
`Context.Attributes` for fallthrough.

`Context.Emit` supports zero or more arguments. Parent listeners can be synchronous or return
`Task`; Core observes asynchronous listeners and routes faults through component and application
error handling.

### 2.3 Asynchronous loading with lifecycle hooks

Setup itself remains synchronous. It creates the render closure and registers asynchronous work
with the lifecycle that owns that work:

```text
@template {
    <section>
        <button type="button" @click="RefreshAsync">Refresh</button>

        <p v-if="IsLoading">Loading...</p>
        <ul v-else>
            <li v-for="item in Items" :key="item.Id">{{ item.Title }}</li>
        </ul>
    </section>
}

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
        (HttpClient?)Context.Services.GetService(typeof(HttpClient))
        ?? throw new InvalidOperationException(
            "The application did not supply HttpClient.");

    partial void OnSetup()
    {
        Context.Lifecycle.OnMounted(LoadAsync);
    }

    private Task RefreshAsync()
    {
        return LoadAsync(Context.Lifecycle.CancellationToken);
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

When pending content must be replaced by a fallback as one coordinated branch, use the Core
`Suspense` built-in with an asynchronous component. Server rendering awaits the default branch and
does not serialize the fallback. Suspense client hydration is currently unsupported; a hydration
attempt fails explicitly rather than partially claiming the server DOM. Boundary timeout/events,
fallback-to-reveal transition choreography, and delaying mounted/post-render effects from the
hidden default branch are also not yet at Vue parity.

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

public static class ApplicationState
{
    public static StateStoreDefinition<CounterStore> Counter { get; } =
        StateStores.Define(
            "counter",
            static () => new CounterStore());
}
```

Consume the application registry from a mounted component:

```text
@template {
    <button type="button" @click="Increment">
        {{ Counter.Count }} / doubled: {{ Counter.Double }}
    </button>
}

@script {
    using Assimalign.Viu.State;

    public CounterStore Counter { get; private set; } = null!;

    partial void OnSetup()
    {
        Counter = ApplicationState.Counter.Use(Context);
    }

    private void Increment()
    {
        Counter.Increment();
    }
}
```

`Use(Context)` locates the application's configured `IStateStoreRegistry`. Every component in that
application receives the same `CounterStore` instance. Resolving the store does not itself
subscribe the component; the generated render subscribes when it reads `Count` and `Double`.

## 3. Pure C# components and trees

### 3.1 An authored component template

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

public sealed class CounterCard : IComponentTemplate
{
    private readonly Reference<int> _count = Reactive.Reference(0);
    private IComponentContext _context = null!;

    public string? Name => nameof(CounterCard);

    public IReadOnlyList<IComponentParameter> Parameters { get; } =
    [
        new ComponentParameter(
            "step",
            defaultFactory: static () => 1,
            validator: static value => value is int step && step > 0),
    ];

    public IReadOnlyList<IComponentEvent> Events { get; } =
    [
        new ComponentEvent(
            "changed",
            static arguments =>
                arguments.Count == 1 && arguments[0] is int),
    ];

    public ComponentRenderer Setup(IComponentContext context)
    {
        _context = context;
        context.Lifecycle.OnMounted(LogMountedAsync);
        return Render;
    }

    private IComponent Render()
    {
        return ComponentTree.Element(
            "button",
            new ComponentAttributes(
            [
                new ComponentAttribute("type", "button"),
                new ComponentAttribute(
                    "onClick",
                    (Func<object?, Task>)HandleClickAsync),
            ]),
            [ComponentTree.Text($"Count: {_count.Value}")]);
    }

    private Task LogMountedAsync(CancellationToken cancellationToken)
    {
        IAuditLog audit =
            (IAuditLog?)_context.Services.GetService(typeof(IAuditLog))
            ?? throw new InvalidOperationException(
                "The application did not supply IAuditLog.");
        return audit.WriteAsync("Counter mounted.", cancellationToken);
    }

    private async Task HandleClickAsync(object? browserEvent)
    {
        await Task.Yield();
        int step = _context.Arguments.Get<int>("step");
        _count.Value += step;
        _context.Emit("changed", _count.Value);
    }
}
```

`Setup` runs once per mount. The returned `ComponentRenderer` may run many times. The render
description is recreated, while `_count`, `_context`, and registered callbacks belong to the
mounted template instance.

### 3.2 Creating the parent request

```csharp
IComponent counter = ComponentTree.Template<CounterCard>(
    arguments: new ComponentArguments(
    [
        new KeyValuePair<string, object?>("step", 2),
    ]),
    listeners: new Dictionary<string, ComponentEventListener>
    {
        ["changed"] = new ComponentEventListener(
            value => Console.WriteLine($"New count: {value}")),
    });
```

For a listener that needs every emitted argument:

```csharp
ComponentEventListener listener =
    ComponentEventListener.ForAsynchronousArguments(
        async arguments =>
        {
            await audit.WriteAsync(
                $"Received {arguments.Count} arguments.",
                CancellationToken.None);
        },
        isOnce: true);
```

### 3.3 Elements, fragments, slots, and named templates

```csharp
ComponentSlots slots = new()
{
    ["default"] = _ =>
        ComponentTree.Element(
            "strong",
            children: [ComponentTree.Text("Slot content")]),
};

IComponent tree = ComponentTree.Fragment(
[
    ComponentTree.Element(
        "h1",
        children: [ComponentTree.Text("Dashboard")]),
    ComponentTree.Template<Panel>(
        slots: slots,
        key: "main-panel"),
]);
```

`ComponentTree.Template("Panel")` creates a request for a statically known registered name. In a
dynamic `:is`-style expression, a plain string deliberately means an element tag because
`IComponentFactory` has no registration-probe API. Select a registered name explicitly:

```csharp
IComponent dynamicPanel = DynamicComponents.DynamicComponent(
    DynamicComponents.Named("Panel"));
```

The compiler emits `ComponentOptimization` and block tracking for `.viu` templates. That metadata
survives the unified component tree, so compatible block roots patch their dynamic descendants
without revisiting static siblings. Most application code should not hand-author those compiler
hints.

## 4. Component factories and application services

`IComponentFactory` is not an `IServiceProvider`. The application chooses both independently.

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

IComponentFactory components = new ComponentFactory(
[
    new ComponentRegistration(
        typeof(App),
        static () => new App(),
        "App"),
    new ComponentRegistration(
        typeof(CounterCard),
        static () => new CounterCard(),
        "CounterCard"),
    new ComponentRegistration(
        typeof(ServiceConstructedPanel),
        () => new ServiceConstructedPanel(
            (IAuditLog)services.GetService(typeof(IAuditLog))!),
        "ServiceConstructedPanel"),
]);
```

All activators are explicit delegates, so activation is trimming- and AOT-safe. An activator may
close over an application container, a generated resolver, or hand-written values. Viu does not
perform constructor discovery or call `Activator.CreateInstance`.

If an application wants a per-component dependency-injection scope, its activator can create that
scope and return an `IComponentTemplate` wrapper that implements `IDisposable`. Core owns the
returned template for that mount and disposes it after unmount. Core does not create service scopes
or dispose the application provider.

### 4.2 Browser application composition

```csharp
using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

using ApplicationServices services = new();

IComponentFactory components = new ComponentFactory(
[
    new ComponentRegistration(typeof(App), static () => new App(), "App"),
    new ComponentRegistration(
        typeof(CounterCard),
        static () => new CounterCard(),
        "CounterCard"),
]);

using StateStoreRegistry state = StateStores.CreateRegistry(
    components,
    services,
    new ReactiveEffectScopeFactory());

BrowserApplicationBuilder builder =
    BrowserApplication.CreateBuilder(ComponentTree.Template<App>());
builder.UseComponentFactory(components);
builder.UseServiceProvider(services);
builder.UseStateRegistry(state);
builder.ConfigureApplication(
    context =>
    {
        context.ErrorHandler =
            (exception, component, information) =>
                Console.Error.WriteLine($"{information}: {exception}");
        context.WarnHandler = Console.Error.WriteLine;
    });

await using BrowserApplication application = builder.Build();
await application.MountAsync("#app");
```

The application borrows `components`, `services`, and `state`. Disposing the browser application
unmounts its tree; the composition root still disposes the registry and provider it created.

`BrowserApplication` derives from `Application<int>` because Browser represents DOM nodes as opaque
integer handles. The host-facing mount contract is `IApplication<TNode>`. The non-generic
`IApplication` base contains only platform-neutral configuration, plugin, state, and unmount
capabilities. A WebView2 host can derive from `Application<WebViewNodeHandle>` and supply its own
renderer operations without changing component, reactivity, or state APIs.

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

query.Value = "vue";
query.Value = "viu";
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

An application composition root owns one `StateStoreRegistry`, passes it to the application
builder, and resolves static definitions through that registry:

```csharp
CounterStore first = ApplicationState.Counter.Use(state);
CounterStore second = ApplicationState.Counter.Use(state);

Console.WriteLine(ReferenceEquals(first, second)); // True
```

Every store definition runs once per registry. The registry creates a detached root reactive scope
and one child scope per initialized store. Removing a store stops that child scope; disposing the
registry stops all store-owned effects and cleanup callbacks.

`StateStores.ActiveRegistry` and parameterless `definition.Use()` are available for browser
bootstrap and tests. Server and multi-request hosts should pass the request-owned registry
explicitly.

### 6.2 Explicit isolated feature state

Create another registry when a route or feature needs isolation. The registry is passed explicitly;
it is not inherited through component-tree injection:

```csharp
using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

public sealed class CheckoutShell : IComponentTemplate, IDisposable
{
    private StateStoreRegistry? _featureState;
    private CheckoutStore? _checkout;

    public ComponentRenderer Setup(IComponentContext context)
    {
        _featureState = StateStores.CreateRegistry(
            context.Components,
            context.Services,
            new ReactiveEffectScopeFactory());
        _checkout = CheckoutState.Definition.Use(
            _featureState,
            context);

        return () => ComponentTree.Template<CheckoutSummary>(
            arguments: new ComponentArguments(
            [
                new KeyValuePair<string, object?>(
                    "store",
                    _checkout),
            ]));
    }

    public void Dispose()
    {
        _featureState?.Dispose();
    }
}

public sealed class CheckoutSummary : IComponentTemplate
{
    public IReadOnlyList<IComponentParameter> Parameters { get; } =
    [
        new ComponentParameter("store", isRequired: true),
    ];

    public ComponentRenderer Setup(IComponentContext context)
    {
        CheckoutStore store =
            context.Arguments.Get<CheckoutStore>("store")
            ?? throw new InvalidOperationException("store is required");

        return () => ComponentTree.Text(
            $"Items: {store.Items.Count}");
    }
}
```

Core disposes `CheckoutShell` after unmount, which disposes its isolated registry. A sibling
`CheckoutShell` creates a different `CheckoutStore`. A child receives the selected store through a
declared parameter or slot; calling `CheckoutState.Definition.Use(childContext)` would instead
resolve the application's global registry.

### 6.3 Component-local state

Use ordinary reactive values when state belongs to exactly one component instance:

```csharp
public sealed class SearchBox : IComponentTemplate
{
    private readonly Reference<string> _text =
        Reactive.Reference(string.Empty);

    public ComponentRenderer Setup(IComponentContext context)
    {
        Reactive.Watch(
            _text,
            (value, previous, onCleanup) =>
                Console.WriteLine($"Search changed to {value}"));

        return () => ComponentTree.Element(
            "input",
            new ComponentAttributes(
            [
                new ComponentAttribute("value", _text.Value),
            ]));
    }
}
```

Core runs setup inside the component's reactive effect scope, so setup-created watchers are stopped
on unmount. A component-local store object may also contain several references, computeds, and
methods; it does not need an `IStateStoreRegistry` unless registry identity and isolated teardown
are useful.

For Pinia-shaped member APIs, State also provides `StateStore<TState>` with typed, reflection-free
`Patch`, `Reset`, `Subscribe`, and `OnAction` support over a source-generated
`IReactiveObject`. Setup-style stores remain the smaller default.

## 7. Boundaries to remember

- Components do not depend on Reactivity, State, Core, or Browser.
- `IComponentFactory` resolves component templates; `IServiceProvider` resolves application
  services. Neither contract implies the other.
- Core never performs reflection-based activation and never owns the supplied application
  provider.
- Application plugins initialize an already-composed application. The built-in component and
  directive resolvers are configured before `Build()`; plugin-driven mutation requires a custom
  resolver that deliberately exposes that capability.
- State replaces the previous Store package. One definition produces one store instance per
  registry.
- Effect scopes stop subscriptions; they do not make descendants subscribe automatically.
- Plain dynamic strings are element tags. Use `DynamicComponents.Named(...)` for a component
  registration selected by name.
- Suspense mount/update behavior is implemented, including fallback and nested-boundary
  coordination. Suspense hydration, boundary timeout/events, fallback-to-reveal transition
  choreography, and hidden-branch post-effect delay are not yet at Vue parity.
- Component-tree `provide`/`inject` is intentionally absent. Use arguments, slots, explicit state
  registries, and the application-owned service provider.
