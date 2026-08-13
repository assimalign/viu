using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

using ViuApplication;

ComponentFactory components = new();
GeneratedViuComponents.Register(components);

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = new ComponentNode(
            ComponentReference.ForName("Counter"));
        options.Components = components;
    })
    .Build()
    .RunAsync();
