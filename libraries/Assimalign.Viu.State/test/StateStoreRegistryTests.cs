using System;
using System.Collections.Generic;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreRegistryTests
{
    [Fact]
    public void GetOrCreate_SameDefinition_UsesOneDetachedRootAndOneAttachedStoreScope()
    {
        MessageService message = new("state");
        ComponentFactory components =
            new(Array.Empty<ComponentRegistration>());
        DictionaryServiceProvider services = new(
            new Dictionary<Type, object>
            {
                [typeof(MessageService)] = message,
            });
        TestReactiveEffectScopeFactory effectScopes = new();
        TestReactiveWatchScheduler watchScheduler = new();
        using StateStoreRegistry registry = new(
            components,
            services,
            effectScopes,
            watchScheduler);
        int setupRuns = 0;
        StateStoreDefinition<MessageStateStore> definition = new(
            "message",
            context =>
            {
                setupRuns++;
                context.Scope.ShouldBeSameAs(effectScopes.CreatedScopes[1]);
                context.Components.ShouldBeSameAs(components);
                context.Services.ShouldBeSameAs(services);
                context.WatchScheduler.ShouldBeSameAs(watchScheduler);
                context.Owner.ShouldBeNull();
                MessageService service =
                    (MessageService)context.Services.GetService(
                        typeof(MessageService))!;
                return new MessageStateStore(service.Message);
            });

        MessageStateStore first = registry.GetOrCreate(definition);
        MessageStateStore second = registry.GetOrCreate(definition);

        first.ShouldBeSameAs(second);
        first.Message.ShouldBe("state");
        setupRuns.ShouldBe(1);
        registry.Count.ShouldBe(1);
        effectScopes.CreatedDetachedValues.ShouldBe(
            new[]
            {
                true,
                false,
            });
        effectScopes.CreatedScopes[1].Parent.ShouldBeSameAs(
            effectScopes.CreatedScopes[0]);
    }

    [Fact]
    public void GetOrCreate_DifferentDefinitionWithSameKey_ThrowsTypedDuplicateError()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> first =
            StateStores.Define("counter", static () => new CounterStore());
        StateStoreDefinition<CounterStore> second =
            StateStores.Define("counter", static () => new CounterStore());

        registry.GetOrCreate(first);

        DuplicateStateStoreKeyException exception =
            Should.Throw<DuplicateStateStoreKeyException>(
                () => registry.GetOrCreate(second));
        exception.StateStoreKey.ShouldBe("counter");
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreate_SetupThrows_StopsChildScopeWithoutRegisteringEntry()
    {
        TestReactiveEffectScopeFactory effectScopes = new();
        using StateStoreRegistry registry = new(
            StateStoreTestSupport.Components,
            StateStoreTestSupport.Services,
            effectScopes);
        StateStoreDefinition<CounterStore> definition = new(
            "counter",
            _ => throw new InvalidOperationException("boom"));

        Should.Throw<InvalidOperationException>(
            () => registry.GetOrCreate(definition));

        registry.Count.ShouldBe(0);
        effectScopes.CreatedScopes.Count.ShouldBe(2);
        effectScopes.CreatedScopes[0].IsActive.ShouldBeTrue();
        effectScopes.CreatedScopes[1].IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Remove_StopsOnlyTheSelectedStoreAndNextUseCreatesAFreshInstance()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> firstDefinition =
            StateStores.Define("first", static () => new CounterStore());
        StateStoreDefinition<CounterStore> secondDefinition =
            StateStores.Define("second", static () => new CounterStore());
        CounterStore first = firstDefinition.Use(registry);
        CounterStore second = secondDefinition.Use(registry);

        firstDefinition.Dispose(registry).ShouldBeTrue();
        CounterStore rebuilt = firstDefinition.Use(registry);

        rebuilt.ShouldNotBeSameAs(first);
        secondDefinition.Use(registry).ShouldBeSameAs(second);
        registry.Count.ShouldBe(2);
    }

    [Fact]
    public void Dispose_StopsRootAndEveryChild_ClearsActiveRegistryAndIsIdempotent()
    {
        TestReactiveEffectScopeFactory effectScopes = new();
        StateStoreRegistry registry = new(
            StateStoreTestSupport.Components,
            StateStoreTestSupport.Services,
            effectScopes);
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        definition.Use(registry);
        StateStores.SetActiveRegistry(registry);

        Should.NotThrow(
            () =>
            {
                registry.Dispose();
                registry.Dispose();
            });

        registry.IsDisposed.ShouldBeTrue();
        registry.Count.ShouldBe(0);
        effectScopes.CreatedScopes.ShouldAllBe(scope => !scope.IsActive);
        StateStores.ActiveRegistry.ShouldBeNull();
        Should.Throw<ObjectDisposedException>(
            () => definition.Use(registry));
    }

    private sealed class MessageStateStore
    {
        internal MessageStateStore(string message)
        {
            Message = message;
        }

        internal string Message { get; }
    }

    private sealed class MessageService
    {
        internal MessageService(string message)
        {
            Message = message;
        }

        internal string Message { get; }
    }

    private sealed class DictionaryServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _services;

        internal DictionaryServiceProvider(
            IReadOnlyDictionary<Type, object> services)
        {
            _services = services;
        }

        public object? GetService(Type serviceType)
        {
            _services.TryGetValue(serviceType, out object? service);
            return service;
        }
    }

    private sealed class TestReactiveEffectScopeFactory :
        IReactiveEffectScopeFactory
    {
        internal List<TestReactiveEffectScope> CreatedScopes { get; } = new();

        internal List<bool> CreatedDetachedValues { get; } = new();

        public IReactiveEffectScope Create(bool isDetached = false)
        {
            TestReactiveEffectScope scope = new(
                isDetached
                    ? null
                    : TestReactiveEffectScope.Current);
            CreatedScopes.Add(scope);
            CreatedDetachedValues.Add(isDetached);
            return scope;
        }
    }

    private sealed class TestReactiveEffectScope : IReactiveEffectScope
    {
        private readonly List<TestReactiveEffectScope> _children = new();

        internal TestReactiveEffectScope(
            TestReactiveEffectScope? parent)
        {
            Parent = parent;
            parent?._children.Add(this);
        }

        internal static TestReactiveEffectScope? Current { get; private set; }

        internal TestReactiveEffectScope? Parent { get; }

        public bool IsActive { get; private set; } = true;

        public void Run(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            TestReactiveEffectScope? previous = Current;
            if (IsActive)
            {
                Current = this;
            }

            try
            {
                action();
            }
            finally
            {
                Current = previous;
            }
        }

        public TResult Run<TResult>(Func<TResult> function)
        {
            ArgumentNullException.ThrowIfNull(function);
            TestReactiveEffectScope? previous = Current;
            if (IsActive)
            {
                Current = this;
            }

            try
            {
                return function();
            }
            finally
            {
                Current = previous;
            }
        }

        public void Stop()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            foreach (TestReactiveEffectScope child in _children)
            {
                child.Stop();
            }
        }

        public void Dispose() => Stop();
    }
}
