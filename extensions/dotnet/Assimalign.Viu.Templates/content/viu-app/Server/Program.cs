using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Assimalign.Viu.Components;
using Assimalign.Viu.ServerRenderer;

using ViuApplication;

ServerRenderRegistry serverRenders = new();
GeneratedViuServerRenders.Register(serverRenders);
ServerRenderAdaptor<HttpContext> renderer = new(
    new RequestScopeFactory(),
    serverRenders);

if (args.Contains("--render-once", StringComparer.Ordinal))
{
    Console.Write(await RenderDocumentAsync(renderer, new DefaultHttpContext()));
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication application = builder.Build();
application.MapGet(
    "/",
    async (HttpContext context) => Results.Content(
        await RenderDocumentAsync(renderer, context),
        "text/html",
        Encoding.UTF8));
await application.RunAsync();

static async Task<string> RenderDocumentAsync(
    ServerRenderAdaptor<HttpContext> renderer,
    HttpContext context)
{
    TextBufferOutput output = new();
    ComponentNode root = new(ComponentReference.ForName("Counter"));
    ServerRenderResult result = await renderer.RenderAsync(
        new ServerRenderRequest<HttpContext>(root, context),
        output,
        context.RequestAborted);
    if (!result.Succeeded)
    {
        throw new InvalidOperationException(
            "Viu could not render the application on the server.",
            result.Failure);
    }

    return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>ViuApplication</title>
        </head>
        <body>
          <div id="app">{{output.Content}}</div>
        </body>
        </html>
        """;
}

internal sealed class RequestScopeFactory :
    IServerRenderRequestScopeFactory<HttpContext>
{
    public ValueTask<IServerRenderRequestScope> CreateAsync(
        ServerRenderRequest<HttpContext> request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ComponentFactory components = new();
        GeneratedViuComponents.Register(components);
        return ValueTask.FromResult<IServerRenderRequestScope>(
            new RequestScope(
                new ServerRenderApplication(request.RootComponent, components),
                new SsrContext()));
    }
}

internal sealed class RequestScope : IServerRenderRequestScope
{
    internal RequestScope(
        ServerRenderApplication application,
        SsrContext renderContext)
    {
        Application = application;
        RenderContext = renderContext;
    }

    public ServerRenderApplication Application { get; }

    public SsrContext RenderContext { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TextBufferOutput : IServerRenderOutput
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
