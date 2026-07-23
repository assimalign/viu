using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Assimalign.Viu.Router;

/// <summary>
/// A compiled path pattern: a regular expression, a specificity <see cref="Score"/>, the ordered
/// parameter <see cref="Keys"/>, and the token segments used to interpolate params back into a
/// path. The C# port of vue-router's <c>PathParser</c>
/// (<c>packages/router/src/matcher/pathParserRanker.ts</c>) — its <see cref="TryParse"/> mirrors
/// <c>parse</c> and <see cref="Stringify"/> mirrors <c>stringify</c>.
/// </summary>
/// <remarks>
/// The regular expression is built from runtime route-table data and constructed with the
/// interpreted engine (never <see cref="RegexOptions.Compiled"/>, which relies on reflection
/// emit), keeping the parser trimming- and NativeAOT-safe. See DESIGN.md.
/// </remarks>
internal sealed class PathParser
{
    private readonly Regex regularExpression;
    private readonly PathToken[][] segments;

    public PathParser(Regex regularExpression, double[][] score, PathParameterKey[] keys, PathToken[][] segments)
    {
        this.regularExpression = regularExpression;
        Score = score;
        Keys = keys;
        this.segments = segments;
    }

    /// <summary>The specificity score, one inner array per path segment (see <see cref="PathScore"/>).</summary>
    public double[][] Score { get; }

    /// <summary>The parameters captured by this pattern, in capturing-group order.</summary>
    public PathParameterKey[] Keys { get; }

    /// <summary>The compiled regular expression source (for diagnostics and tests).</summary>
    public string Pattern => regularExpression.ToString();

    /// <summary>
    /// Attempts to match <paramref name="path"/> and extract its parameters. Mirrors vue-router's
    /// <c>parse</c>: a captured value for a repeatable key is split on <c>/</c> into multiple
    /// values; every other value is stored as a single string (an empty capture stays a single
    /// empty string, matching upstream).
    /// </summary>
    /// <param name="path">The path to match.</param>
    /// <param name="parameters">The extracted parameters when the match succeeds.</param>
    /// <returns><see langword="true"/> when the pattern matches; otherwise <see langword="false"/>.</returns>
    public bool TryParse(string path, out RouteParameters parameters)
    {
        var match = regularExpression.Match(path);
        if (!match.Success)
        {
            parameters = RouteParameters.Empty;
            return false;
        }

        var values = new Dictionary<string, RouteParameterValue>(Keys.Length, StringComparer.Ordinal);
        for (var groupIndex = 1; groupIndex < match.Groups.Count && groupIndex - 1 < Keys.Length; groupIndex++)
        {
            var group = match.Groups[groupIndex];
            var value = group.Success ? group.Value : string.Empty;
            var key = Keys[groupIndex - 1];
            values[key.Name] = value.Length > 0 && key.Repeatable
                ? RouteParameterValue.Multiple(value.Split('/'))
                : RouteParameterValue.Single(value);
        }

        parameters = RouteParameters.FromValues(values);
        return true;
    }

    /// <summary>
    /// Interpolates <paramref name="parameters"/> into a concrete path. Mirrors vue-router's
    /// <c>stringify</c>, including optional-parameter slash elision and the array/repeatable checks.
    /// </summary>
    /// <param name="parameters">The parameter values to substitute.</param>
    /// <returns>The generated path.</returns>
    /// <exception cref="RouteMatcherException">
    /// A required parameter had no value (<see cref="RouteMatcherError.MissingRequiredParameter"/>),
    /// or an array was supplied for a non-repeatable parameter
    /// (<see cref="RouteMatcherError.ParameterNotRepeatable"/>).
    /// </exception>
    public string Stringify(RouteParameters parameters)
    {
        var path = new StringBuilder();
        var avoidDuplicatedSlash = false;

        foreach (var segment in segments)
        {
            if (!avoidDuplicatedSlash || !EndsWithSlash(path))
            {
                path.Append('/');
            }
            avoidDuplicatedSlash = false;

            foreach (var token in segment)
            {
                if (token.Kind == PathTokenKind.Static)
                {
                    path.Append(token.Value);
                    continue;
                }

                var hasValue = parameters.TryGetRawValue(token.Value, out var value);
                var isArray = hasValue && value.IsMultiple;
                if (isArray && !token.Repeatable)
                {
                    throw new RouteMatcherException(
                        RouteMatcherError.ParameterNotRepeatable,
                        $"Provided param \"{token.Value}\" is an array but it is not repeatable (* or + modifiers).");
                }

                var text = isArray
                    ? string.Join("/", value.MultipleValues)
                    : hasValue ? value.SingleValue : string.Empty;

                if (text.Length == 0)
                {
                    if (token.Optional)
                    {
                        // Only elide the slash when the optional param is alone in its segment.
                        if (segment.Length < 2)
                        {
                            if (EndsWithSlash(path))
                            {
                                path.Length -= 1;
                            }
                            else
                            {
                                avoidDuplicatedSlash = true;
                            }
                        }
                    }
                    else
                    {
                        throw new RouteMatcherException(
                            RouteMatcherError.MissingRequiredParameter,
                            $"Missing required param \"{token.Value}\".");
                    }
                }

                path.Append(text);
            }
        }

        return path.Length == 0 ? "/" : path.ToString();
    }

    private static bool EndsWithSlash(StringBuilder builder)
        => builder.Length > 0 && builder[^1] == '/';
}
