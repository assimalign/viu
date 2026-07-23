# Developer consumption examples

**Status:** Design examples for review. These examples describe the intended developer experience
after the package split; they are not a claim that every shown API is implemented in the redesign
scaffold today.

The examples use three categories of API:

| Category | Meaning |
| --- | --- |
| Scaffolded | The contract currently exists under `.redesign`. |
| Restored target | The API already exists in the shipping Core or Store implementation and is intended to move into the new package. |
| Proposed convenience | A small API or generator feature that the examples reveal is needed before the design is final. |

The most important proposed convenience is the `.viu` component-context contract described below.
The current `.viu` generator does not expose its setup context to `@script`.

| Surface used below | Status |
| --- | --- |
| `IComponent`, `IComponentTemplate`, `IComponentContext`, `IComponentFactory`, and `ComponentTree` | Scaffolded |
| Generated `.viu` `Context` and `OnSetup()` | Proposed convenience |
| Component event-listener collection | Proposed convenience |
| `IReactiveReference*` and `IReactiveEffectScope*` interfaces | Scaffolded |
| `Reactive`, references, computeds, effects, watches, scopes, generated reactive objects, and collections | Restored target |
| First-party `ReactiveEffectScopeFactory` adapter | Proposed convenience |
| `StateStoreDefinition<TStore>` and `IStateStoreRegistry` | Scaffolded |
| `StateStoreDefinition<TStore>.Use(IComponentContext)` | Proposed convenience |
| State-owned `IStateStoreContext` implemented by Core's component context | Proposed convenience; retains `Core -> State` |
| Full `StateStore<TState>` member model | Restored behavior with a proposed type name |

## 1. Package-level developer model

A typical application uses all four packages, but each package has a distinct job:

```csharp
using Assimalign.Viu;                 // Application composition and rendering.
using Assimalign.Viu.Components;      // Component contracts and tree values.
using Assimalign.Viu.Reactivity;      // References, computeds, effects, and watches.
using Assimalign.Viu.State;           // Application and explicitly scoped stores.
```

| Developer concept | Primary abstraction | Lifetime |
| --- | --- | --- |
| A render-tree value | `IComponent` | Normally created for a render; compiler-cached static values may be reused |
| An authored component | `IComponentTemplate` | One instance per mounted template |
| Inputs and borrowed application services for a mounted instance | `IComponentContext` | One mounted template |
| Component activation policy | `IComponentFactory` | Application-owned |
| A reactive value | `IReactiveReference<T>` | As long as its owner retains it |
| A group of effects and cleanup callbacks | `IReactiveEffectScope` | Until stopped or disposed |
| A shared store definition | `StateStoreDefinition<TStore>` | Usually static application metadata |
| Store instances and their reactive lifetimes | `IStateStoreRegistry` | One application/request or explicit feature scope |
| Runtime integration | `IApplication` / `IApplicationContext` | One application |

Two distinctions are especially important:

1. `IComponent` is render data. `IComponentTemplate` is the user-authored object that creates that
   data. Core still owns an internal mounted instance for renderer bookkeeping.
2. An effect scope is a lifetime owner, not a broadcast channel. A component updates when its render
   effect reads a reactive value; merely sharing a scope does not subscribe a component.

Public types that appear together in one example would follow the repository rule of living in
separate files. They are grouped in this document only to keep each usage example self-contained.

## 2. Proposed `.viu` component-context contract

### 2.1 Expected generated surface

For every template-bearing `.viu` file, the generator should reserve and generate:

```csharp
private IComponentContext Context { get; set; } = null!;

partial void OnSetup();
```

The generated `IComponentTemplate.Setup` implementation should behave conceptually like this:

```csharp
ComponentRenderer IComponentTemplate.Setup(IComponentContext context)
{
    Context = context;
    OnSetup();

    object?[] cache = new object?[/* compiler-selected size */];
    return () => Render(this, cache);
}
```

This gives the script block two ways to use the context:

- Read `Context` from any property or event handler after setup.
- Implement `partial void OnSetup()` for setup-time work such as resolving a service, reading an
  initial argument, creating a computed, registering a watcher, or registering lifecycle callbacks.

`Context` and `OnSetup` should be reserved generated names. A `.viu` file that declares a conflicting
member should receive a compiler diagnostic.

Core must assign `Context` before calling `OnSetup`, and it must invoke setup inside the mounted
component's reactive effect scope. That makes effects and watches created in `OnSetup` stop
automatically when the component unmounts.

### 2.2 Context member usage

| Member | Normal use |
| --- | --- |
| `Context.Arguments` | Read parent-supplied component parameters |
| `Context.Services` | Resolve application services through the independently supplied `IServiceProvider` |
| `Context.Components` | Access the application-selected component resolver in advanced infrastructure code |
| `Context.Lifecycle` | Register callbacks owned by this mounted component |
| `Context.Emit(...)` | Emit a declared event to the parent |

`Context.Arguments` must present the latest parent-supplied snapshot on every render. The individual
`ComponentArguments` value may remain immutable, but Core cannot freeze a component's parameters at
mount time.

`Context` is valid only after generated setup begins and must not be retained after unmount.
Application code should request rendered children through `ComponentTree.Template(...)`. Calling
`Context.Components.Create(...)` directly creates an unmounted template that Core does not own; that
factory member is intended for advanced infrastructure, not ordinary child rendering.

### 2.3 Setup, updates, and teardown

The intended mounted-instance timeline is:

1. `IComponentFactory.Create(...)` returns a fresh template.
2. Core creates the component effect scope and calls `Setup`; generated `.viu` setup assigns
   `Context` and invokes `OnSetup()` exactly once.
