using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Testing;

namespace Assimalign.Viu.Tests;

// Pins the application-builder services surface ([V01.01.03.24], [V01.01.03.27]): builder.Services /
// ConfigureServices / UseServiceContainer register app-level DI, Build() attaches the provider to the
// app (IApplicationContext.ServicesProvider) and freezes the container, and it is reachable from
// component Setup — while the Vue-semantic component-tree provide/inject stays untouched. Exercised
// through a minimal concrete builder over the in-memory test renderer.
public class ApplicationBuilderServicesTests : IDisposable
{
    private readonly TestRenderer _renderer = new();
    private readonly TestElement _container;
    private readonly TestSchedulerPump _pump;

    public ApplicationBuilderServicesTests()
    {
        Scheduler.Reset();
        _pump = TestSchedulerPump.Install();
        _container = _renderer.CreateContainer();
    }

    public void Dispose()
    {
        Scheduler.Reset();
        _pump.Dispose();
    }

    private sealed class Service
    {
        public int Id { get; init; }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    // A minimal concrete ApplicationBuilder over the test renderer — the analog of
    // BrowserApplicationBuilder/ServerApplicationBuilder. Attaches the built provider before applying
    // configuration, exactly as the platform builders do.
    private sealed class TestApplicationBuilder : ApplicationBuilder
    {
        private readonly Renderer<TestNode> _renderer;

        public TestApplicationBuilder(Renderer<TestNode> renderer, IComponent root)
            : base(root, null)
            => _renderer = renderer;

        public override Application<TestNode> Build()
        {
            var application = _renderer.CreateApplication(RootComponent, RootProperties);
            application.Context.ServicesProvider = BuildServiceProvider();
            ApplyConfiguration(application);
            return application;
        }
    }

    // A bring-your-own IServiceContainer over a plain dictionary — nothing to do with the default
    // provider. Build() returns the SAME provider instance every call, so the app-builder must attach it
    // verbatim.
    private sealed class FakeServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, object> _map = new();

        public FakeServiceContainer() => Provider = new FakeProvider(_map);

        public IServiceProvider Provider { get; }

        public IServiceContainer Add(ServiceRegistration registration)
        {
            _map[registration.ServiceType] = registration.Factory(Provider);
            return this;
        }

        public IServiceProvider Build() => Provider;

        private sealed class FakeProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _map;

            public FakeProvider(Dictionary<Type, object> map) => _map = map;

