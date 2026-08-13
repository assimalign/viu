using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.LanguageServer;

/// <summary>
/// Derives the conditional-compilation symbols the .NET SDK defines for a target framework and
/// configuration, so the editor compilation sees the same <c>#if</c> surface the build does
/// ([V01.01.12.23], #259). The derivation mirrors the SDK's implicit definitions for modern
/// <c>net&lt;major&gt;.&lt;minor&gt;</c> frameworks: the framework family constants, the version
/// constant, the <c>_OR_GREATER</c> chain back through <c>netcoreapp1.0</c>, and the
/// configuration's <c>DEBUG</c>/<c>TRACE</c>.
/// </summary>
internal static class ViuProjectPreprocessorSymbols
{
    private static readonly (int Major, int Minor)[] NetCoreAppVersions =
    [
        (1, 0), (1, 1), (2, 0), (2, 1), (2, 2), (3, 0), (3, 1),
    ];

    /// <summary>Derives the symbol set for <paramref name="targetFramework"/>.</summary>
    /// <param name="targetFramework">The short target framework name (for example <c>net10.0</c>).</param>
    /// <param name="configuration">
    /// The build configuration, or <see langword="null"/> when unknown — the design-time default is
    /// <c>Debug</c>, matching what Visual Studio's design-time build defines.
    /// </param>
    /// <returns>The symbols, or an empty list when the framework is not a modern <c>net</c> one.</returns>
    internal static IReadOnlyList<string> Derive(string targetFramework, string? configuration)
    {
        var symbols = new List<string>();

        // TRACE is defined for every SDK configuration; DEBUG only for Debug (the design-time
        // default when the configuration is unknown).
        symbols.Add("TRACE");
        if (configuration is null ||
            string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            symbols.Add("DEBUG");
        }

        if (!TryParseModernFramework(targetFramework, out var major, out var minor))
        {
            return symbols;
        }

        symbols.Add("NET");
        symbols.Add("NETCOREAPP");
        symbols.Add(FormatVersionSymbol("NET", major, minor));
        foreach (var (coreMajor, coreMinor) in NetCoreAppVersions)
        {
            symbols.Add(FormatVersionSymbol("NETCOREAPP", coreMajor, coreMinor) + "_OR_GREATER");
        }

        for (var greaterMajor = 5; greaterMajor <= major; greaterMajor++)
        {
            symbols.Add(FormatVersionSymbol("NET", greaterMajor, 0) + "_OR_GREATER");
        }

        if (minor > 0)
        {
            symbols.Add(FormatVersionSymbol("NET", major, minor) + "_OR_GREATER");
        }

        return symbols;
    }

    private static bool TryParseModernFramework(string targetFramework, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrEmpty(targetFramework) ||
            !targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var version = targetFramework.Substring(3);
        var separator = version.IndexOf('.');
        if (separator < 0)
        {
            return false;
        }

        return int.TryParse(
                version.Substring(0, separator),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out major) &&
            int.TryParse(
                version.Substring(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out minor) &&
            major >= 5;
    }

    private static string FormatVersionSymbol(string prefix, int major, int minor)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}{major}_{minor}");
}
