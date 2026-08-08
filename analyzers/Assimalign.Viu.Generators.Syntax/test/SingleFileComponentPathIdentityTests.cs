using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.CodeAnalysis;

using Assimalign.Viu.Compiler.SingleFileComponent;

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
        // [VUE-7] decides how many components exist here, and it is deliberately operating-system
        // dependent: on Windows the two files are one path identity, so the .vue peer is shadowed and a
        // single component is emitted; everywhere else they are two components. The two-component branch
        // is where [SFC-CG-5] earns its keep - Choice and choice are ONE hint name to Roslyn's
        // case-insensitive AddSource comparison, and before [V01.01.06.10.01] the second AddSource threw
        // and killed the run. The platform-independent pin for that rule is
        // HintNameCollisionTests.TwoFilesWhoseBaseNamesDifferOnlyByCase_BothEmit, which uses two .viu
        // files - a pair no shadowing rule can ever merge - so the collision forms on every host.
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
        var componentSources = result.GeneratedSources
            .Where(source => source.HintName.EndsWith(
                ".SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .ToArray();

        result.Exception.ShouldBeNull();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            componentSources.ShouldHaveSingleItem();
            result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("VIU1004");
        }
        else
        {
            componentSources.Length.ShouldBe(2);
            result.Diagnostics.ShouldBeEmpty();
            componentSources
                .Select(source => source.HintName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
                .ShouldBe(2);
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
        var componentSources = result.GeneratedSources
            .Where(source => source.HintName.EndsWith(
                ".SingleFileComponent.g.cs",
                StringComparison.Ordinal))
            .ToArray();

        result.Exception.ShouldBeNull();
        componentSources.ShouldHaveSingleItem();
        componentSources.Single().HintName.ShouldBe(
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
