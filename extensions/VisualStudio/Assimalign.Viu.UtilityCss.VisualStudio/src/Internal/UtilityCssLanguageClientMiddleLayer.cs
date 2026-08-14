using System;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServer.Client;

namespace Assimalign.Viu.UtilityCss.VisualStudio;

/// <summary>
/// Short-circuits document messages that reach the client through an HTML-derived content type but
/// do not target an HTML document.
/// </summary>
internal sealed class UtilityCssLanguageClientMiddleLayer :
    ILanguageClientMiddleLayer2<JsonDocument>
{
    private const string TextDocumentMethodPrefix = "textDocument/";

    /// <inheritdoc />
    public bool CanHandle(string methodName) =>
        methodName.StartsWith(TextDocumentMethodPrefix, StringComparison.Ordinal);

    /// <inheritdoc />
    public Task<JsonDocument?> HandleRequestAsync(
        string methodName,
        JsonDocument methodParameters,
        Func<JsonDocument, Task<JsonDocument?>> sendRequest) =>
        UtilityCssDocumentMessageFilter.ShouldForward(methodParameters.RootElement)
            ? sendRequest(methodParameters)
            : Task.FromResult<JsonDocument?>(null);

    /// <inheritdoc />
    public Task HandleNotificationAsync(
        string methodName,
        JsonDocument methodParameters,
        Func<JsonDocument, Task> sendNotification) =>
        UtilityCssDocumentMessageFilter.ShouldForward(methodParameters.RootElement)
            ? sendNotification(methodParameters)
            : Task.CompletedTask;
}
