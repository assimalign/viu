using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Assimalign.Viu.Components;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>Pins streaming output and completed-template subtree flush boundaries.</summary>
public class ServerRendererStreamingTests
{
    [Fact]
    public async Task RenderToStream_ProducesSameOutputAsRenderToString()
    {
        IComponent root = ComponentTree.Element(
            "div",
            TestTree.Attributes(("id", "app")),
            [ComponentTree.Text("hello")]);
        using StringWriter writer = new();

        await ServerRenderer.RenderToStreamAsync(root, writer);

        writer.ToString().ShouldBe("<div id=\"app\">hello</div>");
    }

    [Fact]
    public async Task RenderToStream_FlushesEachCompletedTemplateSubtree()
    {
        InlineComponent childA = new(
            _ => () => TestTree.Element("span", "a"));
        InlineComponent childB = new(
            _ => () => TestTree.Element("span", "b"));
        InlineComponent parent = new(
            _ => () => ComponentTree.Element(
                "div",
                children: [childA.Request(), childB.Request()]));
        ServerApplication application = Ssr.Application(parent.Request());
        RecordingTextWriter writer = new();

        await ServerRenderer.RenderToStreamAsync(application, writer);

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

        public override Task WriteAsync(
            ReadOnlyMemory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            string chunk = buffer.ToString();
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
