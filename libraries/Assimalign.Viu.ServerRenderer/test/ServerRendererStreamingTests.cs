using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

using Assimalign.Viu.RuntimeCore;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Streaming SSR: <see cref="ServerRenderer.RenderToStreamAsync(ServerApplication, TextWriter, SsrContext?, CancellationToken)"/>
/// writes completed subtrees as chunks and awaits the writer's flush (backpressure), rather than
/// buffering the whole document.
/// </summary>
public class ServerRendererStreamingTests
{
    [Fact]
    public async Task RenderToStream_ProducesSameOutputAsRenderToString()
    {
        var component = new InlineComponent((_, _) => () =>
            VirtualNodeFactory.Element("div", VirtualNodeFactory.Properties(("id", "app")), "hello"));

        using var stringWriter = new StringWriter();
        await ServerRenderer.RenderToStreamAsync(component, stringWriter);

        stringWriter.ToString().ShouldBe("<div id=\"app\">hello</div>");
    }

    [Fact]
    public async Task RenderToStream_FlushesEachCompletedSubtree()
    {
        // A parent with two child components flushes at each subtree boundary, so the writer sees multiple
        // chunks and multiple flushes instead of one buffered write.
        var childA = new InlineComponent((_, _) => () => VirtualNodeFactory.Element("span", "a"));
        var childB = new InlineComponent((_, _) => () => VirtualNodeFactory.Element("span", "b"));
        var parent = new InlineComponent((_, _) => () => VirtualNodeFactory.Element(
            "div",
            VirtualNodeFactory.Component(childA),
            VirtualNodeFactory.Component(childB)));

        var writer = new RecordingTextWriter();
        await ServerRenderer.RenderToStreamAsync(parent, writer);

        writer.Text.ShouldBe("<div><span>a</span><span>b</span></div>");
        writer.Chunks.Count.ShouldBeGreaterThan(1);
        writer.FlushCount.ShouldBeGreaterThan(1);
    }

    private sealed class RecordingTextWriter : TextWriter
    {
        private readonly StringBuilder _all = new();

        public List<string> Chunks { get; } = [];

        public int FlushCount { get; private set; }

        public string Text => _all.ToString();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => _all.Append(value);

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            var chunk = buffer.ToString();
            Chunks.Add(chunk);
            _all.Append(chunk);
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCount++;
            return Task.CompletedTask;
        }
    }
}
