using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.LanguageService;

namespace Assimalign.Viu.LanguageServer;

internal sealed class LanguageServerHost
{
    private const int ParseErrorCode = -32700;
    private const int InvalidRequestCode = -32600;
    private const int MethodNotFoundCode = -32601;
    private const int InvalidParametersCode = -32602;
    private const int InternalErrorCode = -32603;

    private readonly IViuLanguageService languageService;
    private readonly HashSet<string> openSupportedDocuments =
        new(StringComparer.OrdinalIgnoreCase);
    private LanguageServerClientCapabilities clientCapabilities =
        LanguageServerClientCapabilities.Default;
    private bool shutdownRequested;

    internal LanguageServerHost()
        : this(ViuLanguageServices.Create())
    {
    }

    internal LanguageServerHost(IViuLanguageService languageService)
        => this.languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));

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
            catch (JsonException exception)
            {
                await WriteErrorAsync(
                        writer,
                        id: null,
                        ParseErrorCode,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }
            catch (InvalidDataException exception)
            {
                await WriteErrorAsync(
                        writer,
                        id: null,
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
                var shouldExit = await ProcessMessageAsync(
                        document.RootElement,
                        writer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (shouldExit)
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
                    clientCapabilities = LanguageServerClientCapabilities.Read(parameters);
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
                    return false;

                case "shutdown":
                    shutdownRequested = true;
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            result: null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "exit":
                    return true;

                case "textDocument/didOpen":
                    await HandleDidOpenAsync(parameters, writer, cancellationToken).ConfigureAwait(false);
                    return false;

                case "textDocument/didChange":
                    await HandleDidChangeAsync(parameters, writer, cancellationToken).ConfigureAwait(false);
                    return false;

                case "textDocument/didClose":
                    await HandleDidCloseAsync(parameters, writer, cancellationToken).ConfigureAwait(false);
                    return false;

                case "textDocument/completion":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            HandleCompletion(parameters),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/hover":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            HandleHover(parameters),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "completionItem/resolve":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            HandleCompletionItemResolve(parameters),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/documentSymbol":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            HandleDocumentSymbol(parameters),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/foldingRange":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            HandleFoldingRange(parameters),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "textDocument/codeAction":
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            HandleCodeAction(parameters),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                default:
                    if (hasIdentifier)
                    {
                        await WriteErrorAsync(
                                writer,
                                identifier,
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
                        identifier,
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
                        identifier,
                        InternalErrorCode,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return false;
        }
    }

    private async ValueTask HandleDidOpenAsync(
        JsonElement parameters,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken cancellationToken)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var text = GetRequiredString(textDocument, "text");
        var version = GetOptionalInteger(textDocument, "version");

        if (ViuDocumentSupport.IsSupported(documentUri))
        {
            openSupportedDocuments.Add(documentUri);
            ConfigureUtilityStylesheet(documentUri);
            languageService.OpenDocument(documentUri, text, version);
        }
        else if (openSupportedDocuments.Remove(documentUri))
        {
            languageService.CloseDocument(documentUri);
        }

        await PublishDiagnosticsAsync(writer, documentUri, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleDidChangeAsync(
        JsonElement parameters,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken cancellationToken)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var version = GetOptionalInteger(textDocument, "version");

        if (!IsOpenAndSupported(documentUri))
        {
            await PublishDiagnosticsAsync(writer, documentUri, cancellationToken).ConfigureAwait(false);
            return;
        }

        var contentChanges = GetRequiredArray(parameters, "contentChanges");
        var changes = new List<LanguageDocumentChange>();

        foreach (var contentChange in contentChanges.EnumerateArray())
        {
            if (contentChange.ValueKind != JsonValueKind.Object)
            {
                throw InvalidParameters("Each content change must be an object.");
            }

            var text = GetRequiredString(contentChange, "text");
            LanguageRange? range = null;
            if (contentChange.TryGetProperty("range", out var rangeElement) &&
                rangeElement.ValueKind != JsonValueKind.Null)
            {
                range = GetRange(rangeElement);
            }

            changes.Add(new LanguageDocumentChange(range, text));
        }

        languageService.ChangeDocument(documentUri, version, changes);
        await PublishDiagnosticsAsync(writer, documentUri, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleDidCloseAsync(
        JsonElement parameters,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken cancellationToken)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");

        if (openSupportedDocuments.Remove(documentUri))
        {
            languageService.CloseDocument(documentUri);
        }

        await WriteNotificationAsync(
                writer,
                "textDocument/publishDiagnostics",
                new JsonObject
                {
                    ["uri"] = documentUri,
                    ["diagnostics"] = new JsonArray(),
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private JsonObject HandleCompletion(JsonElement parameters)
    {
        var (documentUri, position) = GetDocumentPosition(parameters);
        if (!IsOpenAndSupported(documentUri))
        {
            return CreateCompletionList(new JsonArray(), isIncomplete: false);
        }

        ConfigureUtilityStylesheet(documentUri);
        var completions = languageService.GetCompletions(documentUri, position);
        var items = new JsonArray();
        foreach (var completion in completions)
        {
            var item = new JsonObject
            {
                ["label"] = completion.Label,
                ["kind"] = (int)completion.Kind,
                ["detail"] = completion.Detail,
                ["insertText"] = completion.InsertText,
                ["insertTextFormat"] = completion.IsSnippet ? 2 : 1,
                ["sortText"] = completion.SortText,
                // Every item carries the resolve round-trip payload: the label is a sufficient
                // lookup key (resolution recomputes through the hover path) and the URI selects
                // the per-document stylesheet context, keeping the server stateless.
                ["data"] = new JsonObject
                {
                    ["documentUri"] = documentUri,
                    ["label"] = completion.Label,
                },
            };

            // Deferred documentation (utility items) is omitted entirely and computed by
            // completionItem/resolve; inline one-liners keep shipping with the item.
            if (!string.IsNullOrEmpty(completion.Documentation))
            {
                item["documentation"] = LanguageServerMarkupContent.Create(
                    completion.Documentation,
                    clientCapabilities.CompletionDocumentationSupportsMarkdown);
            }

            if (!string.IsNullOrEmpty(completion.FilterText))
            {
                item["filterText"] = completion.FilterText;
            }

            if (completion.EditRange is { } editRange)
            {
                item["textEdit"] = new JsonObject
                {
                    ["range"] = ToJsonRange(editRange),
                    ["newText"] = completion.InsertText,
                };
            }

            items.Add((JsonNode)item);
        }

        // A truncated utility result must be advertised as incomplete, otherwise the client filters
        // its cached page instead of re-requesting and the narrower candidate is never offered.
        return CreateCompletionList(
            items,
            completions.Count >= LanguageCompletionLimits.MaximumItems);
    }

    private JsonNode? HandleHover(JsonElement parameters)
    {
        var (documentUri, position) = GetDocumentPosition(parameters);
        if (!IsOpenAndSupported(documentUri))
        {
            return null;
        }

        ConfigureUtilityStylesheet(documentUri);
        var hover = languageService.GetHover(documentUri, position);
        if (hover is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["contents"] = LanguageServerMarkupContent.Create(
                hover.Markdown,
                clientCapabilities.HoverSupportsMarkdown),
            ["range"] = ToJsonRange(hover.Range),
        };
    }

    private JsonNode HandleCompletionItemResolve(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw InvalidParameters("The completion item is required.");
        }

        var completionLabel = GetRequiredString(parameters, "label");
        var item = (JsonObject)CloneElement(parameters)!;

        // A missing payload or a closed document echoes the item back unchanged: Visual Studio
        // resolves items asynchronously, and a race with didClose must never surface as an error.
        if (!parameters.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("documentUri", out var uriElement) ||
            uriElement.ValueKind != JsonValueKind.String)
        {
            return item;
        }

        var documentUri = uriElement.GetString()!;
        if (!IsOpenAndSupported(documentUri))
        {
            return item;
        }

        ConfigureUtilityStylesheet(documentUri);
        var documentation = languageService.ResolveCompletionDocumentation(
            documentUri,
            completionLabel);
        if (documentation is not null)
        {
            item["documentation"] = LanguageServerMarkupContent.Create(
                documentation,
                clientCapabilities.CompletionDocumentationSupportsMarkdown);
        }

        return item;
    }

    private JsonArray HandleDocumentSymbol(JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var results = new JsonArray();
        if (!IsOpenAndSupported(documentUri))
        {
            return results;
        }

        foreach (var symbol in languageService.GetDocumentSymbols(documentUri))
        {
            if (clientCapabilities.SupportsHierarchicalDocumentSymbols)
            {
                results.Add((JsonNode)ToJsonDocumentSymbol(symbol));
            }
            else
            {
                AppendSymbolInformation(symbol, documentUri, results);
            }
        }

        return results;
    }

    private JsonArray HandleFoldingRange(JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var results = new JsonArray();
        if (!IsOpenAndSupported(documentUri))
        {
            return results;
        }

        foreach (var foldingRange in languageService.GetFoldingRanges(documentUri))
        {
            var json = new JsonObject
            {
                ["startLine"] = foldingRange.StartLine,
                ["endLine"] = foldingRange.EndLine,
            };
            if (foldingRange.Kind is not null)
            {
                json["kind"] = foldingRange.Kind;
            }

            results.Add((JsonNode)json);
        }

        return results;
    }

    private JsonArray HandleCodeAction(JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var range = GetRange(GetRequiredObject(parameters, "range"));
        var results = new JsonArray();

        // Without codeActionLiteralSupport only Command[] results are legal, and this server
        // implements no commands, so the honest answer is an empty result.
        if (!clientCapabilities.SupportsCodeActionLiterals ||
            !IsOpenAndSupported(documentUri))
        {
            return results;
        }

        foreach (var action in languageService.GetCodeActions(documentUri, range))
        {
            var diagnostics = new JsonArray();
            foreach (var diagnostic in action.Diagnostics)
            {
                diagnostics.Add((JsonNode)ToJsonDiagnostic(diagnostic));
            }

            var edits = new JsonArray();
            foreach (var edit in action.Edits)
            {
                edits.Add(
                    (JsonNode)new JsonObject
                    {
                        ["range"] = ToJsonRange(edit.Range),
                        ["newText"] = edit.NewText,
                    });
            }

            results.Add(
                (JsonNode)new JsonObject
                {
                    ["title"] = action.Title,
                    ["kind"] = action.Kind,
                    ["isPreferred"] = action.IsPreferred,
                    ["diagnostics"] = diagnostics,
                    ["edit"] = new JsonObject
                    {
                        ["changes"] = new JsonObject
                        {
                            [documentUri] = edits,
                        },
                    },
                });
        }

        return results;
    }

    private static JsonObject ToJsonDocumentSymbol(LanguageDocumentSymbol symbol)
    {
        var children = new JsonArray();
        foreach (var child in symbol.Children)
        {
            children.Add((JsonNode)ToJsonDocumentSymbol(child));
        }

        var json = new JsonObject
        {
            ["name"] = symbol.Name,
            ["kind"] = (int)symbol.Kind,
            ["range"] = ToJsonRange(symbol.Range),
            ["selectionRange"] = ToJsonRange(symbol.SelectionRange),
            ["children"] = children,
        };
        if (symbol.Detail is not null)
        {
            json["detail"] = symbol.Detail;
        }

        return json;
    }

    private static void AppendSymbolInformation(
        LanguageDocumentSymbol symbol,
        string documentUri,
        JsonArray results)
    {
        results.Add(
            (JsonNode)new JsonObject
            {
                ["name"] = symbol.Name,
                ["kind"] = (int)symbol.Kind,
                ["location"] = new JsonObject
                {
                    ["uri"] = documentUri,
                    ["range"] = ToJsonRange(symbol.Range),
                },
            });
        foreach (var child in symbol.Children)
        {
            AppendSymbolInformation(child, documentUri, results);
        }
    }

    private async ValueTask PublishDiagnosticsAsync(
        LanguageServerProtocolMessageWriter writer,
        string documentUri,
        CancellationToken cancellationToken)
    {
        var diagnostics = new JsonArray();
        if (IsOpenAndSupported(documentUri))
        {
            foreach (var diagnostic in languageService.GetDiagnostics(documentUri))
            {
                diagnostics.Add((JsonNode)ToJsonDiagnostic(diagnostic));
            }
        }

        await WriteNotificationAsync(
                writer,
                "textDocument/publishDiagnostics",
                new JsonObject
                {
                    ["uri"] = documentUri,
                    ["diagnostics"] = diagnostics,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private bool IsOpenAndSupported(string documentUri)
    {
        if (!openSupportedDocuments.Contains(documentUri))
        {
            return false;
        }

        if (ViuDocumentSupport.IsSupported(documentUri))
        {
            return true;
        }

        openSupportedDocuments.Remove(documentUri);
        languageService.CloseDocument(documentUri);
        return false;
    }

    private static JsonObject CreateCompletionList(JsonArray items, bool isIncomplete)
        => new()
        {
            ["isIncomplete"] = isIncomplete,
            ["items"] = items,
        };

    private static JsonObject CreateInitializeResult()
        => new()
        {
            ["capabilities"] = new JsonObject
            {
                ["positionEncoding"] = "utf-16",
                ["textDocumentSync"] = new JsonObject
                {
                    ["openClose"] = true,
                    ["change"] = 2,
                },
                ["completionProvider"] = new JsonObject
                {
                    // Utility completion items defer their documentation bodies to
                    // completionItem/resolve to keep the per-keystroke payload small.
                    ["resolveProvider"] = true,
                    // The quote characters open the list on `class="` itself. Without them nothing
                    // appears until the author types a `-`, which reads as a broken feature.
                    ["triggerCharacters"] = new JsonArray(
                        "@",
                        "<",
                        ":",
                        ".",
                        "v",
                        " ",
                        "-",
                        "[",
                        "(",
                        "/",
                        "!",
                        "\"",
                        "'"),
                },
                ["hoverProvider"] = true,
                ["documentSymbolProvider"] = true,
                ["foldingRangeProvider"] = true,
                ["codeActionProvider"] = new JsonObject
                {
                    ["codeActionKinds"] = new JsonArray("quickfix"),
                },
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "Assimalign.Viu.LanguageServer",
                ["version"] = "0.2.0",
            },
        };

    private void ConfigureUtilityStylesheet(string documentUri)
    {
        if (languageService is IUtilityCssLanguageService utilityCssLanguageService)
        {
            var configuration =
                ViuUtilityStylesheetContext.ReadForDocument(documentUri);
            if (configuration is null)
            {
                utilityCssLanguageService.ConfigureUtilityStylesheet(
                    documentUri,
                    null);
            }
            else
            {
                utilityCssLanguageService.ConfigureUtilityStylesheet(
                    documentUri,
                    configuration.StylesheetText,
                    configuration.StylesheetIdentity,
                    configuration.ReferenceGraph);
            }
        }
    }

    private static (string DocumentUri, LanguagePosition Position) GetDocumentPosition(
        JsonElement parameters)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var positionElement = GetRequiredObject(parameters, "position");
        return (documentUri, GetPosition(positionElement));
    }

    private static LanguageRange GetRange(JsonElement element)
        => new(
            GetPosition(GetRequiredObject(element, "start")),
            GetPosition(GetRequiredObject(element, "end")));

    private static LanguagePosition GetPosition(JsonElement element)
        => new(
            GetRequiredInteger(element, "line"),
            GetRequiredInteger(element, "character"));

    private static JsonObject ToJsonDiagnostic(LanguageDiagnostic diagnostic)
        => new()
        {
            ["range"] = ToJsonRange(diagnostic.Range),
            ["severity"] = (int)diagnostic.Severity,
            ["code"] = diagnostic.Code,
            ["source"] = diagnostic.Source,
            ["message"] = diagnostic.Message,
        };

    private static JsonObject ToJsonRange(LanguageRange range)
        => new()
        {
            ["start"] = ToJsonPosition(range.Start),
            ["end"] = ToJsonPosition(range.End),
        };

    private static JsonObject ToJsonPosition(LanguagePosition position)
        => new()
        {
            ["line"] = position.Line,
            ["character"] = position.Character,
        };

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            throw InvalidParameters($"The '{propertyName}' object is required.");
        }

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            throw InvalidParameters($"The '{propertyName}' array is required.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw InvalidParameters($"The '{propertyName}' string is required.");
        }

        return property.GetString()!;
    }

    private static int GetRequiredInteger(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(propertyName, out var property) ||
            !property.TryGetInt32(out var value))
        {
            throw InvalidParameters($"The '{propertyName}' integer is required.");
        }

        return value;
    }

    private static int? GetOptionalInteger(JsonElement parent, string propertyName)
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

    private static JsonElement RequireIdentifier(bool hasIdentifier, JsonElement identifier)
    {
        if (!hasIdentifier)
        {
            throw InvalidParameters("The request identifier is required.");
        }

        return identifier;
    }

    private static JsonElement? GetOptionalIdentifier(JsonElement message)
    {
        if (message.ValueKind == JsonValueKind.Object &&
            message.TryGetProperty("id", out var identifier))
        {
            return identifier;
        }

        return null;
    }

    private static LanguageServerProtocolRequestException InvalidParameters(string message)
        => new(InvalidParametersCode, message);

    private static async ValueTask WriteResultAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonElement identifier,
        JsonNode? result,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = CloneElement(identifier),
                    ["result"] = result,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask WriteErrorAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonElement? id,
        int code,
        string message,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id.HasValue ? CloneElement(id.Value) : null,
                    ["error"] = new JsonObject
                    {
                        ["code"] = code,
                        ["message"] = message,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask WriteNotificationAsync(
        LanguageServerProtocolMessageWriter writer,
        string method,
        JsonObject parameters,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = method,
                    ["params"] = parameters,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static JsonNode? CloneElement(JsonElement element)
        => JsonNode.Parse(element.GetRawText());
}
