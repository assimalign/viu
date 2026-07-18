using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// The [V01.01.05.01] incremental-caching contract: the AST is immutable records with structural
// equality, so parsing equal input twice yields equal (and equal-hashing) trees, and any content
// difference makes them unequal. This is what lets the Roslyn incremental generator ([V01.01.05.05])
// cache on the parse output.
public class StructuralEqualityTests
{
    private const string NontrivialTemplate =
        "<div id=\"app\" :class=\"cls\" @click.stop=\"go()\">\n" +
        "  <!-- header -->\n" +
        "  <template #header>\n" +
        "    <h1 :[dynamicAttribute]=\"value\">{{ title }}</h1>\n" +
        "  </template>\n" +
        "  <svg viewBox=\"0 0 1 1\"><foreignObject><p>x</p></foreignObject></svg>\n" +
        "  <input v-model.trim=\"name\" disabled>\n" +
        "  text &amp; entities\n" +
        "</div>";

    [Fact]
    public void Parse_SameInputTwice_ProducesEqualRoots()
    {
        var first = TemplateParser.Parse(NontrivialTemplate, ParserOptions.CreateHtml());
        var second = TemplateParser.Parse(NontrivialTemplate, ParserOptions.CreateHtml());

        first.ShouldNotBeSameAs(second);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Parse_SameInputTwice_BaseMode_ProducesEqualRoots()
    {
        var source = "{{ a }}<div v-if=\"ok\" :key=\"id\">text<!--c--></div>";
        TestHelpers.Parse(source).ShouldBe(TestHelpers.Parse(source));
    }

    [Fact]
    public void Parse_DifferentContent_ProducesUnequalRoots()
    {
        TestHelpers.Parse("<div>a</div>").ShouldNotBe(TestHelpers.Parse("<div>b</div>"));
    }

    [Fact]
    public void Parse_DifferentLocations_ProduceUnequalNodes()
    {
        // Same node content at a different offset must not compare equal — locations are part of
        // the record value (a whitespace shift must invalidate the cache).
        var first = TestHelpers.Parse("<div>x</div>").Children[0];
        var second = TestHelpers.Parse(" <div>x</div>").Children[0];

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Nodes_WithExpression_CompareByValueThroughWholeGraph()
    {
        var source = "<div v-on:click.stop=\"handle\"/>";
        var first = TestHelpers.Parse(source).Children[0].ShouldBeOfType<ElementNode>();
        var second = TestHelpers.Parse(source).Children[0].ShouldBeOfType<ElementNode>();

        var firstDirective = first.Properties[0].ShouldBeOfType<DirectiveNode>();
        var secondDirective = second.Properties[0].ShouldBeOfType<DirectiveNode>();
        firstDirective.ShouldBe(secondDirective);
        firstDirective.Modifiers.ShouldBe(secondDirective.Modifiers);
        firstDirective.Expression.ShouldBe(secondDirective.Expression);
    }
}
