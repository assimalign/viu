using System;

namespace Assimalign.Viu.Browser.Router;

/// <summary>
/// Normalizes the raw URL components owned by the browser history implementation. It remains local
/// implementation policy rather than exposing Router's memory-history helpers across assemblies.
/// </summary>
internal static class BrowserHistoryPathNormalization
{
    internal static string NormalizeBase(string? rawBase)
    {
        string value = string.IsNullOrEmpty(rawBase) ? "/" : rawBase;
        if (value[0] != '/' && value[0] != '#')
        {
            value = "/" + value;
        }

        return value.Length > 0 && value[^1] == '/'
            ? value[..^1]
            : value;
    }

    internal static string StripBaseHrefOrigin(string href)
    {
        int schemeIndex = href.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex <= 0 || !IsScheme(href.AsSpan(0, schemeIndex)))
        {
            return href;
        }

        int hostStart = schemeIndex + 3;
        int pathSlash = href.IndexOf('/', hostStart);
        return pathSlash >= 0 ? href[pathSlash..] : string.Empty;
    }

    internal static string StripBase(string pathname, string normalizedBase)
    {
        if (string.IsNullOrEmpty(normalizedBase)
            || !pathname.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            return pathname;
        }

        string stripped = pathname[normalizedBase.Length..];
        return stripped.Length == 0 ? "/" : stripped;
    }

    internal static string CreateHref(string normalizedBase, string location)
    {
        int hashIndex = normalizedBase.IndexOf('#');
        string prefix = hashIndex > 0 ? "#" : normalizedBase;
        return prefix + location;
    }

    internal static string ComputeHashBase(
        string? providedBase,
        string host,
        string pathname,
        string search)
    {
        string result = !string.IsNullOrEmpty(host)
            ? string.IsNullOrEmpty(providedBase) ? pathname + search : providedBase
            : string.Empty;
        if (!result.Contains('#', StringComparison.Ordinal))
        {
            result += "#";
        }

        return result;
    }

    internal static string CreateCurrentLocation(
        string normalizedBase,
        string pathname,
        string search,
        string hash)
    {
        int hashPosition = normalizedBase.IndexOf('#');
        if (hashPosition >= 0)
        {
            string hashBase = normalizedBase[hashPosition..];
            int slicePosition = hash.Contains(hashBase, StringComparison.Ordinal)
                ? hashBase.Length
                : 1;
            string pathFromHash = hash.Length > slicePosition
                ? hash[slicePosition..]
                : string.Empty;
            if (pathFromHash.Length == 0 || pathFromHash[0] != '/')
            {
                pathFromHash = "/" + pathFromHash;
            }

            return pathFromHash;
        }

        return StripBase(pathname, normalizedBase) + search + hash;
    }

    private static bool IsScheme(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }

        return value.Length > 0;
    }
}