3. The render delegate runs for the initial mount.
4. On a reactive invalidation or changed parent argument, Core runs `BeforeUpdate`, invokes the same
   render delegate again, patches the old/new trees, and then runs `Updated`. `OnSetup()` does not run
   again.
5. On unmount, Core runs `BeforeUnmount`, stops the component effect scope and its cleanup callbacks,
   tears down the rendered subtree, runs `Unmounted`, and finally disposes the mount-owned template
   when it implements `IDisposable`.

A property that reads `Context.Arguments` observes the current argument snapshot. Copying an argument
into a field during `OnSetup()` intentionally captures only its initial value.

If setup or initial mounting fails, Core must stop the partially created component scope and dispose
the mount-owned template. `Unmounted` should not be treated as guaranteed for a component that never
finished mounting; resource cleanup that must run on failure belongs in
`Reactive.OnScopeDispose(...)` or the template's `Dispose`.

## 3. `.viu` examples

### 3.1 Component-local counter

This is the smallest expected component-local state experience:

```viu
@template {
    <section class="counter">
        <p>Count: {{ Count }}</p>
        <p>Doubled: {{ Doubled }}</p>
        <button type="button" @click="Increment">Increment</button>
    </section>
}

@script {
    using System;

    using Assimalign.Viu.Components;
    using Assimalign.Viu.Reactivity;

    public IReactiveReference<int> Count { get; } = Reactive.Reference(0);

    public IReactiveReference<int> Doubled { get; private set; } = null!;

    partial void OnSetup()
    {
        Doubled = Reactive.Computed(() => Count.Value * 2);

        Reactive.WatchEffect(() =>
        {
            Console.WriteLine($"The component-local count is {Count.Value}.");
        });

        Context.Lifecycle.Register(
            ComponentLifecycleKind.Mounted,
            () => Console.WriteLine("Counter mounted."));
    }

    public void Increment()
    {
        Count.Value++;
    }
}

@style scoped {
    .counter {
        display: grid;
        gap: 0.5rem;
    }
}
```

Expected behavior:

- A fresh `Count` reference is created for each mounted component template.
- The compiler unwraps `IReactiveReference` values in interpolation, so `{{ Count }}` reads
  `Count.Value`.
- The component render effect reads `Count` and `Doubled`; a changed value schedules only this
  mounted component to render again.
- The `WatchEffect` belongs to the component effect scope and stops on unmount.
- `Computed<T>` remains lazy and cached. Its owning component scope stops observers of it; the
  computed itself is not an effect-scope-owned disposable resource.

Supporting `IReactiveReference` unwrapping is a required generator update. The current script
analyzer recognizes only the concrete reference types.

### 3.2 Arguments, services, lifecycle, and emitted events

The script can declare component metadata with ordinary C# members; no new `.viu` block grammar is
required:

```viu
@template {
    <article class="save-panel">
        <h2>{{ Label }}</h2>
        <p>{{ Status }}</p>
        <button type="button" @click="Save">Save</button>
    </article>
}

@script {
    using System;
    using System.Collections.Generic;

    using Assimalign.Viu.Components;
    using Assimalign.Viu.Reactivity;

    public IReadOnlyList<IComponentParameter>? Parameters { get; } =
    [
        new ComponentParameter("label", isRequired: true),
    ];

    public IReadOnlyList<IComponentEvent>? Events { get; } =
    [
        new ComponentEvent("saved"),
    ];

    public IReactiveReference<string> Status { get; } =
        Reactive.Reference("Not saved");

    public string Label =>
        Context.Arguments.Get<string>("label") ?? "Untitled";

    private IAuditLog AuditLog { get; set; } = null!;

    partial void OnSetup()
    {
        AuditLog =
            (IAuditLog?)Context.Services.GetService(typeof(IAuditLog))
            ?? throw new InvalidOperationException(
                "The application did not provide IAuditLog.");

        Context.Lifecycle.Register(
            ComponentLifecycleKind.Mounted,
            () => AuditLog.Record($"Mounted {Label}."));

        Context.Lifecycle.Register(
            ComponentLifecycleKind.Unmounted,
            () => AuditLog.Record($"Unmounted {Label}."));
    }

    public void Save()
    {
        string message = $"Saved {Label} at {DateTimeOffset.UtcNow:O}";
        Status.Value = message;
        AuditLog.Record(message);
        Context.Emit("saved", message);
    }
}
```

A parent consumes the parameter and event with normal template syntax:

```viu
@template {
    <SavePanel :label="PanelLabel" @saved="HandleSaved" />
    <output>{{ LastSaveMessage }}</output>
}

@script {
    using Assimalign.Viu.Reactivity;

    public string PanelLabel => "Profile";

    public IReactiveReference<string> LastSaveMessage { get; } =
        Reactive.Reference("Nothing saved yet.");

    public void HandleSaved(object? payload)
    {
        LastSaveMessage.Value =
            payload as string ?? "The child emitted an unexpected payload.";
    }
}
```

This event example exposes a scaffold gap: `ITemplateComponent` currently carries arguments and
slots but has no parent-listener collection. The final tree contract needs a dedicated, immutable
event-listener collection so Core can route `Context.Emit(...)` without treating listeners as
ordinary arguments.

### 3.3 Application/global state from a `.viu` component

“Global” means one instance within an application registry. The static value is the definition, not
the mutable store:

```csharp
using System;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

public sealed class CounterStore
{
    public CounterStore()
    {
        Count = Reactive.Reference(0);
        Doubled = Reactive.Computed(() => Count.Value * 2);
    }

    public IReactiveReference<int> Count { get; }

    public IReactiveReference<int> Doubled { get; }

    public void Increment()
    {
        Count.Value++;
    }
}

public static class ApplicationState
{
    public static StateStoreDefinition<CounterStore> Counter { get; } = new(
        "counter",
        context =>
        {
            CounterStore store = new();

            Reactive.Watch(
                store.Count,
                (value, previousValue, _) =>
                {
                    Console.WriteLine(
                        $"Application count: {previousValue} -> {value}");
                });

            Reactive.OnScopeDispose(
                () => Console.WriteLine("Counter store stopped."));

            return store;
        });
}
```

