using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

namespace Assimalign.Viu.Core.Tests;

public sealed class AsynchronousComponentsTests
{
    [Fact]
    public void Definition_ExplicitRegistration_ActivatesFreshWrappersWithoutReflection()
    {
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent<WrapperIdentityComponent>(
                _ => Task.FromResult(AsynchronousComponentTarget.From<TargetComponent>()),
                "deferred-target");

        definition.ComponentType.ShouldBe(typeof(WrapperIdentityComponent));
        definition.Reference.ShouldBe(ComponentReference.ForName("deferred-target"));
        definition.Registration.Reference.ShouldBe(definition.Reference);
        IComponent first = definition.Registration.Activator(null);
        IComponent second = definition.Registration.Activator(null);
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public async Task ConcurrentMounts_SharedSuccessfulLoad_ProduceFreshTargetNodesWithRawInvocation()
    {
        int loadRuns = 0;
        TaskCompletionSource loaderStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<AsynchronousComponentTarget> loaderCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent<WrapperIdentityComponent>(
                _ =>
                {
                    loadRuns++;
                    loaderStarted.TrySetResult();
                    return loaderCompletion.Task;
                });
        ComponentFactory components = new();
        components.Register(definition.Registration);
        ComponentHost host = new(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        ComponentInvocation invocation = new(
            arguments: new Dictionary<string, object?> { ["value"] = 42 });
        ComponentNode requestNode = definition.CreateComponent(invocation);

        Task<IComponentRenderScope> firstRender = host.RenderAsync(
            new ComponentRenderRequest(requestNode)).AsTask();
        Task<IComponentRenderScope> secondRender = host.RenderAsync(
            new ComponentRenderRequest(requestNode)).AsTask();
        await loaderStarted.Task;
        loaderCompletion.SetResult(AsynchronousComponentTarget.From<TargetComponent>());

        await using IComponentRenderScope firstScope = await firstRender;
        await using IComponentRenderScope secondScope = await secondRender;
        ComponentNode firstTarget = firstScope.Tree.ShouldBeOfType<ComponentNode>();
        ComponentNode secondTarget = secondScope.Tree.ShouldBeOfType<ComponentNode>();

        loadRuns.ShouldBe(1);
        firstTarget.ShouldNotBeSameAs(secondTarget);
        firstTarget.Component.ShouldBe(ComponentReference.ForType(typeof(TargetComponent)));
        secondTarget.Component.ShouldBe(ComponentReference.ForType(typeof(TargetComponent)));
        firstTarget.Invocation.ShouldBeSameAs(invocation);
        secondTarget.Invocation.ShouldBeSameAs(invocation);
    }

    [Fact]
    public void Renderer_MountReference_TracksResolvedExposedSurfaceAndClearsOnUnmount()
    {
        using var host = new RendererParityHost();
        Renderer<RendererParityNode> renderer = host.CreateRenderer();
        object exposed = new();
        List<object?> firstValues = [];
        List<object?> secondValues = [];
        AsynchronousComponentDefinition definition =
            AsynchronousComponents.DefineAsynchronousComponent<WrapperIdentityComponent>(
                _ => Task.FromResult(
                    AsynchronousComponentTarget.From<ExposingTargetComponent>()));
        var components = new ComponentFactory();
        components.Register(definition.Registration);
        components.Register(
            new ComponentRegistration(
                ComponentReference.ForType(typeof(ExposingTargetComponent)),
                new ComponentContract(),
                _ => new ExposingTargetComponent(exposed)));
        ComponentNode initial = definition.CreateComponent(
            mountReference: firstValues.Add);
        var application = new ApplicationContext(
            new ApplicationOptions
            {
                RootComponent = initial,
                Components = components,
            });

        renderer.Render(initial, host.Container, application);

        firstValues.ShouldBe([exposed]);

        ComponentNode next = definition.CreateComponent(
            mountReference: secondValues.Add);
        renderer.Render(next, host.Container);

        firstValues.ShouldBe([exposed, null]);
        secondValues.ShouldBe([exposed]);

        renderer.Render(null, host.Container);

        secondValues.ShouldBe([exposed, null]);
    }

    private sealed class TargetComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => new TextNode("target");
    }

    private sealed class ExposingTargetComponent : IComponent
    {
        private readonly object _exposed;

        internal ExposingTargetComponent(object exposed)
        {
            _exposed = exposed;
        }

        public ComponentRenderer Setup(ComponentContext context)
        {
            context.Expose(_exposed);
            return static _ => new TextNode("target");
        }
    }

    private sealed class WrapperIdentityComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) => _ => null;
    }
}
