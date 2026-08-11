using System;
using System.Collections.Generic;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>
/// Pins completion suppression inside C# comments in <c>@script</c>. Comment prose is never an
/// expression or declaration position, regardless of whether project semantics are active.
/// [V01.01.12.07.14]
/// </summary>
public class ScriptCommentCompletionTests
{
    public static IEnumerable<object[]> CommentPositions()
    {
        yield return ["    // Con", "// Con"];
        yield return ["    /* Con */", "/* Con"];
        yield return ["    /* first\n       Con\n    */", "Con\n"];
    }

    [Theory]
    [MemberData(nameof(CommentPositions))]
    public void GetCompletions_CursorInsideScriptComment_ReturnsNothing(
        string comment,
        string caretMarker)
    {
        var source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "public void Handle()\n" +
            "{\n" +
            comment + "\n" +
            "}\n" +
            "}\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var caretOffset = source.IndexOf(caretMarker, StringComparison.Ordinal) + caretMarker.Length;

        var completions = service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            TextCoordinateConverter.GetPosition(source, caretOffset));

        completions.ShouldBeEmpty();
    }

    [Fact]
    public void GetCompletions_AfterClosedScriptComment_StillOffersMemberCompletions()
    {
        const string source =
            "<template>\n  <div>x</div>\n</template>\n" +
            "@script {\n" +
            "/* prose */\n" +
            "Con\n" +
            "}\n";
        var service = LanguageServices.Create();
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);
        var caretOffset = source.IndexOf("Con\n", StringComparison.Ordinal) + "Con".Length;

        var completions = service.GetCompletions(
            ScriptSemanticFixture.DocumentUri,
            TextCoordinateConverter.GetPosition(source, caretOffset));

        completions.ShouldContain(completion => completion.Label == "Context");
    }
}
