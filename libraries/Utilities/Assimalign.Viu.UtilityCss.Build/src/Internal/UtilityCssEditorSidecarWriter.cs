using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.UtilityCss.Build;

internal static class UtilityCssEditorSidecarWriter
{
    internal const string CatalogFileName = "utilitycss.classcatalog.v1.json";
    internal const string ManifestFileName = "utilitycss.manifest.v1.json";
    private const string PreviousCatalogFileName = "utilitycss.catalog.v1.json";

    private static readonly UTF8Encoding Utf8Encoding = new(
        encoderShouldEmitUTF8Identifier: false);

    public static bool Delete(string bundlePath)
    {
        var deleted = false;
        foreach (var path in GetSidecarPaths(bundlePath))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            File.Delete(path);
            deleted = true;
        }

        return deleted;
    }

    public static bool Write(
        string bundlePath,
        string entryStylesheetPath,
        IReadOnlyList<string> sourceFiles,
        UtilityTheme theme,
        IReadOnlyList<UtilityClassMetadata> generatedRules,
        UtilityClassCompletionResult completionResult,
        int maximumItems)
    {
        var catalog = CreateCatalog(
            generatedRules,
            completionResult,
            maximumItems);
        var manifest = CreateManifestJson(
            bundlePath,
            entryStylesheetPath,
            sourceFiles,
            theme);
        var catalogJson = CreateCatalogJson(
            catalog.Items,
            catalog.IsTruncated);
        var directory = GetSidecarDirectory(bundlePath);
        Directory.CreateDirectory(directory);
        var previousCatalogDeleted = DeleteIfExists(
            Path.Combine(directory, PreviousCatalogFileName));
        var manifestWritten = WriteIfChanged(
            Path.Combine(directory, ManifestFileName),
            manifest);
        var catalogWritten = WriteIfChanged(
            Path.Combine(directory, CatalogFileName),
            catalogJson);
        return previousCatalogDeleted || manifestWritten || catalogWritten;
    }

    private static EditorCatalog CreateCatalog(
        IReadOnlyList<UtilityClassMetadata> generatedRules,
        UtilityClassCompletionResult completionResult,
        int maximumItems)
    {
        var selected = new List<UtilityClassMetadata>();
        var selectedCandidateTexts = new HashSet<string>(StringComparer.Ordinal);
        var isTruncated = completionResult.IsTruncated;
        foreach (var rule in generatedRules
                     .OrderBy(item => item.SortOrder)
                     .ThenBy(item => item.CandidateText, StringComparer.Ordinal))
        {
            if (selectedCandidateTexts.Contains(rule.CandidateText))
            {
                continue;
            }

            if (selected.Count == maximumItems)
            {
                isTruncated = true;
                continue;
            }

            selectedCandidateTexts.Add(rule.CandidateText);
            selected.Add(rule);
        }

        foreach (var completion in completionResult.Items
                     .OrderBy(
                         item => item.CandidateText,
                         StringComparer.Ordinal))
        {
            if (selectedCandidateTexts.Contains(completion.CandidateText))
            {
                continue;
            }

            if (selected.Count == maximumItems)
            {
                isTruncated = true;
                continue;
            }

            selectedCandidateTexts.Add(completion.CandidateText);
            selected.Add(completion);
        }

        return new EditorCatalog(
            selected,
            isTruncated);
    }

    private static string CreateManifestJson(
        string bundlePath,
        string entryStylesheetPath,
        IReadOnlyList<string> sourceFiles,
        UtilityTheme theme)
    {
        var absoluteBundlePath = Path.GetFullPath(bundlePath);
        var result = new StringBuilder();
        result.Append("{\n  \"schemaVersion\": 1,\n  \"engineVersion\": ");
        AppendJsonString(result, GetEngineVersion());
        result.Append(",\n  \"entryStylesheetPath\": ");
        if (string.IsNullOrEmpty(entryStylesheetPath))
        {
            result.Append("null");
        }
        else
        {
            AppendJsonString(
                result,
                Path.GetFullPath(entryStylesheetPath));
        }

        result.Append(",\n  \"sourceFiles\": [");
        var orderedSourceFiles = sourceFiles
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < orderedSourceFiles.Length; index++)
        {
            result.Append(index == 0 ? "\n    " : ",\n    ");
            AppendJsonString(result, orderedSourceFiles[index]);
        }

        if (orderedSourceFiles.Length != 0)
        {
            result.Append('\n');
            result.Append("  ");
        }

        result.Append("],\n  \"themeContentHash\": ");
        AppendJsonString(result, CreateThemeContentHash(theme));
        result.Append(",\n  \"bundle\": {\n    \"path\": ");
        AppendJsonString(result, absoluteBundlePath);
        result.Append(",\n    \"name\": ");
        AppendJsonString(result, Path.GetFileName(absoluteBundlePath));
        result.Append("\n  }\n}\n");
        return result.ToString();
    }

    private static string CreateCatalogJson(
        IReadOnlyList<UtilityClassMetadata> items,
        bool isTruncated)
    {
        var result = new StringBuilder();
        result.Append("{\n  \"version\": 1,\n  \"truncated\": ");
        result.Append(isTruncated ? "true" : "false");
        result.Append(",\n  \"entries\": [");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            result.Append(index == 0 ? "\n    {\n" : ",\n    {\n");
            result.Append("      \"class\": ");
            AppendJsonString(result, item.CandidateText);
            result.Append(",\n      \"css\": ");
            AppendJsonString(result, item.Css);
            if (item.ColorValue is not null)
            {
                result.Append(",\n      \"colorValue\": ");
                AppendJsonString(result, item.ColorValue);
            }

            result.Append("\n    }");
        }

        if (items.Count != 0)
        {
            result.Append('\n');
            result.Append("  ");
        }

        result.Append("]\n}\n");
        return result.ToString();
    }

    private static string CreateThemeContentHash(UtilityTheme theme)
    {
        var snapshot = new StringBuilder();
        snapshot.Append("{\"prefix\":");
        if (theme.Prefix is null)
        {
            snapshot.Append("null");
        }
        else
        {
            AppendJsonString(snapshot, theme.Prefix);
        }

        snapshot.Append(",\"important\":");
        snapshot.Append(theme.IsImportant ? "true" : "false");
        snapshot.Append(",\"properties\":[");
        var properties = theme.Properties
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            if (index != 0)
            {
                snapshot.Append(',');
            }

            snapshot.Append("{\"name\":");
            AppendJsonString(snapshot, property.Name);
            snapshot.Append(",\"value\":");
            AppendJsonString(snapshot, property.Value);
            snapshot.Append(",\"options\":");
            snapshot.Append(((int)property.Options).ToString(CultureInfo.InvariantCulture));
            snapshot.Append('}');
        }

        snapshot.Append("],\"keyframes\":[");
        var keyframes = theme.Keyframes
            .OrderBy(keyframe => keyframe.Name, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < keyframes.Length; index++)
        {
            var keyframe = keyframes[index];
            if (index != 0)
            {
                snapshot.Append(',');
            }

            snapshot.Append("{\"name\":");
            AppendJsonString(snapshot, keyframe.Name);
            snapshot.Append(",\"body\":");
            AppendJsonString(snapshot, keyframe.Body);
            snapshot.Append('}');
        }

        snapshot.Append("]}");
        using var algorithm = SHA256.Create();
        var hash = algorithm.ComputeHash(
            Utf8Encoding.GetBytes(snapshot.ToString()));
        var result = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }

    private static string GetEngineVersion()
    {
        var assembly = typeof(UtilityCssRegistry).Assembly;
        return assembly.GetName().Version?.ToString() ??
            "unknown";
    }

    private static bool WriteIfChanged(
        string path,
        string content)
    {
        var bytes = Utf8Encoding.GetBytes(content);
        if (File.Exists(path) &&
            File.ReadAllBytes(path).SequenceEqual(bytes))
        {
            return false;
        }

        File.WriteAllBytes(path, bytes);
        return true;
    }

    private static string[] GetSidecarPaths(string bundlePath)
    {
        var directory = GetSidecarDirectory(bundlePath);
        return new[]
        {
            Path.Combine(directory, ManifestFileName),
            Path.Combine(directory, CatalogFileName),
            Path.Combine(directory, PreviousCatalogFileName),
        };
    }

    private static bool DeleteIfExists(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private static string GetSidecarDirectory(string bundlePath) =>
        Path.GetDirectoryName(Path.GetFullPath(bundlePath)) ??
        throw new InvalidOperationException(
            "The utility CSS bundle path does not have a parent directory.");

    private static void AppendJsonString(
        StringBuilder result,
        string value)
    {
        result.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    result.Append("\\\"");
                    break;
                case '\\':
                    result.Append("\\\\");
                    break;
                case '\b':
                    result.Append("\\b");
                    break;
                case '\f':
                    result.Append("\\f");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        result.Append("\\u");
                        result.Append(((int)character).ToString(
                            "x4",
                            CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result.Append(character);
                    }

                    break;
            }
        }

        result.Append('"');
    }

    private readonly struct EditorCatalog
    {
        public EditorCatalog(
            IReadOnlyList<UtilityClassMetadata> items,
            bool isTruncated)
        {
            Items = items;
            IsTruncated = isTruncated;
        }

        public IReadOnlyList<UtilityClassMetadata> Items { get; }

        public bool IsTruncated { get; }
    }
}
