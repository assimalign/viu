using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentFactoryTests
{
    [Fact]
    public void Create_RegisteredComponent_UsesSharedServiceResolverAndCreatesPerMountInstance()
    {
        MessageService message = new("hello");
        DictionaryServiceProvider services = new(
            new Dictionary<Type, object>
            {
                [typeof(MessageService)] = message,
            });
        ComponentFactory factory = new(
            services,
            new[]
            {
                new ComponentRegistration(
                    typeof(MessageTemplate),
                    provider => new MessageTemplate(
                        (MessageService)provider.GetService(typeof(MessageService))!),
                    "message"),
            });

        MessageTemplate first = factory.Create<MessageTemplate>();
        IComponentTemplate second = factory.Create("message");

        first.ShouldNotBeSameAs(second);
        first.Message.ShouldBe("hello");
        factory.GetService(typeof(MessageService)).ShouldBeSameAs(message);
        factory.GetService(typeof(IServiceProvider)).ShouldBeSameAs(factory);
        factory.GetService(typeof(IComponentFactory)).ShouldBeSameAs(factory);
    }

    private sealed class MessageTemplate : IComponentTemplate
    {
        internal MessageTemplate(MessageService service)
        {
            Message = service.Message;
        }

        internal string Message { get; }

        public ComponentRenderer Setup(IComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return () => ComponentTree.Text(Message);
        }
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
}
