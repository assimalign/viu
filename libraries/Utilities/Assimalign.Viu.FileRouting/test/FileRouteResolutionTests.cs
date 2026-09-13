using System.Collections.Generic;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.FileRouting.Tests;

// Generated-shaped inputs exercise the real matcher/navigation seam ([RTR-1], [RTR-3], [RTR-4]).
public sealed class FileRouteResolutionTests
{
    [Theory]
    [InlineData("/", "index", "Home", 1)]
    [InlineData("/quick-start", "quick-start", "QuickStart", 1)]
    [InlineData("/article/hello", "article-slug", "_Slug_", 1)]
    [InlineData("/search", "search-term", "__Term__", 1)]
    [InlineData("/search/hello", "search-term", "__Term__", 1)]
    [InlineData("/files", "files-rest", "____Rest_", 1)]
    [InlineData("/files/one/two", "files-rest", "____Rest_", 1)]
    [InlineData("/blog", "blog-index", "Index", 2)]
    [InlineData("/blog/entry", "blog-entry", "Entry", 2)]
    [InlineData("/blog/archive/2026", "blog-archive-year", "_Year_", 3)]
    [InlineData("/guides/getting-started", "guides-getting-started", "GettingStarted", 1)]
    public async Task Create_GeneratedRouteShapes_ResolveThroughMemoryRouter(
        string path,
        string name,
        string componentName,
        int depth)
    {
        using IRouterHistory history = RouterHistory.CreateMemory();
        using Assimalign.Viu.Router.Router router = new(history, FileRoutes.Create(Descriptors()));

        NavigationFailure? failure = await router.PushAsync(path);

        failure.ShouldBeNull();
        RouteLocation location = router.CurrentRoute.Value;
        location.Name.ShouldBe(name);
        location.Path.ShouldBe(path);
        location.Matched.Count.ShouldBe(depth);
        location.Matched[depth - 1].Component.ShouldBeOfType<ComponentNode>()
            .Component.RegisteredName.ShouldBe(componentName);
        foreach (RouteRecord record in location.Matched)
        {
            record.ComponentFactory.ShouldBeNull();
        }
    }

    [Fact]
    public void Create_StaticBeforeDynamicAndCatchAllRanking_DoesNotDependOnDescriptorOrder()
    {
        FileRouteDescriptor[] descriptors =
        [
            new("/:rest(.*)*", "rest", "Rest", forwardParameters: true),
            new("/:slug", "slug", "Slug", forwardParameters: true),
            new("/about", "about", "About"),
        ];

        RouteMatcher matcher = new(FileRoutes.Create(descriptors));

        matcher.Resolve("/about").Name.ShouldBe("about");
        matcher.Resolve("/hello").Name.ShouldBe("slug");
        matcher.Resolve("/hello/world").Name.ShouldBe("rest");
    }

    [Theory]
    [InlineData("/article/hello", "slug", "hello")]
    [InlineData("/search/hello", "term", "hello")]
    [InlineData("/files/one/two", "rest", "one/two")]
    [InlineData("/blog/archive/2026", "year", "2026")]
    public void Create_ParameterizedShapes_ForwardSameNamedArguments(string path, string parameterName, string value)
    {
        RouteLocation location = new RouteMatcher(FileRoutes.Create(Descriptors())).Resolve(path);

        location.Matched[location.Matched.Count - 1].ArgumentsResolver!(location)![parameterName].ShouldBe(value);
    }

    private static IReadOnlyList<FileRouteDescriptor> Descriptors() =>
    [
        new("/", "index", "Home"),
        new("/article/:slug", "article-slug", "_Slug_", forwardParameters: true),
        new("/blog", "blog", "Blog", children:
        [
            new("", "blog-index", "Index"),
            new("archive", "blog-archive", "Archive", children:
            [
                new(":year", "blog-archive-year", "_Year_", forwardParameters: true),
            ]),
            new("entry", "blog-entry", "Entry"),
        ]),
        new("/files/:rest(.*)*", "files-rest", "____Rest_", forwardParameters: true),
        new("/guides/getting-started", "guides-getting-started", "GettingStarted"),
        new("/quick-start", "quick-start", "QuickStart"),
        new("/search/:term?", "search-term", "__Term__", forwardParameters: true),
    ];
}
