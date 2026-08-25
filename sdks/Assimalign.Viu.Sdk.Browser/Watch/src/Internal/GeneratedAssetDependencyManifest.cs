using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assimalign.Viu.Sdk.CssHotReload;

internal sealed class GeneratedAssetDependencyManifest
{
    private const string Header = "viu-generated-asset-dependencies-v1";
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private GeneratedAssetDependencyManifest(
        IReadOnlyList<string> files,
        IReadOnlyList<string> roots)
    {
        Files = files;
        Roots = roots;
    }

    public IReadOnlyList<string> Files { get; }

    public IReadOnlyList<string> Roots { get; }

    public static bool TryRead(
        string path,
        out GeneratedAssetDependencyManifest manifest,
        out string error)
    {
        manifest = new GeneratedAssetDependencyManifest(
            Array.Empty<string>(),
            Array.Empty<string>());
        error = string.Empty;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return true;
        }

        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0 ||
                !string.Equals(lines[0], Header, StringComparison.Ordinal))
            {
                error = "invalid-header";
                return false;
            }

            var files = new List<string>();
            var roots = new List<string>();
            for (var index = 1; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("file:", StringComparison.Ordinal))
                {
                    files.Add(DecodeAbsolutePath(line.Substring("file:".Length)));
                }
                else if (line.StartsWith("root:", StringComparison.Ordinal))
                {
                    roots.Add(DecodeAbsolutePath(line.Substring("root:".Length)));
                }
                else
                {
                    error = "invalid-record";
                    return false;
                }
            }

            manifest = new GeneratedAssetDependencyManifest(files, roots);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                FormatException or
                ArgumentException or
                NotSupportedException)
        {
            error = exception.GetType().Name;
            return false;
        }
    }

    private static string DecodeAbsolutePath(string encodedPath)
    {
        var path = StrictUtf8.GetString(Convert.FromBase64String(encodedPath));
        if (!Path.IsPathRooted(path))
        {
            throw new InvalidDataException(
                "Generated-asset dependency records must contain absolute paths.");
        }

        return Path.GetFullPath(path);
    }
}
