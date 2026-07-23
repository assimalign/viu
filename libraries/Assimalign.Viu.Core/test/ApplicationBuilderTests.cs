using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Tests;

public sealed class ApplicationBuilderTests
{
    [Fact]
    public void Build_SuppliedResolvers_RemainIndependentBorrowedApplicationDecisions()
    {
        EmptyServiceProvider services = new();
        ComponentFactory components = new(Array.Empty<ComponentRegistration>());
        IComponent root = ComponentTree.Element("main");
        TestApplicationBuilder builder = new();

        IApplication application = builder
            .UseRootComponent(root)
            .UseComponentFactory(components)
            .UseServiceProvider(services)
            .Build();

        application.ShouldBeAssignableTo<IApplication<int>>();
        application.Context.RootComponent.ShouldBeSameAs(root);
        application.Context.Components.ShouldBeSameAs(components);
        application.Context.Services.ShouldBeSameAs(services);
        application.Context.Services.ShouldNotBeSameAs(components);
        application.Context.State.ShouldBeNull();
    }

    [Fact]
    public async Task MountAsync_InstallsPluginsBeforeHostInitializationAndRender()
    {
        List<string> order = [];
        RecordingPlugin plugin = new(order);
        TestApplication application = (TestApplication)new TestApplicationBuilder(order)
            .UseRootComponent(ComponentTree.Element("main"))
            .UseComponentFactory(new ComponentFactory(Array.Empty<ComponentRegistration>()))
            .UseServiceProvider(new EmptyServiceProvider())
            .Use(plugin)
            .Build();

        await application.MountAsync(42);

        order.ShouldBe(["plugin", "initialize", "mount:42"]);
        application.IsMounted.ShouldBeTrue();
        plugin.Installations.ShouldBe(1);

        await application.MountAsync(42);
        plugin.Installations.ShouldBe(1);
    }

    [Fact]
    public async Task UnmountAsync_UsesTheGenericHostLifecycle()
    {
        TestApplication application = (TestApplication)new TestApplicationBuilder()
            .UseRootComponent(ComponentTree.Element("main"))
            .UseComponentFactory(new ComponentFactory(Array.Empty<ComponentRegistration>()))
            .UseServiceProvider(new EmptyServiceProvider())
            .Build();
        await application.MountAsync(7);

        await application.UnmountAsync();

        application.IsMounted.ShouldBeFalse();
        application.RootContext.ShouldBeNull();
        application.UnmountCount.ShouldBe(1);
    }

    private sealed class TestApplicationBuilder : ApplicationBuilder
    {
        private readonly List<string> _order;

        internal TestApplicationBuilder(List<string>? order = null)
        {
            _order = order ?? [];
        }

        public override IApplication Build()
        {
            TestApplication application = new(CreateContext(), _order);
            ApplyConfiguration(application);
            return application;
        }
    }

    private sealed class TestApplication : Application<int>
    {
        private readonly List<string> _order;

        internal TestApplication(IApplicationContext context, List<string> order)
            : base(context)
        {
            _order = order;
        }

        internal int UnmountCount { get; private set; }

        protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _order.Add("initialize");
            return ValueTask.CompletedTask;
        }

        protected override IComponentContext? MountCore(int container)
        {
            _order.Add($"mount:{container}");
            return null;
        }

        protected override void UnmountCore()
        {
            UnmountCount++;
        }
    }

    private sealed class RecordingPlugin : IApplicationPlugin
    {
        private readonly List<string> _order;

        internal RecordingPlugin(List<string> order)
        {
            _order = order;
        }

        internal int Installations { get; private set; }

        public ValueTask InstallAsync(
            IApplication application,
            CancellationToken cancellationToken = default)
        {
            Installations++;
            _order.Add("plugin");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
