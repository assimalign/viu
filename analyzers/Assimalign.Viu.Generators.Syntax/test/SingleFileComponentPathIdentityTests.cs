using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Generators.Syntax.Tests;

/// <summary>
/// Pins operating-system-aware source-path identity across naming, compatibility shadowing, and
/// development hot-reload metadata.
/// </summary>
public sealed class SingleFileComponentPathIdentityTests
{
    private const string RootNamespace = "Demo";
    private const string ViuSource = "<template>\n    <div>canonical</div>\n</template>\n";
    private const string VueSource = "<template><div>compatibility</div></template>\n";

    [Fact]
    public void Generate_BaseNamesDifferOnlyByCase_ShadowingFollowsOperatingSystemPathIdentity()
    {
        var canonical = new InMemoryAdditionalText(
            "C:/project/Components/Choice.viu",
            ViuSource);
        var compatibility = new InMemoryAdditionalText(
            "C:/project/Components/choice.vue",
            VueSource);
        var driver = GeneratorTestHarness.CreateDriver(
                ImmutableArray.Create<AdditionalText>(canonical, compatibility),
                RootNamespace,
                "C:/project")
            .RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            result.GeneratedSources.ShouldHaveSingleItem();
            result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("VIU1004");
        }
        else
        {
            result.GeneratedSources.Length.ShouldBe(2);
            result.Diagnostics.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Generate_UppercaseFormatExtensions_RemainCaseInsensitive()
    {
        var canonical = new InMemoryAdditionalText(
            "C:/project/Components/Choice.VIU",
            ViuSource);
        var compatibility = new InMemoryAdditionalText(
            "C:/project/Components/Choice.VUE",
            VueSource);
        var driver = GeneratorTestHarness.CreateDriver(
                ImmutableArray.Create<AdditionalText>(canonical, compatibility),
                RootNamespace,
                "C:/project")
            .RunGenerators(GeneratorTestHarness.CreateCompilation());
        var result = driver.GetRunResult().Results[0];

        result.Exception.ShouldBeNull();
        result.GeneratedSources.ShouldHaveSingleItem();
        result.GeneratedSources.Single().HintName.ShouldBe(
            "Components.Choice.SingleFileComponent.g.cs");
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("VIU1004");
    }

    [Fact]
    public void ResolveName_CaseChangedProjectDirectory_FollowsOperatingSystemPathIdentity()
    {
        const string filePath = "C:/project/Components/Choice.viu";
        var matching = SingleFileComponentNameResolver.Resolve(
            filePath,
            "C:/project",
            RootNamespace);
        var caseChanged = SingleFileComponentNameResolver.Resolve(
            filePath,
            "C:/Project",
            RootNamespace);
        var outside = SingleFileComponentNameResolver.Resolve(
            filePath,
            "C:/outside",
            RootNamespace);

        caseChanged.ShouldBe(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? matching
                : outside);
    }

    [Fact]
    public void ResolveHotReloadIdentifier_CaseChangedProjectDirectory_FollowsOperatingSystemPathIdentity()
    {
        const string filePath = "C:/project/Components/Choice.viu";
        var matching = SingleFileComponentHotReloadMetadataFactory.ResolveComponentIdentifier(
            filePath,
            "C:/project");
        var caseChanged = SingleFileComponentHotReloadMetadataFactory.ResolveComponentIdentifier(
            filePath,
            "C:/Project");
        var outside = SingleFileComponentHotReloadMetadataFactory.ResolveComponentIdentifier(
            filePath,
            "C:/outside");

        caseChanged.ShouldBe(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? matching
                : outside);
    }
}
