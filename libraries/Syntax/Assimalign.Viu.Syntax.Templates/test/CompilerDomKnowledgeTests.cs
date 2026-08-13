using System.Linq;

using Shouldly;

using Xunit;

using Assimalign.Viu.Syntax.Templates;

namespace Assimalign.Viu.Syntax.Templates.Tests;

/// <summary>
/// Pins the shared DOM-table enumeration used by native binding completion
/// ([V01.01.12.07.12], #333).
/// </summary>
public class CompilerDomKnowledgeTests
{
    [Fact]
    public void EnumerateKnownHtmlAttributes_ReturnsUniquePredicateAcceptedNames()
    {
        var attributes = CompilerDomKnowledge.EnumerateKnownHtmlAttributes().ToArray();

        attributes.ShouldContain("type");
        attributes.ShouldContain("disabled");
        attributes.Distinct().Count().ShouldBe(attributes.Length);
        attributes.ShouldAllBe(attribute => CompilerDomKnowledge.IsKnownHtmlAttribute(attribute));
    }

    [Fact]
    public void EnumerateKnownSvgAttributes_ReturnsUniquePredicateAcceptedNames()
    {
        var attributes = CompilerDomKnowledge.EnumerateKnownSvgAttributes().ToArray();

        attributes.ShouldContain("viewBox");
        attributes.ShouldContain("fill");
        attributes.Distinct().Count().ShouldBe(attributes.Length);
        attributes.ShouldAllBe(attribute => CompilerDomKnowledge.IsKnownSvgAttribute(attribute));
    }

    [Fact]
    public void EnumerateKnownMathMlAttributes_ReturnsUniquePredicateAcceptedNames()
    {
        var attributes = CompilerDomKnowledge.EnumerateKnownMathMlAttributes().ToArray();

        attributes.ShouldContain("displaystyle");
        attributes.ShouldContain("mathcolor");
        attributes.Distinct().Count().ShouldBe(attributes.Length);
        attributes.ShouldAllBe(attribute => CompilerDomKnowledge.IsKnownMathMlAttribute(attribute));
    }
}
