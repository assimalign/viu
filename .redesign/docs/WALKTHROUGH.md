# End-to-end walkthrough

This example follows one authored component through every lifetime of the adopted model
([`../../REDESIGN-REVIEW.md`](../../REDESIGN-REVIEW.md)). The source generator would normally emit
the reference, contract, and registration; they are written explicitly here so the ownership
boundaries remain visible.

## 1. Immutable identity and contract — Components

```csharp
ComponentReference reference =
    ComponentReference.ForType(typeof(GreetingComponent));

ComponentContract contract = new(
    displayName: "Greeting",
    parameters: new[] { new ComponentParameter("name") },
    events: new[] { new ComponentEvent("selected") });
```

These values do not activate anything. There is no style-scope identifier — scoped CSS is
deferred; reintroducing it is one additive contract member.

## 2. Registration — Components

```csharp
ComponentFactory components = new();
components.Register(
    new ComponentRegistration(
        reference,
        contract,
        services => new GreetingComponent()));
```

`ComponentActivator` is a generated or explicitly registered delegate. The `Type` token is an
identity key, never an input to reflective activation. Registration carries the contract, so the
runtime reads parameters and events before activation.

The code-first form skips the class entirely:
`ComponentRegistration.Define("greeting", contract, setup)` wraps a `ComponentSetup` delegate as
the activator — composition-only per
[ADR-0004](../../docs/adr/0004-composition-only-component-model.md), with no options-object form.

## 3. Host composition — Core

```csharp
ComponentHost host = new(
    new ComponentRuntimeOptions(
        components,
        new ImmediateWatchScheduler(),
        services: null));
```

No convention appears in the options: a state-store registry, when the application uses one, is
registered in `services` (or assigned to `StateStores.ActiveRegistry`) and reached through the
context's `Services` seam.

## 4. Parent-created immutable invocation — Components

```csharp
ComponentInvocation invocation = new(
    arguments: new Dictionary<string, object?>
    {
        ["name"] = "Viu",
        ["class"] = "welcome"
    });

ComponentNode node = new(reference, invocation);
```

The raw invocation carries both a declared parameter and an undeclared fallthrough binding. It is
safe to recreate on every parent render.

## 5. Authored behavior — Components contract, application code

```csharp
public sealed class GreetingComponent : IComponent
{
    public ComponentRenderer Setup(ComponentContext context)
    {
        string name = (string)context.Bindings.Parameters["name"]!;

        return _ => new ElementNode(
            new QualifiedName("button"),
            children: new VirtualNode[]
            {
                new TextNode($"Hello {name}")
            });
    }
}
```

`ComponentBindings.Resolve` split `name` into `Bindings.Parameters` and `class` into
`Bindings.FallthroughBindings`. A store, when needed, is one line inside `Setup` —
`CounterStore.Definition.Use(context)` — resolved through `Services` then the ambient registry,
with no cast and no bridge interface.

The renderer receives the mount's `ComponentRenderFrame` (discarded here as `_`). Compiled
templates call its block assembly and handler cache through that parameter — there is no ambient
render-helper state — while hand-built output like this carries `RenderPlan.None` and patches by
full diff unless the author supplies plans.

In the persistent renderer, the returned `ComponentRenderer` becomes the body of this mount's one
reactive render effect, invoked each time with the mount's frame. Its fresh output then enters
block-aware reconciliation.

## 6. One-shot server execution

```csharp
ServerRenderer serverRenderer = new(host);
using StringWriter writer = new();

await serverRenderer.RenderAsync(node, writer);
```

Internally, ServerRenderer asks `ComponentHost.RenderAsync` for an `IComponentRenderScope`,
serializes `scope.Tree`, and disposes the scope. Nested `ComponentNode` values repeat the same
operation with the current scope as parent. It never sees mounted engine internals, and — with
scoped CSS deferred — it emits no style-scope attributes.

The corresponding executable examples are:

- `libraries/Assimalign.Viu.Core/test/ComponentHostTests.cs`
- `libraries/Assimalign.Viu.ServerRenderer/test/ServerRendererTests.cs`
- `libraries/Assimalign.Viu.Components/test/ComponentBindingsResolveTests.cs`
