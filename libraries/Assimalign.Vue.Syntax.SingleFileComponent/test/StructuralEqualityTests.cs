using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.SingleFileComponent;

// The [V01.01.06.01] incremental-caching contract: descriptors and blocks are immutable records with
// structural equality, so parsing equal input twice yields equal (and equal-hashing) descriptors, and
// any content or location difference makes them unequal. This is what lets the incremental SFC generator
// ([V01.01.06.02]) cache on the parse output.
public class StructuralEqualityTests
{
    private const string Component =
        "@template {\n    <div>{{ title }}</div>\n}\n" +
        "@script lang=\"csharp\" {\n    public string Title = \"hi\";\n}\n" +
        "@style scoped {\n    .a { color: red; }\n}\n" +
        "@docs {\n    notes\n}\n";

    [Fact]
    public void Parse_SameInputTwice_ProducesEqualDescriptors()
    {
        var first = SingleFileComponentTestHelpers.Parse(Component);
        var second = SingleFileComponentTestHelpers.Parse(Component);

        first.ShouldNotBeSameAs(second);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Parse_SameInputTwice_ProducesEqualResults()
    {
        SingleFileComponentParser.Parse(Component).ShouldBe(SingleFileComponentParser.Parse(Component));
    }

    [Fact]
    public void Parse_DifferentContent_ProducesUnequalDescriptors()
    {
        SingleFileComponentTestHelpers.Parse("@template {\n    a\n}\n")
            .ShouldNotBe(SingleFileComponentTestHelpers.Parse("@template {\n    b\n}\n"));
    }

    [Fact]
    public void Parse_ShiftedByWhitespace_ProducesUnequalDescriptors()
    {
        // A leading blank line shifts every offset, so the locations — part of the record value — differ,
        // and the descriptors must not compare equal (a whitespace shift must invalidate the cache).
        SingleFileComponentTestHelpers.Parse(Component).ShouldNotBe(SingleFileComponentTestHelpers.Parse("\n" + Component));
    }

    [Fact]
    public void Parse_Blocks_CompareByValueThroughOptionsAndSpans()
    {
        var first = SingleFileComponentTestHelpers.Parse(Component);
        var second = SingleFileComponentTestHelpers.Parse(Component);

        first.Script.ShouldBe(second.Script);
        first.Styles.ShouldBe(second.Styles);
        first.Styles[0].Options.ShouldBe(second.Styles[0].Options);
        first.CustomBlocks.ShouldBe(second.CustomBlocks);
    }

    [Fact]
    public void Parse_DifferentOptions_ProduceUnequalStyleBlocks()
    {
        var scoped = SingleFileComponentTestHelpers.Parse("@style scoped {\n}\n").Styles[0];
        var plain = SingleFileComponentTestHelpers.Parse("@style {\n}\n").Styles[0];

        scoped.ShouldNotBe(plain);
    }
}
