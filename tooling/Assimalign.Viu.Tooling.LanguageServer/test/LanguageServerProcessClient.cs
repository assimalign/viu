using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Assimalign.Viu.Tooling.LanguageServer;

/// <summary>
/// Drives a real stdio language-server process with Content-Length framing
/// ([V01.01.12.23], #259). The packed-consumer proof runs against the published single-file
/// executable rather than the in-process host on purpose: trimmed single-file publishing is
/// exactly the environment where reflection-dependent code paths break, and an in-process test can
/// never observe that.
/// </summary>
internal sealed class LanguageServerProcessClient : IDisposable
{
    private readonly Process process;
    private readonly Stream input;
    private readonly Stream output;
    private readonly StringBuilder errorOutput = new();

    internal LanguageServerProcessClient(string executablePath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"The language server '{executablePath}' failed to start.");
        input = process.StandardInput.BaseStream;
        output = process.StandardOutput.BaseStream;
        process.ErrorDataReceived += (_, arguments) =>
        {
            if (!string.IsNullOrEmpty(arguments.Data))
            {
                lock (errorOutput)
                {
                    errorOutput.AppendLine(arguments.Data);
                }
            }
        };
        process.BeginErrorReadLine();
    }

    /// <summary>Gets everything the server wrote to standard error (reserved for diagnostics).</summary>
    internal string ErrorOutput
    {
        get
        {
            lock (errorOutput)
            {
                return errorOutput.ToString();
            }
        }
    }

    /// <summary>Writes one framed protocol message to the server.</summary>
    /// <param name="payload">The JSON-RPC message body.</param>
    internal void Send(string payload)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        input.Write(header);
        input.Write(body);
        input.Flush();
    }

    /// <summary>
    /// Reads framed messages until the response with the supplied identifier arrives; every other
    /// message accumulates in <paramref name="notifications"/>.
    /// </summary>
    /// <param name="identifier">The request identifier (string or numeric raw text).</param>
    /// <param name="notifications">The accumulator for interleaved server-initiated messages.</param>
    internal JsonDocument ReadResponse(string identifier, List<JsonDocument> notifications)
    {
        while (true)
        {
            var message = ReadMessage()
                ?? throw new InvalidOperationException(
                    "The server output ended before the awaited response arrived. " +
                    $"Server stderr:\n{ErrorOutput}");
            if (message.RootElement.TryGetProperty("id", out var responseIdentifier) &&
                GetIdentifierText(responseIdentifier) == identifier)
            {
                return message;
            }

            notifications.Add(message);
        }
    }

    /// <summary>Sends a request and reads until its response arrives.</summary>
    /// <param name="payload">The JSON-RPC request body.</param>
    /// <param name="identifier">The request identifier (string or numeric raw text).</param>
    /// <param name="notifications">The accumulator for interleaved server-initiated messages.</param>
    internal JsonDocument ReadResponseAfterSend(
        string payload,
        string identifier,
        List<JsonDocument> notifications)
    {
        Send(payload);
        return ReadResponse(identifier, notifications);
    }

    /// <summary>
    /// Reads framed messages until a notification with the supplied method arrives; every message
    /// read, the match included, accumulates in <paramref name="notifications"/>.
    /// </summary>
    /// <param name="method">The awaited notification method.</param>
    /// <param name="notifications">The accumulator for every message read.</param>
    internal void ReadUntilNotification(string method, List<JsonDocument> notifications)
    {
        while (true)
        {
            var message = ReadMessage()
                ?? throw new InvalidOperationException(
                    $"The server output ended before a '{method}' notification arrived. " +
                    $"Server stderr:\n{ErrorOutput}");
            notifications.Add(message);
            if (message.RootElement.TryGetProperty("method", out var messageMethod) &&
                messageMethod.GetString() == method)
            {
                return;
            }
        }
    }

    /// <summary>Reads one framed message, or <see langword="null"/> at end of stream.</summary>
    internal JsonDocument? ReadMessage()
    {
        var contentLength = -1;
        while (true)
        {
            var line = ReadHeaderLine();
            if (line is null)
            {
                return null;
            }

            if (line.Length == 0)
            {
                break;
            }

            var separator = line.IndexOf(':');
            if (separator > 0 &&
                line.AsSpan(0, separator).Trim().Equals(
                    "Content-Length",
                    StringComparison.OrdinalIgnoreCase))
            {
                contentLength = int.Parse(line.AsSpan(separator + 1).Trim());
            }
        }

        if (contentLength < 0)
        {
            throw new InvalidDataException("The Content-Length header is required.");
        }

        var buffer = new byte[contentLength];
        var received = 0;
        while (received < contentLength)
        {
            var count = output.Read(buffer, received, contentLength - received);
            if (count <= 0)
            {
                throw new EndOfStreamException(
                    "The language-server message payload ended unexpectedly.");
            }

            received += count;
        }

        return JsonDocument.Parse(buffer);
    }

    /// <summary>Waits for the process to exit and yields its exit code.</summary>
    /// <param name="timeout">The bounded wait.</param>
    internal int WaitForExit(TimeSpan timeout)
    {
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"The language server did not exit within {timeout}. Server stderr:\n{ErrorOutput}");
        }

        return process.ExitCode;
    }

    private string? ReadHeaderLine()
    {
        var builder = new List<byte>(64);
        while (true)
        {
            var next = output.ReadByte();
            if (next < 0)
            {
                return builder.Count == 0 ? null : Encoding.ASCII.GetString(builder.ToArray());
            }

            if (next == '\n')
            {
                if (builder.Count > 0 && builder[^1] == (byte)'\r')
                {
                    builder.RemoveAt(builder.Count - 1);
                }

                return Encoding.ASCII.GetString(builder.ToArray());
            }

            builder.Add((byte)next);
        }
    }

    private static string GetIdentifierText(JsonElement element)
        => element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                input.Close();
                if (!process.WaitForExit(3000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
            // Best-effort teardown: the assertion that abandoned the session is the signal.
        }

        process.Dispose();
    }
}
