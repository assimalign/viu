using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;

using ViuApplication;

await new BrowserApplicationBuilder()
    .ConfigureApplication(options =>
    {
        options.RootComponent = ComponentNode.For<Counter>();
    })
    .Build()
    .RunAsync();
