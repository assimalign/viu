using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentEventListenerTests
{
    [Fact]
    public void Listener_SynchronousHandler_CarriesHandlerWithoutTaskContract()
    {
        object? received = null;
        ComponentEventHandler handler = value => received = value;

        ComponentEventListener listener = new(handler);

        listener.IsAsynchronous.ShouldBeFalse();
        listener.Handler.ShouldBeSameAs(handler);
        listener.AsynchronousHandler.ShouldBeNull();

        listener.Handler!("saved");
        received.ShouldBe("saved");
    }

    [Fact]
    public void Listener_AsynchronousHandler_ExposesTaskForCoreToObserve()
    {
        Task expectedTask = Task.CompletedTask;
        AsynchronousComponentEventHandler handler = _ => expectedTask;

        ComponentEventListener listener = new(handler);

        listener.IsAsynchronous.ShouldBeTrue();
        listener.Handler.ShouldBeNull();
        listener.AsynchronousHandler.ShouldBeSameAs(handler);
        listener.AsynchronousHandler!("saved").ShouldBeSameAs(expectedTask);
    }

    [Fact]
    public void ForArguments_SynchronousOnceHandler_CarriesAllArgumentsAndOnceMetadata()
    {
        IReadOnlyList<object?>? received = null;
        ComponentEventArgumentsHandler handler = arguments => received = arguments;

        ComponentEventListener listener =
            ComponentEventListener.ForArguments(handler, isOnce: true);

        listener.IsAsynchronous.ShouldBeFalse();
        listener.IsOnce.ShouldBeTrue();
        listener.ArgumentsHandler.ShouldBeSameAs(handler);
        listener.Handler.ShouldBeNull();
        listener.AsynchronousHandler.ShouldBeNull();
        listener.AsynchronousArgumentsHandler.ShouldBeNull();

        object?[] arguments = [7, "saved"];
        listener.ArgumentsHandler!(arguments);
        received.ShouldBeSameAs(arguments);
    }

    [Fact]
    public void ForAsynchronousArguments_TaskHandler_ExposesTaskForCoreToObserve()
    {
        Task expectedTask = Task.CompletedTask;
        AsynchronousComponentEventArgumentsHandler handler = _ => expectedTask;

        ComponentEventListener listener =
            ComponentEventListener.ForAsynchronousArguments(handler);

        listener.IsAsynchronous.ShouldBeTrue();
        listener.IsOnce.ShouldBeFalse();
        listener.AsynchronousArgumentsHandler.ShouldBeSameAs(handler);
        listener.AsynchronousArgumentsHandler!([]).ShouldBeSameAs(expectedTask);
    }

    [Fact]
    public void Metadata_Validators_ArePreserved()
    {
        System.Func<object?, bool> parameterValidator = value => value is int;
        System.Func<IReadOnlyList<object?>, bool> eventValidator =
            arguments => arguments.Count == 2;

        ComponentParameter parameter = new(
            "count",
            validator: parameterValidator);
        ComponentEvent componentEvent = new(
            "changed",
            eventValidator);

        parameter.Validator.ShouldBeSameAs(parameterValidator);
        componentEvent.Validator.ShouldBeSameAs(eventValidator);
    }
}
