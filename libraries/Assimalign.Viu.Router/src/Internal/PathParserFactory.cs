using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.Router;

/// <summary>
/// Turns tokenized path segments into a compiled <see cref="PathParser"/> — building the regular
/// expression, the specificity score, and the parameter keys in one pass. The C# port of
/// vue-router's <c>tokensToParser</c> (<c>packages/router/src/matcher/pathParserRanker.ts</c>).
/// </summary>
internal static class PathParserFactory
{
    // Upstream BASE_PARAM_PATTERN — a lazy "anything but a slash" match.
    private const string BaseParameterPattern = "[^/]+?";

    // Upstream catch-all pattern whose presence earns the wildcard penalty.
    private const string WildcardPattern = ".*";

    /// <summary>Compiles tokenized segments into a <see cref="PathParser"/>.</summary>
    /// <param name="segments">The segments produced by <see cref="PathTokenizer.Tokenize"/>.</param>
    /// <param name="options">Strict / case-sensitive matching options.</param>
    /// <exception cref="RouteMatcherException">A custom parameter pattern was not a valid regular expression.</exception>
    public static PathParser Compile(List<List<PathToken>> segments, PathMatchingOptions options)
    {
        // The top-level matcher always anchors both ends (upstream start/end default to true).
        var strict = options.Strict;
        var sensitive = options.Sensitive;

        var score = new List<double[]>();
        var pattern = new StringBuilder("^");
        var keys = new List<PathParameterKey>();
        var storedSegments = new PathToken[segments.Count][];

        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            storedSegments[segmentIndex] = segment.ToArray();

            // An empty segment (root "/" or a trailing slash) scores the Root bonus.
            var segmentScores = new List<double>();
            if (segment.Count == 0)
            {
                segmentScores.Add(PathScore.Root);
                if (strict)
                {
                    pattern.Append('/');
                }
            }

            for (var tokenIndex = 0; tokenIndex < segment.Count; tokenIndex++)
            {
                var token = segment[tokenIndex];
                var subSegmentScore = PathScore.Segment + (sensitive ? PathScore.BonusCaseSensitive : 0);

                if (token.Kind == PathTokenKind.Static)
                {
                    if (tokenIndex == 0)
                    {
                        pattern.Append('/');
                    }
                    pattern.Append(EscapeStaticText(token.Value));
                    subSegmentScore += PathScore.Static;
                }
                else
                {
                    keys.Add(new PathParameterKey(token.Value, token.Repeatable, token.Optional));

                    var innerPattern = token.CustomPattern.Length == 0 ? BaseParameterPattern : token.CustomPattern;
                    if (!string.Equals(innerPattern, BaseParameterPattern, StringComparison.Ordinal))
                    {
                        subSegmentScore += PathScore.BonusCustomPattern;
                        ValidateCustomPattern(token.Value, innerPattern);
                    }

                    // A repeatable param captures one-or-more slash-separated occurrences.
                    var subPattern = token.Repeatable
                        ? $"((?:{innerPattern})(?:/(?:{innerPattern}))*)"
                        : $"({innerPattern})";

                    if (tokenIndex == 0)
                    {
                        // Make the leading slash itself optional only when the optional param is the
                        // whole segment (upstream: `optional && segment.length < 2`).
                        subPattern = token.Optional && segment.Count < 2
                            ? $"(?:/{subPattern})"
                            : "/" + subPattern;
                    }

                    if (token.Optional)
                    {
                        subPattern += "?";
                    }

                    pattern.Append(subPattern);

                    subSegmentScore += PathScore.Dynamic;
                    if (token.Optional)
                    {
                        subSegmentScore += PathScore.BonusOptional;
                    }
                    if (token.Repeatable)
                    {
                        subSegmentScore += PathScore.BonusRepeatable;
                    }
                    if (string.Equals(innerPattern, WildcardPattern, StringComparison.Ordinal))
                    {
                        subSegmentScore += PathScore.BonusWildcard;
                    }
                }

                segmentScores.Add(subSegmentScore);
            }

            score.Add(segmentScores.ToArray());
        }

        // The strict bonus lands only on the last sub-segment of the last segment.
        if (strict && score.Count > 0)
        {
            var lastSegment = score[^1];
            lastSegment[^1] += PathScore.BonusStrict;
        }

        if (!strict)
        {
            pattern.Append("/?");
        }

        // The top-level matcher is anchored at the end.
        pattern.Append('$');

        var regexOptions = RegexOptions.CultureInvariant | (sensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        var regularExpression = new Regex(pattern.ToString(), regexOptions);

        return new PathParser(regularExpression, score.ToArray(), keys.ToArray(), storedSegments);
    }

    // Upstream: token.value.replace(REGEX_CHARS_RE, '\\$&').
    private static string EscapeStaticText(string value)
        => value.Length == 0
            ? value
            : RegularExpressionPatterns.RegularExpressionSpecialCharacters()
                .Replace(value, static match => "\\" + match.Value);

    private static void ValidateCustomPattern(string parameterName, string innerPattern)
    {
        try
        {
            _ = new Regex($"({innerPattern})");
        }
        catch (ArgumentException exception)
        {
            throw new RouteMatcherException(
                RouteMatcherError.InvalidCustomPattern,
                $"Invalid custom RegExp for param \"{parameterName}\" ({innerPattern}): {exception.Message}");
        }
    }
}
