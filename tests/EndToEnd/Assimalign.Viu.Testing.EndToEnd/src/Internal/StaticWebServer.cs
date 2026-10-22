using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Viu.Testing.EndToEnd;

internal sealed class StaticWebServer : IAsyncDisposable
{
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private readonly string _rootDirectory;
    private readonly string _rootPrefix;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentBag<Task> _requests = [];
    private readonly Task _acceptLoop;

    private StaticWebServer(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _rootPrefix = _rootDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Address = new Uri(
            $"http://127.0.0.1:{port}/",
            UriKind.Absolute);
        _acceptLoop = AcceptLoopAsync();
    }

    internal Uri Address { get; }

    internal static StaticWebServer Start(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The static web root does not exist: {rootDirectory}");
        }

        return new StaticWebServer(rootDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Task[] requests = _requests.ToArray();
        if (requests.Length > 0)
        {
            await Task.WhenAll(requests).ConfigureAwait(false);
        }

        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(
                    _stopping.Token).ConfigureAwait(false);
                _requests.Add(ServeAsync(client, _stopping.Token));
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (_stopping.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task ServeAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string request = await ReadRequestAsync(
                    stream,
                    cancellationToken).ConfigureAwait(false);
                string[] lines = request.Split("\r\n", StringSplitOptions.None);
                string[] requestParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (requestParts.Length < 2
                    || requestParts[0] is not ("GET" or "HEAD"))
                {
                    await WriteResponseAsync(
                        stream,
                        405,
                        "Method Not Allowed",
                        "text/plain; charset=utf-8",
                        "Method Not Allowed"u8.ToArray(),
                        includeBody: true,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                string requestPath = requestParts[1].Split('?', 2)[0];
                string decodedPath = Uri.UnescapeDataString(requestPath).TrimStart('/');
                string relativePath = decodedPath.Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrEmpty(relativePath))
                {
                    relativePath = "index.html";
                }

                string candidatePath = Path.GetFullPath(
                    Path.Combine(_rootDirectory, relativePath));
                if (!candidatePath.StartsWith(
                        _rootPrefix,
                        PathComparison)
                    && !string.Equals(
                        candidatePath,
                        _rootDirectory,
                        PathComparison))
                {
                    await WriteNotFoundAsync(stream, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (Directory.Exists(candidatePath))
                {
                    candidatePath = Path.Combine(candidatePath, "index.html");
                }

                if (!File.Exists(candidatePath))
                {
                    await WriteNotFoundAsync(stream, cancellationToken).ConfigureAwait(false);
                    return;
                }

                byte[] content = await File.ReadAllBytesAsync(
                    candidatePath,
                    cancellationToken).ConfigureAwait(false);
                await WriteResponseAsync(
                    stream,
                    200,
                    "OK",
                    ContentType(candidatePath),
                    content,
                    includeBody: requestParts[0] == "GET",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    private static async Task<string> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        using MemoryStream request = new();
        byte[] buffer = new byte[4096];
        while (request.Length < 64 * 1024)
        {
            int count = await stream.ReadAsync(
                buffer,
                cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            request.Write(buffer, 0, count);
            if (EndsWithHeaderTerminator(request.GetBuffer(), (int)request.Length))
            {
                break;
            }
        }

        if (request.Length == 0)
        {
            throw new IOException("The browser closed the connection before sending a request.");
        }

        return Encoding.ASCII.GetString(request.GetBuffer(), 0, (int)request.Length);
    }

    private static bool EndsWithHeaderTerminator(byte[] buffer, int length)
    {
        if (length < HeaderTerminator.Length)
        {
            return false;
        }

        for (int index = 0; index < HeaderTerminator.Length; index++)
        {
            if (buffer[length - HeaderTerminator.Length + index] != HeaderTerminator[index])
            {
                return false;
            }
        }

        return true;
    }

    private static Task WriteNotFoundAsync(
        Stream stream,
        CancellationToken cancellationToken) => WriteResponseAsync(
        stream,
        404,
        "Not Found",
        "text/plain; charset=utf-8",
        "Not Found"u8.ToArray(),
        includeBody: true,
        cancellationToken);

    private static async Task WriteResponseAsync(
        Stream stream,
        int statusCode,
        string reason,
        string contentType,
        byte[] content,
        bool includeBody,
        CancellationToken cancellationToken)
    {
        string header = $"HTTP/1.1 {statusCode} {reason}\r\n"
            + $"Content-Length: {content.Length}\r\n"
            + $"Content-Type: {contentType}\r\n"
            + "Cache-Control: no-store\r\n"
            + "Connection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        if (includeBody)
        {
            await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string ContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" or ".mjs" => "text/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".wasm" => "application/wasm",
            ".dll" => "application/octet-stream",
            ".pdb" => "application/octet-stream",
            ".dat" => "application/octet-stream",
            ".webcil" => "application/octet-stream",
            ".ico" => "image/x-icon",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
}
