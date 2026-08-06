using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins the SDK-mirroring symbol derivation ([V01.01.12.23], #259) that keeps the editor
/// compilation's <c>#if</c> surface identical to the build's.
/// </summary>
public class ViuProjectPreprocessorSymbolTests
{
    [Fact]
    public void Derive_Net10Debug_DefinesTheFrameworkFamilyAndConfigurationSymbols()
    {
        var symbols = ViuProjectPreprocessorSymbols.Derive("net10.0", "Debug");

        symbols.ShouldContain("NET");
        symbols.ShouldContain("NETCOREAPP");
        symbols.ShouldContain("NET10_0");
        symbols.ShouldContain("NET5_0_OR_GREATER");
        symbols.ShouldContain("NET10_0_OR_GREATER");
        symbols.ShouldContain("NETCOREAPP3_1_OR_GREATER");
        symbols.ShouldContain("DEBUG");
        symbols.ShouldContain("TRACE");
    }

    [Fact]
    public void Derive_ReleaseConfiguration_OmitsDebugButKeepsTrace()
    {
        var symbols = ViuProjectPreprocessorSymbols.Derive("net10.0", "Release");

        symbols.ShouldNotContain("DEBUG");
        symbols.ShouldContain("TRACE");
    }

    [Fact]
    public void Derive_UnknownConfiguration_DefaultsToTheDesignTimeDebug()
    {
        var symbols = ViuProjectPreprocessorSymbols.Derive("net10.0", null);

        symbols.ShouldContain("DEBUG");
    }

    [Fact]
    public void Derive_NonModernFramework_YieldsOnlyConfigurationSymbols()
    {
        var symbols = ViuProjectPreprocessorSymbols.Derive("netstandard2.0", "Debug");

        symbols.ShouldBe(["TRACE", "DEBUG"]);
    }
}
