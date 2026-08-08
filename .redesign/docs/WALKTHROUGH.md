# End-to-end walkthrough

An application first defines an explicit, reflection-free component registration:

```csharp
ComponentRegistration greeting = ComponentRegistration.Define(
    "greeting",
    new ComponentContract(
        "Greeting",
        parameters: new[] { new ComponentParameter("name") }),
    context =>
    {
        string name = (string)context.Bindings.Parameters["name"]!;
        return _ => new ElementNode(
            new QualifiedName("strong"),
            children: new VirtualNode[] { new TextNode($"Hello {name}") });
    });

ComponentFactory components = new();
components.Register(greeting);
```

The parent creates only an immutable request:

```csharp
VirtualNode root = new ComponentNode(
    greeting.Reference,
    new ComponentInvocation(
        arguments: new Dictionary<string, object?> { ["name"] = "Viu" }));
```

Browser and Testing mount that same tree through Core. Server rendering uses the same registrations
without creating a persistent application lifetime:

```csharp
ServerRenderApplication application = new(root, components);
string html = await ServerRenderer.RenderToStringAsync(application);
```

For a routed application, a memory or browser history feeds `Router.CurrentRoute`; `RouterView`
resolves the matched `VirtualNode`, merges route arguments for `ComponentNode`, and carries explicit
outlet depth. Testing mounts this composition unchanged, while ServerRenderer can serialize the
resolved memory-history route and Testing can hydrate its marker-bearing result (`[RTR-3]`,
`[RTR-4]`, `[SSR-MARKERS-3]`, `[HYD-2]`).

The generated-code path differs only in who creates the registration and render plan. The staged
compiled fixture registers a generated root and a code-first child in the same `ComponentFactory`,
then proves a reactive update crosses that boundary.
