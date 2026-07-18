using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax;

// The incremental-caching contract at the primitive level ([V01.01.05.01]/[V01.01.06.01]): SyntaxList<T>
// is value-equatable (element-wise Equals + 17/31 hash), so the AST/descriptor records that hold it
// compare by value end to end. These tests moved here from the template-compiler and single-file-component
// suites when the primitive became the shared Assimalign.Vue.Syntax base ([V01.01.05.09]); preserving the
// exact equality semantics is what keeps the derived generators' caches hitting.
public class SyntaxListTests
{
    // A minimal located node standing in for the derived parsers' record types (which are not visible
    // to this base test project).
    private sealed record Item
    {
        public required string Content { get; init; }

        public required SourceLocation Location { get; init; }
    }

    [Fact]
    public void SyntaxList_StructuralEquality_ComparesElementsInOrder()
    {
        var a = new SyntaxList<Item>(new[]
        {
            new Item { Content = "x", Location = Location(0, 1) },
        });
        var b = new SyntaxList<Item>(new[]
        {
            new Item { Content = "x", Location = Location(0, 1) },
        });
        var c = new SyntaxList<Item>(new[]
        {
            new Item { Content = "y", Location = Location(0, 1) },
        });

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
        (a != c).ShouldBeTrue();
        SyntaxList<Item>.Empty.ShouldBe(default);
    }

    [Fact]
    public void SyntaxList_EmptyEqualsDefault()
    {
        SyntaxList<Item>.Empty.ShouldBe(default);
    }

    private static SourceLocation Location(int start, int end)
        => new(new Position(start, 1, start + 1), new Position(end, 1, end + 1), "x");
}
