using System.Text.Json;

using Shouldly;

using Xunit;

using Assimalign.Viu.VisualStudio.UtilityCss;

namespace Assimalign.Viu.VisualStudio.UtilityCss.Tests;

public sealed class UtilityCssDocumentMessageFilterTests
{
    [Theory]
    [InlineData("file:///C:/Source/Application/Index.html")]
    [InlineData("file:///C:/Source/Application/Index.HTM")]
    [InlineData("file:///C:/Source/Application/Document.__virtual.html")]
    [InlineData("file:///C:/Source/Application/Index%20Page.html?version=1")]
    public void ShouldForward_HtmlDocumentUri_ReturnsTrue(string documentUri)
    {
        using JsonDocument parameters = CreateParameters(documentUri);

        UtilityCssDocumentMessageFilter.ShouldForward(parameters.RootElement)
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData("file:///C:/Source/Application/Index.cshtml")]
    [InlineData("file:///C:/Source/Application/Index.razor")]
    [InlineData("file:///C:/Source/Application/Index.viu")]
    [InlineData("file:///C:/Source/Application/Index.vue")]
    [InlineData("file:///C:/Source/Application/Index.txt")]
    public void ShouldForward_NonHtmlDocumentUri_ReturnsFalse(string documentUri)
    {
        using JsonDocument parameters = CreateParameters(documentUri);

        UtilityCssDocumentMessageFilter.ShouldForward(parameters.RootElement)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"textDocument\":{}}")]
    [InlineData("{\"textDocument\":{\"uri\":null}}")]
    [InlineData("{\"textDocument\":{\"uri\":\"\"}}")]
    public void ShouldForward_MissingOrInvalidDocumentUri_ReturnsFalse(string parametersJson)
    {
        using JsonDocument parameters = JsonDocument.Parse(parametersJson);

        UtilityCssDocumentMessageFilter.ShouldForward(parameters.RootElement)
            .ShouldBeFalse();
    }

    private static JsonDocument CreateParameters(string documentUri) =>
        JsonDocument.Parse(
            "{\"textDocument\":{\"uri\":" +
            JsonSerializer.Serialize(documentUri) +
            "}}");
}
