using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Tooling.LanguageService;

namespace Assimalign.Viu.Tooling.LanguageServer;

internal sealed class LanguageServerHost
{
    private const int ParseErrorCode = -32700;
    private const int InvalidRequestCode = -32600;
    private const int MethodNotFoundCode = -32601;
    private const int InvalidParametersCode = -32602;
    private const int InternalErrorCode = -32603;
    // The Language Server Protocol's RequestCancelled code: the reply for a request whose
    // $/cancelRequest arrived while it was still in flight.
    private const int RequestCancelledCode = -32800;

    private readonly IViuLanguageService languageService;
    private readonly HashSet<string> openSupportedDocuments =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ViuProjectContextReader projectContextReader = new();
    // The status transition ledger ([V01.01.12.23], #259): project file path -> the last reported
    // state signature. A status is reported only when its signature differs from the project's
    // last reported one — never once per keystroke, and never merely once ever: recovery
    // overwrites the signature, so a later re-degradation re-warns. Concurrent request tasks race
    // through TryAdd/TryUpdate, so exactly one task wins any given transition.
    private readonly ConcurrentDictionary<string, string> reportedProjectStatuses =
        new(StringComparer.OrdinalIgnoreCase);
    // Keyed by the request identifier's raw JSON text (JsonElement.GetRawText()), so the numeric
    // identifier 1 and the string identifier "1" stay distinct per JSON-RPC. The loop thread adds
    // entries; request tasks remove their own entry from pool threads.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> pendingRequestCancellations =
        new(StringComparer.Ordinal);
    // Loop-thread only: the dispatched request tasks. Completed tasks are pruned each iteration,
    // and the list is drained before the shutdown reply and before RunAsync returns.
    private readonly List<Task> inFlightRequests = new();
    private LanguageServerClientCapabilities clientCapabilities =
        LanguageServerClientCapabilities.Default;
    private bool shutdownRequested;

    internal LanguageServerHost()
        : this(ViuLanguageServices.Create())
    {
    }

