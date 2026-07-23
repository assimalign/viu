using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;
using Assimalign.Viu.State;

namespace Assimalign.Viu.State.Tests;

public sealed class StateStoreRegistryTests
{
    [Fact]
    public void GetOrCreate_SameDefinition_UsesComponentsServicesAndOneDetachedScope()
    {
        MessageService message = new("state");
        ComponentFactory components = new(
            new DictionaryServiceProvider(
                new Dictionary<Type, object>
                {
                    [typeof(MessageService)] = message,
                }),
            Array.Empty<ComponentRegistration>());
        TestReactiveScopeFactory scopes = new();
        StateStoreRegistry registry = new(components, scopes);
        int setupRuns = 0;
        StateStoreDefinition<MessageStore> definition = new(
            "message",
            context =>
            {
                setupRuns++;
                context.Scope.ShouldBeSameAs(scopes.CreatedScopes[0]);
                context.Services.ShouldBeSameAs(components);
                MessageService service =
                    (MessageService)context.Services.GetService(typeof(MessageService))!;
                return new MessageStore(service.Message);
            });

        MessageStore first = registry.GetOrCreate(definition);
        MessageStore second = registry.GetOrCreate(definition);
        registry.Dispose();

        first.ShouldBeSameAs(second);
        first.Message.ShouldBe("state");
        setupRuns.ShouldBe(1);
        scopes.CreatedDetachedValues.ShouldBe(new[] { true });
        scopes.CreatedScopes[0].IsActive.ShouldBeFalse();
    }

    private sealed class MessageStore
    {
        internal MessageStore(string message)
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

        internal DictionaryServiceProvider(IReadOnlyDictionary<Type, object> services)
        {
            _services = services;
        }

        public object? GetService(Type serviceType)
        {
            _services.TryGetValue(serviceType, out object? service);
            return service;
        }
    }

    private sealed class TestReactiveScopeFactory : IReactiveScopeFactory
    {
        internal List<TestReactiveScope> CreatedScopes { get; } = new();

        internal List<bool> CreatedDetachedValues { get; } = new();

        public IReactiveScope Create(bool isDetached = false)
        {
            TestReactiveScope scope = new();
            CreatedScopes.Add(scope);
            CreatedDetachedValues.Add(isDetached);
            return scope;
        }
    }

    private sealed class TestReactiveScope : IReactiveScope
    {
        public bool IsActive { get; private set; } = true;

        public void Run(Action action)
        {
            action();
        }

        public TResult Run<TResult>(Func<TResult> function)
        {
            return function();
        }

        public void Stop()
        {
            IsActive = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
