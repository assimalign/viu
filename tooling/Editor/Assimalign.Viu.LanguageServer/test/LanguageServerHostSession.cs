using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// An interactive in-process protocol session ([V01.01.12.23], #259): the host runs over duplex
/// pipes so a test can change the file system between requests — the mid-session transition
/// harness the all-up-front <see cref="MemoryStream"/> style cannot express. Reads are bounded so
/// a protocol regression fails fast instead of hanging the suite.
/// </summary>
internal sealed class LanguageServerHostSession : IAsyncDisposable
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(60);

    private readonly Pipe inputPipe = new();
    private readonly Pipe outputPipe = new();
    private readonly Stream requestStream;
    private readonly LanguageServerProtocolMessageReader responseReader;
    private readonly Task<int> run;
    private readonly List<JsonDocument> notifications = [];

    internal LanguageServerHostSession(LanguageServerHost host)
    {
        requestStream = inputPipe.Writer.AsStream();
        responseReader = new LanguageServerProtocolMessageReader(outputPipe.Reader.AsStream());
        run = Task.Run(
            () => host.RunAsync(inputPipe.Reader.AsStream(), outputPipe.Writer.AsStream()));
    }

    /// <summary>Gets every non-response message read so far, in stream order.</summary>
    internal IReadOnlyList<JsonDocument> Notifications => notifications;

    /// <summary>Writes one framed protocol message to the host.</summary>
    /// <param name="payload">The JSON-RPC message body.</param>
    internal async Task SendAsync(string payload)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        await requestStream.WriteAsync(header);
        await requestStream.WriteAsync(body);
        await requestStream.FlushAsync();
    }

    /// <summary>
    /// Reads framed messages until the response carrying the string identifier arrives; every
    /// other message accumulates in <see cref="Notifications"/>.
    /// </summary>
    /// <param name="identifier">The request's string identifier.</param>
    internal async Task<JsonDocument> ReadResponseAsync(string identifier)
    {
        while (true)
        {
            var message = await responseReader.ReadAsync().AsTask().WaitAsync(ReadTimeout)
                ?? throw new InvalidOperationException(
                    "The host output ended before the awaited response arrived.");
            if (message.RootElement.TryGetProperty("id", out var responseIdentifier) &&
                responseIdentifier.ValueKind == JsonValueKind.String &&
                responseIdentifier.GetString() == identifier)
            {
                return message;
            }

            notifications.Add(message);
        }
    }

    /// <summary>
    /// Reads framed messages until a matching notification arrives, retaining every notification
    /// in <see cref="Notifications"/> and returning an independently owned matching clone.
    /// </summary>
    internal async Task<JsonDocument> ReadNotificationAsync(
        string method,
        Func<JsonElement, bool> matches)
    {
        while (true)
        {
            var message = await responseReader.ReadAsync().AsTask().WaitAsync(ReadTimeout)
                ?? throw new InvalidOperationException(
                    "The host output ended before the awaited notification arrived.");
            if (!message.RootElement.TryGetProperty("method", out var notificationMethod) ||
                notificationMethod.ValueKind != JsonValueKind.String)
            {
                message.Dispose();
                throw new InvalidOperationException(
                    "An unexpected response arrived while awaiting a notification.");
            }

            notifications.Add(message);
            if (notificationMethod.GetString() == method &&
                message.RootElement.TryGetProperty("params", out var parameters) &&
                matches(parameters))
            {
                return JsonDocument.Parse(message.RootElement.GetRawText());
            }
        }
    }

    /// <summary>Waits for the host loop to return and yields its exit code.</summary>
    internal Task<int> CompleteAsync() => run.WaitAsync(ReadTimeout);

    public async ValueTask DisposeAsync()
    {
        // Completing the input signals end of stream, so a host still looping drains and returns.
        await inputPipe.Writer.CompleteAsync();
        try
        {
            await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            // The failing assertion that abandoned the session is the signal; never mask it.
        }

        foreach (var notification in notifications)
        {
            notification.Dispose();
        }
    }
}
