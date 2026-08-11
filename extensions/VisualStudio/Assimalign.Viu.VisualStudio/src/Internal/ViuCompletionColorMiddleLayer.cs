using System;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServer.Client;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Captures the opaque Viu color payload after the server computes a completion response.
/// </summary>
internal sealed class ViuCompletionColorMiddleLayer :
    ILanguageClientMiddleLayer2<JsonDocument>
{
    /// <summary>The standard Language Server Protocol completion request method.</summary>
    internal const string CompletionMethodName = "textDocument/completion";

    private readonly ViuCompletionColorState state;

    /// <summary>Initializes the middle layer over the process-wide completion-color state.</summary>
    internal ViuCompletionColorMiddleLayer(ViuCompletionColorState state) =>
        this.state = state ?? throw new ArgumentNullException(nameof(state));

    /// <inheritdoc />
    public bool CanHandle(string methodName) =>
        string.Equals(methodName, CompletionMethodName, StringComparison.Ordinal);

    /// <inheritdoc />
    public async Task<JsonDocument?> HandleRequestAsync(
        string methodName,
        JsonDocument methodParameters,
        Func<JsonDocument, Task<JsonDocument?>> sendRequest)
    {
        JsonDocument? response = await sendRequest(methodParameters).ConfigureAwait(false);
        if (response is not null &&
            CanHandle(methodName) &&
            ViuCompletionColorPublication.TryParse(
                methodParameters.RootElement,
                response.RootElement,
                out var publication))
        {
            this.state.Publish(publication!);
        }

        return response;
    }

    /// <inheritdoc />
    public Task HandleNotificationAsync(
        string methodName,
        JsonDocument methodParameters,
        Func<JsonDocument, Task> sendNotification)
        => sendNotification(methodParameters);
}
