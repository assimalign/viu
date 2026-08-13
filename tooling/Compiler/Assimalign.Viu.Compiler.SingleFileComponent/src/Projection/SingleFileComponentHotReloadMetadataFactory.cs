using System;
using System.Collections.Generic;
using System.Globalization;

using Assimalign.Viu.Syntax.SingleFileComponent;
using Assimalign.Viu.Compiler.Css;

namespace Assimalign.Viu.Compiler.SingleFileComponent;

/// <summary>
/// Produces the deterministic [V01.01.06.05] development metadata for a parsed single-file component.
/// The implementation uses only string and integer operations: no reflection, file access, dynamic code,
/// or runtime hashing APIs enter the analyzer or the generated application.
/// </summary>
public static class SingleFileComponentHotReloadMetadataFactory
{
    private const string ComponentIdentifierPrefix = "viu-";
    private const ulong HashOffset = 14695981039346656037UL;
    private const ulong HashPrime = 1099511628211UL;

    /// <summary>
    /// Derives the component identifier from the project-relative normalized path. A linked source
    /// outside the project falls back to its leaf name so machine-specific checkout roots never enter
    /// generated metadata.
    /// </summary>
    /// <param name="filePath">The single-file-component source path.</param>
    /// <param name="projectDirectory">The consuming project directory, or <see langword="null"/>.</param>
    /// <returns>The stable component identifier.</returns>
    public static string ResolveComponentIdentifier(string filePath, string? projectDirectory)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var identityPath = RelativePath(normalizedPath, projectDirectory);
        var hash = HashOffset;
        AppendString(ref hash, "component");
        AppendString(ref hash, identityPath);
        return ComponentIdentifierPrefix + FormatHash(hash);
    }

    /// <summary>Creates independent template, script, and style hashes for the parsed blocks.</summary>
    /// <param name="componentIdentifier">The path-stable component identifier.</param>
    /// <param name="template">The template block, or <see langword="null"/>.</param>
    /// <param name="script">The ordinary script block, or <see langword="null"/>.</param>
    /// <param name="scriptSetup">The setup script block, or <see langword="null"/>.</param>
    /// <param name="styles">The style blocks in source order.</param>
    /// <returns>The immutable hot-reload metadata.</returns>
    public static SingleFileComponentHotReloadMetadata Create(
        string componentIdentifier,
        SingleFileComponentTemplateBlock? template,
        SingleFileComponentScriptBlock? script,
        SingleFileComponentScriptBlock? scriptSetup,
        IReadOnlyList<SingleFileComponentStyleBlock> styles)
    {
        var templateHash = HashOffset;
        AppendString(ref templateHash, "template");
        AppendInteger(ref templateHash, template is null ? 0 : 1);
        if (template is not null)
        {
            AppendBlock(ref templateHash, "template", template);
        }

        var scriptHash = HashOffset;
        AppendString(ref scriptHash, "script");
        AppendInteger(ref scriptHash, PresentCount(script, scriptSetup));
        if (scriptSetup is not null &&
            (script is null || scriptSetup.Location.Start.Offset < script.Location.Start.Offset))
        {
            AppendBlock(ref scriptHash, "setup", scriptSetup);
            if (script is not null)
            {
                AppendBlock(ref scriptHash, "ordinary", script);
            }
        }
        else
        {
            if (script is not null)
            {
                AppendBlock(ref scriptHash, "ordinary", script);
            }

            if (scriptSetup is not null)
            {
                AppendBlock(ref scriptHash, "setup", scriptSetup);
            }
        }

        var styleHash = HashOffset;
        AppendString(ref styleHash, "style");
        AppendInteger(ref styleHash, styles.Count);
        for (var index = 0; index < styles.Count; index++)
        {
            AppendBlock(ref styleHash, "style", styles[index]);
        }

        return new SingleFileComponentHotReloadMetadata(
            componentIdentifier,
            FormatHash(templateHash),
            FormatHash(scriptHash),
            FormatHash(styleHash));
    }

    private static int PresentCount(
        SingleFileComponentScriptBlock? script,
        SingleFileComponentScriptBlock? scriptSetup)
        => (script is null ? 0 : 1) + (scriptSetup is null ? 0 : 1);

    private static string RelativePath(string normalizedPath, string? projectDirectory)
    {
        if (!string.IsNullOrEmpty(projectDirectory))
        {
            var normalizedDirectory = projectDirectory!.Replace('\\', '/').TrimEnd('/');
            var prefix = normalizedDirectory + "/";
            if (normalizedPath.StartsWith(
                    prefix,
                    SingleFileComponentPathComparison.Comparison))
            {
                return normalizedPath.Substring(prefix.Length);
            }
        }

        var separator = normalizedPath.LastIndexOf('/');
        return separator >= 0 ? normalizedPath.Substring(separator + 1) : normalizedPath;
    }

    private static void AppendBlock(
        ref ulong hash,
        string role,
        SingleFileComponentBlock block)
    {
        AppendString(ref hash, role);
        AppendString(ref hash, block.Name);
        AppendInteger(ref hash, block.Options.Count);
        foreach (var option in block.Options)
        {
            AppendString(ref hash, option.Name);
            AppendNullableString(ref hash, option.Value);
        }

        AppendString(ref hash, block.Content);
    }

    private static void AppendNullableString(ref ulong hash, string? value)
    {
        if (value is null)
        {
            AppendInteger(ref hash, -1);
            return;
        }

        AppendString(ref hash, value);
    }

    private static void AppendString(ref ulong hash, string value)
    {
        AppendInteger(ref hash, value.Length);
        foreach (var character in value)
        {
            Update(ref hash, (byte)character);
            Update(ref hash, (byte)(character >> 8));
        }
    }

    private static void AppendInteger(ref ulong hash, int value)
    {
        unchecked
        {
            Update(ref hash, (byte)value);
            Update(ref hash, (byte)(value >> 8));
            Update(ref hash, (byte)(value >> 16));
            Update(ref hash, (byte)(value >> 24));
        }
    }

    private static void Update(ref ulong hash, byte value)
    {
        unchecked
        {
            hash = (hash ^ value) * HashPrime;
        }
    }

    private static string FormatHash(ulong hash)
        => hash.ToString("x16", CultureInfo.InvariantCulture);
}