The desired component-facing call is:

```viu
@template {
    <section>
        <p>Shared count: {{ Counter.Count }}</p>
        <p>Shared doubled: {{ Counter.Doubled }}</p>
        <button type="button" @click="Counter.Increment">Increment globally</button>
    </section>
}

@script {
    using Assimalign.Viu.State;

    public CounterStore Counter { get; private set; } = null!;

    partial void OnSetup()
    {
        Counter = ApplicationState.Counter.Use(Context);
    }
}
```

`StateStoreDefinition<TStore>.Use(IComponentContext)` is a proposed State-owned convenience
extension. It keeps Components independent of State while making the common call concise.

With only the currently scaffolded interfaces, the equivalent is:

```csharp
IStateStoreRegistry registry =
    (IStateStoreRegistry?)Context.Services.GetService(
        typeof(IStateStoreRegistry))
    ?? throw new InvalidOperationException(
        "The application did not provide IStateStoreRegistry.");

Counter = registry.GetOrCreate(ApplicationState.Counter);
```

The current-scaffold workaround asks `Context.Services` to return the same `IStateStoreRegistry`
passed to `ApplicationBuilder.UseStateRegistry(...)`. That creates two configuration paths that can
silently diverge.

The recommended end state is a State-owned context capability implemented by Core's concrete
component context:

```csharp
public interface IStateStoreContext
{
    IStateStoreRegistry? State { get; }
}
```

`Definition.Use(Context)` can cast the runtime context to `IStateStoreContext`, obtain the registry,
and call `GetOrCreate(definition)` with no component owner. Components remains independent of State,
and the application has one authoritative registry. The service-provider lookup above is only the
scaffold-compatible fallback.

If a parent and three children all call `ApplicationState.Counter.Use(Context)` against the same
registry, they receive the same `CounterStore`. They do not subscribe merely by resolving it. Each
component whose template reads `Counter.Count` or `Counter.Doubled` establishes its own render
dependency and updates when that value changes.

### 3.4 Explicit isolated registry passed into a subtree

The current scaffold can express isolated feature state by creating a separate registry and passing
the store explicitly to descendants:

```viu
@template {
    <CheckoutForm :store="Checkout" />
}

@script {
    using System;

    using Assimalign.Viu.Components;
    using Assimalign.Viu.Reactivity;
    using Assimalign.Viu.State;

    private StateStoreRegistry State { get; set; } = null!;

    public CheckoutStore Checkout { get; private set; } = null!;

    partial void OnSetup()
    {
        IReactiveEffectScopeFactory effectScopes =
            (IReactiveEffectScopeFactory?)Context.Services.GetService(
                typeof(IReactiveEffectScopeFactory))
            ?? throw new InvalidOperationException(
                "The application did not provide an effect-scope factory.");

        State = new StateStoreRegistry(
            Context.Components,
            Context.Services,
            effectScopes);

        Reactive.OnScopeDispose(State.Dispose);

        Checkout = State.GetOrCreate(
            CheckoutState.Definition,
            Context);
    }
}
```

The child receives the scoped store as an ordinary parameter:

```viu
@template {
    <form>
        <p>Items: {{ Store.Items.Count }}</p>
        <p>Total: {{ Store.Total }}</p>
    </form>
}

@script {
    using System;
    using System.Collections.Generic;

    using Assimalign.Viu.Components;

    public IReadOnlyList<IComponentParameter>? Parameters { get; } =
    [
        new ComponentParameter("store", isRequired: true),
    ];

    public CheckoutStore Store =>
        Context.Arguments.Get<CheckoutStore>("store")
        ?? throw new InvalidOperationException(
            "CheckoutForm requires a checkout store.");
}
```

Two mounted checkout boundaries create two independent registries and therefore two independent
`CheckoutStore` instances. Unmounting one boundary disposes its registry and stops its store-owned
effects without affecting the other checkout or the application registry.

This is an isolated registry passed as a parameter, not implicit hierarchical injection.
`Reactive.OnScopeDispose(State.Dispose)` covers ordinary unmount as well as setup or initial-mount
failure, provided Core honors the component-scope failure guarantee described above. The scaffold
does not yet let a descendant call `Definition.Use(Context)` and automatically find the nearest
subtree registry. If that experience is desired, State still needs a first-class child-scope
contract and a nearest-scope lookup rule.

### 3.5 Component-level store object without a registry

A component can organize local state into a class without registering it globally:

```viu
@template {
    <button type="button" @click="Counter.Increment">
        Local store count: {{ Counter.Count }}
    </button>
}

@script {
    public CounterStore Counter { get; private set; } = null!;

    partial void OnSetup()
    {
        Counter = new CounterStore();
    }
}
```

The store is unique to that mounted component. Its reactive values are observed by the component's
render effect, and the object becomes unreachable after unmount. Use an application or scoped
registry only when identity must be shared beyond the component.

## 4. Pure C# component examples

### 4.1 A component template with arguments, lifecycle, and an event

