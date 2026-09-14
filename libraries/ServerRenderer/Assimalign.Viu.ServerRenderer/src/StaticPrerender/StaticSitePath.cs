using System;
using System.Collections.Generic;

namespace Assimalign.Viu.ServerRenderer;

/// <summary>Maps base-stripped route locations to portable document paths.</summary>
/// <remarks>Query and fragment never affect storage; unsafe segments are rejected, never normalized through parent directories. Specified by <c>[SSG-2]</c>.</remarks>
public static class StaticSitePath
{
    /// <summary>Maps root to index.html and other routes to a directory's index.html.</summary>
    /// <param name="route">A leading-slash route reference with optional query and fragment.</param>
    /// <returns>The decoded, slash-separated relative document path.</returns>
    /// <exception cref="ArgumentException">The location or a decoded path segment is unsafe for portable storage.</exception>
    public static string GetRelativePath(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        int suffix = route.AsSpan().IndexOfAny('?', '#');
        string path = suffix < 0 ? route : route[..suffix];
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("A static route must be a base-stripped path beginning with one '/'.", nameof(route));
        }

        if (path == "/")
        {
            return "index.html";
        }

        string[] segments = path[1..].TrimEnd('/').Split('/');
        List<string> decoded = new(segments.Length + 1);
        foreach (string segment in segments)
        {
            for (int index = 0; index < segment.Length; index++)
            {
                if (segment[index] == '%' && (index + 2 >= segment.Length
                    || !Uri.IsHexDigit(segment[index + 1]) || !Uri.IsHexDigit(segment[index + 2])))
                {
                    throw new ArgumentException("A static route contains a malformed percent escape.", nameof(route));
                }
            }

            string value = Uri.UnescapeDataString(segment);
            ValidateSegment(value);
            decoded.Add(value);
        }

        decoded.Add("index.html");
        return string.Join('/', decoded);
    }

    internal static void ValidateSegment(string segment)
    {
        if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' '))
        {
            throw new ArgumentException("Static paths cannot contain empty, dot, parent, or trailing-dot/space segments.");
        }

        foreach (char character in segment)
        {
            if (char.IsControl(character) || "<>:\"/\\|?*".Contains(character))
            {
                throw new ArgumentException($"Static path segment '{segment}' contains a non-portable character.");
            }
        }

        string stem = segment.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4 && stem[3] is >= '1' and <= '9'
                && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))))
        {
            throw new ArgumentException($"Static path segment '{segment}' is a reserved device name.");
        }
    }
}
