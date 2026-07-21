using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.ServerRenderer.Tests;

/// <summary>
/// Pins text and value escaping against the exact upstream tables
/// (<c>@vue/shared/src/escapeHtml.ts</c>). Escaping is security-adjacent, so injection-shaped inputs
/// are covered here directly.
/// </summary>
public class ServerRenderEscapingTests
{
    [Fact]
    public void EscapeHtml_AllFiveCharacters_MatchesUpstreamTable()
    {
        // Upstream escapeHtml: " -> &quot;  & -> &amp;  ' -> &#39;  < -> &lt;  > -> &gt;
        ServerRender.EscapeHtml("\"").ShouldBe("&quot;");
        ServerRender.EscapeHtml("&").ShouldBe("&amp;");
        ServerRender.EscapeHtml("'").ShouldBe("&#39;");
        ServerRender.EscapeHtml("<").ShouldBe("&lt;");
        ServerRender.EscapeHtml(">").ShouldBe("&gt;");
    }

    [Fact]
    public void EscapeHtml_NoSpecialCharacters_ReturnsInputUnchanged()
        => ServerRender.EscapeHtml("plain text 123").ShouldBe("plain text 123");

    [Fact]
    public void EscapeHtml_Null_ReturnsEmpty() => ServerRender.EscapeHtml((string?)null).ShouldBe(string.Empty);

    [Fact]
    public void EscapeHtml_ScriptInjection_IsNeutralized()
    {
        // The canonical XSS payload must not survive as live markup.
        var escaped = ServerRender.EscapeHtml("<script>alert('xss')&</script>");
        escaped.ShouldBe("&lt;script&gt;alert(&#39;xss&#39;)&amp;&lt;/script&gt;");
        escaped.ShouldNotContain("<script>");
    }

    [Fact]
    public void EscapeHtml_MixedRuns_EscapesOnlySpecials()
        => ServerRender.EscapeHtml("a<b>c&d\"e'f").ShouldBe("a&lt;b&gt;c&amp;d&quot;e&#39;f");

    [Fact]
    public void EscapeHtmlComment_StripsTerminatorSequences()
    {
        // A comment payload must not break out of its <!-- --> wrapper.
        ServerRender.EscapeHtmlComment("a-->b").ShouldBe("ab");
        ServerRender.EscapeHtmlComment("<!--nested-->").ShouldBe("nested");
        ServerRender.EscapeHtmlComment("--!>x").ShouldBe("x");
    }

    [Fact]
    public void EscapeHtmlComment_RepeatsUntilStable()
        // Overlapping sequences must not reconstitute a terminator after one pass.
        => ServerRender.EscapeHtmlComment("--!<!--->").ShouldNotContain("-->");

    [Fact]
    public void SsrRenderComment_EmptyContent_ProducesAnchor()
        => ServerRender.SsrRenderComment("").ShouldBe("<!---->");

    [Fact]
    public void SsrInterpolate_EscapesDisplayString()
    {
        ServerRender.SsrInterpolate("<b>").ShouldBe("&lt;b&gt;");
        ServerRender.SsrInterpolate(null).ShouldBe(string.Empty);
        ServerRender.SsrInterpolate(42).ShouldBe("42");
        ServerRender.SsrInterpolate(true).ShouldBe("true");
    }

    [Fact]
    public void SsrRenderClass_NormalizesThenEscapes()
    {
        // Normalizes an array/map to a class string, then escapes it.
        ServerRender.SsrRenderClass(new List<object?> { "a", "b" }).ShouldBe("a b");
        ServerRender.SsrRenderClass(new Dictionary<string, object?> { ["active"] = true, ["off"] = false })
            .ShouldBe("active");
        ServerRender.SsrRenderClass("a<b").ShouldBe("a&lt;b");
    }

    [Fact]
    public void SsrRenderStyle_StringAndMap_AreEscaped()
    {
        ServerRender.SsrRenderStyle(null).ShouldBe(string.Empty);
        ServerRender.SsrRenderStyle("color:red").ShouldBe("color:red");
        ServerRender.SsrRenderStyle(new Dictionary<string, object?> { ["color"] = "red", ["fontSize"] = "12px" })
            .ShouldBe("color:red;font-size:12px;");
        // Injection through a style string is escaped, not executed.
        ServerRender.SsrRenderStyle("\"><script>").ShouldBe("&quot;&gt;&lt;script&gt;");
    }
}