    internal LanguageServerHost(IViuLanguageService languageService)
        => this.languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));

    /// <summary>
    /// Runs the JSON-RPC message loop. Notifications and lifecycle messages apply inline in
    /// receive order before the next message is read; feature requests dispatch to tracked tasks
    /// and may answer out of receive order relative to each other. A dispatched request may
    /// therefore observe document synchronization newer than its send time (monotonic reads) —
    /// the accepted Language Server Protocol idiom, which clients compensate for with document
    /// versions and <c>$/cancelRequest</c>.
    /// </summary>
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
            inFlightRequests.RemoveAll(task => task.IsCompleted);

            JsonDocument? document;
            try
            {
                document = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                await WriteErrorAsync(
                        writer,
                        identifier: (JsonNode?)null,
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
                        identifier: (JsonNode?)null,
                        ParseErrorCode,
                        exception.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (document is null)
            {
                // End of input without an exit message: drain so no request task is left writing
                // to an output stream the caller disposes right after RunAsync returns.
                await DrainInFlightRequestsAsync().ConfigureAwait(false);
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
            var optionalIdentifier = GetOptionalIdentifier(message);
            await WriteErrorAsync(
                    writer,
                    optionalIdentifier.HasValue ? CloneElement(optionalIdentifier.Value) : null,
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
                    return false;

                case "$/cancelRequest":
                    HandleCancelRequest(parameters);
                    return false;

                case "shutdown":
                    shutdownRequested = true;
                    // Draining before the reply preserves the contract that every response
                    // precedes the shutdown reply.
                    await DrainInFlightRequestsAsync().ConfigureAwait(false);
                    await WriteResultAsync(
                            writer,
                            RequireIdentifier(hasIdentifier, identifier),
                            result: null,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return false;

                case "exit":
                    // Deliberate deviation from the protocol's "exit immediately": every compute
                    // is bounded synchronous CPU work, and draining without cancelling keeps
                    // in-flight responses off an output stream the caller disposes right after
                    // RunAsync returns.
                    await DrainInFlightRequestsAsync().ConfigureAwait(false);
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
                case "textDocument/hover":
                case "completionItem/resolve":
                case "textDocument/documentSymbol":
                case "textDocument/foldingRange":
                case "textDocument/codeAction":
                    await DispatchFeatureRequestAsync(
                            method,
                            RequireIdentifier(hasIdentifier, identifier),
                            parameters,
                            writer,
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

    private void HandleCancelRequest(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("id", out var identifier))
        {
            return;
        }

        // An unknown identifier is silently ignored: the request either never dispatched or has
        // already answered, and cancellation of a finished request is a no-op per the protocol.
        if (pendingRequestCancellations.TryGetValue(identifier.GetRawText(), out var requestCancellation))
        {
            try
            {
                requestCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Benign race: the request completed and disposed its source between the lookup
                // and the cancel.
            }
        }
    }

    private async ValueTask DrainInFlightRequestsAsync()
    {
        if (inFlightRequests.Count == 0)
        {
            return;
        }

        // A tracked request task never faults — every failure becomes a JSON-RPC reply or is
        // swallowed on host teardown — so a bare WhenAll is safe here.
        await Task.WhenAll(inFlightRequests).ConfigureAwait(false);
        inFlightRequests.Clear();
    }

    private async ValueTask DispatchFeatureRequestAsync(
        string method,
        JsonElement identifier,
        JsonElement parameters,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken cancellationToken)
    {
        // Parameters are parsed fully on the loop thread: every JsonElement points into the pooled
        // JsonDocument the loop disposes at the end of this iteration, so a dispatched task must
        // only ever see the JsonDocument-independent pending-request record.
        var pending = CreatePendingRequest(method, identifier, parameters, out var inlineResult);
        if (pending is null)
        {
            // Closed or unsupported documents (and clients without a required capability) answer
            // inline with the empty/echo result, keeping those replies in strict receive order.
            await WriteResultAsync(writer, identifier, inlineResult, cancellationToken).ConfigureAwait(false);
            return;
        }

        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!pendingRequestCancellations.TryAdd(pending.IdentifierKey, requestCancellation))
        {
            // A duplicate in-flight identifier would make $/cancelRequest ambiguous, so the
            // protocol-violating request is rejected instead of orphaning a cancellation source.
            requestCancellation.Dispose();
            await WriteErrorAsync(
                    writer,
                    identifier,
                    InvalidRequestCode,
                    "A request with this identifier is already in flight.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        inFlightRequests.Add(RunRequestAsync(pending, requestCancellation, writer, cancellationToken));
    }

    private LanguageServerPendingRequest? CreatePendingRequest(
        string method,
        JsonElement identifier,
        JsonElement parameters,
        out JsonNode? inlineResult)
    {
        switch (method)
        {
            case "textDocument/completion":
            case "textDocument/hover":
            {
                var (documentUri, position) = GetDocumentPosition(parameters);
                if (!IsOpenAndSupported(documentUri))
                {
                    inlineResult = method == "textDocument/completion"
                        ? CreateCompletionList(new JsonArray(), isIncomplete: false)
                        : null;
                    return null;
                }

                inlineResult = null;
                return new LanguageServerPendingRequest
                {
                    Method = method,
                    Identifier = CloneElement(identifier),
                    IdentifierKey = identifier.GetRawText(),
                    ClientCapabilities = clientCapabilities,
                    DocumentUri = documentUri,
                    Position = position,
                };
            }

            case "completionItem/resolve":
            {
                if (parameters.ValueKind != JsonValueKind.Object)
                {
                    throw InvalidParameters("The completion item is required.");
                }

                var completionLabel = GetRequiredString(parameters, "label");
                var item = (JsonObject)CloneElement(parameters)!;

                // A missing payload or a closed document echoes the item back unchanged: Visual
                // Studio resolves items asynchronously, and a race with didClose must never
                // surface as an error.
                if (!parameters.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Object ||
                    !data.TryGetProperty("documentUri", out var uriElement) ||
                    uriElement.ValueKind != JsonValueKind.String)
                {
                    inlineResult = item;
                    return null;
                }

                var documentUri = uriElement.GetString()!;
                if (!IsOpenAndSupported(documentUri))
                {
                    inlineResult = item;
                    return null;
                }

                inlineResult = null;
                return new LanguageServerPendingRequest
                {
                    Method = method,
                    Identifier = CloneElement(identifier),
                    IdentifierKey = identifier.GetRawText(),
                    ClientCapabilities = clientCapabilities,
                    DocumentUri = documentUri,
                    CompletionLabel = completionLabel,
                    ResolveItem = item,
                };
            }

            case "textDocument/documentSymbol":
            case "textDocument/foldingRange":
            {
                var textDocument = GetRequiredObject(parameters, "textDocument");
                var documentUri = GetRequiredString(textDocument, "uri");
                if (!IsOpenAndSupported(documentUri))
                {
                    inlineResult = new JsonArray();
                    return null;
                }

                inlineResult = null;
                return new LanguageServerPendingRequest
                {
                    Method = method,
                    Identifier = CloneElement(identifier),
                    IdentifierKey = identifier.GetRawText(),
                    ClientCapabilities = clientCapabilities,
                    DocumentUri = documentUri,
                };
            }

            default:
            {
                var textDocument = GetRequiredObject(parameters, "textDocument");
                var documentUri = GetRequiredString(textDocument, "uri");
                var range = GetRange(GetRequiredObject(parameters, "range"));

                // Without codeActionLiteralSupport only Command[] results are legal, and this
                // server implements no commands, so the honest answer is an empty result.
                if (!clientCapabilities.SupportsCodeActionLiterals ||
                    !IsOpenAndSupported(documentUri))
                {
                    inlineResult = new JsonArray();
                    return null;
                }

                inlineResult = null;
                return new LanguageServerPendingRequest
                {
                    Method = method,
                    Identifier = CloneElement(identifier),
                    IdentifierKey = identifier.GetRawText(),
                    ClientCapabilities = clientCapabilities,
                    DocumentUri = documentUri,
                    Range = range,
                };
            }
        }
    }

    private async Task RunRequestAsync(
        LanguageServerPendingRequest request,
        CancellationTokenSource requestCancellation,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken hostCancellation)
    {
        JsonNode? result = null;
        JsonObject? statusNotification = null;
        LanguageServerProtocolRequestException? requestFailure = null;
        Exception? internalFailure = null;
        var cancelled = false;
        try
        {
            result = await Task.Run(
                    () =>
                    {
                        // The project-context probe (disk IO included) runs on the request task,
                        // mirroring the utility-stylesheet configuration the compute methods
                        // already perform, and its status notification is written below before
                        // the reply.
                        if (IsSemanticFeatureRequest(request.Method))
                        {
                            statusNotification = ConfigureProjectContext(request.DocumentUri);
                        }

                        return ComputeRequestResult(request, requestCancellation.Token);
                    },
                    requestCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        catch (LanguageServerProtocolRequestException exception)
        {
            requestFailure = exception;
        }
        catch (Exception exception)
        {
            internalFailure = exception;
        }
        finally
        {
            pendingRequestCancellations.TryRemove(request.IdentifierKey, out _);
            requestCancellation.Dispose();
        }

        try
        {
            // The status notification precedes the reply — legal interleaving, and the shared
            // writer serializes whole messages so the pair can never interleave with another
            // task's frames.
            if (statusNotification is not null)
            {
                await WriteNotificationAsync(
                        writer,
                        "window/logMessage",
                        statusNotification,
                        hostCancellation)
                    .ConfigureAwait(false);
            }

            // Replies use the host token only: a per-request token firing between the header and
            // payload writes would corrupt the stream framing for every later message.
            if (cancelled)
            {
                await WriteErrorAsync(
                        writer,
                        request.Identifier,
                        RequestCancelledCode,
                        "The request was canceled.",
                        hostCancellation)
                    .ConfigureAwait(false);
            }
            else if (requestFailure is not null)
            {
                await WriteErrorAsync(
                        writer,
                        request.Identifier,
                        requestFailure.Code,
                        requestFailure.Message,
                        hostCancellation)
                    .ConfigureAwait(false);
            }
            else if (internalFailure is not null)
            {
                await WriteErrorAsync(
                        writer,
                        request.Identifier,
                        InternalErrorCode,
                        internalFailure.Message,
                        hostCancellation)
                    .ConfigureAwait(false);
            }
            else
            {
                await WriteResultAsync(writer, request.Identifier, result, hostCancellation)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Host teardown mid-write: a tracked task must always complete successfully so the
            // shutdown/exit drains stay bare Task.WhenAll calls.
        }
    }

    private JsonNode? ComputeRequestResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
        => request.Method switch
        {
            "textDocument/completion" => ComputeCompletionResult(request, cancellationToken),
            "textDocument/hover" => ComputeHoverResult(request, cancellationToken),
            "completionItem/resolve" => ComputeCompletionItemResolveResult(request, cancellationToken),
            "textDocument/documentSymbol" => ComputeDocumentSymbolResult(request, cancellationToken),
            "textDocument/foldingRange" => ComputeFoldingRangeResult(request, cancellationToken),
            _ => ComputeCodeActionResult(request, cancellationToken),
        };

    private async ValueTask HandleDidOpenAsync(
        JsonElement parameters,
        LanguageServerProtocolMessageWriter writer,
        CancellationToken cancellationToken)
    {
        var textDocument = GetRequiredObject(parameters, "textDocument");
        var documentUri = GetRequiredString(textDocument, "uri");
        var text = GetRequiredString(textDocument, "text");
        var version = GetOptionalInteger(textDocument, "version");

        JsonObject? statusNotification = null;
        if (ViuDocumentSupport.IsSupported(documentUri))
        {
            openSupportedDocuments.Add(documentUri);
            ConfigureUtilityStylesheet(documentUri);
            statusNotification = ConfigureProjectContext(documentUri);
            languageService.OpenDocument(documentUri, text, version);
        }
        else if (openSupportedDocuments.Remove(documentUri))
        {
            languageService.CloseDocument(documentUri);
        }

        if (statusNotification is not null)
        {
            await WriteNotificationAsync(
                    writer,
                    "window/logMessage",
                    statusNotification,
                    cancellationToken)
                .ConfigureAwait(false);
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

    private JsonObject ComputeCompletionResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
    {
        // The stylesheet configuration (disk probing included) runs on the request task, mirroring
        // the previous per-request configure-then-compute order without blocking the loop thread.
        ConfigureUtilityStylesheet(request.DocumentUri);
        var completions = languageService.GetCompletions(
            request.DocumentUri,
            request.Position,
            cancellationToken);
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
                    ["documentUri"] = request.DocumentUri,
                    ["label"] = completion.Label,
                },
            };

            // Deferred documentation (utility items) is omitted entirely and computed by
            // completionItem/resolve; inline one-liners keep shipping with the item.
            if (!string.IsNullOrEmpty(completion.Documentation))
            {
                item["documentation"] = LanguageServerMarkupContent.Create(
                    completion.Documentation,
                    request.ClientCapabilities.CompletionDocumentationSupportsMarkdown);
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

    private JsonNode? ComputeHoverResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
    {
        ConfigureUtilityStylesheet(request.DocumentUri);
        var hover = languageService.GetHover(
            request.DocumentUri,
            request.Position,
            cancellationToken);
        if (hover is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["contents"] = LanguageServerMarkupContent.Create(
                hover.Markdown,
                request.ClientCapabilities.HoverSupportsMarkdown),
            ["range"] = ToJsonRange(hover.Range),
        };
    }

    private JsonNode ComputeCompletionItemResolveResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
    {
        ConfigureUtilityStylesheet(request.DocumentUri);
        var documentation = languageService.ResolveCompletionDocumentation(
            request.DocumentUri,
            request.CompletionLabel!,
            cancellationToken);
        var item = request.ResolveItem!;
        if (documentation is not null)
        {
            item["documentation"] = LanguageServerMarkupContent.Create(
                documentation,
                request.ClientCapabilities.CompletionDocumentationSupportsMarkdown);
        }

        return item;
    }

    private JsonArray ComputeDocumentSymbolResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
    {
        var results = new JsonArray();
        foreach (var symbol in languageService.GetDocumentSymbols(
                     request.DocumentUri,
                     cancellationToken))
        {
            if (request.ClientCapabilities.SupportsHierarchicalDocumentSymbols)
            {
                results.Add((JsonNode)ToJsonDocumentSymbol(symbol));
            }
            else
            {
                AppendSymbolInformation(symbol, request.DocumentUri, results);
            }
        }

        return results;
    }

    private JsonArray ComputeFoldingRangeResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
    {
        var results = new JsonArray();
        foreach (var foldingRange in languageService.GetFoldingRanges(
                     request.DocumentUri,
                     cancellationToken))
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

    private JsonArray ComputeCodeActionResult(
        LanguageServerPendingRequest request,
        CancellationToken cancellationToken)
    {
        var results = new JsonArray();
        foreach (var action in languageService.GetCodeActions(
                     request.DocumentUri,
                     request.Range,
                     cancellationToken))
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
                            [request.DocumentUri] = edits,
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

    // Loop-thread only: reads and mutates openSupportedDocuments, so a dispatched request task
    // must never call this — the open/supported evaluation happens before dispatch.
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
                ["name"] = "Assimalign.Viu.Tooling.LanguageServer",
                ["version"] = "0.2.0",
            },
        };

    // Semantic script features consume the project context, so the probe runs exactly where the
    // utility stylesheet configures: document open plus the completion, hover, and resolve
    // requests ([V01.01.12.23], #259).
    private static bool IsSemanticFeatureRequest(string method)
        => method is "textDocument/completion" or "textDocument/hover" or "completionItem/resolve";

    /// <summary>
    /// Probes the document's project context, feeds it to the language service, and composes the
    /// <c>window/logMessage</c> payload the caller writes before its reply — or
    /// <see langword="null"/> when the status is silent or unchanged since the project's last
    /// reported state.
    /// </summary>
    private JsonObject? ConfigureProjectContext(string documentUri)
    {
        if (languageService is not IScriptSemanticLanguageService scriptSemanticLanguageService)
        {
            return null;
        }

        var (context, status) = projectContextReader.Read(documentUri);
        scriptSemanticLanguageService.ConfigureProjectContext(documentUri, context);
        if (!status.TryCreateLogMessage(out var messageType, out var message) ||
            !TryRecordStatusTransition(status))
        {
            return null;
        }

        return new JsonObject
        {
            ["type"] = messageType,
            ["message"] = message,
        };
    }

    // Records a reportable status in the transition ledger: true exactly once per state-signature
    // transition per project, regardless of how many concurrent requests observe it.
    private bool TryRecordStatusTransition(ViuProjectContextStatus status)
    {
        // Every reportable status names its project; the silent NoOwningProject state never
        // reaches this method because TryCreateLogMessage already returned false for it.
        var projectFilePath = status.ProjectFilePath;
        if (projectFilePath is null)
        {
            return false;
        }

        var signature = status.StateSignature;
        while (true)
        {
            if (reportedProjectStatuses.TryGetValue(projectFilePath, out var lastSignature))
            {
                if (string.Equals(lastSignature, signature, StringComparison.Ordinal))
                {
                    return false;
                }

                if (reportedProjectStatuses.TryUpdate(projectFilePath, signature, lastSignature))
                {
                    return true;
                }
            }
            else if (reportedProjectStatuses.TryAdd(projectFilePath, signature))
            {
                return true;
            }
        }
    }

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

    private static ValueTask WriteResultAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonElement identifier,
        JsonNode? result,
        CancellationToken cancellationToken)
        => WriteResultAsync(writer, CloneElement(identifier), result, cancellationToken);

    private static async ValueTask WriteResultAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonNode? identifier,
        JsonNode? result,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(
                new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = identifier,
                    ["result"] = result,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask WriteErrorAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonElement identifier,
        int code,
        string message,
        CancellationToken cancellationToken)
        => WriteErrorAsync(writer, CloneElement(identifier), code, message, cancellationToken);

    private static async ValueTask WriteErrorAsync(
        LanguageServerProtocolMessageWriter writer,
        JsonNode? identifier,
        int code,
        string message,
        CancellationToken cancellationToken)
    {
        await writer.WriteAsync(
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
