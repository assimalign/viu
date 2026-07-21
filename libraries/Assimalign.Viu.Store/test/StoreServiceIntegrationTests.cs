using System;

using Assimalign.Viu;
using Assimalign.Viu.Testing;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Store.Tests;

// Pins the additive service-based store path ([V01.01.03.24]): AddStore registers the registry through
// the app service provider (and keeps the plugin/provide parity), and UseStore() resolves
// service-first-then-provide. The existing provide-only path (StoreApplicationIntegrationTests) is
// unchanged. Exercised DOM-free through the test harness's Services option.
public sealed class StoreServiceIntegrationTests : IDisposable
{
    private readonly TestSchedulerPump _pump = TestSchedulerPump.Install();

    public void Dispose()
    {
        _pump.RunUntilIdle();
        _pump.Dispose();
        Stores.SetActiveRegistry(null);
    }

    // A recording IApplicationBuilder that captures Use/Provide and exposes a real service builder — no
    // renderer or interop, so it isolates what AddStore records.
    private sealed class RecordingApplicationBuilder : IApplicationBuilder
    {
        public IServiceProviderBuilder Services { get; } = new ServiceProviderBuilder();

        public int UseCount { get; private set; }

        public IComponentDefinition RootComponent => throw new NotSupportedException();

        public VirtualNodeProperties? RootProperties => null;

        public IApplicationBuilder Use(IPlugin plugin, object? options = null)
        {
            UseCount++;
            return this;
        }

        public IApplicationBuilder Provide<T>(InjectionKey<T> key, T value) => this;

        public IApplicationBuilder Provide(string key, object? value) => this;

        public IApplicationBuilder Component(string name, IComponentDefinition definition) => this;

        public IApplicationBuilder Directive(string name, IDirective directive) => this;

        public IApplicationBuilder ConfigureApplication(Action<ApplicationConfiguration> configure) => this;

        public IApplicationBuilder UseServiceProviderBuilder(IServiceProviderBuilder services) => this;

        public IApplicationBuilder ConfigureServices(Action<IServiceProviderBuilder> configure)
        {
            configure(Services);
            return this;
        }

        public IApplication Build() => throw new NotSupportedException();
    }

    [Fact]
    public void AddStore_RegistersRegistryAsAService_AndInstallsThePlugin()
    {
        var registry = new StoreRegistry();
        var builder = new RecordingApplicationBuilder();

        builder.AddStore(registry);

        // Service path: the registry resolves from the built provider.
        builder.Services.Build().GetRequiredService<StoreRegistry>().ShouldBeSameAs(registry);
        // Provide-path parity: the store plugin was installed exactly once.
        builder.UseCount.ShouldBe(1);
    }

    [Fact]
    public void UseStore_ResolvesFromTheServiceProvider_WhenRegisteredAsAService()
    {
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        CounterStore? resolved = null;
        var component = new SetupComponent
        {
            SetupFunction = () =>
            {
                resolved = useCounter.UseStore();
                return static () => VirtualNodeFactory.Text("root");
            },
        };
        var services = new ServiceProviderBuilder().AddSingleton(registry).Build();

        using var wrapper = ViuTest.Mount(component, new ComponentMountOptions { Services = services });

        resolved.ShouldNotBeNull();
        resolved.ShouldBeSameAs(useCounter.UseStore(registry));
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void UseStore_PrefersTheServiceRegistry_OverAProvidedRegistry()
    {
        var serviceRegistry = new StoreRegistry();
        var provideRegistry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        CounterStore? resolved = null;
        var component = new SetupComponent
        {
            SetupFunction = () =>
            {
                resolved = useCounter.UseStore();
                return static () => VirtualNodeFactory.Text("root");
            },
        };
        var services = new ServiceProviderBuilder().AddSingleton(serviceRegistry).Build();
        var options = new ComponentMountOptions { Services = services };
        options.Provide(StoreRegistry.InjectionKey, provideRegistry);

        using var wrapper = ViuTest.Mount(component, options);

        // Service-first: the store was resolved from the service registry, never the provided one.
        resolved.ShouldBeSameAs(useCounter.UseStore(serviceRegistry));
        serviceRegistry.Count.ShouldBe(1);
        provideRegistry.Count.ShouldBe(0);
    }

    [Fact]
    public void UseStore_FallsBackToProvide_WhenNoServiceRegistryIsRegistered()
    {
        // A tree with an empty service provider but a provided registry resolves the provided one —
        // the provide path is intact under the service-first probe.
        var registry = new StoreRegistry();
        var useCounter = Stores.DefineStore("counter", () => new CounterStore());
        CounterStore? resolved = null;
        var component = new SetupComponent
        {
            SetupFunction = () =>
            {
                resolved = useCounter.UseStore();
                return static () => VirtualNodeFactory.Text("root");
            },
        };
        var options = new ComponentMountOptions { Services = new ServiceProviderBuilder().Build() };
        options.Provide(StoreRegistry.InjectionKey, registry);

        using var wrapper = ViuTest.Mount(component, options);

        resolved.ShouldBeSameAs(useCounter.UseStore(registry));
        registry.Count.ShouldBe(1);
    }
}
