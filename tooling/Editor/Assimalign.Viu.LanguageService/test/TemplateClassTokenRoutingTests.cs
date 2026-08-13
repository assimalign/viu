using System;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins the document-structure boundary around template class-token scanning.
/// </summary>
public class TemplateClassTokenRoutingTests
{
    private const string CaretMarker = "[|]";
    private const string DocumentUri = "file:///workspace/TemplateClassTokenRouting.viu";

    [Fact]
    public void LanguageService_ClassMarkupInsideScriptString_DoesNotRouteAsTemplateClass()
    {
        const string markedSource =
            """"
            <template>
              <div></div>
            </template>
            @script {
            private string Markup = """<div class="gap-[|]4"></div>""";
            }
            <style>
            .gap-4 { gap: 2rem; }
            </style>
            """";
        var caretOffset = markedSource.IndexOf(CaretMarker, StringComparison.Ordinal);
        caretOffset.ShouldBeGreaterThanOrEqualTo(0);
        var source = markedSource.Remove(caretOffset, CaretMarker.Length);
        var position = TextCoordinateConverter.GetPosition(source, caretOffset);
        var service = LanguageServices.Create();
        service.OpenDocument(DocumentUri, source, 1);

        var completions = service.GetCompletions(DocumentUri, position);
        var hover = service.GetHover(DocumentUri, position);

        completions.ShouldBeEmpty();
        hover.ShouldBeNull();
    }
}
