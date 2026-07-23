using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentFactoryTests
{
    [Fact]
    public void Create_RegisteredComponent_UsesOpaqueActivatorAndCreatesPerMountInstance()
    {
        MessageService message = new("hello");
        ComponentFactory factory = new(
            new[]
            {
                new ComponentRegistration(
                    typeof(MessageTemplate),
                    () => new MessageTemplate(message),
                    "message"),
            });

        MessageTemplate first = factory.Create<MessageTemplate>();
        IComponentTemplate second = factory.Create("message");

        first.ShouldNotBeSameAs(second);
        first.Message.ShouldBe("hello");
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

}
