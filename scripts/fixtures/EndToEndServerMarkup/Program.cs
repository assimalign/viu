using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

namespace EndToEndServerMarkup;

internal static class Program
{
    internal static async Task<int> Main(string[] arguments)
    {
        if (arguments.Length != 1)
        {
            Console.Error.WriteLine("Usage: EndToEndServerMarkup <output-index-html>");
            return 2;
        }

        ComponentNode root = HydrationFixture.CreateRoot();
        RequestScopeFactory requestScopeFactory = new();
        ServerRenderAdaptor<ServerMarkupRequestContext> adaptor =
            new(requestScopeFactory);
        TextBufferOutput output = new();
        ServerRenderResult result = await adaptor.RenderAsync(
            new ServerRenderRequest<ServerMarkupRequestContext>(
                root,
                new ServerMarkupRequestContext("end-to-end hydration")),
            output);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "ServerRenderAdaptor failed to produce the hydration fixture markup.",
                result.Failure);
        }

        if (output.Content.Length == 0 || requestScopeFactory.DisposalCount != 1)
        {
            throw new InvalidOperationException(
                "ServerRenderAdaptor did not produce output and dispose exactly one request scope.");
        }

        string outputPath = Path.GetFullPath(arguments[0]);
        string? directory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException("The output path has no parent directory.");
        }

        Directory.CreateDirectory(directory);
        string document = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Viu ServerRenderAdaptor hydration fixture</title>
              <link rel="preload" id="webassembly" />
              <script type="importmap"></script>
              <script type="module" src="main#[.{fingerprint}].js"></script>
            </head>
            <body>
              <div id="app">{{output.Content}}</div>
              <script>
                globalThis.__viuServerHeading = document.querySelector('[data-testid="hydrated-heading"]');
                globalThis.__viuServerMarkupSource = 'ServerRenderAdaptor';
              </script>
            </body>
            </html>
            """;
        await File.WriteAllTextAsync(outputPath, document, Encoding.UTF8);
        Console.WriteLine(
            $"ServerRenderAdaptor wrote {output.Content.Length} markup characters to {outputPath}.");
        return 0;
    }

    private sealed record ServerMarkupRequestContext(string Scenario);

    private sealed class RequestScopeFactory :
        IServerRenderRequestScopeFactory<ServerMarkupRequestContext>
    {
        internal int DisposalCount { get; private set; }

        public ValueTask<IServerRenderRequestScope> CreateAsync(
            ServerRenderRequest<ServerMarkupRequestContext> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = request.RequestContext.Scenario;
            return ValueTask.FromResult<IServerRenderRequestScope>(
                new RequestScope(
                    new ServerRenderApplication(
                        request.RootComponent,
                        HydrationFixture.CreateComponents()),
                    new SsrContext(),
                    () => DisposalCount++));
        }
    }

    private sealed class RequestScope : IServerRenderRequestScope
    {
        private readonly Action _dispose;

        internal RequestScope(
            ServerRenderApplication application,
            SsrContext renderContext,
            Action dispose)
        {
            Application = application;
            RenderContext = renderContext;
            _dispose = dispose;
        }

        public ServerRenderApplication Application { get; }

        public SsrContext RenderContext { get; }

        public ValueTask DisposeAsync()
        {
            _dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TextBufferOutput : IServerRenderOutput
    {
        private readonly StringBuilder _content = new();

        internal string Content => _content.ToString();

        // A memory buffer is always replaceable, so the response is never committed ([SSR-12]).
        public bool ResponseCommitted => false;

        public ValueTask WriteAsync(
            ReadOnlyMemory<char> content,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _content.Append(content.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
