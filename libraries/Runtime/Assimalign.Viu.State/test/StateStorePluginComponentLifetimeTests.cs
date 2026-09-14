using System;
using System.Globalization;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.Testing;

namespace Assimalign.Viu.State.Tests;

// [STA-3], [STA-7], [STA-10]: real component teardown stops component attachments while
// plugin subscriptions remain bound to the registry-owned store scope.
public sealed class StateStorePluginComponentLifetimeTests
{
    [Fact]
    public async Task Unmount_PluginHandlersSurviveComponentTeardownAndStopWhenStoreIsRemoved()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(
            new ApplicationWatchScheduler());
        ObservingPlugin plugin = new();
        registry.Use(plugin);
        StateStoreDefinition<ModelCounterStateStore> definition = StateStores.Define(
            "component-plugin-counter", static () => new ModelCounterStateStore());
        CounterComponent component = new(definition);
        registry.Count.ShouldBe(0);

        using ComponentWrapper wrapper = ComponentTest.Mount(
            component,
            new ComponentMountOptions
            {
                Services = new RegistryServices(registry),
                State = registry,
            });
        ModelCounterStateStore store = component.Store.ShouldNotBeNull();
        IReactiveEffectScope componentScope = wrapper.Context.ShouldNotBeNull().Scope;
        IReactiveEffectScope storeScope = plugin.Scope.ShouldNotBeNull();
        storeScope.ShouldNotBeSameAs(componentScope);
        registry.Count.ShouldBe(1);
        plugin.ApplyCount.ShouldBe(1);
        wrapper.Text().ShouldBe("0");

        store.Increment();
        plugin.ActionCount.ShouldBe(1);
        component.ActionCount.ShouldBe(1);
        plugin.NotificationCount.ShouldBe(0);
        component.NotificationCount.ShouldBe(0);
        await wrapper.FlushAsync();

        wrapper.Text().ShouldBe("1");
        component.RenderCount.ShouldBe(2);
        plugin.NotificationCount.ShouldBe(1);
        component.NotificationCount.ShouldBe(1);

        wrapper.Unmount();
        await wrapper.FlushAsync();

        wrapper.Exists().ShouldBeFalse();
        componentScope.IsActive.ShouldBeFalse();
        component.Subscription.ShouldNotBeNull().IsActive.ShouldBeFalse();
        component.ActionSubscription.ShouldNotBeNull().IsActive.ShouldBeFalse();
        storeScope.IsActive.ShouldBeTrue();
        plugin.Subscription.ShouldNotBeNull().IsActive.ShouldBeTrue();
        plugin.ActionSubscription.ShouldNotBeNull().IsActive.ShouldBeTrue();
        registry.IsDisposed.ShouldBeFalse();
        definition.Use(registry).ShouldBeSameAs(store);
        plugin.ApplyCount.ShouldBe(1);

        store.Increment();
        plugin.ActionCount.ShouldBe(2);
        component.ActionCount.ShouldBe(1);
        plugin.NotificationCount.ShouldBe(1);
        await wrapper.FlushAsync();

        plugin.NotificationCount.ShouldBe(2);
        component.NotificationCount.ShouldBe(1);
        component.RenderCount.ShouldBe(2);
        definition.Remove(registry).ShouldBeTrue();
        registry.Count.ShouldBe(0);
        storeScope.IsActive.ShouldBeFalse();
        plugin.Subscription.ShouldNotBeNull().IsActive.ShouldBeFalse();
        plugin.ActionSubscription.ShouldNotBeNull().IsActive.ShouldBeFalse();

        store.Increment();
        await wrapper.FlushAsync();

        plugin.NotificationCount.ShouldBe(2);
        plugin.ActionCount.ShouldBe(2);
        component.NotificationCount.ShouldBe(1);
        component.ActionCount.ShouldBe(1);
        component.RenderCount.ShouldBe(2);
    }

    private sealed class ObservingPlugin : IStateStorePlugin
    {
        internal int ApplyCount { get; private set; }

        internal int NotificationCount { get; private set; }

        internal int ActionCount { get; private set; }

        internal IReactiveEffectScope? Scope { get; private set; }

        internal StateStoreSubscription? Subscription { get; private set; }

        internal StateStoreSubscription? ActionSubscription { get; private set; }

        public void Apply(StateStorePluginContext context)
        {
            context.TryGetStore<ModelCounterStateStore>(out ModelCounterStateStore? store)
                .ShouldBeTrue();
            ApplyCount++;
            Scope = context.Scope;
            Subscription = store!.Subscribe((_, _) => NotificationCount++);
            ActionSubscription = store.OnAction(_ => ActionCount++);
        }
    }

    private sealed class CounterComponent : IComponent
    {
        private readonly StateStoreDefinition<ModelCounterStateStore> _definition;

        internal CounterComponent(StateStoreDefinition<ModelCounterStateStore> definition)
        {
            _definition = definition;
        }

        internal ModelCounterStateStore? Store { get; private set; }

        internal int NotificationCount { get; private set; }

        internal int ActionCount { get; private set; }

        internal int RenderCount { get; private set; }

        internal StateStoreSubscription? Subscription { get; private set; }

        internal StateStoreSubscription? ActionSubscription { get; private set; }

        public ComponentRenderer Setup(ComponentContext context)
        {
            ModelCounterStateStore store = _definition.Use(context);
            Store = store;
            Subscription = store.Subscribe((_, _) => NotificationCount++);
            ActionSubscription = store.OnAction(_ => ActionCount++);
            return _ =>
            {
                RenderCount++;
                return new ElementNode(
                    new QualifiedName("span"),
                    children: [new TextNode(store.State.Count.ToString(CultureInfo.InvariantCulture))]);
            };
        }
    }

    private sealed class RegistryServices : IServiceProvider
    {
        private readonly IStateStoreRegistry _registry;

        internal RegistryServices(IStateStoreRegistry registry)
        {
            _registry = registry;
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IStateStoreRegistry) ? _registry : null;
    }
}
