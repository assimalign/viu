using System;

namespace Assimalign.Viu.UtilityCss;

internal static class UtilityCompletionPrefix
{
    public static void Split(
        string? typedPrefix,
        out string variantPrefix,
        out string baseFragment)
    {
        var prefix = typedPrefix ?? string.Empty;
        var separator = FindLastTopLevelSeparator(prefix);
        if (separator < 0)
        {
            variantPrefix = string.Empty;
            baseFragment = prefix;
            return;
        }

        variantPrefix = prefix.Substring(
            0,
            separator + 1);
        baseFragment = prefix.Substring(separator + 1);
    }

    public static bool HasConfiguredPrefix(
        string variantPrefix,
        string? configuredPrefix)
    {
        if (string.IsNullOrEmpty(configuredPrefix))
        {
            return true;
        }

        var separator = FindFirstTopLevelSeparator(variantPrefix);
        return separator == configuredPrefix!.Length &&
            variantPrefix.StartsWith(
                configuredPrefix + ":",
                StringComparison.Ordinal);
    }

    public static string Compose(
        string variantPrefix,
        string? configuredPrefix,
        string baseCandidate)
    {
        if (variantPrefix.Length > 0)
        {
            return variantPrefix + baseCandidate;
        }

        return string.IsNullOrEmpty(configuredPrefix)
            ? baseCandidate
            : configuredPrefix + ":" + baseCandidate;
    }

    public static string RemoveConfiguredPrefix(
        string candidate,
        string? configuredPrefix)
    {
        if (string.IsNullOrEmpty(configuredPrefix))
        {
            return candidate;
        }

        var marker = configuredPrefix + ":";
        return candidate.StartsWith(
                marker,
                StringComparison.Ordinal)
            ? candidate.Substring(marker.Length)
            : candidate;
    }

    public static bool HasVariant(string candidate) =>
        FindLastTopLevelSeparator(candidate) >= 0;

    private static int FindFirstTopLevelSeparator(string text)
    {
        var separator = -1;
        VisitTopLevelSeparators(
            text,
            current =>
            {
                separator = current;
                return false;
            });
        return separator;
    }

    private static int FindLastTopLevelSeparator(string text)
    {
        var separator = -1;
        VisitTopLevelSeparators(
            text,
            current =>
            {
                separator = current;
                return true;
            });
        return separator;
    }

    private static void VisitTopLevelSeparators(
        string text,
        Func<int, bool> visitor)
    {
        var bracketDepth = 0;
        var parenthesisDepth = 0;
        var isEscaped = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            switch (character)
            {
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth = Math.Max(
                        0,
                        bracketDepth - 1);
                    break;
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth = Math.Max(
                        0,
                        parenthesisDepth - 1);
                    break;
                case ':' when bracketDepth == 0 && parenthesisDepth == 0:
                    if (!visitor(index))
                    {
                        return;
                    }

                    break;
            }
        }
    }
}