```csharp
using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

public sealed class CounterTemplate : IComponentTemplate
{
    public string? Name => "Counter";

    public IReadOnlyList<IComponentParameter>? Parameters { get; } =
    [
        new ComponentParameter("initialCount"),
    ];

    public IReadOnlyList<IComponentEvent>? Events { get; } =
    [
        new ComponentEvent("changed"),
    ];

    public ComponentRenderer Setup(IComponentContext context)
    {
        int initialCount = context.Arguments.Contains("initialCount")
            ? context.Arguments.Get<int>("initialCount")
            : 0;

        IReactiveReference<int> count =
            Reactive.Reference(initialCount);

        IReactiveReference<int> doubled =
            Reactive.Computed(() => count.Value * 2);

        context.Lifecycle.Register(
            ComponentLifecycleKind.Mounted,
            () => Console.WriteLine("Counter mounted."));

        void Increment()
        {
            count.Value++;
            context.Emit("changed", count.Value);
        }

        return () => ComponentTree.Element(
            "button",
            new ComponentAttributes(
            [
                new ComponentAttribute("type", "button"),
                new ComponentAttribute("onClick", (Action)Increment),
            ]),
            [
                ComponentTree.Text(
                    $"Count: {count.Value}; doubled: {doubled.Value}"),
            ]);
    }
}
```

The parent creates render data, not the activated template. The expected pure-code endpoint includes
a dedicated component-event listener map:

```csharp
using System.Collections.Generic;

using Assimalign.Viu.Components;

int lastCount = 0;

void HandleChanged(object? payload)
{
    if (payload is int count)
    {
        lastCount = count;
    }
}

IComponent counter = ComponentTree.Template<CounterTemplate>(
    arguments: new ComponentArguments(
        new Dictionary<string, object?>
        {
            ["initialCount"] = 5,
        }),
    listeners: new Dictionary<string, ComponentEventHandler>
    {
        ["changed"] = HandleChanged,
    },
    key: "primary-counter");
```

When Core mounts that `ITemplateComponent`, it asks the application-selected
`IComponentFactory` for a fresh `CounterTemplate`, calls `Setup` once, and owns that returned
template until unmount.

`ComponentEventHandler` and the `listeners` parameter are proposed. `TemplateComponent` should copy
the dictionary into an immutable snapshot, and `Context.Emit("changed", payload)` should dispatch to
that snapshot. The `.viu` compiler lowers `@changed="HandleChanged"` to the same representation.
Element-host events remain `ComponentAttribute` values with the existing canonical property naming
(`onClick`, `onClickCapture`, and similar names); they are distinct from component-emitted listeners.

### 4.2 A fragment and child template

```csharp
using System.Collections.Generic;

using Assimalign.Viu.Components;

IComponent page = ComponentTree.Fragment(
[
    ComponentTree.Element(
        "h1",
        children:
        [
            ComponentTree.Text("Dashboard"),
        ]),
    ComponentTree.Template<CounterTemplate>(
        arguments: new ComponentArguments(
            new Dictionary<string, object?>
            {
                ["initialCount"] = 10,
            })),
    ComponentTree.Comment("End of dashboard"),
]);
```

Hand-written component trees default to `ComponentOptimization.None`. Generated `.viu` render code
adds patch flags and block dynamic-child metadata so Core can use the block-tree fast path.

### 4.3 What compiler-produced block metadata looks like

Application developers should not normally write this by hand, but this illustrates how the unified
component tree retains Vue-style optimized updates:

```csharp
using Assimalign.Viu.Components;
using Assimalign.Viu.Shared;

IComponent dynamicText = ComponentTree.Text(
    Count.Value.ToString(),
    optimization: new ComponentOptimization(
        patchFlags: PatchFlags.Text));

IComponent block = ComponentTree.Element(
    "section",
    children:
    [
        ComponentTree.Element(
            "h1",
            children:
            [
                ComponentTree.Text("Static heading"),
            ]),
        dynamicText,
    ],
    optimization: new ComponentOptimization(
        dynamicChildren:
        [
            dynamicText,
        ]));
```

On the next render, a compatible old/new block root lets Core patch `dynamicChildren` directly.
Static siblings are not revisited. A non-null empty dynamic-child collection still means “optimized
block with no dynamic descendants”; `null` means “not a block.”

## 5. Component factories and application services

### 5.1 Default explicit factory without dependency injection

```csharp
using Assimalign.Viu.Components;

IReportClient reportClient = new BrowserReportClient();

IComponentFactory components = new ComponentFactory(
[
    new ComponentRegistration(
        typeof(ApplicationRoot),
        () => new ApplicationRoot(),
        "ApplicationRoot"),
    new ComponentRegistration(
        typeof(ReportPanel),
        () => new ReportPanel(reportClient),
        "ReportPanel"),
]);
```

The activators are explicit and AOT-safe. Each call must return a fresh component template for one
mount.

### 5.2 Application-selected container

An application may use a dependency-injection container without making `IComponentFactory`
implement `IServiceProvider`:

```csharp
using System;

using Assimalign.Viu.Components;

public sealed class ApplicationComponentFactory : IComponentFactory
{
    private readonly IServiceProvider _services;

    public ApplicationComponentFactory(IServiceProvider services)
    {
        _services = services;
    }

    public IComponentTemplate Create(Type componentType)
    {
        if (componentType == typeof(ApplicationRoot))
        {
            return new ApplicationRoot();
        }

        if (componentType == typeof(ReportPanel))
        {
            IReportClient reportClient =
                (IReportClient?)_services.GetService(typeof(IReportClient))
                ?? throw new InvalidOperationException(
                    "IReportClient is not registered.");

            return new ReportPanel(reportClient);
        }

        throw new InvalidOperationException(
            $"Component type \"{componentType}\" is not registered.");
    }

    public IComponentTemplate Create(string name)
    {
        return name switch
        {
            "ApplicationRoot" => Create(typeof(ApplicationRoot)),
            "ReportPanel" => Create(typeof(ReportPanel)),
            _ => throw new InvalidOperationException(
                $"Component name \"{name}\" is not registered."),
        };
    }
}
```

This is a small hand-written example. A source-generated switch or a container's generated resolver
can supply the same behavior. Viu does not inspect constructors or call `Activator.CreateInstance`.

