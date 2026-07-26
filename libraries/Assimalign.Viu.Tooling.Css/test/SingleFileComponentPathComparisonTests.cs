using System;
using System.Runtime.InteropServices;

using Assimalign.Viu.Tooling.Css;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.Css.Tests;

/// <summary>
/// Pins the shared operating-system-aware path identity used by both single-file-component build hosts.
/// </summary>
public sealed class SingleFileComponentPathComparisonTests
{
    [Fact]
    public void ComparisonForOperatingSystem_Windows_UsesOrdinalIgnoreCase()
    {
        SingleFileComponentPathComparison.ComparisonForOperatingSystem(isWindows: true)
            .ShouldBe(StringComparison.OrdinalIgnoreCase);
        SingleFileComponentPathComparison.ComparerForOperatingSystem(isWindows: true)
            .Equals("C:/Project/Card.viu", "C:/project/Card.viu")
            .ShouldBeTrue();
    }

    [Fact]
    public void ComparisonForOperatingSystem_NonWindows_UsesOrdinal()
    {
        SingleFileComponentPathComparison.ComparisonForOperatingSystem(isWindows: false)
            .ShouldBe(StringComparison.Ordinal);
        SingleFileComponentPathComparison.ComparerForOperatingSystem(isWindows: false)
            .Equals("C:/Project/Card.viu", "C:/project/Card.viu")
            .ShouldBeFalse();
    }

    [Fact]
    public void Bundle_BaseNamesDifferOnlyByCase_ShadowingFollowsOperatingSystemPathIdentity()
    {
        var canonical = new SingleFileComponentStyleInput(
            "C:/project/Components/Choice.viu",
            "@style {\n.canonical-choice { color: red; }\n}\n");
        var compatibility = new SingleFileComponentStyleInput(
            "C:/project/Components/choice.vue",
            "<style>\n.compatibility-choice { color: blue; }\n</style>\n");

        var bundle = SingleFileComponentStyleBundler.Bundle(
            new[] { compatibility, canonical },
            "C:/project");

        bundle.ShouldNotBeNull();
        bundle!.ShouldContain(".canonical-choice");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            bundle.ShouldNotContain(".compatibility-choice");
        }
        else
        {
            bundle.ShouldContain(".compatibility-choice");
        }
    }

    [Fact]
    public void Bundle_UppercaseFormatExtensions_RemainCaseInsensitive()
    {
        var canonical = new SingleFileComponentStyleInput(
            "C:/project/Components/Choice.VIU",
            "@style {\n.canonical-choice { color: red; }\n}\n");
        var compatibility = new SingleFileComponentStyleInput(
            "C:/project/Components/Choice.VUE",
            "<style>\n.compatibility-choice { color: blue; }\n</style>\n");

        var bundle = SingleFileComponentStyleBundler.Bundle(
            new[] { compatibility, canonical },
            "C:/project");

        bundle.ShouldNotBeNull();
        bundle!.ShouldContain("Components/Choice.VIU");
        bundle.ShouldContain(".canonical-choice");
        bundle.ShouldNotContain("Components/Choice.VUE");
        bundle.ShouldNotContain(".compatibility-choice");
    }

    [Fact]
    public void ScopeIdentifier_CaseChangedProjectDirectory_FollowsOperatingSystemPathIdentity()
    {
        const string filePath = "C:/project/Components/Choice.viu";
        var matching = StyleScopeId.Resolve(filePath, "C:/project");
        var caseChanged = StyleScopeId.Resolve(filePath, "C:/Project");
        var outside = StyleScopeId.Resolve(filePath, "C:/outside");

        caseChanged.ShouldBe(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? matching
                : outside);
    }
}
