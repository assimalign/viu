using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreDefinitionTests : IDisposable
{
    public void Dispose()
    {
        StateStores.SetActiveRegistry(null);
    }

    [Fact]
    public void Define_ContextAwareSetup_CarriesMetadataAndRunsOncePerRegistry()
    {
        int setupRuns = 0;
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define(
                "counter",
                context =>
                {
                    setupRuns++;
                    context.Scope.IsActive.ShouldBeTrue();
                    return new CounterStore();
                });
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();

        // [STA-1], [STA-2] Definitions are reusable metadata; mutable instances are registry-owned.
        CounterStore first = definition.Use(registry);
        CounterStore second = definition.Use(registry);

        definition.Key.ShouldBe("counter");
        definition.Identifier.ShouldBe("counter");
        definition.Setup.ShouldNotBeNull();
        first.ShouldBeSameAs(second);
        setupRuns.ShouldBe(1);
    }

    [Fact]
    public void Define_ParameterlessSetup_PreservesAotSafeConvenience()
    {
        int setupRuns = 0;
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define(
                "counter",
                () =>
                {
                    setupRuns++;
                    return new CounterStore();
                });
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();

        definition.Use(registry);

        setupRuns.ShouldBe(1);
    }

    [Fact]
    public void Use_AcrossRegistries_ReturnsIsolatedInstances()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        using StateStoreRegistry firstRegistry =
            StateStoreTestSupport.CreateRegistry();
        using StateStoreRegistry secondRegistry =
            StateStoreTestSupport.CreateRegistry();

        // [STA-2] Definition metadata is shared while each registry owns its own mutable instance.
        CounterStore first = definition.Use(firstRegistry);
        CounterStore second = definition.Use(secondRegistry);
        first.Increment();

        first.ShouldNotBeSameAs(second);
        first.Count.Value.ShouldBe(1);
        second.Count.Value.ShouldBe(0);
    }

    [Fact]
    public void Use_UsesActiveRegistryOutsideComponentSetup()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStores.SetActiveRegistry(registry);

        // [STA-4] Argument-less resolution uses only the explicitly selected ambient registry.
        CounterStore store = definition.Use();

        store.ShouldBeSameAs(definition.Use(registry));
    }

    [Fact]
    public void Use_WithNoActiveRegistry_ThrowsDescriptiveError()
    {
        StateStores.SetActiveRegistry(null);
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(definition.Use);

        exception.Message.ShouldContain("counter");
    }

    [Fact]
    public void Use_ComponentServices_ResolvesConfiguredRegistry()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        RegistryServiceProvider services = new(registry);
        TestComponentContext context = new(services);

        // [STA-4], [CMP-33] State attaches through Services without a component capability protocol.
        CounterStore store = definition.Use(context);

        store.ShouldBeSameAs(definition.Use(registry));
    }

    [Fact]
    public void Use_ComponentServices_PrecedesAmbientRegistry()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        using StateStoreRegistry configuredRegistry =
            StateStoreTestSupport.CreateRegistry();
        using StateStoreRegistry ambientRegistry =
            StateStoreTestSupport.CreateRegistry();
        StateStores.SetActiveRegistry(ambientRegistry);
        TestComponentContext context = new(
            new RegistryServiceProvider(configuredRegistry));

        CounterStore store = definition.Use(context);

        store.ShouldBeSameAs(definition.Use(configuredRegistry));
        ambientRegistry.Count.ShouldBe(0);
    }

    [Fact]
    public void Use_ComponentWithoutConfiguredService_FallsBackToAmbientRegistry()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStores.SetActiveRegistry(registry);
        TestComponentContext context = new(services: null);

        CounterStore store = definition.Use(context);

        store.ShouldBeSameAs(definition.Use(registry));
    }

    [Fact]
    public void Use_ComponentWithoutReachableRegistry_ThrowsDescriptiveError()
    {
        StateStores.SetActiveRegistry(null);
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        TestComponentContext context = new(services: null);

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(
                () => definition.Use(context));

        exception.Message.ShouldContain("counter");
        exception.Message.ShouldContain("application services");
    }

    [Fact]
    public void Construction_NullOrEmptyKeyOrSetup_Throws()
    {
        Should.Throw<ArgumentException>(
            () => new StateStoreDefinition<CounterStore>(
                string.Empty,
                _ => new CounterStore()));
        Should.Throw<ArgumentException>(
            () => new StateStoreDefinition<CounterStore>(
                null!,
                _ => new CounterStore()));
        Should.Throw<ArgumentNullException>(
            () => new StateStoreDefinition<CounterStore>(
                "counter",
                null!));
        Should.Throw<ArgumentNullException>(
            () => StateStores.Define<CounterStore>(
                "counter",
                (Func<CounterStore>)null!));
    }

    private sealed class RegistryServiceProvider : IServiceProvider
    {
        private readonly IStateStoreRegistry _registry;

        internal RegistryServiceProvider(IStateStoreRegistry registry)
        {
            _registry = registry;
        }

        public object? GetService(Type serviceType)
            => serviceType == typeof(IStateStoreRegistry)
                ? _registry
                : null;
    }

    private sealed class TestComponentContext : ComponentContext
    {
        internal TestComponentContext(IServiceProvider? services)
        {
            Services = services;
        }

        public override ComponentBindings Bindings =>
            throw new NotSupportedException();

        public override ComponentLifecycle Lifecycle =>
            throw new NotSupportedException();

        public override ComponentContext? Parent => null;

        public override IServiceProvider? Services { get; }

        public override IReactiveEffectScope Scope =>
            throw new NotSupportedException();

        public override IReactiveWatchScheduler? WatchScheduler => null;

        public override void Emit(string name, params object?[] arguments)
        {
        }

        public override void Expose(object? value)
        {
        }

        public override void Warn(string message)
        {
        }

        protected override void OnWatchError(Exception exception)
        {
        }
    }
}
