using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

using Assimalign.Viu.Components;
using Assimalign.Viu.Router;

namespace Assimalign.Viu.FileRouting.Tests;

// These tests pin the add-on's descriptor boundary while preserving [RTR-1], [RTR-4], and [CMP-7].
public sealed class FileRoutesTests
{
    [Fact]
    public void Create_StaticDescriptor_PreservesIdentityWithoutActivatingOrForwarding()
    {
        FileRouteDescriptor descriptor = new("/quick-start", "quick-start", "QuickStart");

        RouteRecord record = FileRoutes.Create([descriptor])[0];

        record.Path.ShouldBe("/quick-start");
        record.Name.ShouldBe("quick-start");
        ComponentNode component = record.Component.ShouldBeOfType<ComponentNode>();
        component.Component.ShouldBe(ComponentReference.ForName("QuickStart"));
        component.Invocation.ShouldBeSameAs(ComponentInvocation.Empty);
        record.ArgumentsResolver.ShouldBeNull();
        record.Children.ShouldBeEmpty();
        record.ComponentFactory.ShouldBeNull();
    }

    [Fact]
    public void Create_ParameterDescriptor_ForwardsResolvedStringsIncludingRepeatableParameters()
    {
        IReadOnlyList<RouteRecord> records = FileRoutes.Create(
            [new("/users/:user/files/:rest(.*)*", "user-files-rest", "Files", forwardParameters: true)]);
        RouteLocation location = new RouteMatcher(records).Resolve("/users/jane/files/a/b");

        IReadOnlyDictionary<string, object?> arguments = records[0].ArgumentsResolver!(location)!;

        arguments["user"].ShouldBe("jane");
        arguments["rest"].ShouldBe("a/b");
        records[0].ComponentFactory.ShouldBeNull();
    }

    [Fact]
    public void Create_NestedDescriptors_PreservesRelativeAndEmptyPathsAndDeclaredOrder()
    {
        FileRouteDescriptor descriptor = new("/blog", "blog", "Blog", children:
        [
            new("", "blog-index", "Index"),
            new(":slug", "blog-slug", "_Slug_", forwardParameters: true),
        ]);

        RouteRecord parent = FileRoutes.Create([descriptor])[0];

        parent.Children.Count.ShouldBe(2);
        parent.Children[0].Path.ShouldBe("");
        parent.Children[0].Name.ShouldBe("blog-index");
        parent.Children[1].Path.ShouldBe(":slug");
        parent.Children[1].Name.ShouldBe("blog-slug");
        new RouteMatcher([parent]).Resolve("/blog").Matched.ShouldBe([parent, parent.Children[0]]);
        new RouteMatcher([parent]).Resolve("/blog/example").Matched.ShouldBe([parent, parent.Children[1]]);
    }

    [Fact]
    public void Constructor_ChildrenAreChangedLater_RetainsOriginalSnapshot()
    {
        FileRouteDescriptor original = new("first", "first", "First");
        FileRouteDescriptor[] children = [original];
        FileRouteDescriptor descriptor = new("/parent", "parent", "Parent", children: children);

        children[0] = new("second", "second", "Second");

        descriptor.Children.ShouldBe([original]);
        FileRoutes.Create([descriptor])[0].Children[0].Path.ShouldBe("first");
    }

    [Fact]
    public void Create_SameDescriptorsTwice_ProducesIndependentRecordIdentities()
    {
        FileRouteDescriptor[] descriptors = [new("/", "index", "Index")];

        RouteRecord first = FileRoutes.Create(descriptors)[0];
        RouteRecord second = FileRoutes.Create(descriptors)[0];

        first.ShouldNotBeSameAs(second);
        first.Component.ShouldNotBeSameAs(second.Component);
    }

    [Fact]
    public void Create_EmptyList_ProducesAnEmptyTable()
    {
        FileRoutes.Create([]).ShouldBeEmpty();
    }

    [Fact]
    public void Create_NullList_RejectsMissingInput()
    {
        Should.Throw<ArgumentNullException>(() => FileRoutes.Create(null!)).ParamName.ShouldBe("descriptors");
    }

    [Fact]
    public void Create_NullDescriptor_RejectsInvalidInput()
    {
        Should.Throw<ArgumentNullException>(() => FileRoutes.Create([null!])).ParamName.ShouldBe("descriptors");
    }

    [Fact]
    public void Constructor_NullChild_RejectsInvalidInput()
    {
        Should.Throw<ArgumentNullException>(() => new FileRouteDescriptor("/", "index", "Index", children: [null!]))
            .ParamName.ShouldBe("children");
    }

    [Theory]
    [InlineData(null, "index", "Index", "path")]
    [InlineData("/", null, "Index", "name")]
    [InlineData("/", "index", null, "componentName")]
    public void Constructor_NullIdentity_RejectsInvalidInput(string? path, string? name, string? componentName, string parameterName)
    {
        Should.Throw<ArgumentNullException>(() => new FileRouteDescriptor(path!, name!, componentName!))
            .ParamName.ShouldBe(parameterName);
    }

    [Theory]
    [InlineData("", "Index", "name")]
    [InlineData("index", "", "componentName")]
    public void Constructor_EmptyIdentity_RejectsInvalidInput(string name, string componentName, string parameterName)
    {
        Should.Throw<ArgumentException>(() => new FileRouteDescriptor("/", name, componentName))
            .ParamName.ShouldBe(parameterName);
    }
}
