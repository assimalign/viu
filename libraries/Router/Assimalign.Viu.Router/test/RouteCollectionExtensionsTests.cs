using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;

namespace Assimalign.Viu.Router.Tests;

public sealed class RouteCollectionExtensionsTests
{
    [Fact]
    public void Add_NameAndTitle_CreateOrdinaryRouteAndCopyMetadata()
    {
        // [RTR-31] Explicit title wins, without mutating the input metadata.
        ICollection<RouteRecord> routes = new List<RouteRecord>();
        Dictionary<string, object?> metadata = new() { ["title"] = "old", ["role"] = "reader" };
        RouteRecord record = routes.Add("/", "HomePage", "Home", meta: metadata);
        routes.ShouldHaveSingleItem().ShouldBeSameAs(record);
        record.Name.ShouldBe("HomePage");
        record.Meta["title"].ShouldBe("Home");
        record.Meta["role"].ShouldBe("reader");
        metadata["title"].ShouldBe("old");
        ((ComponentNode)record.Component!).Component.RegisteredName.ShouldBe("HomePage");
    }

    [Fact]
    public void Add_TypedDefinition_UsesOverriddenRegistrationNameAndOptionalArguments()
    {
        IList<RouteRecord> routes = new List<RouteRecord>();
        RouteRecord child = new("child");
        RouteRecord record = routes.Add<HomePage>("/", "Home", [child], name: "home");
        record.Name.ShouldBe("home");
        record.Children.ShouldHaveSingleItem().ShouldBeSameAs(child);
        ((ComponentNode)record.Component!).Component.RegisteredName.ShouldBe("renamed-home");
    }

    private sealed class HomePage : IComponentDefinition
    {
        public static ComponentRegistration Registration { get; } = ComponentRegistration.Define(
            "renamed-home", new ComponentContract(), _ => _ => null);
    }
}
