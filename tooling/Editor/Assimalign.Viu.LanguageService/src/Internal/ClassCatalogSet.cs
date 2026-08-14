using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Assimalign.Viu.LanguageService;

/// <summary>Loads and merges one immutable host-provided class-catalog snapshot.</summary>
internal sealed class ClassCatalogSet
{
    private ClassCatalogSet(
        IReadOnlyList<ClassCatalogEntry> entries,
        bool isTruncated)
    {
        Entries = entries;
        IsTruncated = isTruncated;
    }

    /// <summary>Gets the empty catalog set.</summary>
    internal static ClassCatalogSet Empty { get; } =
        new(Array.Empty<ClassCatalogEntry>(), false);

    /// <summary>Gets first-wins entries in host-provided catalog and entry order.</summary>
    internal IReadOnlyList<ClassCatalogEntry> Entries { get; }

    /// <summary>Gets whether any successfully loaded catalog was truncated by its producer.</summary>
    internal bool IsTruncated { get; }

    /// <summary>Loads the generic version-one JSON documents, ignoring malformed catalogs.</summary>
    internal static ClassCatalogSet Load(LanguageClassCatalogConfiguration configuration)
    {
        var entries = new List<ClassCatalogEntry>();
        var classNames = new HashSet<string>(StringComparer.Ordinal);
        var isTruncated = false;
        foreach (var json in configuration.CatalogJsonDocuments)
        {
            if (!TryLoadCatalog(json, out var catalogEntries, out var catalogIsTruncated))
            {
                continue;
            }

            isTruncated |= catalogIsTruncated;
            foreach (var entry in catalogEntries)
            {
                if (classNames.Add(entry.ClassName))
                {
                    entries.Add(entry with { Order = entries.Count });
                }
            }
        }

        return entries.Count == 0 && !isTruncated
            ? Empty
            : new ClassCatalogSet(entries, isTruncated);
    }

    /// <summary>Finds one exact class entry.</summary>
    internal ClassCatalogEntry? Find(string className)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.ClassName, className, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private static bool TryLoadCatalog(
        string json,
        out IReadOnlyList<ClassCatalogEntry> entries,
        out bool isTruncated)
    {
        entries = Array.Empty<ClassCatalogEntry>();
        isTruncated = false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryReadVersion(root, out var version) ||
                version != 1 ||
                !root.TryGetProperty("truncated", out var truncatedElement) ||
                truncatedElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False ||
                !root.TryGetProperty("entries", out var entriesElement) ||
                entriesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var loaded = new List<ClassCatalogEntry>(entriesElement.GetArrayLength());
            foreach (var element in entriesElement.EnumerateArray())
            {
                if (!TryReadEntry(element, loaded.Count, out var entry))
                {
                    return false;
                }

                loaded.Add(entry);
            }

            entries = loaded;
            isTruncated = truncatedElement.GetBoolean();
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryReadVersion(JsonElement root, out int version)
    {
        version = 0;
        return root.TryGetProperty("version", out var versionElement) &&
               versionElement.ValueKind == JsonValueKind.Number &&
               versionElement.TryGetInt32(out version);
    }

    private static bool TryReadEntry(
        JsonElement element,
        int order,
        out ClassCatalogEntry entry)
    {
        entry = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            !TryReadRequiredString(element, "class", out var className) ||
            !TryReadRequiredString(element, "css", out var css) ||
            !TryReadOptionalString(element, "colorValue", out var colorValue) ||
            !TryReadOptionalString(element, "sortText", out var sortText))
        {
            return false;
        }

        entry = new ClassCatalogEntry(
            className,
            css,
            colorValue,
            sortText,
            order);
        return true;
    }

    private static bool TryReadRequiredString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return value.Length != 0;
    }

    private static bool TryReadOptionalString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrEmpty(value);
    }
}
