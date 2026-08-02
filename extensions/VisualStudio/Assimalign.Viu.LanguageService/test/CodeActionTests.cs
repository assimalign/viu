using System;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService;

/// <summary>
/// Pins the VIU1206 quick fix: a <c>.vue</c> script block without <c>lang="csharp"</c> offers a
/// preferred quickfix that inserts or replaces the <c>lang</c> attribute, gated on the requested
/// range intersecting the diagnostic.
/// </summary>
public class CodeActionTests
{
    private const string DocumentUri = "file:///workspace/Counter.viu";
    private const string VueDocumentUri = "file:///workspace/Counter.vue";

    [Fact]
    public void GetCodeActions_VueScriptWithoutLanguage_InsertsCSharpLanguageAttribute()
    {
        const string source = "<script>export default {}</script>\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var actions = service.GetCodeActions(
            VueDocumentUri,
            new LanguageRange(
                new LanguagePosition(0, 0),
                new LanguagePosition(0, 10)));

        var action = actions.ShouldHaveSingleItem();
        action.Title.ShouldBe("Use lang=\"csharp\"");
        action.Kind.ShouldBe("quickfix");
        action.IsPreferred.ShouldBeTrue();
        action.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe("VIU1206");
        var edit = action.Edits.ShouldHaveSingleItem();
        var insertCharacter = source.IndexOf('>');
        edit.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(0, insertCharacter),
                new LanguagePosition(0, insertCharacter)));
        edit.NewText.ShouldBe(" lang=\"csharp\"");
    }

    [Fact]
    public void GetCodeActions_VueScriptWithJavaScriptLanguage_ReplacesLanguageAttribute()
    {
        const string source = "<script lang=\"ts\">export default {}</script>\n";
        var optionStart = source.IndexOf("lang=\"ts\"", StringComparison.Ordinal);
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var actions = service.GetCodeActions(
            VueDocumentUri,
            new LanguageRange(
                new LanguagePosition(0, optionStart),
                new LanguagePosition(0, optionStart + 2)));

        var action = actions.ShouldHaveSingleItem();
        var edit = action.Edits.ShouldHaveSingleItem();
        edit.Range.ShouldBe(
            new LanguageRange(
                new LanguagePosition(0, optionStart),
                new LanguagePosition(0, optionStart + "lang=\"ts\"".Length)));
        edit.NewText.ShouldBe("lang=\"csharp\"");
        action.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe("VIU1206");
    }

    [Fact]
    public void GetCodeActions_RangeOutsideDiagnostic_ReturnsEmpty()
    {
        const string source =
            "<template><div /></template>\n" +
            "<script>export default {}</script>\n";
        var service = ViuLanguageServices.Create();
        service.OpenDocument(VueDocumentUri, source, 1);

        var actions = service.GetCodeActions(
            VueDocumentUri,
            new LanguageRange(
                new LanguagePosition(0, 0),
                new LanguagePosition(0, 5)));

        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCodeActions_ViuFormatDocument_ReturnsEmpty()
    {
        // VIU1206 is Vue-format-only: a .viu @script block is always C#.
        var service = ViuLanguageServices.Create();
        service.OpenDocument(DocumentUri, "@script {\n    public int Count;\n}\n", 1);

        var actions = service.GetCodeActions(
            DocumentUri,
            new LanguageRange(
                new LanguagePosition(0, 0),
                new LanguagePosition(2, 1)));

        actions.ShouldBeEmpty();
    }
}
