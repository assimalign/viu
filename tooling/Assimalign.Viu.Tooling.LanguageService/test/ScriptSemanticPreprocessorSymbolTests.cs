using System;
using System.Threading;

using Shouldly;

using Xunit;

namespace Assimalign.Viu.Tooling.LanguageService;

/// <summary>
/// Pins conditional-compilation fidelity ([V01.01.12.23], #259): the engine parses with the
/// context's <see cref="LanguageProjectContext.PreprocessorSymbols"/>, so a member the build guards
/// with <c>#if</c> exists in the editor compilation exactly when the build defines the symbol —
/// the editor and the compiler cannot disagree about what compiles.
/// </summary>
public class ScriptSemanticPreprocessorSymbolTests
{
    private const string ComponentSource =
        "<template>\n" +
        "  <div>x</div>\n" +
        "</template>\n" +
        "@script {\n" +
        "#if DEBUG\n" +
        "public int DebugOnlyCount { get; set; }\n" +
        "#endif\n" +
        "    \n" +
        "}\n";

    [Fact]
    public void GetCompletions_GuardedMember_AppearsWhenTheSymbolIsDefined()
    {
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(
                "stamp-debug",
                ScriptSemanticFixture.PreprocessorSymbols),
            ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.DocumentFilePath,
            ComponentSource,
            OffsetAfter(ComponentSource, "#endif\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Items.ShouldContain(item => item.Label == "DebugOnlyCount");
    }

    [Fact]
    public void GetCompletions_GuardedMember_IsAbsentWhenTheSymbolIsUndefined()
    {
        var engine = new ScriptSemanticEngine();

        var result = engine.GetCompletions(
            ScriptSemanticFixture.CreateContext(
                "stamp-release",
                ["TRACE", "NET", "NETCOREAPP", "NET10_0", "NET10_0_OR_GREATER"]),
            ScriptSemanticFixture.DocumentUri,
            ScriptSemanticFixture.DocumentFilePath,
            ComponentSource,
            OffsetAfter(ComponentSource, "#endif\n    "),
            string.Empty,
            ScriptCompletionContextKind.Expression,
            CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Items.ShouldNotContain(item => item.Label == "DebugOnlyCount");
    }

    private static int OffsetAfter(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, "the probe marker must occur in the source");
        return index + marker.Length;
    }
}
