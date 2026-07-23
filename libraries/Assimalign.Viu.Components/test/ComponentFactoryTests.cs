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

    [Theory]
    [InlineData("raw-widget", typeof(RawNameTemplate))]
    [InlineData("camel-widget", typeof(CamelNameTemplate))]
    [InlineData("pascal-widget", typeof(PascalNameTemplate))]
    public void Create_HyphenatedName_ResolvesRawCamelAndPascalRegistrations(
        string name,
        Type expectedType)
    {
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(RawNameTemplate),
                static () => new RawNameTemplate(),
                "raw-widget"),
            new ComponentRegistration(
                typeof(CamelNameTemplate),
                static () => new CamelNameTemplate(),
                "camelWidget"),
            new ComponentRegistration(
                typeof(PascalNameTemplate),
                static () => new PascalNameTemplate(),
                "PascalWidget"),
        ]);

        IComponentTemplate template = factory.Create(name);

        template.GetType().ShouldBe(expectedType);
    }

    [Fact]
    public void Create_AliasEquivalentRegistrations_PrefersRawThenCamelThenPascal()
    {
        ComponentFactory factory = new(
        [
            new ComponentRegistration(
                typeof(RawNameTemplate),
                static () => new RawNameTemplate(),
                "my-widget"),
            new ComponentRegistration(
                typeof(CamelNameTemplate),
                static () => new CamelNameTemplate(),
                "myWidget"),
            new ComponentRegistration(
                typeof(PascalNameTemplate),
                static () => new PascalNameTemplate(),
                "MyWidget"),
        ]);
        ComponentFactory fallbackFactory = new(
        [
            new ComponentRegistration(
                typeof(CamelNameTemplate),
                static () => new CamelNameTemplate(),
                "myWidget"),
            new ComponentRegistration(
                typeof(PascalNameTemplate),
                static () => new PascalNameTemplate(),
                "MyWidget"),
        ]);

        factory.Create("my-widget").ShouldBeOfType<RawNameTemplate>();
        factory.Create("myWidget").ShouldBeOfType<CamelNameTemplate>();
        factory.Create("MyWidget").ShouldBeOfType<PascalNameTemplate>();
        fallbackFactory.Create("my-widget").ShouldBeOfType<CamelNameTemplate>();
    }

    [Fact]
    public void Constructor_DuplicateType_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () =>
            {
                _ = new ComponentFactory(
                [
                    new ComponentRegistration(
                        typeof(RawNameTemplate),
                        static () => new RawNameTemplate(),
                        "first-widget"),
                    new ComponentRegistration(
                        typeof(RawNameTemplate),
                        static () => new RawNameTemplate(),
                        "second-widget"),
                ]);
            });

        exception.Message.ShouldContain("registered more than once");
    }

    [Fact]
    public void Constructor_DuplicateRawName_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () =>
            {
                _ = new ComponentFactory(
                [
                    new ComponentRegistration(
                        typeof(RawNameTemplate),
                        static () => new RawNameTemplate(),
                        "duplicate-widget"),
                    new ComponentRegistration(
                        typeof(CamelNameTemplate),
                        static () => new CamelNameTemplate(),
                        "duplicate-widget"),
                ]);
            });

        exception.Message.ShouldContain("registered more than once");
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

    private abstract class NamedTemplate : IComponentTemplate
    {
        public ComponentRenderer Setup(IComponentContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return static () => ComponentTree.Comment();
        }
    }

    private sealed class RawNameTemplate : NamedTemplate
    {
    }

    private sealed class CamelNameTemplate : NamedTemplate
    {
    }

    private sealed class PascalNameTemplate : NamedTemplate
    {
    }
}