            public object? GetService(Type serviceType)
                => _map.TryGetValue(serviceType, out var value) ? value : null;
        }
    }

    private static TestComponent Leaf(string text = "leaf", Action<ComponentProperties, ComponentSetupContext>? onSetup = null)
        => new()
        {
            SetupFunction = (properties, context) =>
            {
                onSetup?.Invoke(properties, context);
                return () => VirtualNodeFactory.Text(text);
            },
        };

    [Fact]
    public void Build_AttachesServicesProvider_ResolvableFromApp()
    {
        var service = new Service { Id = 1 };
        var builder = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builder.Services.AddSingleton(service);

        var application = builder.Build();

        application.Context.ServicesProvider.ShouldNotBeNull();
        application.Context.ServicesProvider!.GetRequiredService<Service>().ShouldBeSameAs(service);
    }

    [Fact]
    public void ConfigureServices_RegistersServices()
    {
        var service = new Service { Id = 2 };
        var builder = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builder.ConfigureServices(services => services.AddSingleton(service));

        builder.Build().Context.ServicesProvider!.GetRequiredService<Service>().ShouldBeSameAs(service);
    }

    [Fact]
    public void UseServiceContainer_AttachesTheBuiltProviderVerbatim()
    {
        var service = new Service { Id = 3 };
        var fake = new FakeServiceContainer();
        var builder = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builder.UseServiceContainer(fake);
        builder.Services.AddSingleton(service); // now goes to the fake

        var application = builder.Build();

        // The exact IServiceProvider the BYO container returned is what the app exposes.
        application.Context.ServicesProvider.ShouldBeSameAs(fake.Provider);
        application.Context.ServicesProvider!.GetRequiredService<Service>().ShouldBeSameAs(service);
    }

    [Fact]
    public void Add_AfterBuild_Throws_FreezeSemantics()
    {
        // Freeze semantics ([V01.01.03.27]): Build() freezes the container, so a later registration throws
        // with an actionable message.
        var builder = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builder.Services.AddSingleton(new Service { Id = 5 });
        builder.Build();

        Should.Throw<InvalidOperationException>(() => builder.Services.AddSingleton(new Service { Id = 6 }))
            .Message.ShouldContain("before calling builder.Build()");
    }

    [Fact]
    public void Services_AreReachableFromComponentSetup()
    {
        var service = new Service { Id = 4 };
        Service? fromComposition = null;
        IServiceProvider? fromInstanceSurface = null;
        var root = Leaf(onSetup: (_, _) =>
        {
            fromComposition = DependencyInjection.GetRequiredService<Service>();
            fromInstanceSurface = ComponentInstance.Current!.Services;
        });
        var builder = new TestApplicationBuilder(_renderer.Renderer, root);
        builder.Services.AddSingleton(service);

        builder.Build().Mount(_container);

        fromComposition.ShouldBeSameAs(service);
        fromInstanceSurface.ShouldNotBeNull();
        fromInstanceSurface!.GetRequiredService<Service>().ShouldBeSameAs(service);
    }

    [Fact]
    public void TwoApplications_HaveIsolatedServiceInstances()
    {
        var builderA = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builderA.Services.AddSingleton(_ => new Service());
        var builderB = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builderB.Services.AddSingleton(_ => new Service());

        var serviceA = builderA.Build().Context.ServicesProvider!.GetRequiredService<Service>();
        var serviceB = builderB.Build().Context.ServicesProvider!.GetRequiredService<Service>();

        serviceA.ShouldNotBeSameAs(serviceB);
    }

    [Fact]
    public void Dispose_CascadesToOwnedDisposableSingleton()
    {
        var builder = new TestApplicationBuilder(_renderer.Renderer, Leaf());
        builder.Services.AddSingleton(_ => new TrackingDisposable());
        var application = builder.Build();
        var disposable = (TrackingDisposable)application.Context.ServicesProvider!.GetRequiredService<TrackingDisposable>();

        application.Dispose();

        disposable.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void GetService_OutsideSetup_WarnsAndReturnsNull()
    {
        // No active component instance: the composition function is inert (a dev warning, null result).
        DependencyInjection.GetService<Service>().ShouldBeNull();
    }

    [Fact]
    public void GetRequiredService_OutsideSetup_Throws()
    {
        Should.Throw<InvalidOperationException>(() => DependencyInjection.GetRequiredService<Service>());
    }

    [Fact]
    public void GetRequiredService_WhenAppHasNoServiceRegistration_Throws()
    {
        Service? captured = null;
        var threw = false;
        var root = Leaf(onSetup: (_, _) =>
        {
            try
            {
                captured = DependencyInjection.GetRequiredService<Service>();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
        });
        var builder = new TestApplicationBuilder(_renderer.Renderer, root);
        // No registration: the provider is empty but present, so GetRequiredService throws "not registered".

        builder.Build().Mount(_container);

        threw.ShouldBeTrue();
        captured.ShouldBeNull();
    }

    [Fact]
    public void ProvideInject_IsUntouched_WhenServicesArePresent()
    {
        // The Vue-semantic component-tree provide/inject must behave exactly as before even though the
        // app also has a service provider — the two mechanisms are independent.
        var key = new InjectionKey<string>("greeting");
        var childSetupRuns = 0;
        string? injected = null;

        var child = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                childSetupRuns++;
                injected = DependencyInjection.Inject(key);
                return static () => VirtualNodeFactory.Text("child");
            },
        };
        var root = new TestComponent
        {
            SetupFunction = (_, _) =>
            {
                DependencyInjection.Provide(key, "hello");
                return () => VirtualNodeFactory.Component(child);
            },
        };
        var builder = new TestApplicationBuilder(_renderer.Renderer, root);
        builder.Services.AddSingleton(new Service()); // services present but irrelevant to provide/inject

        builder.Build().Mount(_container);

        injected.ShouldBe("hello");
        childSetupRuns.ShouldBe(1);
    }
}
