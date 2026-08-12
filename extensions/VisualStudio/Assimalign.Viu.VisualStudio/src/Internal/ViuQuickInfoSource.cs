using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

using StreamJsonRpc;

namespace Assimalign.Viu.VisualStudio;

/// <summary>
/// Draws the Viu tooltip from classified text runs rather than from the server's Markdown.
/// </summary>
/// <remarks>
/// <para>
/// Two things follow from rendering it here. The declaration is colored by the same pass that colors
/// the document behind it, so a signature reads the same in both; and the copy button a rendered
/// Markdown fence carries is simply never created, because no Markdown is handed over. The middle
/// layer declines the editor's own hover request so this remains the single answer to the gesture.
/// </para>
/// <para>
/// What the tooltip is made of is <see cref="ViuHoverPresentation"/>'s decision, which is editor-free
/// and unit-tested; this type asks the server, translates runs into editor elements, and does
/// nothing else. Every failure — no server attached, a request that faults, a body with nothing in
/// it — produces no tooltip, which is what a hover over nothing produces anyway.
/// </para>
/// </remarks>
internal sealed class ViuQuickInfoSource : IAsyncQuickInfoSource
{
    private readonly ITextBuffer textBuffer;
    private readonly ViuCompletionDocumentPath documentPath;

    /// <summary>Initializes the source over one buffer.</summary>
    internal ViuQuickInfoSource(ITextBuffer textBuffer, ViuCompletionDocumentPath documentPath)
    {
        this.textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
        this.documentPath = documentPath ?? throw new ArgumentNullException(nameof(documentPath));
    }

    /// <inheritdoc />
    public async Task<QuickInfoItem?> GetQuickInfoItemAsync(
        IAsyncQuickInfoSession session,
        CancellationToken cancellationToken)
    {
        SnapshotPoint? triggerPoint = session?.GetTriggerPoint(this.textBuffer.CurrentSnapshot);
        JsonRpc? connection = ViuLanguageServerConnection.Current;
        string? filePath = this.documentPath.Current;
        if (triggerPoint is not { } point ||
            connection is null ||
            string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        string? markdown = await RequestHoverAsync(
            connection,
            filePath!,
            point,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IReadOnlyList<ViuHoverRun>> lines = ViuHoverPresentation.Create(markdown);
        if (lines.Count == 0)
        {
            return null;
        }

        var elements = new List<object>(lines.Count);
        foreach (IReadOnlyList<ViuHoverRun> line in lines)
        {
            var runs = new List<ClassifiedTextRun>(line.Count);
            foreach (ViuHoverRun run in line)
            {
                runs.Add(new ClassifiedTextRun(run.ClassificationTypeName, run.Text));
            }

            elements.Add(new ClassifiedTextElement(runs));
        }

        ITextSnapshotLine snapshotLine = point.Snapshot.GetLineFromPosition(point.Position);
        return new QuickInfoItem(
            point.Snapshot.CreateTrackingSpan(
                snapshotLine.Extent,
                SpanTrackingMode.EdgeInclusive),
            new ContainerElement(ContainerElementStyle.Stacked, elements));
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private static async Task<string?> RequestHoverAsync(
        JsonRpc connection,
        string filePath,
        SnapshotPoint point,
        CancellationToken cancellationToken)
    {
        ITextSnapshotLine line = point.Snapshot.GetLineFromPosition(point.Position);
        var parameters = new
        {
            textDocument = new { uri = new Uri(filePath).AbsoluteUri },
            position = new
            {
                line = line.LineNumber,
                character = point.Position - line.Start.Position,
            },
        };

        try
        {
            JsonDocument? response = await connection
                .InvokeWithParameterObjectAsync<JsonDocument?>(
                    ViuCompletionColorMiddleLayer.HoverMethodName,
                    parameters,
                    cancellationToken)
                .ConfigureAwait(false);
            return ReadMarkdown(response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RemoteRpcException)
        {
            // A server that has gone, or one that faulted on this position, leaves the gesture
            // unanswered rather than failing the session.
            return null;
        }
    }

    private static string? ReadMarkdown(JsonDocument? response)
    {
        if (response is null ||
            response.RootElement.ValueKind != JsonValueKind.Object ||
            !response.RootElement.TryGetProperty("contents", out JsonElement contents))
        {
            return null;
        }

        return contents.ValueKind == JsonValueKind.Object &&
            contents.TryGetProperty("value", out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
