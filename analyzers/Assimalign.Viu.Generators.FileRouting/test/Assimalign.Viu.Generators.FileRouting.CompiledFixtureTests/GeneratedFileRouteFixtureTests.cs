using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.FileRouting;
using Assimalign.Viu.Router;
using Assimalign.Viu.Testing;

using ViuRouter = Assimalign.Viu.Router.Router;

namespace Assimalign.Viu.Generators.FileRouting.CompiledFixtureTests;

/// <summary>
/// Compiles real page files through both generators and exercises their public registration and
/// route output through the ordinary host-free router and DOM-free renderer.
/// Pins <c>[CMP-6]</c>, <c>[CMP-26]</c>, <c>[RTR-1]</c>, <c>[RTR-4]</c>, and <c>[RTR-11]</c>.
/// </summary>
public sealed class GeneratedFileRouteFixtureTests
{
    [Fact]
    public void Create_AllPageReferences_ResolveFromTheCompiledComponentCatalog()
    {
        ComponentFactory components = CreateComponents();
        IReadOnlyList<RouteRecord> records = GeneratedViuFileRoutes.Create();
        RouteRecord[] flattened = Flatten(records).ToArray();

        flattened.Length.ShouldBe(8);
        foreach (RouteRecord record in flattened)
        {
            ComponentNode component = record.Component.ShouldBeOfType<ComponentNode>();
            components.TryResolve(component.Component, out ComponentRegistration? registration)
                .ShouldBeTrue();
            registration.ShouldNotBeNull();
            record.ComponentFactory.ShouldBeNull();
        }

        // [CMP-6] Generated registration includes ordinary components, while the add-on only
        // selects Pages. Eager requests remain names in that explicit registry, never type lookup.
        components.TryResolve(ComponentReference.ForName("DecorativeCard"), out _).ShouldBeTrue();
        flattened.Select(record => record.Component.ShouldBeOfType<ComponentNode>()
                .Component.RegisteredName)
            .ShouldNotContain("DecorativeCard");
    }

    [Theory]
    [InlineData("/", "index", "Index", 1)]
    [InlineData("/quick-start", "quick-start", "QuickStart", 1)]
    [InlineData("/blog/post", "blog-slug", "_Slug_", 1)]
    [InlineData("/blog/archive", "blog-archive", "Archive", 1)]
    [InlineData("/optional", "optional-filter", "__Filter__", 1)]
    [InlineData("/optional/recent", "optional-filter", "__Filter__", 1)]
    [InlineData("/files", "files-rest", "____Rest_", 1)]
    [InlineData("/files/guides/install", "files-rest", "____Rest_", 1)]
    [InlineData("/compatible", "compatible", "VuePage", 1)]
    [InlineData("/custom/42", "custom", "OverridePage", 1)]
    public void Resolve_CompiledPageConventions_SelectTheExpectedRegisteredComponent(
        string path,
        string name,
        string componentName,
        int matchedCount)
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using ViuRouter router = new(history, GeneratedViuFileRoutes.Create());

        RouteLocation location = router.Resolve(path);

        location.Name.ShouldBe(name);
        location.Matched.Count.ShouldBe(matchedCount);
        location.Matched[^1].Component.ShouldBeOfType<ComponentNode>()
            .Component.RegisteredName.ShouldBe(componentName);
        CreateComponents().Resolve(ComponentReference.ForName(componentName)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("/blog/post", "slug", "post")]
    [InlineData("/optional/recent", "filter", "recent")]
    [InlineData("/files/guides/install", "rest", "guides/install")]
    [InlineData("/custom/42", "id", "42")]
    public void Resolve_CompiledParameters_ForwardToAttributeDeclaredInputs(
        string path,
        string parameterName,
        string expectedValue)
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using ViuRouter router = new(history, GeneratedViuFileRoutes.Create());
        RouteLocation location = router.Resolve(path);

