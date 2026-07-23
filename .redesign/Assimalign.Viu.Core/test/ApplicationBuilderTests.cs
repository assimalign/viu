using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Tests;

public sealed class ApplicationBuilderTests
{
    [Fact]
    public void Build_SuppliedFactory_IsBothComponentActivatorAndServiceProvider()
    {
        EmptyServiceProvider services = new();
        ComponentFactory components = new(services, Array.Empty<ComponentRegistration>());
        IComponent root = ComponentTree.Element("main");
        ApplicationBuilder builder = new();

        IApplication application = builder
            .UseRootComponent(root)
            .UseComponentFactory(components)
            .Build();

        application.Context.RootComponent.ShouldBeSameAs(root);
        application.Context.Components.ShouldBeSameAs(components);
        application.Context.Services.ShouldBeSameAs(components);
        application.Context.State.ShouldBeNull();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
