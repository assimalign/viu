using System;
using System.Collections.Generic;
using System.Threading;

namespace Assimalign.Viu.Tooling.UtilityCss;

internal static class UtilityCandidateScanEngine
{
    public static UtilityCandidateScanResult Scan(
        string? text,
        UtilityCandidateScanOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        if (string.IsNullOrEmpty(text))
        {
            return new UtilityCandidateScanResult(
                UtilityCollection<UtilityCandidateDetection>.Empty,
                UtilityCollection<UtilityCandidateScanDiagnostic>.Empty);
        }

        var diagnostics = new List<UtilityCandidateScanDiagnostic>();
        var regions = UtilityPlainTextCandidateScanner.Scan(
            text!,
            cancellationToken);
        var occurrences = new List<DetectedOccurrence>();

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanRegion(
                text!,
                region,
                options,
                occurrences,
                diagnostics,
                cancellationToken);
        }

        return BuildResult(
            occurrences,
            diagnostics,
            cancellationToken);
    }

    public static UtilityCandidateToken? FindTokenAtPosition(
        string? text,
        int position,
        UtilityCandidateScanOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);

        if (string.IsNullOrEmpty(text) ||
            position < 0 ||
            position > text!.Length)
        {
            return null;
        }

        var regions = CollectRegions(
            text!,
            cancellationToken);

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (position < region.Start || position > region.End)
            {
                continue;
            }

            var span = UtilityCandidateTextScanner.FindAtPosition(
                text!,
                region.Start,
                region.End,
                position,
                cancellationToken);

            if (IsDynamicToken(text!, span, region))
            {
                return null;
            }

            var prefixLength = position - span.Start;
            if (prefixLength < 0)
            {
                prefixLength = 0;
            }
            else if (prefixLength > span.Length)
            {
                prefixLength = span.Length;
            }

            return new UtilityCandidateToken(
                text!.Substring(span.Start, span.Length),
                text.Substring(span.Start, prefixLength),
                CreateSourceSpan(
                    options,
                    span.Start,
                    span.Length));
        }

        return null;
    }

    internal static bool IsInsideAttributeValue(
        string? text,
        int position,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(text) ||
            position < 0 ||
            position > text!.Length)
        {
            return false;
        }

        var attributeValues = new List<UtilityCandidateTextSpan>();
        CollectRegions(
            text!,
            attributeValues,
            cancellationToken);

        foreach (var attributeValue in attributeValues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (position >= attributeValue.Start &&
                position <= attributeValue.End)
            {
                return true;
            }
        }

        return false;
    }

    private static List<UtilityCandidateScanRegion> CollectRegions(
        string text,
        CancellationToken cancellationToken) =>
        CollectRegions(
            text,
            null,
            cancellationToken);

    private static List<UtilityCandidateScanRegion> CollectRegions(
        string text,
        ICollection<UtilityCandidateTextSpan>? attributeValues,
        CancellationToken cancellationToken)
    {
        var regions = new List<UtilityCandidateScanRegion>();
        var index = 0;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (text[index] != '<')
            {
                index++;
                continue;
            }

            if (Matches(text, index, "<!--"))
            {
                var commentEnd = FindSequence(
                    text,
                    index + 4,
                    "-->",
                    cancellationToken);
                index = commentEnd < 0
                    ? text.Length
                    : commentEnd + 3;
                continue;
            }

            var tagStart = index;
            index++;
            if (index < text.Length &&
                text[index] is '!' or '?')
            {
                index = FindTagEnd(
                    text,
                    index + 1,
                    cancellationToken);
                continue;
            }

            var isClosingTag =
                index < text.Length &&
                text[index] == '/';
            if (isClosingTag)
            {
                index++;
            }

            SkipWhitespace(
                text,
                ref index,
                text.Length,
                cancellationToken);
            var tagNameStart = index;
            while (index < text.Length &&
                   !char.IsWhiteSpace(text[index]) &&
                   text[index] is not ('>' or '/'))
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
            }

            if (tagNameStart == index)
            {
                index = tagStart + 1;
                continue;
            }

            if (isClosingTag)
            {
                index = FindTagEnd(
                    text,
                    index,
                    cancellationToken);
                continue;
            }

            while (index < text.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SkipWhitespace(
                    text,
                    ref index,
                    text.Length,
                    cancellationToken);

                if (index >= text.Length)
                {
                    break;
                }

                if (text[index] == '>')
                {
                    index++;
                    break;
                }

                if (text[index] == '/')
                {
                    index++;
                    continue;
                }

                var attributeNameStart = index;
                while (index < text.Length &&
                       !IsAttributeNameTerminator(text[index]))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    index++;
                }

                if (attributeNameStart == index)
                {
                    index++;
                    continue;
                }

                var attributeName = text.Substring(
                    attributeNameStart,
                    index - attributeNameStart);

                SkipWhitespace(
                    text,
                    ref index,
                    text.Length,
                    cancellationToken);
                if (index >= text.Length ||
                    text[index] != '=')
                {
                    continue;
                }

                index++;
                SkipWhitespace(
                    text,
                    ref index,
                    text.Length,
                    cancellationToken);
                if (index >= text.Length)
                {
                    break;
                }

                var quote = text[index];
                var isQuoted = quote is '"' or '\'';
                var valueStart = isQuoted
                    ? index + 1
                    : index;
                var valueEnd = isQuoted
                    ? FindClosingQuote(
                        text,
                        valueStart,
                        text.Length,
                        quote,
                        cancellationToken)
                    : FindUnquotedAttributeEnd(
                        text,
                        valueStart,
                        cancellationToken);
                // Every attribute value this walk resolves is an editor context in which markup-name
                // completion is wrong, including the values that carry no utility candidate. Recording
                // the span here reuses the single tag/attribute walk instead of adding a second markup
                // parser to the language service.
                attributeValues?.Add(
                    new UtilityCandidateTextSpan(
                        valueStart,
                        valueEnd - valueStart));

                if (IsStaticClassAttribute(attributeName))
                {
                    regions.Add(
                        new UtilityCandidateScanRegion(
                            valueStart,
                            valueEnd,
                            false,
                            false));

                }
                else if (IsBoundClassAttribute(attributeName))
                {
                    CollectBindingRegions(
                        text,
                        valueStart,
                        valueEnd,
                        regions,
                        cancellationToken);
                }

                index = isQuoted && valueEnd < text.Length
                    ? valueEnd + 1
                    : valueEnd;
            }
        }

        regions.Sort(
            static (left, right) =>
                left.Start != right.Start
                    ? left.Start.CompareTo(right.Start)
                    : left.End.CompareTo(right.End));
        return regions;
    }

    private static void CollectBindingRegions(
        string text,
        int start,
        int end,
        ICollection<UtilityCandidateScanRegion> regions,
        CancellationToken cancellationToken)
    {
        var delimiterStack = new List<char>();
        var index = start;

        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];

            if (character is '\'' or '"' or '`')
            {
                var literalQuoteStart = index;
                var literalStart = index + 1;
                var literalEnd = FindClosingQuote(
                    text,
                    literalStart,
                    end,
                    character,
                    cancellationToken);
                var hasClosingQuote = literalEnd < end;
                var afterLiteral = hasClosingQuote
                    ? literalEnd + 1
                    : literalEnd;

                regions.Add(
                    new UtilityCandidateScanRegion(
                        literalStart,
                        literalEnd,
                        IsConcatenatedBefore(
                            text,
                            start,
                            literalQuoteStart),
                        IsConcatenatedAfter(
                            text,
                            afterLiteral,
                            end)));

                if (!hasClosingQuote)
                {
                    break;
                }

                index = afterLiteral;
                continue;
            }

            if (TryGetClosingDelimiter(character, out var closingDelimiter))
            {
                delimiterStack.Add(closingDelimiter);
                index++;
                continue;
            }

            if (IsClosingDelimiter(character))
            {
                if (delimiterStack.Count != 0 &&
                    delimiterStack[delimiterStack.Count - 1] == character)
                {
                    delimiterStack.RemoveAt(delimiterStack.Count - 1);
                }

                index++;
                continue;
            }

            if (character == ':' &&
                delimiterStack.Count != 0 &&
                delimiterStack[delimiterStack.Count - 1] == '}' &&
                TryGetUnquotedObjectKey(
                    text,
                    start,
                    index,
                    cancellationToken,
                    out var keyStart,
                    out var keyEnd))
            {
                regions.Add(
                    new UtilityCandidateScanRegion(
                        keyStart,
                        keyEnd,
                        false,
                        false));
            }

            index++;
        }
    }

    private static bool TryGetUnquotedObjectKey(
        string text,
        int expressionStart,
        int colon,
        CancellationToken cancellationToken,
        out int keyStart,
        out int keyEnd)
    {
        keyStart = colon;
        keyEnd = colon;

        var segmentStart = colon - 1;
        while (segmentStart >= expressionStart &&
               text[segmentStart] is not ('{' or ','))
        {
            cancellationToken.ThrowIfCancellationRequested();
            segmentStart--;
        }

        segmentStart++;
        var segmentEnd = colon;
        while (segmentStart < segmentEnd &&
               char.IsWhiteSpace(text[segmentStart]))
        {
            segmentStart++;
        }

        while (segmentEnd > segmentStart &&
               char.IsWhiteSpace(text[segmentEnd - 1]))
        {
            segmentEnd--;
        }

        if (segmentStart == segmentEnd)
        {
            return false;
        }

        for (var index = segmentStart; index < segmentEnd; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];
            if (char.IsWhiteSpace(character) ||
                character is '?' or '\'' or '"' or '`' or '[' or ']' or '(' or ')')
            {
                return false;
            }
        }

        keyStart = segmentStart;
        keyEnd = segmentEnd;
        return true;
    }

    private static void ScanRegion(
        string text,
        UtilityCandidateScanRegion region,
        UtilityCandidateScanOptions options,
        ICollection<DetectedOccurrence> occurrences,
        ICollection<UtilityCandidateScanDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var spans = UtilityCandidateTextScanner.Scan(
            text,
            region.Start,
            region.End,
            cancellationToken);

        foreach (var span in spans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (span.Length == 0)
            {
                continue;
            }

            var rawText = text.Substring(span.Start, span.Length);
            if (IsDynamicToken(text, span, region))
            {
                diagnostics.Add(
                    CreateDiagnostic(
                        options,
                        UtilityCandidateScanDiagnosticCode.DynamicInterpolation,
                        UtilityCandidateDiagnosticSeverity.Error,
                        "A runtime interpolation or concatenation fragment is not a complete literal utility candidate.",
                        span.Start,
                        span.Length,
                        null));
                continue;
            }

            var parse = UtilityCandidateParser.Parse(
                rawText,
                options.Prefix,
                options.VariantRegistry ?? UtilityVariantRegistry.BuiltIn,
                cancellationToken);

            foreach (var diagnostic in parse.Diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnostics.Add(
                    CreateDiagnostic(
                        options,
                        UtilityCandidateScanDiagnosticCode.CandidateParserDiagnostic,
                        diagnostic.Severity,
                        diagnostic.Message,
                        span.Start + diagnostic.Start,
                        diagnostic.Length,
                        diagnostic.Code));
            }

            if (parse.Candidate is not null)
            {
                occurrences.Add(
                    new DetectedOccurrence(
                        parse.Candidate,
                        CreateSourceSpan(
                            options,
                            span.Start,
                            span.Length)));
            }
        }
    }

    private static UtilityCandidateScanResult BuildResult(
        List<DetectedOccurrence> occurrences,
        List<UtilityCandidateScanDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        occurrences.Sort(
            static (left, right) =>
            {
                var textComparison = string.CompareOrdinal(
                    left.Candidate.RawText,
                    right.Candidate.RawText);
                if (textComparison != 0)
                {
                    return textComparison;
                }

                var startComparison = left.SourceSpan.Start.CompareTo(
                    right.SourceSpan.Start);
                return startComparison != 0
                    ? startComparison
                    : left.SourceSpan.Length.CompareTo(right.SourceSpan.Length);
            });

        var detections = new List<UtilityCandidateDetection>();
        var occurrenceIndex = 0;
        while (occurrenceIndex < occurrences.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var first = occurrences[occurrenceIndex];
            var spans = new List<UtilityCandidateSourceSpan>();
            var nextIndex = occurrenceIndex;

            while (nextIndex < occurrences.Count &&
                   string.Equals(
                       occurrences[nextIndex].Candidate.RawText,
                       first.Candidate.RawText,
                       StringComparison.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                spans.Add(occurrences[nextIndex].SourceSpan);
                nextIndex++;
            }

            detections.Add(
                new UtilityCandidateDetection(
                    first.Candidate,
                    new UtilityCollection<UtilityCandidateSourceSpan>(spans)));
            occurrenceIndex = nextIndex;
        }

        diagnostics.Sort(
            static (left, right) =>
            {
                var startComparison = left.SourceSpan.Start.CompareTo(
                    right.SourceSpan.Start);
                if (startComparison != 0)
                {
                    return startComparison;
                }

                var lengthComparison = left.SourceSpan.Length.CompareTo(
                    right.SourceSpan.Length);
                if (lengthComparison != 0)
                {
                    return lengthComparison;
                }

                var codeComparison = left.Code.CompareTo(right.Code);
                if (codeComparison != 0)
                {
                    return codeComparison;
                }

                return Nullable.Compare(
                    left.CandidateDiagnosticCode,
                    right.CandidateDiagnosticCode);
            });

        return new UtilityCandidateScanResult(
            detections.Count == 0
                ? UtilityCollection<UtilityCandidateDetection>.Empty
                : new UtilityCollection<UtilityCandidateDetection>(detections),
            diagnostics.Count == 0
                ? UtilityCollection<UtilityCandidateScanDiagnostic>.Empty
                : new UtilityCollection<UtilityCandidateScanDiagnostic>(diagnostics));
    }

    private static UtilityCandidateScanDiagnostic CreateDiagnostic(
        UtilityCandidateScanOptions options,
        UtilityCandidateScanDiagnosticCode code,
        UtilityCandidateDiagnosticSeverity severity,
        string message,
        int start,
        int length,
        UtilityCandidateDiagnosticCode? candidateDiagnosticCode) =>
        new(
            code,
            severity,
            message,
            CreateSourceSpan(
                options,
                start,
                length),
            candidateDiagnosticCode);

    private static UtilityCandidateSourceSpan CreateSourceSpan(
        UtilityCandidateScanOptions options,
        int start,
        int length) =>
        new(
            options.SourceIdentity,
            checked(options.ContentOffset + start),
            length);

    private static bool IsDynamicToken(
        string text,
        UtilityCandidateTextSpan span,
        UtilityCandidateScanRegion region)
    {
        if ((region.RejectsStartBoundary && span.Start == region.Start) ||
            (region.RejectsEndBoundary && span.End == region.End))
        {
            return true;
        }

        var rawText = text.Substring(span.Start, span.Length);
        return ContainsDynamicInterpolation(rawText);
    }

    private static bool ContainsDynamicInterpolation(string text)
    {
        if (text.IndexOf("${", StringComparison.Ordinal) >= 0 ||
            text.IndexOf("{{", StringComparison.Ordinal) >= 0 ||
            text.IndexOf("}}", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        var closingDelimiters = new List<char>();
        var quote = '\0';
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

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (closingDelimiters.Count != 0 &&
                character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character is '[' or '(')
            {
                closingDelimiters.Add(
                    character == '['
                        ? ']'
                        : ')');
                continue;
            }

            if (character is ']' or ')')
            {
                if (closingDelimiters.Count != 0 &&
                    closingDelimiters[closingDelimiters.Count - 1] == character)
                {
                    closingDelimiters.RemoveAt(closingDelimiters.Count - 1);
                }

                continue;
            }

            if (character == '{' &&
                closingDelimiters.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConcatenatedBefore(
        string text,
        int start,
        int quoteStart)
    {
        var index = quoteStart - 1;
        while (index >= start && char.IsWhiteSpace(text[index]))
        {
            index--;
        }

        return index >= start && text[index] == '+';
    }

    private static bool IsConcatenatedAfter(
        string text,
        int afterQuote,
        int end)
    {
        var index = afterQuote;
        while (index < end && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index < end && text[index] == '+';
    }

    private static int FindClosingQuote(
        string text,
        int start,
        int end,
        char quote,
        CancellationToken cancellationToken)
    {
        var isEscaped = false;
        for (var index = start; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            if (character == quote)
            {
                return index;
            }
        }

        return end;
    }

    private static int FindUnquotedAttributeEnd(
        string text,
        int start,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < text.Length &&
               !char.IsWhiteSpace(text[index]) &&
               text[index] != '>')
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
        }

        return index;
    }

    private static int FindTagEnd(
        string text,
        int start,
        CancellationToken cancellationToken)
    {
        var index = start;
        var quote = '\0';
        var isEscaped = false;

        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var character = text[index];
            if (isEscaped)
            {
                isEscaped = false;
                index++;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                index++;
                continue;
            }

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                index++;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                index++;
                continue;
            }

            index++;
            if (character == '>')
            {
                break;
            }
        }

        return index;
    }

    private static int FindSequence(
        string text,
        int start,
        string sequence,
        CancellationToken cancellationToken)
    {
        for (var index = start;
             index <= text.Length - sequence.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Matches(text, index, sequence))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Matches(
        string text,
        int start,
        string value)
    {
        if (start < 0 ||
            start + value.Length > text.Length)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (text[start + index] != value[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void SkipWhitespace(
        string text,
        ref int index,
        int end,
        CancellationToken cancellationToken)
    {
        while (index < end && char.IsWhiteSpace(text[index]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
        }
    }

    private static bool IsAttributeNameTerminator(char character) =>
        char.IsWhiteSpace(character) ||
        character is '=' or '>' or '/';

    private static bool IsStaticClassAttribute(string name) =>
        string.Equals(name, "class", StringComparison.OrdinalIgnoreCase);

    private static bool IsBoundClassAttribute(string name) =>
        string.Equals(name, ":class", StringComparison.Ordinal) ||
        string.Equals(name, "v-bind:class", StringComparison.Ordinal);

    private static bool TryGetClosingDelimiter(
        char character,
        out char closingDelimiter)
    {
        switch (character)
        {
            case '(':
                closingDelimiter = ')';
                return true;
            case '[':
                closingDelimiter = ']';
                return true;
            case '{':
                closingDelimiter = '}';
                return true;
            default:
                closingDelimiter = '\0';
                return false;
        }
    }

    private static bool IsClosingDelimiter(char character) =>
        character is ')' or ']' or '}';

    private static void ValidateOptions(UtilityCandidateScanOptions options)
    {
        if (options.ContentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The scan content offset cannot be negative.");
        }
    }

    private readonly struct DetectedOccurrence
    {
        public DetectedOccurrence(
            UtilityCandidate candidate,
            UtilityCandidateSourceSpan sourceSpan)
        {
            Candidate = candidate;
            SourceSpan = sourceSpan;
        }

        public UtilityCandidate Candidate { get; }

        public UtilityCandidateSourceSpan SourceSpan { get; }
    }
}
