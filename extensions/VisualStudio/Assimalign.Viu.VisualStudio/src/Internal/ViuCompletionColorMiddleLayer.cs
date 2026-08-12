using System;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.VisualStudio.LanguageServer.Client;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Captures the opaque Viu color payload after the server computes a completion response, and
/// declines the editor's own hover request.
/// </summary>
/// <remarks>
/// Declining hover is what leaves the Quick Info adapter as the single answer to a hover gesture. The
/// adapter renders the tooltip from classified runs — colorizing the declaration and leaving off the
/// copy button a rendered Markdown fence carries — and asks the server for the content itself, so
/// letting the editor's request through as well would stack a second tooltip under the first.
/// </remarks>
internal sealed class ViuCompletionColorMiddleLayer :
    ILanguageClientMiddleLayer2<JsonDocument>
{
    /// <summary>The standard Language Server Protocol completion request method.</summary>
    internal const string CompletionMethodName = "textDocument/completion";

    /// <summary>The standard Language Server Protocol hover request method.</summary>
    internal const string HoverMethodName = "textDocument/hover";

    private readonly ViuCompletionColorState state;

    /// <summary>Initializes the middle layer over the process-wide completion-color state.</summary>
    internal ViuCompletionColorMiddleLayer(ViuCompletionColorState state) =>
        this.state = state ?? throw new ArgumentNullException(nameof(state));

    /// <inheritdoc />
    public bool CanHandle(string methodName) =>
        string.Equals(methodName, CompletionMethodName, StringComparison.Ordinal) ||
        string.Equals(methodName, HoverMethodName, StringComparison.Ordinal);

    /// <inheritdoc />
    public async Task<JsonDocument?> HandleRequestAsync(
        string methodName,
        JsonDocument methodParameters,
        Func<JsonDocument, Task<JsonDocument?>> sendRequest)
    {
        // The Quick Info adapter answers this gesture, and it asks the server itself.
        if (string.Equals(methodName, HoverMethodName, StringComparison.Ordinal))
        {
            return null;
        }

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