        RouteComponentArgumentsResolver arguments = location.Matched[^1].ArgumentsResolver
            .ShouldNotBeNull();

        arguments(location).ShouldNotBeNull()[parameterName].ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData("/", "File routing home")]
    [InlineData("/quick-start", "Quick start")]
    [InlineData("/blog/post", "post")]
    [InlineData("/optional", "")]
    [InlineData("/optional/recent", "recent")]
    [InlineData("/files/guides/install", "guides/install")]
    [InlineData("/compatible", "Compatible container page")]
    [InlineData("/custom/42", "42")]
    public async Task RouterView_GeneratedTable_MountsCompiledPagesAndBindsParameters(
        string path,
        string expectedText)
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using ViuRouter router = new(history, GeneratedViuFileRoutes.Create());
        (await router.PushAsync(path)).ShouldBeNull();

        using ComponentWrapper wrapper = ComponentTest.Mount(
            RouterView.Registration,
            new ComponentMountOptions
            {
                Components = CreateComponents(),
                Services = new RouterServiceProvider(router),
            });

        wrapper.Text().ShouldBe(expectedText);
    }

    [Fact]
    public async Task RouterView_CompiledNestedLayout_RendersAndRetainsInstancesAcrossParameters()
    {
        // [RTR-4] BlogLayout explicitly renders depth 1; the root outlet uses depth 0.
        // [CMP-26] The compiled child receives the route's same-named slug argument.
        // See README.md: the unchanged syntax compiler cannot compile a sibling layout file
        // together with its same-named directory, so this generated-shaped tree is explicit.
        using IRouterHistory history = RouterHistory.CreateMemory();
        using ViuRouter router = new(history, FileRoutes.Create(
        [
            new FileRouteDescriptor("/blog", "blog", "BlogLayout", children:
            [
                new FileRouteDescriptor(":slug", "blog-slug", "_Slug_", forwardParameters: true),
                new FileRouteDescriptor("archive", "blog-archive", "Archive"),
            ]),
        ]));
        (await router.PushAsync("/blog/first")).ShouldBeNull();
        using ComponentWrapper wrapper = ComponentTest.Mount(
            RouterView.Registration,
            new ComponentMountOptions
            {
                Components = CreateComponents(),
                Services = new RouterServiceProvider(router),
            });
        ComponentWrapper layout = wrapper.GetComponent<Components.BlogLayout>();
        ComponentWrapper post = wrapper.GetComponent<Pages.Blog._Slug_>();

        wrapper.Get(".blog-post").Text().ShouldBe("first");
        (await router.PushAsync("/blog/second")).ShouldBeNull();
        await wrapper.NextTickAsync();

        wrapper.Get(".blog-post").Text().ShouldBe("second");
        wrapper.GetComponent<Components.BlogLayout>().Instance.ShouldBeSameAs(layout.Instance);
        wrapper.GetComponent<Pages.Blog._Slug_>().Instance.ShouldBeSameAs(post.Instance);

        (await router.PushAsync("/blog/archive")).ShouldBeNull();
        await wrapper.NextTickAsync();

        wrapper.Get(".blog-archive").Text().ShouldBe("Archive");
        wrapper.GetComponent<Components.BlogLayout>().Instance.ShouldBeSameAs(layout.Instance);
        post.Exists().ShouldBeFalse();
    }

    private static ComponentFactory CreateComponents()
    {
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        components.Register(RouterView.Registration);
        components.Register(new ComponentRegistration(
            ComponentReference.ForName("RouterView"),
            RouterView.Registration.Contract,
            RouterView.Registration.Activator));
        return components;
    }

    private static IEnumerable<RouteRecord> Flatten(IReadOnlyList<RouteRecord> records)
    {
        foreach (RouteRecord record in records)
        {
            yield return record;
            foreach (RouteRecord child in Flatten(record.Children))
            {
                yield return child;
            }
        }
    }

    private sealed class RouterServiceProvider(ViuRouter router) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ViuRouter) ? router : null;
    }
}
