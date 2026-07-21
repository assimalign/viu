using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.Reactivity;
using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Component-level SSR: setup runs once server-side, props resolve, undeclared attrs fall through, and
/// nested/multi-root components serialize — pinned to <c>@vue/server-renderer</c>'s
/// <c>renderComponentVNode</c>/<c>renderComponentSubTree</c> and <c>renderComponentRoot</c>.
/// </summary>
public class ServerRendererComponentTests
{
    [Fact]
    public async Task Component_Setup_RunsOnceAndRenders()
    {
        var setupCount = 0;
        var component = new InlineComponent((_, _) =>
        {
            setupCount++;
            return () => VirtualNodeFactory.Element("div", "ready");
        });

        var html = await Ssr.RenderAsync(component);

        html.ShouldBe("<div>ready</div>");
        setupCount.ShouldBe(1);
    }

    [Fact]
    public async Task Component_DeclaredProperty_ResolvesFromRootProps()
    {
        var component = new InlineComponent(
            (properties, _) => () => VirtualNodeFactory.Element("h1", (string)properties["title"]!),
            properties: [new ComponentPropertyDefinition("title")]);

        var html = await ServerRenderer.RenderToStringAsync(component, VirtualNodeFactory.Properties(("title", "Hi")));

        html.ShouldBe("<h1>Hi</h1>");
    }

    [Fact]
    public async Task Component_UndeclaredProps_FallThroughOntoSingleRoot()
    {
        // No declared props: the class prop becomes a fallthrough attr merged onto the single element root.
        var component = new InlineComponent((_, _) => () => VirtualNodeFactory.Element("div", "x"));

        var html = await ServerRenderer.RenderToStringAsync(component, VirtualNodeFactory.Properties(("class", "box")));

        html.ShouldBe("<div class=\"box\">x</div>");
    }

    [Fact]
    public async Task Component_ComputedInSetup_EvaluatesExactlyOncePerRender()
    {
        // Run-count pin: a computed created in setup is evaluated once for the single server render — a
        // caching regression (re-evaluation) or an eager double-render would move this off 1.
        var evaluationCount = 0;
        var component = new InlineComponent((_, _) =>
        {
            var count = Reactive.Reference(21);
            var doubled = Reactive.Computed(() =>
            {
                evaluationCount++;
                return count.Value * 2;
            });
            return () => VirtualNodeFactory.Element("div", doubled.Value.ToString());
        });

        var html = await Ssr.RenderAsync(component);

        html.ShouldBe("<div>42</div>");
        evaluationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Component_Nested_SerializesChildSubtree()
    {
        var child = new InlineComponent((_, _) => () => VirtualNodeFactory.Element("span", "child"), name: "Child");
        var parent = new InlineComponent((_, _) => () =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Component(child)));

        var html = await Ssr.RenderAsync(parent);

        html.ShouldBe("<div><span>child</span></div>");
    }

    [Fact]
    public async Task Component_MultiRootFragment_IsWrappedInHydrationAnchors()
    {
        var component = new InlineComponent((_, _) => () => VirtualNodeFactory.Fragment(
            VirtualNodeFactory.Element("span", "a"),
            VirtualNodeFactory.Element("span", "b")));

        var html = await Ssr.RenderAsync(component);

        html.ShouldBe("<!--[--><span>a</span><span>b</span><!--]-->");
    }

    [Fact]
    public async Task Component_ProvideInject_ResolvesAppLevelProvide()
    {
        // App-level provide reaches a descendant's inject through the shared application context.
        var key = new InjectionKey<string>("greeting");
        var component = new InlineComponent((_, _) =>
        {
            var greeting = DependencyInjection.Inject(key, "default");
            return () => VirtualNodeFactory.Element("div", greeting!);
        });
        var application = new ServerApplication(component).Provide(key, "provided");

        var html = await ServerRenderer.RenderToStringAsync(application);

        html.ShouldBe("<div>provided</div>");
    }
}
