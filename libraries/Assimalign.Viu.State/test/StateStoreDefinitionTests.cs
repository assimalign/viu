using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreDefinitionTests : IDisposable
{
    public void Dispose()
    {
        StateStores.SetActiveRegistry(null);
    }

    [Fact]
    public void Define_ContextAwareSetup_CarriesKeyAndRunsOncePerRegistry()
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

        CounterStore first = definition.Use(registry);
        CounterStore second = definition.Use(registry);

        definition.Key.ShouldBe("counter");
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

        CounterStore first = definition.Use(firstRegistry);
        CounterStore second = definition.Use(secondRegistry);
        first.Increment();

        first.ShouldNotBeSameAs(second);
        first.Count.Value.ShouldBe(1);
        second.Count.Value.ShouldBe(0);
    }

    [Fact]
    public void Use_UsesActiveRegistryOutsideAComponent()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStores.SetActiveRegistry(registry);

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
    public void Use_ComponentStateCapability_UsesItsRegistryWithoutRecordingOwner()
    {
        StateStoreDefinition<OwnerStateStore> definition = new(
            "owner",
            context => new OwnerStateStore(context.Owner));
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        TestComponentContext componentContext = new(registry);

        OwnerStateStore stateStore = definition.Use(componentContext);

        stateStore.Owner.ShouldBeNull();
    }

    [Fact]
    public void Use_ExplicitScopedRegistry_CanRecordComponentOwner()
    {
        StateStoreDefinition<OwnerStateStore> definition = new(
            "owner",
            context => new OwnerStateStore(context.Owner));
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        TestComponentContext componentContext = new(registry);

        OwnerStateStore stateStore =
            definition.Use(registry, componentContext);

        stateStore.Owner.ShouldBeSameAs(componentContext);
    }

    [Fact]
    public void Use_ComponentWithoutStateCapability_Throws()
    {
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        ComponentContextWithoutState context = new();

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(
                () => definition.Use(context));

        exception.Message.ShouldContain("Configure State");
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

    private sealed class OwnerStateStore
    {
        internal OwnerStateStore(IComponentContext? owner)
        {
            Owner = owner;
        }

        internal IComponentContext? Owner { get; }
    }

    private sealed class TestComponentContext :
        ComponentContextWithoutState,
        IStateStoreContext
    {
        internal TestComponentContext(IStateStoreRegistry registry)
        {
            State = registry;
        }

        public IStateStoreRegistry? State { get; }
    }

    private class ComponentContextWithoutState : IComponentContext
    {
        public IComponentArguments Arguments { get; } =
            new ComponentArguments();

        public IReadOnlyDictionary<string, ComponentSlot> Slots { get; } =
            new Dictionary<string, ComponentSlot>();

        public IComponentAttributeCollection Attributes { get; } =
            new ComponentAttributes();

        public IComponentFactory Components =>
            StateStoreTestSupport.Components;

        public IServiceProvider Services =>
            StateStoreTestSupport.Services;

        public IComponentLifecycle Lifecycle =>
            throw new NotSupportedException();

        public void Emit(string eventName, params object?[] arguments)
        {
        }
    }
}
