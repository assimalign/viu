using System.IO;
using System.Threading.Tasks;

using Assimalign.Viu;
using Assimalign.Viu.Components;
using Assimalign.Viu.Reactivity;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class ServerRendererTests
{
    [Fact]
    public async Task RenderAsync_ComponentInvocation_UsesCoreRenderScopeWithoutFriendAccess()
    {
        var reference = ComponentReference.ForType(typeof(ParagraphComponent));
        var components = new ComponentFactory();
        components.Register(
            new ComponentRegistration(
                reference,
                new ComponentContract(),
                _ => new ParagraphComponent()));
        var host = new ComponentHost(
            new ComponentRuntimeOptions(components, new ImmediateWatchScheduler()));
        var renderer = new ServerRenderer(host);
        using var writer = new StringWriter();

        await renderer.RenderAsync(new ComponentNode(reference), writer);

        writer.ToString().ShouldBe("<p>Hello</p>");
    }

    private sealed class ParagraphComponent : IComponent
    {
        public ComponentRenderer Setup(ComponentContext context) =>
            _ => new ElementNode(
                new QualifiedName("p"),
                children: new VirtualNode[] { new TextNode("Hello") });
    }
}
