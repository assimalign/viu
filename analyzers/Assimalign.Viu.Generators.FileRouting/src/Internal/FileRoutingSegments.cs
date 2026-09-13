using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Generators.FileRouting;

internal static class FileRoutingSegments
{
    internal static bool TryMap(string value, out string segment)
    {
        segment = string.Empty;
        var parameter = value;
        var suffix = string.Empty;
        if (value.StartsWith("[[", StringComparison.Ordinal) && value.EndsWith("]]", StringComparison.Ordinal))
        {
            parameter = value.Substring(2, value.Length - 4);
            suffix = "?";
        }
        else if (value.StartsWith("[...", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
        {
            parameter = value.Substring(4, value.Length - 5);
            suffix = "(.*)*";
        }
        else if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
        {
            parameter = value.Substring(1, value.Length - 2);
        }

        if (parameter != value)
        {
            if (!IsParameterName(parameter))
            {
                return false;
            }
            segment = ":" + parameter.ToLowerInvariant() + suffix;
            return true;
        }

        if (!IsStaticName(value))
        {
            return false;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '-' or '_')
            {
                builder.Append('-');
                continue;
            }
            if (IsUpper(character) && index != 0 && value[index - 1] is not ('-' or '_')
                && (IsLower(value[index - 1]) || IsDigit(value[index - 1])
                    || (IsUpper(value[index - 1]) && index + 1 < value.Length && IsLower(value[index + 1]))))
            {
                builder.Append('-');
            }
            builder.Append(char.ToLowerInvariant(character));
        }
        segment = builder.ToString();
        return true;
    }

    internal static string ComponentName(string value)
    {
        // [SFC-CG-3] The existing component compiler replaces every non-identifier character with
        // an underscore and prefixes digit-starting identifiers. Registration strips keyword escapes.
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }
        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }
        return builder.ToString();
    }

    internal static bool TryValidatePath(string path, out string? problem, out bool catchAllNotLast)
    {
        problem = null;
        catchAllNotLast = false;
        if (path.Length == 0 || path == "/")
        {
            return true;
        }

        var segments = path.TrimStart('/').Split('/');
        if (path.StartsWith("//", StringComparison.Ordinal))
        {
            problem = "paths cannot contain empty segments";
            return false;
        }
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (segment.StartsWith(":", StringComparison.Ordinal))
            {
                var name = ParameterName(segment);
                var suffix = segment.Substring(name.Length + 1);
                if (!IsParameterName(name) || suffix is not ("" or "?" or "*" or "+" or "(.*)*"))
                {
                    problem = "parameters must occupy a segment and use :name, :name?, :name*, :name+, or :name(.*)*";
                    return false;
                }
                if (suffix == "(.*)*" && index != segments.Length - 1)
                {
                    catchAllNotLast = true;
                    return false;
                }
            }
            else if (!IsStaticName(segment))
            {
                problem = "static paths must contain ASCII words separated by single hyphens or underscores";
                return false;
            }
        }
        return true;
    }

    internal static string RouteName(string path)
    {
        var names = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length != 0)
            {
                names.Add(segment[0] == ':' ? ParameterName(segment) : segment);
            }
        }
        return names.Count == 0 ? "index" : string.Join("-", names);
    }

    private static string ParameterName(string segment)
    {
        var end = 1;
        while (end < segment.Length && (IsLetter(segment[end]) || IsDigit(segment[end]) || segment[end] == '_'))
        {
            end++;
        }
        return segment.Substring(1, end - 1);
    }

    private static bool IsParameterName(string value)
    {
        if (value.Length == 0 || !IsLetter(value[0]))
        {
            return false;
        }
        foreach (var character in value)
        {
            if (!IsLetter(character) && !IsDigit(character) && character != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsStaticName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }
        var previousSeparator = true;
        foreach (var character in value)
        {
            if (IsLetter(character) || IsDigit(character))
            {
                previousSeparator = false;
            }
            else if (character is '-' or '_' && !previousSeparator)
            {
                previousSeparator = true;
            }
            else
            {
                return false;
            }
        }
        return !previousSeparator;
    }

    private static bool IsLetter(char character) => IsUpper(character) || IsLower(character);
    private static bool IsUpper(char character) => character is >= 'A' and <= 'Z';
    private static bool IsLower(char character) => character is >= 'a' and <= 'z';
    private static bool IsDigit(char character) => character is >= '0' and <= '9';
}
