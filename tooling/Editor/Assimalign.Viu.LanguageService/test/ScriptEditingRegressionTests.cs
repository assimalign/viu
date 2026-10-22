using System;
using System.Linq;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.LanguageService.Tests;

/// <summary>Pins caret-local completion and incomplete import coloring. [V01.01.12.07.18]</summary>
public class ScriptEditingRegressionTests
{
    [Theory]
    [InlineData("using System.Col|lections.Generic;", "Collections", "Collections")]
    [InlineData("using System.|Collections.Generic;", "Collections", "Collections")]
    [InlineData("public void Handle() { Context.Life|cycle.OnMounted(() => { }); }", "Lifecycle", "Lifecycle")]
    public void GetCompletions_MiddleOfDottedName_ReplacesOnlyCurrentIdentifier(
        string script, string label, string replacedText)
    {
        int offset = script.IndexOf('|');
        string source = "<template><div /></template>\n@script {\n" + script.Replace("|", "") + "\n}";
        offset += source.IndexOf(script[..script.IndexOf('|')], StringComparison.Ordinal);
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
            ScriptSemanticFixture.DocumentUri, ScriptSemanticFixture.CreateContext());
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var item = service.GetCompletions(ScriptSemanticFixture.DocumentUri,
            TextCoordinateConverter.GetPosition(source, offset)).Single(candidate => candidate.Label == label);

        item.EditRange.ShouldNotBeNull();
        TextCoordinateConverter.TryGetOffset(source, item.EditRange!.Value.Start, out int start).ShouldBeTrue();
        TextCoordinateConverter.TryGetOffset(source, item.EditRange.Value.End, out int end).ShouldBeTrue();
        source[start..end].ShouldBe(replacedText);
        (source[..start] + item.InsertText + source[end..]).ShouldBe(source);
    }

    [Theory]
    [InlineData("using System.Collections.Generic")]
    [InlineData("using System.Collections.")]
    [InlineData("using System.Col")]
    public void GetClassifications_IncompleteUsing_KeepsKnownNamespaceColored(string directive)
    {
        string source = "<template><div /></template>\n@script {\n" + directive + "\npublic int Count;\n}";
        var service = LanguageServices.Create();
        ((IScriptSemanticLanguageService)service).ConfigureProjectContext(
            ScriptSemanticFixture.DocumentUri, ScriptSemanticFixture.CreateContext());
        service.OpenDocument(ScriptSemanticFixture.DocumentUri, source, 1);

        var classifications = service.GetClassifications(ScriptSemanticFixture.DocumentUri);

        classifications.ShouldContain(classification =>
            classification.Range.Start == new LanguagePosition(2, 6) &&
            classification.ClassificationTypeName == LanguageClassificationTypeNames.NamespaceName);
    }
}
