using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.State.Tests;

// [STA-11] Descriptor input is immutable definition metadata and paths name ordinal JSON members.
public sealed class StateStorePersistenceDescriptorTests
{
    [Fact]
    public void Constructor_DefaultArguments_UsesLocalStorageAndIncludesAllMembers()
    {
        StateStorePersistenceDescriptor descriptor = new("preferences");

        descriptor.Key.ShouldBe("preferences");
        descriptor.StorageKind.ShouldBe(StateStorageKind.Local);
        descriptor.IncludePaths.ShouldBeEmpty();
        descriptor.ExcludePaths.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_MutablePathInputs_CopiesDefinitionMetadata()
    {
        List<string> included = ["profile.theme"];
        List<string> excluded = ["profile.secret"];
        StateStorePersistenceDescriptor descriptor = new(
            "preferences", StateStorageKind.Session, included, excluded);

        included[0] = "count";
        excluded.Clear();

        descriptor.StorageKind.ShouldBe(StateStorageKind.Session);
        descriptor.IncludePaths.ShouldBe(["profile.theme"]);
        descriptor.ExcludePaths.ShouldBe(["profile.secret"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_EmptyStorageKey_RejectsInput(string? key)
    {
        Should.Throw<ArgumentException>(() => new StateStorePersistenceDescriptor(key!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".profile")]
    [InlineData("profile.")]
    [InlineData("profile..theme")]
    public void Constructor_PathWithEmptySegment_RejectsIncludesAndExcludes(string path)
    {
        Should.Throw<ArgumentException>(
            () => new StateStorePersistenceDescriptor("preferences", includePaths: [path]));
        Should.Throw<ArgumentException>(
            () => new StateStorePersistenceDescriptor("preferences", excludePaths: [path]));
    }
}