The factory and `IServiceProvider` are application-owned and remain separate values. Core borrows
both. Core owns each template returned for a mount and disposes it after unmount when it implements
`IDisposable`.

`Context.Services` is the borrowed application provider; Core does not create a dependency-injection
scope per component. An application that wants that policy creates a scope inside its custom
`IComponentFactory` and returns an `IDisposable` template wrapper that owns the scope. Core's normal
mount-owned template disposal then closes it. The wrapper must also dispose the scope when inner
component resolution fails.

### 5.3 Application composition

The target composition shape is:

```csharp
using Microsoft.Extensions.DependencyInjection;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

ServiceCollection registrations = new();

registrations.AddSingleton<IReportClient, BrowserReportClient>();
registrations.AddSingleton<
    IReactiveEffectScopeFactory,
    ReactiveEffectScopeFactory>();

registrations.AddSingleton<IComponentFactory>(
    provider => new ComponentFactory(
    [
        new ComponentRegistration(
            typeof(ApplicationRoot),
            () => new ApplicationRoot(),
            "ApplicationRoot"),
        new ComponentRegistration(
            typeof(ReportPanel),
            () => new ReportPanel(
                provider.GetRequiredService<IReportClient>()),
            "ReportPanel"),
    ]));

registrations.AddSingleton<IStateStoreRegistry>(
    provider => new StateStoreRegistry(
        provider.GetRequiredService<IComponentFactory>(),
        provider,
        provider.GetRequiredService<IReactiveEffectScopeFactory>()));

using ServiceProvider services =
    registrations.BuildServiceProvider();

IComponentFactory components =
    services.GetRequiredService<IComponentFactory>();

IStateStoreRegistry state =
    services.GetRequiredService<IStateStoreRegistry>();

IApplication application = new ApplicationBuilder()
    .UseRootComponent(ComponentTree.Template<ApplicationRoot>())
    .UseComponentFactory(components)
    .UseServiceProvider(services)
    .UseStateRegistry(state)
    .Build();
```

`ReactiveEffectScopeFactory` is a proposed first-party adapter over
`Reactive.EffectScope(...)`; it has no implementation in the scaffold yet. Because
`StateStoreRegistry` currently accepts `IReactiveEffectScopeFactory` in its public constructor, the
adapter must be public (or State must add a simpler public constructor and keep the seam internal).
Normal applications should not have to implement this infrastructure.

The container in this example is an application-layer choice, not a dependency of Components or
Core. `ApplicationBuilder` does not dispose the supplied factory, services, or state registry. The
composition root disposes the service provider it created, and that provider in turn disposes its
singleton state registry.

This scaffold currently ends at platform-neutral `Build()`. The final Browser/Core integration still
needs to define the public mount/unmount entry point for the new `IApplication`; this document does
not invent a method name before that host contract exists.

## 6. Reactivity examples

All examples in this section are restored-target APIs. The interfaces exist in the scaffold; the
engine and `Reactive` facade still need to move from Core into Reactivity.

### 6.1 Reference, computed, and effect

```csharp
using System;

using Assimalign.Viu.Reactivity;

IReactiveReference<int> count = Reactive.Reference(0);

IReactiveReference<int> doubled =
    Reactive.Computed(() => count.Value * 2);

ReactiveEffect output = Reactive.Effect(
    () =>
    {
        Console.WriteLine(
            $"{count.Value} x 2 = {doubled.Value}");
    });

count.Value++;
output.Stop();
```

Infrastructure that does not know the contained type can use the non-generic contract without
reflection:

```csharp
IReactiveReference untyped = count;

object? currentValue = untyped.Value;
bool isReference = Reactive.IsRef(untyped);
```

The interface is the public substitution boundary. First-party implementations should continue to
derive from `ReactiveValue<T>` so the engine keeps its shared dependency cell and class dispatch on
hot paths:

```text
IReactiveReference<T>
          ^
ReactiveValue<T>
          ^
Reference<T>, Computed<T>, ShallowReference<T>, CustomReference<T>
```

`ReactiveValue` remains useful and should not be replaced by interface-only engine internals.

A getter-only computed also reports its write policy through `IReactiveReadOnly`:

```csharp
IReactiveReadOnly readOnly =
    (IReactiveReadOnly)doubled;

bool rejectsWrites = readOnly.IsReadOnly;
```

With the current interface shape, a getter-only computed still has the
`IReactiveReference<T>.Value` setter at compile time; a write is rejected by the implementation
(with the existing development warning/no-op behavior). If compile-time prevention is preferred,
the design needs a separate generic read-only-reference contract rather than only
`IReactiveReadOnly` metadata.

### 6.2 Writable computed and batching

```csharp
using Assimalign.Viu.Reactivity;

IReactiveReference<string> firstName =
    Reactive.Reference("Ada");

IReactiveReference<string> lastName =
    Reactive.Reference("Lovelace");

IReactiveReference<string> fullName = Reactive.Computed(
    () => $"{firstName.Value} {lastName.Value}",
    value =>
    {
        string[] parts = value.Split(' ', 2);
        firstName.Value = parts[0];
        lastName.Value =
            parts.Length > 1 ? parts[1] : string.Empty;
    });

fullName.Value = "Grace Hopper";

Reactive.StartBatch();
try
{
    firstName.Value = "Katherine";
    lastName.Value = "Johnson";
}
finally
{
    Reactive.EndBatch();
}
```

The batch coalesces downstream effect delivery. It does not make the two writes atomic for arbitrary
non-reactive code.

### 6.3 Watch with stale-work cleanup

