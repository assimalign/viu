using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Viu.Router;

/// <summary>
/// Splits a route path string into segments of <see cref="PathToken"/>s. The C# port of
/// vue-router's <c>tokenizePath</c> (<c>packages/router/src/matcher/pathTokenizer.ts</c>),
/// reproducing its character-by-character state machine so that dynamic (<c>:id</c>), optional
/// (<c>:id?</c>), repeatable (<c>:id+</c>/<c>:id*</c>), and custom-pattern (<c>:id(\d+)</c>)
/// parameters — plus sub-segment mixes like <c>/user-:id</c> — tokenize identically to upstream.
/// </summary>
/// <remarks>
/// This is compile-time-free tokenization (no regular expressions, no reflection), so it is fully
/// trimming- and NativeAOT-safe. It runs once per route record when the table is built.
/// </remarks>
internal static class PathTokenizer
{
    // Upstream ROOT_TOKEN: tokenizePath("/") is a single segment holding one empty Static token,
    // which the ranker scores as Segment + Static (not the empty-segment Root bonus).
    private static readonly PathToken[] RootSegment = [PathToken.Static(string.Empty)];

    private enum TokenizerState
    {
        Static,
        Parameter,
        ParameterCustomPattern,
        ParameterCustomPatternEnd,
        EscapeNext,
    }

    /// <summary>
    /// Tokenizes <paramref name="path"/> into segments (outer list) of tokens (inner list). An
    /// empty path yields one empty segment (<c>[[]]</c>); <c>"/"</c> yields one root segment.
    /// </summary>
    /// <param name="path">The route path, e.g. <c>/users/:id(\d+)</c>.</param>
    /// <exception cref="RouteMatcherException">
    /// The path did not start with <c>/</c>, a repeatable parameter was not alone in its segment,
    /// or a custom pattern was left unclosed.
    /// </exception>
    public static List<List<PathToken>> Tokenize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
        {
            // Upstream: `if (!path) return [[]]` — one empty segment.
            return [[]];
        }
        if (path == "/")
        {
            return [[.. RootSegment]];
        }
        if (path[0] != '/')
        {
            throw new RouteMatcherException(
                RouteMatcherError.InvalidRoutePath,
                $"Route paths should start with a \"/\": \"{path}\" should be \"/{path}\".");
        }

        var state = TokenizerState.Static;
        var previousState = state;
        var tokens = new List<List<PathToken>>();
        List<PathToken>? segment = null;
        var buffer = new StringBuilder();
        var customPattern = new StringBuilder();

        void FinalizeSegment()
        {
            if (segment is not null)
            {
                tokens.Add(segment);
            }
            segment = [];
        }

        void ConsumeBuffer(char triggeringCharacter)
        {
            if (buffer.Length == 0)
            {
                return;
            }
            var value = buffer.ToString();
            if (state == TokenizerState.Static)
            {
                segment!.Add(PathToken.Static(value));
            }
            else if (state is TokenizerState.Parameter
                or TokenizerState.ParameterCustomPattern
                or TokenizerState.ParameterCustomPatternEnd)
            {
                var repeatable = triggeringCharacter is '*' or '+';
                var optional = triggeringCharacter is '*' or '?';
                // Upstream checks `segment.length > 1` — counting only the tokens already pushed
                // ahead of this parameter — so a static prefix like "/user-:id+" is allowed.
                if (segment!.Count > 1 && repeatable)
                {
                    throw new RouteMatcherException(
                        RouteMatcherError.RepeatableParameterNotAlone,
                        $"A repeatable param (\"{value}\") must be alone in its segment. eg: \"/:ids+\".");
                }
                segment.Add(PathToken.Parameter(value, customPattern.ToString(), optional, repeatable));
            }
            buffer.Clear();
        }

        var index = 0;
        while (index < path.Length)
        {
            var character = path[index++];

            if (character == '\\' && state != TokenizerState.ParameterCustomPattern)
            {
                previousState = state;
                state = TokenizerState.EscapeNext;
                continue;
            }

            switch (state)
            {
                case TokenizerState.Static:
                    if (character == '/')
                    {
                        ConsumeBuffer(character);
                        FinalizeSegment();
                    }
                    else if (character == ':')
                    {
                        ConsumeBuffer(character);
                        state = TokenizerState.Parameter;
                    }
                    else
                    {
                        buffer.Append(character);
                    }
                    break;

                case TokenizerState.EscapeNext:
                    buffer.Append(character);
                    state = previousState;
                    break;

                case TokenizerState.Parameter:
                    if (character == '(')
                    {
                        state = TokenizerState.ParameterCustomPattern;
                    }
                    else if (IsValidParameterNameCharacter(character))
                    {
                        buffer.Append(character);
                    }
                    else
                    {
                        ConsumeBuffer(character);
                        state = TokenizerState.Static;
                        // Reprocess this character unless it was a consumed modifier.
                        if (character is not ('*' or '?' or '+'))
                        {
                            index--;
                        }
                    }
                    break;

                case TokenizerState.ParameterCustomPattern:
                    if (character == ')')
                    {
                        // A ')' preceded by a backslash is an escaped literal, not the end.
                        if (customPattern.Length > 0 && customPattern[^1] == '\\')
                        {
                            customPattern[^1] = character;
                        }
                        else
                        {
                            state = TokenizerState.ParameterCustomPatternEnd;
                        }
                    }
                    else
                    {
                        customPattern.Append(character);
                    }
                    break;

                case TokenizerState.ParameterCustomPatternEnd:
                    ConsumeBuffer(character);
                    state = TokenizerState.Static;
                    if (character is not ('*' or '?' or '+'))
                    {
                        index--;
                    }
                    customPattern.Clear();
                    break;
            }
        }

        if (state == TokenizerState.ParameterCustomPattern)
        {
            throw new RouteMatcherException(
                RouteMatcherError.UnfinishedCustomPattern,
                $"Unfinished custom RegExp for param \"{buffer}\".");
        }

        ConsumeBuffer('\0');
        FinalizeSegment();

        return tokens;
    }

    // Upstream VALID_PARAM_RE = /[a-zA-Z0-9_]/ — checked directly to avoid a regular expression.
    private static bool IsValidParameterNameCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character == '_';
}
