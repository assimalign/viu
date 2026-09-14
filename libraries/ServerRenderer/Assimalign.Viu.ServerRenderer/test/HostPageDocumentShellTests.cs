using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

public sealed class HostPageDocumentShellTests
{
    [Fact]
    public async Task WriteAsync_PublishedPage_PreservesAssetsAndReplacesOnlyMountChildren()
    {
        const string prefix = "\uFEFF<!DOCTYPE html>\r\n<html><head><base href=\"/docs/\">"
            + "<link href=\"application.4f829.css\" rel=\"stylesheet\"></head>\r\n"
            + "<body><div data-quoted=\"a>b\" ID = 'app'>";
        const string suffix = "</div>\r\n<script type=\"module\" src=\"main.2ac53.js\"></script>"
            + "<script src=\"_framework/dotnet.#[.{fingerprint}].js\"></script></body></html>";
        HostPageDocumentShell shell = new(prefix + "<p>Loading...</p>" + suffix);

        string document = await WriteDocumentAsync(shell, "<main>Rendered</main>");

        // [SSG-3] preserves published asset spelling and every character outside mount children.
        document.ShouldBe(prefix + "<main>Rendered</main>" + suffix);
    }

    [Theory]
    [InlineData("<div id=app><div><div>nested</div></div>old</div><footer>end</footer>",
        "<div id=app>root</div><footer>end</footer>")]
    [InlineData("<DIV id='app'><div>nested</DiV></dIv>", "<DIV id='app'>root</dIv>")]
    [InlineData("<section id=app data-value='>'>old</section>",
        "<section id=app data-value='>'>root</section>")]
    [InlineData("<main data-note=\"<div id='app'>\" id='app'></main>",
        "<main data-note=\"<div id='app'>\" id='app'>root</main>")]
    [InlineData("<div id='&#97;pp'>old</div>", "<div id='&#97;pp'>root</div>")]
    [InlineData("<div id=app data-path=/ >old</div>", "<div id=app data-path=/ >root</div>")]
    [InlineData("<div id=app data-path=/>old</div>", "<div id=app data-path=/>root</div>")]
    [InlineData("<div id=app><template><div>inert</div></template>old</div>", "<div id=app>root</div>")]
    public async Task WriteAsync_ExplicitMountBoundary_KeepsMatchingClosingTag(string hostPage, string expected)
    {
        // [SSG-3] matches case-insensitive HTML names and case-sensitive decoded identifiers.
        HostPageDocumentShell shell = new(hostPage);

        string document = await WriteDocumentAsync(shell, "root");

        document.ShouldBe(expected);
    }

    [Theory]
    [InlineData("<!-- <div id='app'>fake</div> -->")]
    [InlineData("<script>const host = \"<div id='app'>fake</div>\";</script>")]
    [InlineData("<script>const host = '</script-name><div id=app>fake</div>';</script>")]
    [InlineData("<style>div::before { content: '<div id=app>fake</div>'; }</style>")]
    [InlineData("<textarea><div id=app>fake</div></textarea>")]
    [InlineData("<title><div id=app>fake</div></title>")]
    [InlineData("<noscript><div id=app>fake</div></noscript>")]
    [InlineData("<template><div id=app>fake</div></template>")]
    [InlineData("<template><template><div id=app>fake</div></template></template>")]
    public async Task WriteAsync_InertLookalikeMarkup_DoesNotMatchMount(string inertMarkup)
    {
        HostPageDocumentShell shell = new(inertMarkup + "<div id='app'>old</div>" + inertMarkup);

        string document = await WriteDocumentAsync(shell, "root");

        // [SSG-3] never treats markup-looking text or template content as a live mount target.
        document.ShouldBe(inertMarkup + "<div id='app'>root</div>" + inertMarkup);
    }

