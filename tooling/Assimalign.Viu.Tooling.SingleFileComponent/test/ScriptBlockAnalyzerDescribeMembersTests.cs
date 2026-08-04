using Shouldly;
using Xunit;

namespace Assimalign.Viu.Tooling.SingleFileComponent.Tests;

/// <summary>
/// Pins the editor entry point of the shared script core ([V01.01.06.11]):
/// <see cref="ScriptBlockAnalyzer.DescribeMembers"/> reuses the exact leading-using split and probe
/// wrapper the build's <c>Analyze</c> path uses (the drift the extraction kills), parses with
/// <c>DocumentationMode.Parse</c> so <c>///</c> summaries surface, and reports block-content-relative
/// locations so a description is a pure, cacheable function of the script text.
/// </summary>
public sealed class ScriptBlockAnalyzerDescribeMembersTests
{
    [Fact]
    public void DescribeMembers_FieldPropertyAndMethod_AreDescribedInDeclarationOrder()
    {
        const string content =
            "public int Count = 0;\n" +
            "public string Title { get; set; } = \"\";\n" +
            "private string Name() => \"x\";\n";

        var members = ScriptBlockAnalyzer.DescribeMembers(content);

        members.Count.ShouldBe(3);
        members[0].Name.ShouldBe("Count");
        members[0].Kind.ShouldBe(ScriptDeclaredMemberKind.Field);
        members[0].Detail.ShouldBe("int");
        members[1].Name.ShouldBe("Title");
        members[1].Kind.ShouldBe(ScriptDeclaredMemberKind.Property);
        members[1].Detail.ShouldBe("string");
        members[2].Name.ShouldBe("Name");
        members[2].Kind.ShouldBe(ScriptDeclaredMemberKind.Method);
        members[2].Detail.ShouldBe("string Name()");
        members.ShouldAllBe(member => member.DocumentationSummary == null);
    }

    [Fact]
    public void DescribeMembers_LeadingUsings_AreSplitOutAndLocationsStayBlockRelative()
    {
        // The same line-boundary cut Analyze performs: the using line is cut, and the described member
        // locations are relative to the WHOLE block content — offset 14 (after "using System;\n"),
        // one-based line 2 — so the service can compose them with ComposeToFilePosition exactly like
        // the emitted #line map.
        const string content =
            "using System;\n" +               // block-content line 1 — cut
            "public int Count = 0;\n" +        // line 2
            "private string Name() => \"x\";\n"; // line 3

        var members = ScriptBlockAnalyzer.DescribeMembers(content);

        members.Count.ShouldBe(2);

        var field = members[0];
        field.MemberLocation.Start.Offset.ShouldBe(14);
        field.MemberLocation.Start.Line.ShouldBe(2);
        field.MemberLocation.Start.Column.ShouldBe(1);
        field.MemberLocation.Source.ShouldBe("public int Count = 0;");
        field.MemberLocation.End.Offset.ShouldBe(35);
        field.IdentifierLocation.Start.Offset.ShouldBe(content.IndexOf("Count"));
        field.IdentifierLocation.Start.Line.ShouldBe(2);
        field.IdentifierLocation.Start.Column.ShouldBe(12);
        field.IdentifierLocation.Source.ShouldBe("Count");

        var method = members[1];
        method.MemberLocation.Start.Offset.ShouldBe(36);
        method.MemberLocation.Start.Line.ShouldBe(3);
        method.MemberLocation.Start.Column.ShouldBe(1);
        method.IdentifierLocation.Start.Offset.ShouldBe(content.IndexOf("Name"));
        method.IdentifierLocation.Start.Line.ShouldBe(3);
        method.IdentifierLocation.Start.Column.ShouldBe(16);
        method.IdentifierLocation.Source.ShouldBe("Name");
    }

    [Fact]
    public void DescribeMembers_DocumentationSummary_SurfacesTheFirstSummaryLine()
    {
        // DocumentationMode.Parse — the editor path's deliberate divergence from the build path's None —
        // makes the /// trivia structured; inline tags are stripped from the surfaced line.
        const string content =
            "/// <summary>The current <see cref=\"Count\"/> value.</summary>\n" +
            "public int Count = 0;\n";

        var members = ScriptBlockAnalyzer.DescribeMembers(content);

        members.Count.ShouldBe(1);
        members[0].DocumentationSummary.ShouldBe("The current  value.");
    }

    [Fact]
    public void DescribeMembers_MultiVariableField_DescribesEachVariableWithItsOwnIdentifier()
    {
        const string content = "public int First = 0, Second = 1;\n";

        var members = ScriptBlockAnalyzer.DescribeMembers(content);

        members.Count.ShouldBe(2);
        members[0].Name.ShouldBe("First");
        members[1].Name.ShouldBe("Second");
        // Both variables share the full field declaration span; identifiers are their own.
        members[0].MemberLocation.ShouldBe(members[1].MemberLocation);
        members[0].IdentifierLocation.Start.Offset.ShouldBe(content.IndexOf("First"));
        members[1].IdentifierLocation.Start.Offset.ShouldBe(content.IndexOf("Second"));
    }

    [Fact]
    public void DescribeMembers_VerbatimIdentifier_KeepsTheAtSign()
    {
        var members = ScriptBlockAnalyzer.DescribeMembers("public int @class = 0;\n");

        members.ShouldHaveSingleItem().Name.ShouldBe("@class");
    }

    [Fact]
    public void DescribeMembers_WhitespaceOnlyOrUsingOnlyContent_DescribesNothing()
    {
        ScriptBlockAnalyzer.DescribeMembers("   \n\n").Count.ShouldBe(0);
        ScriptBlockAnalyzer.DescribeMembers("using System;\n\n").Count.ShouldBe(0);
    }

    [Fact]
    public void DescribeMembers_IdenticalContent_ProducesValueEqualDescriptions()
    {
        // The description is a pure function of the script text: the language service caches it keyed on
        // content, so two runs over the same text must be value-equal (EquatableArray + record equality).
        const string content =
            "using System;\n" +
            "/// <summary>The count.</summary>\n" +
            "public int Count = 0;\n" +
            "public string Name() => \"x\";\n";

        var first = ScriptBlockAnalyzer.DescribeMembers(content);
        var second = ScriptBlockAnalyzer.DescribeMembers(content);

        first.ShouldBe(second);
    }
}