```csharp
using System.Threading;

using Assimalign.Viu.Reactivity;

IReactiveReference<string> query =
    Reactive.Reference(string.Empty);

using WatchHandle watcher = Reactive.Watch(
    query,
    (value, previousValue, onCleanup) =>
    {
        CancellationTokenSource cancellation = new();
        onCleanup(() =>
        {
            cancellation.Cancel();
            cancellation.Dispose();
        });

        _ = LoadResultsAsync(
            value,
            cancellation.Token);
    },
    new WatchOptions
    {
        Immediate = true,
    });

query.Value = "component tree";
query.Value = "block tree";
```

Before the second request begins, the watcher cancels work registered by the first callback. Disposing
the `WatchHandle` stops the watcher and runs its final cleanup. The handle also preserves the existing
`Pause`, `Resume`, and `Stop` behavior.

The restored interface-facing overload should accept `IReactiveReference<T>`, rather than forcing a
consumer back to the concrete `ReactiveValue<T>` base.

### 6.4 Effect scope

```csharp
using System;

using Assimalign.Viu.Reactivity;

IReactiveReference<int> count = Reactive.Reference(0);

using EffectScope scope =
    Reactive.EffectScope(detached: true);

scope.Run(() =>
{
    Reactive.Effect(
        () => Console.WriteLine(count.Value));

    Reactive.Watch(
        count,
        (value, previousValue, _) =>
        {
            Console.WriteLine(
                $"{previousValue} -> {value}");
        });

    Reactive.OnScopeDispose(
        () => Console.WriteLine("Scope stopped."));
});

count.Value++;
scope.Stop();
count.Value++;
```

The last write changes `count.Value`, but the stopped effect and watcher no longer run.

`EffectScope` is the normal Vue-shaped developer API. `IReactiveEffectScope` is the abstraction used
where a package such as State needs to own a reactive lifetime without depending on a concrete
engine type:

```csharp
using System;

using Assimalign.Viu.Reactivity;

public sealed class SearchSession : IDisposable
{
    private readonly IReactiveEffectScope _scope;

    public SearchSession(IReactiveEffectScopeFactory scopes)
    {
        _scope = scopes.Create(isDetached: true);

        _scope.Run(() =>
        {
            Reactive.WatchEffect(
                () => Refresh(SelectedQuery.Value));
        });
    }

    public IReactiveReference<string> SelectedQuery { get; } =
        Reactive.Reference(string.Empty);

    public void Dispose()
    {
        _scope.Dispose();
    }
}
```

Neither scope causes a component tree to subscribe. Scope membership answers “who stops this
effect?” Reactive reads answer “which effect reruns after this write?”

### 6.5 Generated reactive object

```csharp
using System;

using Assimalign.Viu.Reactivity;

[Reactive]
public partial class UserProfile
{
    public partial string DisplayName { get; set; }

    public partial int SignInCount { get; set; }
}
```

Consumer code:

```csharp
using System;

using Assimalign.Viu.Reactivity;

UserProfile profile = new()
{
    DisplayName = "Ada",
    SignInCount = 1,
};

Reactive.Effect(
    () => Console.WriteLine(profile.DisplayName));

var references = profile.ToReferences();

IReactiveReference<string> displayName =
    references.DisplayName;

displayName.Value = "Grace";
```

The source generator emits tracking and triggering directly into the partial properties. It should
also emit `IReactiveReference<T>` values from `ToReferences()`. There is no runtime proxy,
reflection-based member discovery, or dynamic code generation.

### 6.6 Reactive collections

The element can itself use generated reactive members:

```csharp
using Assimalign.Viu.Reactivity;

[Reactive]
public partial class TaskItem
{
    public partial string Title { get; set; }

    public partial bool IsComplete { get; set; }
}
```

```csharp
using System;
using System.Linq;

using Assimalign.Viu.Reactivity;

ReactiveList<TaskItem> tasks = new();

IReactiveReference<int> remaining = Reactive.Computed(
    () => tasks.Count(task => !task.IsComplete));

Reactive.Effect(
    () => Console.WriteLine(
        $"{remaining.Value} remaining"));

TaskItem first = new()
{
    Title = "Review the abstraction examples",
};

tasks.Add(first);
first.IsComplete = true;
```

```csharp
using System;

using Assimalign.Viu.Reactivity;

ReactiveDictionary<string, decimal> prices = new()
{
    ["coffee"] = 3.50m,
    ["tea"] = 2.75m,
};

Reactive.Effect(
    () => Console.WriteLine(prices["coffee"]));

prices["coffee"] = 3.75m;
```

```csharp
using System;

using Assimalign.Viu.Reactivity;

ReactiveSet<int> selectedIdentifiers = new();

Reactive.Effect(
    () => Console.WriteLine(
        selectedIdentifiers.Contains(42)));

selectedIdentifiers.Add(42);
```

Collections track structural operations such as indices, keys, membership, count, and iteration.
They do not automatically make an arbitrary element object's members reactive; an element should use
generated reactive properties or its own references when member-level tracking is required.

## 7. State-management examples

The setup delegate receives `IStateContext`. This lets the store use application services while its
effects are collected by the store-owned scope:

```csharp
using System;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

StateStoreDefinition<ReportStore> reports = new(
    "reports",
    context =>
    {
        IReportClient reportClient =
            (IReportClient?)context.Services.GetService(
                typeof(IReportClient))
            ?? throw new InvalidOperationException(
                "The application did not provide IReportClient.");

        ReportStore store = new(reportClient);

        Reactive.WatchEffect(store.Refresh);
        Reactive.OnScopeDispose(store.Stop);

        return store;
    });
```

`context.Scope` is already the active scope while setup runs, so ordinary store authors do not need
to call `Scope.Run(...)` themselves. `context.Components` is available to advanced state
infrastructure that deliberately resolves component templates. `context.Owner` should not influence
an application store because it is merely the component that won the first-resolution race.

