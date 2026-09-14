using System;
using System.Collections.Generic;
using System.Text.Json;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.State.Tests;

/// <summary>Observable plugin contracts specified by [STA-10] and [V01.01.09.04].</summary>
public sealed class StateStorePluginTests
{
    [Fact]
    public void Use_RegisteredPlugins_RunOnceInOrderWithoutRevisitingExistingStores()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        List<string> visits = new();
        StateStoreDefinition<object> existing = StateStores.Define("existing", static () => new object());
        StateStoreDefinition<object> first = StateStores.Define("first", static () => new object());
        StateStoreDefinition<object> second = StateStores.Define("second", static () => new object());
        existing.Use(registry);

        registry.Use(new CallbackPlugin(context => visits.Add("one:" + context.Key)))
            .ShouldBeSameAs(registry);
        registry.Use(new CallbackPlugin(context => visits.Add("two:" + context.Key)));
        first.Use(registry);
        first.Use(registry);
        existing.Use(registry);
        registry.Use(new CallbackPlugin(context => visits.Add("three:" + context.Key)));
        second.Use(registry);

        visits.ShouldBe(["one:first", "two:first", "one:second", "two:second", "three:second"]);
        registry.Count.ShouldBe(3);
    }

    [Fact]
    public void GetOrCreate_PluginsRegisteredDuringSetupOrApply_OnlyVisitLaterCreations()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        List<string> visits = new();
        registry.Use(new CallbackPlugin(context =>
        {
            visits.Add("first:" + context.Key);
            registry.Use(new CallbackPlugin(later => visits.Add("during-plugin:" + later.Key)));
        }));
        StateStoreDefinition<object> first = StateStores.Define("first", () =>
        {
            registry.Use(new CallbackPlugin(context => visits.Add("during-setup:" + context.Key)));
            return new object();
        });
        StateStoreDefinition<object> second = StateStores.Define("second", static () => new object());

        first.Use(registry);
        second.Use(registry);

        visits.ShouldBe(["first:first", "first:second", "during-setup:second", "during-plugin:second"]);
    }

    [Fact]
    public void Apply_ContextCarriesExactDefinitionStoreServicesAndCurrentStoreScope()
    {
        EmptyServices services = new();
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler, services);
        IStateContext? setupContext = null;
        StateStorePluginContext? observedContext = null;
        StateStoreDefinition<ModelCounterStateStore> definition = StateStores.Define("counter", context =>
        {
            setupContext = context;
            return new ModelCounterStateStore();
        });
        registry.Use(new CallbackPlugin(context =>
        {
            observedContext = context;
            context.Registry.ShouldBeSameAs(registry);
            context.Definition.ShouldBeSameAs(definition);
            context.Identifier.ShouldBe("counter");
            context.Key.ShouldBe("counter");
            context.Services.ShouldBeSameAs(services);
            context.StateContext.ShouldBeSameAs(setupContext);
            context.StateContext.WatchScheduler.ShouldBeSameAs(scheduler);
            context.Scope.ShouldBeSameAs(setupContext!.Scope);
            Reactive.CurrentScope.ShouldBeSameAs(context.Scope);
            StateStoreSetupRuntime.Current.ShouldBeSameAs(setupContext);
            context.TryGetStore<ModelCounterStateStore>(out ModelCounterStateStore? typedStore).ShouldBeTrue();
            typedStore.ShouldBeSameAs(context.Store);
            context.TryGetStore<string>(out string? unrelatedStore).ShouldBeFalse();
            unrelatedStore.ShouldBeNull();
            registry.Count.ShouldBe(0);
        }));

        ModelCounterStateStore store = definition.Use(registry);

        observedContext.ShouldNotBeNull();
        observedContext.Store.ShouldBeSameAs(store);
        StateStoreSetupRuntime.Current.ShouldBeNull();
    }

    [Fact]
    public void GetExtension_UsesExactTypeKeysAndRequiresTheOwningRegistry()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        using StateStoreRegistry otherRegistry = StateStoreTestSupport.CreateRegistry();
        DisposableExtension extension = new();
        StateStoreDefinition<object> definition = StateStores.Define("store", static () => new object());
        registry.Use(new CallbackPlugin(context =>
        {
            context.SetExtension<IDisposable>(extension);
            context.SetExtension(42);
            context.Registry.GetExtension<IDisposable>(context.Store).ShouldBeSameAs(extension);
            Should.Throw<InvalidOperationException>(() => context.SetExtension<IDisposable>(new DisposableExtension()));
            Should.Throw<ArgumentNullException>(() => context.SetExtension<string>(null!));
        }));

        object store = definition.Use(registry);

        registry.GetExtension<IDisposable>(store).ShouldBeSameAs(extension);
        registry.GetExtension<int>(store).ShouldBe(42);
        Should.Throw<InvalidOperationException>(() => registry.GetExtension<DisposableExtension>(store));
        Should.Throw<InvalidOperationException>(() => otherRegistry.GetExtension<IDisposable>(store));
        definition.Remove(registry).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => registry.GetExtension<IDisposable>(store));
        extension.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public void ScopeStop_DisposesOneExtensionInstanceOnceAcrossSeveralTypeKeys()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        DisposableExtension extension = new();
        StateStorePluginContext? captured = null;
        registry.Use(new CallbackPlugin(context =>
        {
            captured = context;
            context.SetExtension(extension);
            context.SetExtension<IDisposable>(extension);
        }));
        StateStoreDefinition<object> definition = StateStores.Define("store", static () => new object());
        object store = definition.Use(registry);

        captured!.Scope.Stop();
        captured.Scope.Stop();

        extension.DisposeCount.ShouldBe(1);
        Should.Throw<ObjectDisposedException>(() => registry.GetExtension<IDisposable>(store));
        Should.Throw<ObjectDisposedException>(() => captured.SetExtension("late"));
        definition.Remove(registry).ShouldBeTrue();
        registry.Dispose();
        extension.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public void RegistryDispose_DisposesExtensionsAndTheStoreEvenWhenAnExtensionThrows()
    {
        StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        DisposableExtension first = new(throwOnDispose: true);
        DisposableExtension second = new();
        DisposableExtension store = new();
        registry.Use(new CallbackPlugin(context =>
        {
            context.SetExtension(first);
            context.SetExtension<IDisposable>(second);
            context.SetExtension<object>(store);
        }));
        StateStores.Define("store", () => store).Use(registry);

        Should.Throw<InvalidOperationException>(registry.Dispose).Message.ShouldBe("extension cleanup");
        registry.Dispose();

        first.DisposeCount.ShouldBe(1);
        second.DisposeCount.ShouldBe(1);
        store.DisposeCount.ShouldBe(1);
        registry.IsDisposed.ShouldBeTrue();
        registry.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_HandlersSurviveCallerScopeStopAndEndWithTheStore()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        using EffectScope componentScope = Reactive.EffectScope();
        int notifications = 0;
        int actions = 0;
        StateStoreSubscription? subscription = null;
        StateStoreSubscription? actionSubscription = null;
        registry.Use(new CallbackPlugin(context =>
        {
            context.TryGetStore<ModelCounterStateStore>(out ModelCounterStateStore? store).ShouldBeTrue();
            subscription = store!.Subscribe((_, _) => notifications++);
            actionSubscription = store.OnAction(_ => actions++);
        }));
        StateStoreDefinition<ModelCounterStateStore> definition =
            StateStores.Define("counter", static () => new ModelCounterStateStore());
        ModelCounterStateStore store = componentScope.Run(() => definition.Use(registry));

        componentScope.Stop();
        store.Increment();

        notifications.ShouldBe(1);
        actions.ShouldBe(1);
        subscription!.IsActive.ShouldBeTrue();
        actionSubscription!.IsActive.ShouldBeTrue();
        definition.Remove(registry).ShouldBeTrue();
        store.Increment();
        notifications.ShouldBe(1);
        actions.ShouldBe(1);
        subscription.IsActive.ShouldBeFalse();
        actionSubscription.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Apply_Throws_StopsEarlierAttachmentsAndPreservesFailureDespiteCleanupErrors()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        DisposableExtension extension = new(throwOnDispose: true);
        DisposableModelStore? createdStore = null;
        StateStorePluginContext? captured = null;
        int notifications = 0;
        int actions = 0;
        int laterRuns = 0;
        registry.Use(new CallbackPlugin(context =>
        {
            captured = context;
            context.SetExtension(extension);
            DisposableModelStore store = (DisposableModelStore)context.Store;
            store.Subscribe((_, _) => notifications++);
            store.OnAction(_ => actions++);
        }));
        registry.Use(new CallbackPlugin(_ => throw new InvalidOperationException("plugin failure")));
        registry.Use(new CallbackPlugin(_ => laterRuns++));
        StateStoreDefinition<DisposableModelStore> definition = StateStores.Define(
            "counter", () => createdStore = new DisposableModelStore());

        Should.Throw<InvalidOperationException>(() => definition.Use(registry))
            .Message.ShouldBe("plugin failure");
        createdStore!.Increment();

        registry.Count.ShouldBe(0);
        captured!.Scope.IsActive.ShouldBeFalse();
        extension.DisposeCount.ShouldBe(1);
        createdStore.DisposeCount.ShouldBe(1);
        notifications.ShouldBe(0);
        actions.ShouldBe(0);
        laterRuns.ShouldBe(0);
        StateStoreSetupRuntime.Current.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreate_FailedPluginCreationCanBeRetriedWithoutStaleEntryOrKeyClaim()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        int attempts = 0;
        registry.Use(new CallbackPlugin(_ =>
        {
            if (++attempts == 1)
            {
                throw new InvalidOperationException("first attempt");
            }
        }));
        StateStoreDefinition<object> definition = StateStores.Define("store", static () => new object());

        Should.Throw<InvalidOperationException>(() => definition.Use(registry));
        object store = definition.Use(registry);

        definition.Use(registry).ShouldBeSameAs(store);
        attempts.ShouldBe(2);
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void Apply_RecursivelyResolvingTheSameDefinition_FailsCreationWithoutPartialEntry()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<object> definition = StateStores.Define("store", static () => new object());
        registry.Use(new CallbackPlugin(_ => definition.Use(registry)));

        Should.Throw<InvalidOperationException>(() => definition.Use(registry))
            .Message.ShouldContain("recursively");
        registry.Count.ShouldBe(0);
    }

    [Fact]
    public void Apply_ResolvingAnotherDefinitionWithTheSameKey_ThrowsDuplicateKey()
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<object> definition = StateStores.Define("store", static () => new object());
        StateStoreDefinition<object> duplicate = StateStores.Define("store", static () => new object());
        registry.Use(new CallbackPlugin(_ => duplicate.Use(registry)));

        Should.Throw<DuplicateStateStoreKeyException>(() => definition.Use(registry));
        registry.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_StopsScopeOrDisposesRegistry_DoesNotPublishTheStore(bool disposeRegistry)
    {
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        DisposableExtension store = new();
        registry.Use(new CallbackPlugin(context =>
        {
            if (disposeRegistry)
            {
                context.Registry.Dispose();
            }
            else
            {
                context.Scope.Stop();
            }
        }));
        StateStoreDefinition<DisposableExtension> definition = StateStores.Define("store", () => store);

        if (disposeRegistry)
        {
            Should.Throw<ObjectDisposedException>(() => definition.Use(registry));
        }
        else
        {
            Should.Throw<InvalidOperationException>(() => definition.Use(registry));
        }

        registry.Count.ShouldBe(0);
        store.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public void Use_DisposedRegistryOrNullPlugin_RejectsRegistration()
    {
        StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        Should.Throw<ArgumentNullException>(() => registry.Use(null!));
        registry.Dispose();

        Should.Throw<ObjectDisposedException>(() => registry.Use(new CallbackPlugin(_ => { })));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Persistence_EarlyCreationFlushThenRestore_NotifiesRestoredStateOnlyOnce(
        bool flushDuringSetup,
        bool flushAfterRestore)
    {
        // [STA-7], [STA-11]: an early flush can consume the initialization watcher; a later
        // restore queues it again, and that pending job must replace deferred direct delivery.
        TestReactiveWatchScheduler scheduler = new();
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry(scheduler);
        InMemoryStateStorage storage = new();
        storage.Write("saved", """{"version":1,"stores":{"counter":{"Count":40,"Step":2}}}""");
        List<int> observedCounts = new();
        StateStoreDefinition<ModelCounterStateStore> definition = StateStores.Define(
            "counter",
            () =>
            {
                ModelCounterStateStore store = new();
                store.Subscribe((_, state) => observedCounts.Add(state.Count));
                if (flushDuringSetup)
                {
                    store.State.Count = 1;
                    scheduler.RunUntilIdle();
                    observedCounts.ShouldBeEmpty();
                }

                return store;
            },
            new CounterSerializer(),
            new StateStorePersistenceDescriptor("saved"));
        registry.Use(new CallbackPlugin(context =>
        {
            if (!flushDuringSetup)
            {
                ((ModelCounterStateStore)context.Store).State.Count = 1;
                scheduler.RunUntilIdle();
                observedCounts.ShouldBeEmpty();
            }
        }));
        registry.Use(new StateStorePersistencePlugin(new StateStorePersistenceOptions(storage)));
        registry.Use(new CallbackPlugin(_ =>
        {
            if (flushAfterRestore)
            {
                scheduler.RunUntilIdle();
                observedCounts.ShouldBeEmpty();
            }
        }));

        ModelCounterStateStore result = definition.Use(registry);

        result.State.Count.ShouldBe(40);
        result.State.Step.ShouldBe(2);
        observedCounts.Count.ShouldBe(flushAfterRestore ? 1 : 0);
        scheduler.RunUntilIdle();
        observedCounts.ShouldBe([40]);
        result.State.Count = 41;
        scheduler.RunUntilIdle();
        observedCounts.ShouldBe([40, 41]);
    }

    [Fact]
    public void Persistence_DeferredSubscriberReads_DoNotTrackTheEffectThatCreatesTheStore()
    {
        // [STA-11] Deferred creation delivery retains Watch's tracking boundary.
        using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
        Reference<int> subscriberDependency = Reactive.Reference(0);
        int subscriberRuns = 0;
        int effectRuns = 0;
        StateStoreDefinition<ModelCounterStateStore> definition = StateStores.Define(
            "counter",
            () =>
            {
                ModelCounterStateStore store = new();
                store.Subscribe((_, _) =>
                {
                    subscriberRuns++;
                    _ = subscriberDependency.Value;
                });
                store.State.Count = 1;
                return store;
            },
            new CounterSerializer(),
            new StateStorePersistenceDescriptor("saved"));

        using ReactiveEffect effect = Reactive.Effect(() =>
        {
            effectRuns++;
            definition.Use(registry);
        });
        subscriberDependency.Value = 1;

        subscriberRuns.ShouldBe(1);
        effectRuns.ShouldBe(1);
    }

    private sealed class CounterSerializer : IStateStoreSerializer<ModelCounterStateStore>
    {
        public void Serialize(Utf8JsonWriter writer, ModelCounterStateStore stateStore)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Count", stateStore.State.Count);
            writer.WriteNumber("Step", stateStore.State.Step);
            writer.WriteEndObject();
        }

        public void Restore(ModelCounterStateStore stateStore, JsonElement state)
        {
            int count = state.GetProperty("Count").GetInt32();
            int step = state.GetProperty("Step").GetInt32();
            stateStore.Patch(value =>
            {
                value.Count = count;
                value.Step = step;
            });
        }
    }

    private sealed class CallbackPlugin : IStateStorePlugin
    {
        private readonly Action<StateStorePluginContext> _apply;

        internal CallbackPlugin(Action<StateStorePluginContext> apply) => _apply = apply;

        public void Apply(StateStorePluginContext context) => _apply(context);
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class DisposableExtension : IDisposable
    {
        private readonly bool _throwOnDispose;

        internal DisposableExtension(bool throwOnDispose = false) => _throwOnDispose = throwOnDispose;

        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (_throwOnDispose)
            {
                throw new InvalidOperationException("extension cleanup");
            }
        }
    }

    private sealed class DisposableModelStore : StateStore<CounterState>, IDisposable
    {
        internal DisposableModelStore() : base("counter", new CounterState())
        {
        }

        internal int DisposeCount { get; private set; }

        internal void Increment() => RunAction(nameof(Increment), () => State.Count++);

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("store cleanup");
        }
    }
}
