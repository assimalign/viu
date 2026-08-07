using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tests;

public sealed class ComponentHostTests
{
    [Fact]
    public async Task RenderAsync_RawInvocation_ProducesNormalizedBindingsAndFreshTree()
    {
        var reference = ComponentReference.ForType(typeof(GreetingComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(parameters: new[] { new ComponentParameter("name") }),
                _ => new GreetingComponent()));
        var host = new ComponentHost(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["name"] = "Viu" });

        await using var scope = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation)));

        var output = scope.Tree.ShouldBeOfType<TextNode>();
        output.Text.ShouldBe("Hello Viu");
        scope.Context.Bindings.Parameters["name"].ShouldBe("Viu");
        scope.Context.Parent.ShouldBeNull();
    }

    [Fact]
    public async Task RenderAsync_NestedRequest_ParentScopeContextBecomesChildContextParent()
    {
        var reference = ComponentReference.ForType(typeof(GreetingComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(parameters: new[] { new ComponentParameter("name") }),
                _ => new GreetingComponent()));
        var host = new ComponentHost(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        var invocation = new ComponentInvocation(
            arguments: new Dictionary<string, object?> { ["name"] = "Viu" });

        await using var parent = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation)));
        await using var child = await host.RenderAsync(
            new ComponentRenderRequest(new ComponentNode(reference, invocation), parent));

        child.Context.Parent.ShouldBeSameAs(parent.Context);
    }

    private sealed class GreetingComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new TextNode($"Hello {context.Bindings.Parameters["name"]}");
    }
}