The scaffolded registry owns and disposes the store's reactive scope; it does not call `Dispose` on
the arbitrary object returned by setup. A store that owns external resources should register them
with `Reactive.OnScopeDispose(...)`, as `ReportStore.Stop` does above. If State instead adopts
automatic `IDisposable` store disposal, callback ordering and failure handling need to become an
explicit contract before implementation.

### 7.1 Application state

The definition is reusable metadata:

```csharp
public static StateStoreDefinition<CounterStore> Counter { get; } =
    new(
        "counter",
        context =>
        {
            CounterStore store = new();

            Reactive.WatchEffect(
                () => Persist(store.Count.Value));

            return store;
        });
```

The registry owns one instance per definition:

```csharp
CounterStore first =
    state.GetOrCreate(ApplicationState.Counter);

CounterStore second =
    state.GetOrCreate(ApplicationState.Counter);

bool sameInstance =
    ReferenceEquals(first, second); // true
```

A second application registry creates an independent instance:

```csharp
using IStateStoreRegistry secondApplicationState =
    new StateStoreRegistry(
        components,
        services,
        effectScopes);

CounterStore isolated =
    secondApplicationState.GetOrCreate(
        ApplicationState.Counter);

bool sharedAcrossApplications =
    ReferenceEquals(first, isolated); // false
```

One store can be stopped and forgotten without disposing the registry:

```csharp
bool removed =
    state.Remove(ApplicationState.Counter); // true

CounterStore recreated =
    state.GetOrCreate(ApplicationState.Counter);

bool reusedRemovedInstance =
    ReferenceEquals(first, recreated); // false
```

The final State implementation should preserve the existing Store lifetime topology: one detached
registry root scope with one child scope per initialized store. Disposing the registry stops the root
and every store. Removing one definition stops only that store's child scope.

### 7.2 Explicit isolated feature or route registry

Pure C# can use the same explicit-boundary pattern as the `.viu` checkout example:

```csharp
using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

public sealed class CheckoutBoundary :
    IComponentTemplate,
    IDisposable
{
    private readonly StateStoreRegistry _state;

    public CheckoutBoundary(
        IComponentFactory components,
        IServiceProvider services,
        IReactiveEffectScopeFactory effectScopes)
    {
        _state = new StateStoreRegistry(
            components,
            services,
            effectScopes);
    }

    public ComponentRenderer Setup(IComponentContext context)
    {
        CheckoutStore checkout = _state.GetOrCreate(
            CheckoutState.Definition,
            context);

        ComponentArguments arguments = new(
            new Dictionary<string, object?>
            {
                ["store"] = checkout,
            });

        return () => ComponentTree.Template<CheckoutForm>(
            arguments: arguments);
    }

    public void Dispose()
    {
        _state.Dispose();
    }
}
```

Core's mount-owned template policy disposes this boundary after unmount, which disposes its private
state registry. Descendants receive the scoped store explicitly.

For route scopes outside the render tree, a router or feature-session object can own the registry and
dispose it when navigation ends. The same rule applies: the object that creates the registry owns it.

### 7.3 Component-local state

```csharp
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

public sealed class SearchBox : IComponentTemplate
{
    public ComponentRenderer Setup(IComponentContext context)
    {
        IReactiveReference<string> query =
            Reactive.Reference(string.Empty);

        IReactiveReference<bool> isEmpty =
            Reactive.Computed(
                () => string.IsNullOrWhiteSpace(query.Value));

        Reactive.WatchEffect(
            () => PrefetchSuggestions(query.Value));

        return () => ComponentTree.Element(
            "output",
            children:
            [
                ComponentTree.Text(
                    isEmpty.Value
                        ? "Enter a search term."
                        : $"Searching for {query.Value}"),
            ]);
    }
}
```

Core must call `Setup` while the component's effect scope is current. The watcher is then
component-owned and stops on unmount. The references are ordinary objects; after the component and
its render closure are released, they are collected normally.

This deliberately bypasses State. The current design has no component-owned
`StateStoreDefinition`/registry lifetime; component-level state is a setup closure or a locally
created store object. Add a component-owned State API only if definition-based identity or the full
store member model is needed at that level.

### 7.4 Optional full Store-package member model

The earlier `CounterStore` is the lightweight setup style: any object containing references,
computeds, and methods can be a store. State should continue to support that as the simplest,
canonical authoring style.

Because State replaces `Assimalign.Viu.Store`, it must also retain the optional richer member model.
Developers who need `Patch`, `Reset`, `Subscribe`, and `OnAction` opt into a base class; this is a
second supported authoring style, not a competing registry design.

The target developer experience could use a `StateStore<TState>` base in the State namespace:

```csharp
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

[Reactive]
public partial class ManagedCounterState
{
    public partial int Count { get; set; }

    public partial int Step { get; set; }
}

public sealed class ManagedCounterStore :
    StateStore<ManagedCounterState>
{
    public ManagedCounterStore()
        : base(
            "managed-counter",
            () => new ManagedCounterState
            {
                Count = 0,
                Step = 1,
            },
            (target, source) =>
            {
                target.Count = source.Count;
                target.Step = source.Step;
            })
    {
        Doubled = Reactive.Computed(
            () => State.Count * 2);
    }

    public IReactiveReference<int> Doubled { get; }

    public void Increment()
    {
        RunAction(
            nameof(Increment),
            () => State.Count += State.Step);
    }
}

public static class ManagedState
{
    public static StateStoreDefinition<ManagedCounterStore> Counter { get; } =
        new(
            "managed-counter",
            _ => new ManagedCounterStore());
}
```

Consumption remains Pinia-shaped:

