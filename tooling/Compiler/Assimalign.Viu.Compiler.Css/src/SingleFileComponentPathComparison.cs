using System;
using System.Runtime.InteropServices;

namespace Assimalign.Viu.Compiler.Css;

/// <summary>
/// Centralizes source-path identity for the single-file-component build-time hosts. Windows paths are
/// compared case-insensitively; paths on every other operating system retain ordinal case sensitivity.
/// File-format extension checks intentionally remain separate and case-insensitive.
/// </summary>
public static class SingleFileComponentPathComparison
{
    /// <summary>The path comparison for the operating system running the build host.</summary>
    public static readonly StringComparison Comparison =
        ComparisonForOperatingSystem(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

    /// <summary>The path comparer for the operating system running the build host.</summary>
    public static readonly StringComparer Comparer =
        ComparerForOperatingSystem(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

    /// <summary>Resolves the identity comparison for a known operating-system family.</summary>
    /// <param name="isWindows">
    /// <see langword="true"/> for Windows; <see langword="false"/> for every other operating system.
    /// </param>
    /// <returns>The ordinal path comparison.</returns>
    public static StringComparison ComparisonForOperatingSystem(bool isWindows)
        => isWindows
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>Resolves the identity comparer for a known operating-system family.</summary>
    /// <param name="isWindows">
    /// <see langword="true"/> for Windows; <see langword="false"/> for every other operating system.
    /// </param>
    /// <returns>The ordinal path comparer.</returns>
    public static StringComparer ComparerForOperatingSystem(bool isWindows)
        => isWindows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
