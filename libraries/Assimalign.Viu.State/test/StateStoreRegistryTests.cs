using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreRegistryTests
{
    [Fact]
    public void Use_SameDefinitionAndRegistry_ReturnsOneRegistryOwnedStore()
    {
        using var registry = StateStores.CreateRegistry();
        var activationCount = 0;
        var definition = new StateStoreDefinition<CounterStore>(
            "counter",
            _ =>
            {
                activationCount++;
                return new CounterStore();
            });

        var first = definition.Use(registry);
        var second = definition.Use(registry);

        second.ShouldBeSameAs(first);
        activationCount.ShouldBe(1);
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreate_SameDefinition_UsesOneDetachedRootAndOneAttachedStoreScope()
    {
        MessageService message = new("state");
        DictionaryServiceProvider services = new(
            new Dictionary<Type, object>
            {
                [typeof(MessageService)] = message,
            });
        TestReactiveEffectScopeFactory effectScopes = new();
        TestReactiveWatchScheduler watchScheduler = new();
        using StateStoreRegistry registry = new(
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
                context.Services.ShouldBeSameAs(services);
                context.WatchScheduler.ShouldBeSameAs(watchScheduler);
                object? resolvedService = context.Services!.GetService(
                    typeof(MessageService));
                MessageService resolvedMessage =
                    resolvedService.ShouldBeOfType<MessageService>();
                return new MessageStateStore(resolvedMessage.Message);
            });

        // [STA-2] One definition in one registry is a reference-identity cache hit.
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
    public void GetOrCreate_DifferentDefinitionWithSameOrdinalKey_ThrowsTypedDuplicateError()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> first =
            StateStores.Define("counter", static () => new CounterStore());
        StateStoreDefinition<CounterStore> second =
            StateStores.Define("counter", static () => new CounterStore());

        registry.GetOrCreate(first);

        // [STA-2] A key collision never replaces the owning definition.
        DuplicateStateStoreKeyException exception =
            Should.Throw<DuplicateStateStoreKeyException>(
                () => registry.GetOrCreate(second));
        exception.StateStoreKey.ShouldBe("counter");
        registry.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreate_KeyComparison_IsOrdinalAndCaseSensitive()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> lower =
            StateStores.Define("counter", static () => new CounterStore());
        StateStoreDefinition<CounterStore> upper =
            StateStores.Define("Counter", static () => new CounterStore());

        CounterStore lowerStore = lower.Use(registry);
        CounterStore upperStore = upper.Use(registry);

        lowerStore.ShouldNotBeSameAs(upperStore);
        registry.Count.ShouldBe(2);
    }

    [Fact]
    public void GetOrCreate_SetupThrows_StopsChildScopeWithoutRegisteringEntry()
    {
        TestReactiveEffectScopeFactory effectScopes = new();
        using StateStoreRegistry registry = new(
            services: null,
            effectScopes);
        StateStoreDefinition<CounterStore> definition = new(
            "counter",
            _ => throw new InvalidOperationException("boom"));

        // [STA-2] Failed setup leaves no partial entry and stops its new child scope.
        Should.Throw<InvalidOperationException>(
            () => registry.GetOrCreate(definition));

        registry.Count.ShouldBe(0);
        effectScopes.CreatedScopes.Count.ShouldBe(2);
        effectScopes.CreatedScopes[0].IsActive.ShouldBeTrue();
        effectScopes.CreatedScopes[1].IsActive.ShouldBeFalse();
    }

    [Fact]
    public void GetOrCreate_SetupReturnsNull_StopsChildScopeWithoutRegisteringEntry()
    {
        TestReactiveEffectScopeFactory effectScopes = new();
        using StateStoreRegistry registry = new(
            services: null,
            effectScopes);
        StateStoreDefinition<CounterStore> definition = new(
            "counter",
            _ => null!);

        InvalidOperationException exception =
            Should.Throw<InvalidOperationException>(
                () => registry.GetOrCreate(definition));

        exception.Message.ShouldContain("counter");
        registry.Count.ShouldBe(0);
        effectScopes.CreatedScopes[1].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task GetOrCreate_ConcurrentIsolatedSetups_RestoreAmbientStateAndDisposeOnlyOwnedStores()
    {
        using Barrier setupBarrier = new(2);
        ConcurrentQueue<string> disposedStores = new();
        TaskCompletionSource firstReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task RunRequestAsync(
            string requestName,
            TaskCompletionSource ready,
            Task release)
        {
            using IDisposable reactivityIsolation = Reactive.EnterExecutionFlow();
            using IDisposable isolation = StateStores.EnterExecutionFlow();
            using StateStoreRegistry registry = StateStoreTestSupport.CreateRegistry();
            StateStores.ActiveRegistry = registry;
            StateStoreDefinition<ConcurrentDisposableStore> definition = StateStores.Define(
                requestName,
                context =>
                {
                    setupBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    StateStoreSetupRuntime.Current.ShouldBeSameAs(context);
                    StateStores.ActiveRegistry.ShouldBeSameAs(registry);
                    return new ConcurrentDisposableStore(requestName, disposedStores);
                });

            definition.Use(registry);
            ready.SetResult();
            await release.ConfigureAwait(false);
        }

        Task first = Task.Run(
            () => RunRequestAsync("first", firstReady, releaseFirst.Task));
        Task second = Task.Run(
            () => RunRequestAsync("second", secondReady, releaseSecond.Task));
        await Task.WhenAll(firstReady.Task, secondReady.Task)
            .WaitAsync(TimeSpan.FromSeconds(10));

        releaseFirst.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(10));
        disposedStores.ToArray().ShouldBe(["first"]);

        releaseSecond.SetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(10));
        disposedStores.ToArray().ShouldBe(["first", "second"]);
        StateStores.ActiveRegistry.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreate_StagedRestoreThrows_DisposesNewStoreAndPreservesRestoreFailure()
    {
        TestReactiveEffectScopeFactory effectScopes = new();
        using StateStoreRegistry registry = new(
            services: null,
            effectScopes);
        StateStorePayload payload = StateStorePayload.Parse(
            "{\"version\":1,\"stores\":{\"restore-failure\":{}}}");
        ThrowingDisposableStore? createdStore = null;
        StateStoreDefinition<ThrowingDisposableStore> definition = StateStores.Define(
            "restore-failure",
            () => createdStore = new ThrowingDisposableStore(),
            new ThrowingRestoreSerializer());
        registry.RestorePayload(payload);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => definition.Use(registry));

        exception.Message.ShouldBe("restore failure");
        createdStore.ShouldNotBeNull();
        createdStore.DisposeCount.ShouldBe(1);
        registry.Count.ShouldBe(0);
        effectScopes.CreatedScopes.Count.ShouldBe(2);
        effectScopes.CreatedScopes[0].IsActive.ShouldBeTrue();
        effectScopes.CreatedScopes[1].IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Remove_StopsOnlySelectedStoreAndNextUseCreatesFreshInstance()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<CounterStore> firstDefinition =
            StateStores.Define("first", static () => new CounterStore());
        StateStoreDefinition<CounterStore> secondDefinition =
            StateStores.Define("second", static () => new CounterStore());
        CounterStore first = firstDefinition.Use(registry);
        CounterStore second = secondDefinition.Use(registry);

        // [STA-2] Removing one definition ends only that child lifetime.
        firstDefinition.Remove(registry).ShouldBeTrue();
        CounterStore rebuilt = firstDefinition.Use(registry);

        rebuilt.ShouldNotBeSameAs(first);
        secondDefinition.Use(registry).ShouldBeSameAs(second);
        registry.Count.ShouldBe(2);
    }

    [Fact]
    public void Remove_DisposesMaterializedStore()
    {
        using StateStoreRegistry registry =
            StateStoreTestSupport.CreateRegistry();
        StateStoreDefinition<DisposableStore> definition =
            StateStores.Define("disposable", static () => new DisposableStore());
        DisposableStore store = definition.Use(registry);

        definition.Remove(registry).ShouldBeTrue();

        store.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_StopsRootAndChildrenClearsAmbientAndIsIdempotent()
    {
        TestReactiveEffectScopeFactory effectScopes = new();
        StateStoreRegistry registry = new(
            services: null,
            effectScopes);
        StateStoreDefinition<CounterStore> definition =
            StateStores.Define("counter", static () => new CounterStore());
        definition.Use(registry);
        StateStores.SetActiveRegistry(registry);

        // [STA-2] Registry disposal ends every store lifetime and is idempotent.
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

    private sealed class DisposableStore : IDisposable
    {
        internal bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class ConcurrentDisposableStore : IDisposable
    {
        private readonly ConcurrentQueue<string> _disposedStores;
        private readonly string _requestName;

        internal ConcurrentDisposableStore(
            string requestName,
            ConcurrentQueue<string> disposedStores)
        {
            _requestName = requestName;
            _disposedStores = disposedStores;
        }

        public void Dispose() => _disposedStores.Enqueue(_requestName);
    }

    private sealed class ThrowingDisposableStore : IDisposable
    {
        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("dispose failure");
        }
    }

    private sealed class ThrowingRestoreSerializer :
        IStateStoreSerializer<ThrowingDisposableStore>
    {
        public void Serialize(Utf8JsonWriter writer, ThrowingDisposableStore stateStore)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }

        public void Restore(ThrowingDisposableStore stateStore, JsonElement state) =>
            throw new InvalidOperationException("restore failure");
    }

    private sealed class MessageService
    {
        internal MessageService(string message)
        {
            Message = message;
        }

        internal string Message { get; }
    }

    private sealed class MessageStateStore
    {
        internal MessageStateStore(string message)
        {
            Message = message;
        }

        internal string Message { get; }
    }

    private sealed class TestReactiveEffectScope : IReactiveEffectScope
    {
        private readonly List<TestReactiveEffectScope> _children = new();

        internal TestReactiveEffectScope(TestReactiveEffectScope? parent)
        {
            Parent = parent;
            parent?._children.Add(this);
        }

        internal static TestReactiveEffectScope? Current { get; private set; }

        public bool IsActive { get; private set; } = true;

        internal TestReactiveEffectScope? Parent { get; }

        public void Dispose() => Stop();

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
    }

    private sealed class TestReactiveEffectScopeFactory : IReactiveEffectScopeFactory
    {
        internal List<bool> CreatedDetachedValues { get; } = new();

        internal List<TestReactiveEffectScope> CreatedScopes { get; } = new();

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

    private sealed class CounterStore
    {
    }
}
