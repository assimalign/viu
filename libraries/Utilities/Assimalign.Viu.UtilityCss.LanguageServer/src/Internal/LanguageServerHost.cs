using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.UtilityCss;

namespace Assimalign.Viu.UtilityCss.LanguageServer;

internal sealed class LanguageServerHost
{
    private const int ParseErrorCode = -32700;
    private const int InvalidRequestCode = -32600;
    private const int MethodNotFoundCode = -32601;
    private const int InvalidParametersCode = -32602;
    private const int InternalErrorCode = -32603;

    private readonly UtilityCssLanguageDocumentStore documentStore = new();
    private readonly UtilityCssProjectContextProvider projectContextProvider = new();
    private bool shutdownRequested;

    // Message framing, lifecycle ordering, document snapshots, and UTF-16 coordinate conversion
    // intentionally duplicate the established Viu server patterns. Sharing them is a later design
    // decision; this payload must not reference tooling/Editor or pull Roslyn into its closure.
    internal async Task<int> RunAsync(
        Stream input,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        var reader = new LanguageServerProtocolMessageReader(input);
        var writer = new LanguageServerProtocolMessageWriter(output);
        while (!cancellationToken.IsCancellationRequested)
        {
            JsonDocument? document;
            try
            {
                document = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is JsonException or InvalidDataException)
            {
                await WriteErrorAsync(
                        writer,
                        null,
                        ParseErrorCode,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (document is null)
            {
                return 0;
            }

            using (document)
            {
                if (await ProcessMessageAsync(
                        document.RootElement,
                        writer,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    return shutdownRequested ? 0 : 1;
                }
            }
        }

        return 0;
    }

    private async ValueTask<bool> ProcessMessageAsync(
        JsonElement message,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken cancellationToken)
    {
        if (message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind != JsonValueKind.String)
        {
            await WriteErrorAsync(
                    writer,
                    GetOptionalIdentifier(message),
                    InvalidRequestCode,
                    "A JSON-RPC message must contain a string method.",
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        var method = methodElement.GetString()!;
        var hasIdentifier = message.TryGetProperty("id", out var identifier);
        var parameters = message.TryGetProperty("params", out var parameterElement)
            ? parameterElement
            : default;

        try
        {
            switch (method)
            {
                case "initialize":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            CreateInitializeResult(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "initialized":
                case "$/setTrace":
                case "$/cancelRequest":
                case "workspace/didChangeWatchedFiles":
                    return false;

                case "shutdown":
                    shutdownRequested = true;
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "exit":
                    return true;

                case "textDocument/didOpen":
                    HandleDidOpen(parameters);
                    return false;

                case "textDocument/didChange":
                    HandleDidChange(parameters);
                    return false;

                case "textDocument/didClose":
                    HandleDidClose(parameters);
                    return false;

                case "textDocument/completion":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            ComputeCompletionResult(parameters, cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "completionItem/resolve":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            ComputeCompletionResolveResult(parameters, cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/hover":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            ComputeHoverResult(parameters, cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/documentColor":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            ComputeDocumentColorResult(parameters, cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/colorPresentation":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            ComputeColorPresentationResult(parameters, cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                default:
                    if (hasIdentifier)
                    {
                        await WriteErrorAsync(
                                writer,
                                CloneElement(identifier),
                                MethodNotFoundCode,
                                $"The method '{method}' is not supported.",
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return false;
            }
        }
        catch (LanguageServerProtocolRequestException exception)
        {
            if (hasIdentifier)
            {
                await WriteErrorAsync(
                        writer,
                        CloneElement(identifier),
                        exception.Code,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (hasIdentifier)
            {
                await WriteErrorAsync(
                        writer,
                        CloneElement(identifier),
                        InternalErrorCode,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return false;
        }
    }

    private void HandleDidOpen(JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        if (!UtilityCssDocumentRegion.IsSupported(documentUri))
        {
            return;
        }

        documentStore.Open(
            documentUri,
            GetRequiredString(textDocument, "text"),
            GetOptionalInteger(textDocument, "version"));
    }

    private void HandleDidChange(JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var changes = GetRequiredArray(parameters, "contentChanges");
        string? text = null;
        foreach (var change in changes.EnumerateArray())
        {
            text = GetRequiredString(change, "text");
        }

        if (text is null)
        {
            throw InvalidParameters("At least one full document change is required.");
        }

        documentStore.Change(
            documentUri,
            text,
            GetOptionalInteger(textDocument, "version"));
    }

    private void HandleDidClose(JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        documentStore.Close(GetRequiredString(textDocument, "uri"));
    }

    private JsonObject ComputeCompletionResult(
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var (documentUri, position) = GetDocumentPosition(parameters);
        if (!TryGetCandidateToken(
                documentUri,
                position,
                cancellationToken,
                out var document,
                out var token,
                out var context))
        {
            return CreateCompletionList(new JsonArray(), false);
        }

        var result = context.GetCompletions(
            token.Prefix,
            UtilityClassCompletionQuery.DefaultMaximumItems,
            cancellationToken);
        var items = new JsonArray();
        foreach (var metadata in result.Items)
        {
            var data = new JsonObject
            {
                ["documentUri"] = documentUri,
                ["label"] = metadata.CandidateText,
            };
            if (metadata.ColorValue is not null)
            {
                data["colorValue"] = metadata.ColorValue;
            }

            var item = new JsonObject
            {
                ["label"] = metadata.CandidateText,
                ["kind"] = metadata.ColorValue is null ? 21 : 16,
                ["detail"] = metadata.Description,
                ["insertText"] = metadata.CandidateText,
                ["insertTextFormat"] = 1,
                ["sortText"] = metadata.SortOrder.ToString(
                    "D10",
                    CultureInfo.InvariantCulture) +
                    ":" +
                    metadata.CandidateText,
                ["textEdit"] = new JsonObject
                {
                    ["range"] = ToJsonRange(
                        TextCoordinateConverter.GetRange(
                            document.Text,
                            token.SourceSpan.Start,
                            token.SourceSpan.End)),
                    ["newText"] = metadata.CandidateText,
                },
                ["data"] = data,
            };
            items.Add((JsonNode)item);
        }

        return CreateCompletionList(items, result.IsTruncated);
    }

    private JsonObject ComputeCompletionResolveResult(
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw InvalidParameters("The completion item is required.");
        }

        var item = (JsonObject)(CloneElement(parameters) ?? new JsonObject());
        var label = GetRequiredString(parameters, "label");
        if (!parameters.TryGetProperty("data", out var dataElement) ||
            dataElement.ValueKind != JsonValueKind.Object ||
            !dataElement.TryGetProperty("documentUri", out var documentUriElement) ||
            documentUriElement.ValueKind != JsonValueKind.String)
        {
            return item;
        }

        var documentUri = documentUriElement.GetString()!;
        if (!documentStore.TryGet(documentUri, out _))
        {
            return item;
        }

        var resolution = projectContextProvider
            .Get(documentUri, cancellationToken)
            .Resolve(label, cancellationToken);
        if (!resolution.IsSuccess || resolution.Metadata is null)
        {
            return item;
        }

        item["documentation"] = CreateMarkdownContent(resolution.Metadata);
        if (resolution.Metadata.ColorValue is not null && item["data"] is JsonObject data)
        {
            data["colorValue"] = resolution.Metadata.ColorValue;
        }

        return item;
    }

    private JsonNode? ComputeHoverResult(
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var (documentUri, position) = GetDocumentPosition(parameters);
        if (!TryGetCandidateToken(
                documentUri,
                position,
                cancellationToken,
                out var document,
                out var token,
                out var context))
        {
            return null;
        }

        var resolution = context.Resolve(token.Text, cancellationToken);
        if (!resolution.IsSuccess || resolution.Metadata is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["contents"] = CreateMarkdownContent(resolution.Metadata),
            ["range"] = ToJsonRange(
                TextCoordinateConverter.GetRange(
                    document.Text,
                    token.SourceSpan.Start,
                    token.SourceSpan.End)),
        };
    }

    private JsonArray ComputeDocumentColorResult(
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var results = new JsonArray();
        if (!documentStore.TryGet(documentUri, out var document) ||
            document.Region is not { } region)
        {
            return results;
        }

        var context = projectContextProvider.Get(documentUri, cancellationToken);
        var scan = UtilityCandidateScanner.Scan(
            region.Content,
            context.CreateScanOptions(documentUri, region.ContentOffset),
            cancellationToken);
        var occurrences = new List<(int Start, int End, CssColor Color)>();
        foreach (var detection in scan.Candidates)
        {
            var resolution = context.Resolve(
                detection.Candidate.RawText,
                cancellationToken);
            var colorValue = resolution.IsSuccess
                ? resolution.Metadata?.ColorValue
                : null;
            if (colorValue is null || !CssColorParser.TryParse(colorValue, out var color))
            {
                continue;
            }

            foreach (var sourceSpan in detection.SourceSpans)
            {
                occurrences.Add((sourceSpan.Start, sourceSpan.End, color));
            }
        }

        foreach (var occurrence in occurrences.OrderBy(item => item.Start))
        {
            results.Add(
                (JsonNode)new JsonObject
                {
                    ["range"] = ToJsonRange(
                        TextCoordinateConverter.GetRange(
                            document.Text,
                            occurrence.Start,
                            occurrence.End)),
                    ["color"] = ToJsonColor(occurrence.Color),
                });
        }

        return results;
    }

    private JsonArray ComputeColorPresentationResult(
        JsonElement parameters,
        CancellationToken cancellationToken)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var colorElement = GetRequiredObject(parameters, "color");
        var color = new CssColor(
            GetRequiredDouble(colorElement, "red"),
            GetRequiredDouble(colorElement, "green"),
            GetRequiredDouble(colorElement, "blue"),
            GetRequiredDouble(colorElement, "alpha"));
        var range = GetRange(GetRequiredObject(parameters, "range"));
        if (!documentStore.TryGet(documentUri, out var document) ||
            !TextCoordinateConverter.TryGetOffset(
                document.Text,
                range.Start,
                out var start) ||
            !TextCoordinateConverter.TryGetOffset(
                document.Text,
                range.End,
                out var end) ||
            end < start)
        {
            return new JsonArray();
        }

        var originalCandidate = document.Text.Substring(start, end - start);
        var context = projectContextProvider.Get(documentUri, cancellationToken);
        var label = CreateColorPresentationCandidate(
            originalCandidate,
            CssColorParser.ToHexadecimal(color),
            context,
            cancellationToken);
        return new JsonArray(
            new JsonObject
            {
                ["label"] = label,
                ["textEdit"] = new JsonObject
                {
                    ["range"] = ToJsonRange(range),
                    ["newText"] = label,
                },
            });
    }

    private static string CreateColorPresentationCandidate(
        string originalCandidate,
        string hexadecimalColor,
        UtilityCssProjectContext context,
        CancellationToken cancellationToken)
    {
        var parse = UtilityCandidateParser.Parse(
            originalCandidate,
            context.Theme.Prefix,
            context.VariantRegistry,
            cancellationToken);
        if (!parse.IsSuccess ||
            parse.Candidate is not { } candidate ||
            candidate.IsNegative ||
            string.IsNullOrEmpty(candidate.Root))
        {
            return originalCandidate;
        }

        var utility = candidate.Kind == UtilityCandidateKind.ArbitraryProperty
            ? "[" + candidate.Root + ":" + hexadecimalColor + "]"
            : candidate.Root + "-[" + hexadecimalColor + "]";
        if (candidate.IsImportant)
        {
            utility += "!";
        }

        var variants = string.Join(
            ":",
            candidate.Variants.Select(variant => variant.RawText));
        var presentationCandidate = variants.Length == 0
            ? utility
            : variants + ":" + utility;
        var resolution = context.Resolve(
            presentationCandidate,
            cancellationToken);
        return resolution.IsSuccess && resolution.Metadata?.ColorValue is not null
            ? presentationCandidate
            : originalCandidate;
    }

    private bool TryGetCandidateToken(
        string documentUri,
        TextPosition position,
        CancellationToken cancellationToken,
        out UtilityCssLanguageDocument document,
        out UtilityCandidateToken token,
        out UtilityCssProjectContext context)
    {
        token = null!;
        context = UtilityCssProjectContext.Default;
        if (!documentStore.TryGet(documentUri, out document!) ||
            document.Region is not { } region ||
            !TextCoordinateConverter.TryGetOffset(
                document.Text,
                position,
                out var documentOffset) ||
            !region.TryGetContentPosition(documentOffset, out var contentPosition) ||
            !UtilityCandidateScanner.IsInsideAttributeValue(
                region.Content,
                contentPosition,
                cancellationToken))
        {
            return false;
        }

        context = projectContextProvider.Get(documentUri, cancellationToken);
        token = UtilityCandidateScanner.FindTokenAtPosition(
            region.Content,
            contentPosition,
            context.CreateScanOptions(documentUri, region.ContentOffset),
            cancellationToken)!;
        return token is not null;
    }

    private static JsonObject CreateInitializeResult()
        => new()
        {
            ["capabilities"] = new JsonObject
            {
                ["positionEncoding"] = "utf-16",
                ["textDocumentSync"] = new JsonObject
                {
                    ["openClose"] = true,
                    ["change"] = 1,
                },
                ["completionProvider"] = new JsonObject
                {
                    ["resolveProvider"] = true,
                    ["triggerCharacters"] = new JsonArray(
                        "\"",
                        "'",
                        " ",
                        "-",
                        ":"),
                },
                ["hoverProvider"] = true,
                ["colorProvider"] = true,
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "Assimalign.Viu.UtilityCss.LanguageServer",
                ["version"] = "0.1.0",
            },
        };

    private static JsonObject CreateCompletionList(
        JsonArray items,
        bool isIncomplete)
        => new()
        {
            ["isIncomplete"] = isIncomplete,
            ["items"] = items,
        };

    private static JsonObject CreateMarkdownContent(UtilityClassMetadata metadata)
        => new()
        {
            ["kind"] = "markdown",
            ["value"] =
                "`" +
                metadata.CandidateText +
                "` — " +
                metadata.Description +
                "\n\n```css\n" +
                metadata.Css +
                "\n```",
        };

    private static JsonObject ToJsonColor(CssColor color)
        => new()
        {
            ["red"] = color.Red,
            ["green"] = color.Green,
            ["blue"] = color.Blue,
            ["alpha"] = color.Alpha,
        };

    private static (string DocumentUri, TextPosition Position) GetDocumentPosition(
        JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        return (
            documentUri,
            GetPosition(GetRequiredObject(parameters, "position")));
    }

    private static TextRange GetRange(JsonElement element)
        => new(
            GetPosition(GetRequiredObject(element, "start")),
            GetPosition(GetRequiredObject(element, "end")));

    private static TextPosition GetPosition(JsonElement element)
        => new(
            GetRequiredInteger(element, "line"),
            GetRequiredInteger(element, "character"));

    private static JsonObject ToJsonRange(TextRange range)
        => new()
        {
            ["start"] = ToJsonPosition(range.Start),
            ["end"] = ToJsonPosition(range.End),
        };

    private static JsonObject ToJsonPosition(TextPosition position)
        => new()
        {
            ["line"] = position.Line,
            ["character"] = position.Character,
        };

    private static JsonElement GetRequiredObject(
        JsonElement parent,
        string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            throw InvalidParameters($"The '{propertyName}' object is required.");
        }

        return property;
    }

    private static JsonElement GetRequiredArray(
        JsonElement parent,
        string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            throw InvalidParameters($"The '{propertyName}' array is required.");
        }

        return property;
    }

    private static string GetRequiredString(
        JsonElement parent,
        string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw InvalidParameters($"The '{propertyName}' string is required.");
        }

        return property.GetString()!;
    }

    private static int GetRequiredInteger(
        JsonElement parent,
        string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            !property.TryGetInt32(out var value))
        {
            throw InvalidParameters($"The '{propertyName}' integer is required.");
        }

        return value;
    }

    private static double GetRequiredDouble(
        JsonElement parent,
        string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            !property.TryGetDouble(out var value) ||
            !double.IsFinite(value))
        {
            throw InvalidParameters($"The '{propertyName}' number is required.");
        }

        return value;
    }

    private static int? GetOptionalInteger(
        JsonElement parent,
        string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.TryGetInt32(out var value))
        {
            return value;
        }

        throw InvalidParameters($"The '{propertyName}' value must be an integer.");
    }

    private static JsonElement RequireIdentifier(
        bool hasIdentifier,
        JsonElement identifier)
    {
        if (!hasIdentifier)
        {
            throw InvalidParameters("The request identifier is required.");
        }

        return identifier;
    }

    private static JsonNode? GetOptionalIdentifier(JsonElement message)
        => message.ValueKind == JsonValueKind.Object &&
           message.TryGetProperty("id", out var identifier)
            ? CloneElement(identifier)
            : null;

    private static LanguageServerProtocolRequestException InvalidParameters(string message)
        => new(InvalidParametersCode, message);

    private static ValueTask WriteResultAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonElement identifier,
        JsonNode? result,
        CancellationToken cancellationToken)
        => WriteResultAsync(
            writer,
            CloneElement(identifier),
            result,
            cancellationToken);

    private static async ValueTask WriteResultAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonNode? identifier,
        JsonNode? result,
        CancellationToken cancellationToken)
        => await writer.WriteAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = identifier,
                    ["result"] = result,
                },
                cancellationToken)
            .ConfigureAwait(false);

    private static async ValueTask WriteErrorAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonNode? identifier,
        int code,
        string message,
        CancellationToken cancellationToken)
        => await writer.WriteAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = identifier,
                    ["error"] = new JsonObject
                    {
                        ["code"] = code,
                        ["message"] = message,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

    private static JsonNode? CloneElement(JsonElement element)
        => JsonNode.Parse(element.GetRawText());
}
