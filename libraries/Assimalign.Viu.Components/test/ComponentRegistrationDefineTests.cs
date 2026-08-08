using System;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Components.Tests;

public sealed class ComponentRegistrationDefineTests
{
    [Fact]
    public void Define_SetupDelegate_ActivatorYieldsComponentThatRunsSetupAndRenderer()
    {
        var contract = new ComponentContract();
        ComponentContext? observedContext = null;
        ComponentRenderFrame? observedFrame = null;
        var registration = ComponentRegistration.Define(
            "greeting",
            contract,
            setupContext =>
            {
                observedContext = setupContext;
                return frame =>
                {
                    observedFrame = frame;
                    return new TextNode("Hello");
                };
            });

        var instance = registration.Activator(null);
        var context = new StubComponentContext();
        var renderer = instance.Setup(context);
        var renderFrame = new ComponentRenderFrame();
        var tree = renderer(renderFrame);

        registration.Reference.Kind.ShouldBe(ComponentReferenceKind.RegisteredName);
        registration.Reference.RegisteredName.ShouldBe("greeting");
        registration.Contract.ShouldBeSameAs(contract);
        observedContext.ShouldBeSameAs(context);
        observedFrame.ShouldBeSameAs(renderFrame);
        tree.ShouldBeOfType<TextNode>().Text.ShouldBe("Hello");
    }

    private sealed class StubComponentContext : ComponentContext
    {
        public override ComponentBindings Bindings { get; } = new();

        public override IServiceProvider? Services => null;

        public override ComponentLifecycle Lifecycle { get; } = new();

        public override IReactiveEffectScope Scope { get; } = new StubEffectScope();

        public override IReactiveWatchScheduler? WatchScheduler => null;

        public override ComponentContext? Parent => null;

        public override void Emit(string name, params object?[] arguments)
        {
        }

        public override void Expose(object? value)
        {
        }

        public override void Warn(string message)
        {
        }

        protected override void OnWatchError(Exception exception)
        {
        }
    }

    private sealed class StubEffectScope : IReactiveEffectScope
    {
        public bool IsActive => true;

        public void Run(Action action) => action();

        public TResult Run<TResult>(Func<TResult> function) => function();

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