    [Theory]
    [InlineData("<div></div>", "does not match")]
    [InlineData("<div id=App></div>", "does not match")]
    [InlineData("<template><div id=app></div></template>", "does not match")]
    [InlineData("<div id=app>", "no matching closing tag")]
    [InlineData("<div id=app></div><main id=app></main>", "more than one")]
    [InlineData("<div id=app><div id=app></div></div>", "more than one")]
    [InlineData("<input id=app>", "non-void container")]
    [InlineData("<div id='app'/>", "non-void container")]
    [InlineData("<script id=app></script>", "non-void container")]
    [InlineData("<style id=app></style>", "non-void container")]
    [InlineData("<textarea id=app></textarea>", "non-void container")]
    [InlineData("<template id=app></template>", "non-void container")]
    [InlineData("<div id=app id=other></div>", "duplicate id attributes")]
    [InlineData("<!-- never closed", "unterminated HTML comment")]
    [InlineData("<div id='app>", "unterminated HTML tag")]
    [InlineData("<script>never closed", "unterminated 'script'")]
    public void Constructor_InvalidPage_ReportsSelectorAndReason(string hostPage, string reason)
    {
        // [SSG-3] rejects ambiguous or unavailable mount containers before document output begins.
        ArgumentException failure = Should.Throw<ArgumentException>(() => new HostPageDocumentShell(hostPage));

        failure.Message.ShouldContain("#app");
        failure.Message.ShouldContain(reason);
        failure.ParamName.ShouldBe("hostPage");
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("app")]
    [InlineData(".app")]
    [InlineData("#app main")]
    [InlineData("#app,#other")]
    [InlineData("#123")]
    [InlineData("#app\\:main")]
    public void Constructor_UnsupportedSelector_ReportsSupportedGrammar(string selector)
    {
        ArgumentException failure = Should.Throw<ArgumentException>(
            () => new HostPageDocumentShell("<div id=app></div>", selector));

        failure.ParamName.ShouldBe("mountSelector");
        failure.Message.ShouldContain("General CSS selectors are unsupported");
    }

    [Fact]
    public async Task WriteAsync_CustomIdentifier_SelectsAuthoredMount()
    {
        HostPageDocumentShell shell = new("<main id='_mount-42'>old</main>", "#_mount-42");

        string document = await WriteDocumentAsync(shell, "root");

        document.ShouldBe("<main id='_mount-42'>root</main>");
    }

    [Fact]
    public void WriteSuffixAsync_TeleportOutput_RejectsBeforeWritingSuffix()
    {
        HostPageDocumentShell shell = new("<div id=app></div>");
        RecordingOutput output = new();
        Dictionary<string, string> teleports = new() { ["#modal"] = "content" };

        // [SSG-3], [HYD-6]: a successful document cannot silently discard teleport payloads.
        NotSupportedException failure = Should.Throw<NotSupportedException>(
            () => shell.WriteSuffixAsync(output, teleports));

        failure.Message.ShouldContain("teleport");
        output.Text.ShouldBeEmpty();
    }

    [Fact]
    public void WriteAsync_CancelledRequest_DoesNotWrite()
    {
        HostPageDocumentShell shell = new("<div id=app></div>");
        RecordingOutput output = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(() => shell.WritePrefixAsync(output, cancellation.Token));
        Should.Throw<OperationCanceledException>(() => shell.WriteSuffixAsync(output,
            new Dictionary<string, string>(), cancellation.Token));
        output.Text.ShouldBeEmpty();
    }

    private static async Task<string> WriteDocumentAsync(HostPageDocumentShell shell, string root)
    {
        RecordingOutput output = new();
        await shell.WritePrefixAsync(output);
        await output.WriteAsync(root.AsMemory());
        await shell.WriteSuffixAsync(output, new Dictionary<string, string>());
        return output.Text;
    }

    private sealed class RecordingOutput : IServerRenderOutput
    {
        private readonly StringBuilder _text = new();

        internal string Text => _text.ToString();

        public bool ResponseCommitted => false;

        public ValueTask WriteAsync(ReadOnlyMemory<char> content, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _text.Append(content.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