```csharp
ManagedCounterStore counter =
    state.GetOrCreate(ManagedState.Counter);

using StoreSubscription stateSubscription =
    counter.Subscribe(
        (mutation, currentState) =>
        {
            Save(currentState);
        },
        detached: true);

using StoreSubscription actionSubscription =
    counter.OnAction(
        action =>
        {
            action.After(
                _ => Log($"{action.Name} completed."));

            action.OnError(
                exception => Log(exception.Message));
        });

counter.Patch(
    currentState =>
    {
        currentState.Count = 5;
        currentState.Step = 2;
    });

counter.Reset();
```

`StateStore<TState>` is a proposed name; it is not scaffolded. Keeping the existing type name
`Store<TState>` inside the new `Assimalign.Viu.State` namespace is the lower-migration alternative.
The behavior should be preserved either way.

## 8. Update and lifetime walkthrough

Assume a parent and two children all read the same application store's `Count`:

```text
Application StateStoreRegistry
  -> CounterStore scope
       -> store-internal watch/effect

Parent component scope
  -> parent render effect reads Counter.Count

Child A component scope
  -> child A render effect reads Counter.Count

Child B component scope
  -> child B never reads Counter.Count
```

After `Counter.Count.Value++`:

1. The store-owned watcher is notified.
2. The parent render effect is scheduled.
3. Child A's render effect is scheduled.
4. Child B is not scheduled.
5. Each scheduled render produces the current `IComponent` subtree while allowing compiler-hoisted
   static or `v-once` values to be reused.
6. Core uses keys, kinds, patch flags, and block dynamic children to patch only the necessary DOM
   work.

“Scheduled” does not promise that the DOM is already updated when the setter returns. The final Core
scheduler should preserve Vue-style queued batching and define whether developers await it through a
`NextTick` counterpart.

After Child A unmounts:

- Child A's component scope stops, so its render effect and component-local watchers stop.
- The application `CounterStore` stays alive.
- The parent's subscription stays alive.
- The store-owned watcher stays alive until the store is removed or its registry is disposed.

After the application state registry is disposed:

- Store-owned effects and cleanup callbacks stop.
- Existing component render effects are not stopped by the store scope. Components should normally
  unmount before application-state disposal. Treating a later store read as an application-lifetime
  error is a usage rule only—the current store object and references do not detect registry disposal.

## 9. Design decisions exposed by these examples

The examples are implementable only after the following points are resolved:

1. **Generated context access:** adopt the reserved `Context` property and optional `OnSetup()` hook,
   or choose another explicit generated contract. The current generator does not expose setup
   context to `@script`.
2. **Component setup lifetime:** Core must guarantee that `IComponentTemplate.Setup` runs inside the
   mounted component's effect scope.
3. **Live arguments:** `Context.Arguments` must observe parent updates even though each
   `ComponentArguments` snapshot is immutable.
4. **Event listeners:** add listener bindings to `ITemplateComponent` and
   `ComponentTree.Template(...)`; `IComponentContext.Emit(...)` currently has no tree-carried parent
   callback to invoke.
5. **Slots and fallthrough attributes:** `IComponentContext` does not expose slots or fallthrough
   attributes. Slot-consuming script examples should wait until those contracts are defined.
6. **Lifecycle signatures:** a single `Action` registration is sufficient for ordinary mounted and
   unmounted callbacks, but it cannot model `ErrorCaptured` return behavior or asynchronous
   `ServerPrefetch`. Those phases need typed contracts or separate APIs.
7. **State resolution from components:** adopt the proposed State-owned `IStateStoreContext`
   capability on Core's concrete component context, or choose another single-source bridge. Requiring
   both `UseStateRegistry(...)` and a matching service-provider registration risks divergence.
8. **Scoped state:** separate registries support explicit isolation today. Implicit nearest-subtree
   resolution requires a new scope and lookup contract.
9. **Application-store owner:** when a caller passes an owner while initializing an application
   store, `IStateContext.Owner` becomes whichever component won that first-resolution race. The
   recommended global `Use(Context)` path must leave it null; ownership is meaningful only for an
   explicit isolated registry.
10. **Store lifetime topology:** preserve the existing detached registry root with per-store child
    scopes instead of creating every store as an unrelated detached scope.
11. **Effect-scope factory:** provide a first-party adapter or keep the seam internal so applications
    do not have to implement infrastructure merely to create a state registry.
12. **Store feature parity:** migrate `Patch`, `Reset`, `Subscribe`, `OnAction`, SSR requirements,
    and later persistence/plugin requirements into State before removing Store.
13. **Store object disposal:** decide whether a registry owns only its reactive scope or also invokes
    `IDisposable` on the returned store, including teardown order and exception behavior. Until then,
    store resources register through `Reactive.OnScopeDispose(...)`.
14. **Dynamic component lookup:** `IComponentFactory.Create(string)` cannot currently be represented
    as an unactivated tree request. If string-named dynamic components remain supported, the
    component tree needs a name-based template request or an explicit name-to-type resolution step.
15. **Reactive interface facade:** `Watch`, `Unref`, `IsRef`, `ToRef`, and multi-source watch APIs
    should accept the restored `IReactiveReference` contracts. Forced triggering should require a
    tracked-reference contract rather than silently accepting an arbitrary external implementation.
16. **Read-only reference typing:** getter-only computeds currently expose a setter through
    `IReactiveReference<T>` and report rejection through `IReactiveReadOnly`. Decide whether runtime
    metadata is sufficient or a generic read-only contract is warranted.
17. **Host and scheduler surface:** define the Browser/Core mount, unmount, and queued-update await
    APIs for the new `IApplication`; the abstraction scaffold currently ends at `Build()`.

These are design findings, not reasons to abandon the split. Making them explicit before the
shipping refactor prevents the compiler, renderer, State package, and component contracts from
settling on incompatible assumptions.
