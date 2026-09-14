using System;
using System.Collections.Generic;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Router.Tests;

// [RTR-12], [V01.01.08.09]: query values preserve wire text separately from decoded ordered
// pairs, with immutable builders and boxing-free, ordinal accessors.
public class RouteQueryTests
{
    [Fact]
    public void Default_IsEmpty_AndHasTheSameValueAndHashAsParsedEmptyQueries()
    {
        var query = default(RouteQuery);

        query.RawText.ShouldBe(string.Empty);
        query.Count.ShouldBe(0);
        query.Names.ShouldBeEmpty();
        query.GetStrings("missing").ShouldBeEmpty();
        query.ShouldBe(RouteQuery.Empty);
        query.ShouldBe(RouteQuery.Parse(string.Empty));
        query.ShouldBe(RouteQuery.Parse("&&"));
        query.GetHashCode().ShouldBe(RouteQuery.Parse("&&").GetHashCode());
        RouteQuery.Parse("&&").RawText.ShouldBe("&&");
    }

    [Fact]
    public void Parse_RepeatedAndEmptyNames_RetainsValuesAndFirstAppearanceNameOrder()
    {
        var query = RouteQuery.Parse("x=1&=empty-name&y=3&x=2&=second-empty-name&x=");

        query.RawText.ShouldBe("x=1&=empty-name&y=3&x=2&=second-empty-name&x=");
        query.Count.ShouldBe(3);
        query.Names.ShouldBe(new[] { "x", string.Empty, "y" });
        query.GetString("x").ShouldBe("1");
        query.GetStrings("x").ShouldBe(new[] { "1", "2", string.Empty });
        query.GetStrings(string.Empty).ShouldBe(new[] { "empty-name", "second-empty-name" });
    }

    [Fact]
    public void Parse_EmptySegmentsAndMissingAssignments_SkipsSegmentsButRetainsEmptyValues()
    {
        var query = RouteQuery.Parse("&&flag&empty=&=&equals=a=b=c&&");

        query.Count.ShouldBe(4);
        query.GetStrings("flag").ShouldBe(new[] { string.Empty });
        query.GetStrings("empty").ShouldBe(new[] { string.Empty });
        query.GetString(string.Empty).ShouldBe(string.Empty);
        query.GetString("equals").ShouldBe("a=b=c");
        query.TryGetString("flag", out var value).ShouldBeTrue();
        value.ShouldBe(string.Empty);
    }

    [Fact]
    public void Parse_EncodedNamesAndValues_DecodesAfterSplittingAndPreservesRawText()
    {
        const string rawQuery = "a%26b=one%3Dtwo%26three%23four%3Ffive&%78=1&x=2&?=question";
        var query = RouteQuery.Parse(rawQuery);

        query.RawText.ShouldBe(rawQuery);
        query.Count.ShouldBe(3);
        query.GetString("a&b").ShouldBe("one=two&three#four?five");
        query.GetStrings("x").ShouldBe(new[] { "1", "2" });
        query.GetString("?").ShouldBe("question");
    }

    [Theory]
    [InlineData("x=hello+world", "hello world")]
    [InlineData("x=hello%20world", "hello world")]
    [InlineData("x=%2b+%2B", "+ +")]
    [InlineData("x=%252B", "%2B")]
    [InlineData("x=%C3%A9", "é")]
    [InlineData("x=é%E2%98%95%F0%9F%98%80", "é☕😀")]
    [InlineData("x=%00%EF%BB%BF", "\0\uFEFF")]
    [InlineData("x=%", "%")]
    [InlineData("x=%2", "%2")]
    [InlineData("x=%GG%2Z", "%GG%2Z")]
    [InlineData("x=%E2%82", "\uFFFD")]
    [InlineData("x=%F0%9F%92", "\uFFFD")]
    [InlineData("x=%C0%AF", "\uFFFD\uFFFD")]
    [InlineData("x=%E2%28%A1", "\uFFFD(\uFFFD")]
    [InlineData("x=%ED%A0%80", "\uFFFD\uFFFD\uFFFD")]
    public void Parse_FormEncoding_UsesUnicodeReplacementFallbackAndLiteralMalformedEscapes(string rawQuery, string expected)
    {
        RouteQuery.Parse(rawQuery).GetString("x").ShouldBe(expected);
    }

    [Fact]
    public void Parse_LongUnicodeValues_DecodesBeyondTheStackBuffer()
    {
        var value = new string('é', 300) + " ☕😀";
        var query = RouteQuery.Empty.With("value", value);

        RouteQuery.Parse(query.RawText).GetString("value").ShouldBe(value);
    }

    [Fact]
    public void Accessors_OrdinalNames_DistinguishCaseAndUnicodeSpelling()
    {
        var query = RouteQuery.Parse("I=upper&i=lower&é=composed&e%CC%81=decomposed");

        query.Count.ShouldBe(4);
        query.GetString("I").ShouldBe("upper");
        query.GetString("i").ShouldBe("lower");
        query.GetString("é").ShouldBe("composed");
        query.GetString("e\u0301").ShouldBe("decomposed");
        query.TryGetString("missing", out var value).ShouldBeFalse();
        value.ShouldBe(string.Empty);
        query.GetStrings("missing").ShouldBeEmpty();
        Should.Throw<KeyNotFoundException>(() => query.GetString("missing"));
    }

    [Fact]
    public void Accessors_RepeatedReads_ReturnCachedReadOnlyCollections()
    {
        var query = RouteQuery.Parse("x=1&x=2");
        var names = query.Names;
        var values = query.GetStrings("x");

        query.Names.ShouldBeSameAs(names);
        query.GetStrings("x").ShouldBeSameAs(values);
        query.GetStrings("missing").ShouldBeSameAs(query.GetStrings("missing"));
        (names is string[]).ShouldBeFalse();
        (values is string[]).ShouldBeFalse();
        (query.GetStrings("missing") is string[]).ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => ((IList<string>)names)[0] = "changed");
        Should.Throw<NotSupportedException>(() => ((IList<string>)values)[0] = "changed");
        query.GetString("x").ShouldBe("1");
    }

    [Fact]
    public void Accessors_RepeatedReads_AllocateNoMemory()
    {
        var query = RouteQuery.Parse("x=1&x=2");
        query.GetString("x");
        query.GetStrings("x");
        query.TryGetString("x", out _);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            _ = query.Count;
            _ = query.Names;
            _ = query.GetString("x");
            _ = query.GetStrings("x");
            _ = query.GetStrings("missing");
            _ = query.TryGetString("x", out _);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allocated.ShouldBe(0);
    }

    [Fact]
    public void With_ReplacesAtFirstPosition_AndCanonicalizesWithoutMutatingSource()
    {
        var original = RouteQuery.Parse("a=%20&x=1&b=2&x=3&flag");

        var changed = original.With("x", "new value");

        original.RawText.ShouldBe("a=%20&x=1&b=2&x=3&flag");
        original.GetStrings("x").ShouldBe(new[] { "1", "3" });
        changed.RawText.ShouldBe("a=+&x=new+value&b=2&flag=");
        changed.GetStrings("x").ShouldBe(new[] { "new value" });
        changed.ShouldBe(RouteQuery.Parse(changed.RawText));
    }

    [Fact]
    public void WithMany_ReplacesValuesTogetherAtFirstPosition_AndCopiesCallerArray()
    {
        var original = RouteQuery.Parse("a=0&x=1&b=2&x=3&c=4");
        var replacement = new[] { "five", string.Empty, "six" };

        var changed = original.WithMany("x", replacement);
        replacement[0] = "caller mutation";

        changed.RawText.ShouldBe("a=0&x=five&x=&x=six&b=2&c=4");
        changed.GetStrings("x").ShouldBe(new[] { "five", string.Empty, "six" });
        original.GetStrings("x").ShouldBe(new[] { "1", "3" });
        changed.ShouldBe(RouteQuery.Parse(changed.RawText));
    }

    [Fact]
    public void WithMany_EmptyInput_RemovesEveryPairOfTheName()
    {
        var original = RouteQuery.Parse("x=1&a=2&x=3");

        var changed = original.WithMany("x");

        changed.RawText.ShouldBe("a=2");
        changed.Names.ShouldBe(new[] { "a" });
        changed.GetStrings("x").ShouldBeEmpty();
        changed.WithMany("a").ShouldBe(RouteQuery.Empty);
        original.GetStrings("x").ShouldBe(new[] { "1", "3" });
    }

    [Fact]
    public void WithMany_NewNames_AppendsInBuilderOrderAndAllowsAnEmptyName()
    {
        var query = RouteQuery.Empty.With("x", "1").WithMany(string.Empty, "2", "3").With("y", "4");

        query.RawText.ShouldBe("x=1&=2&=3&y=4");
        query.Names.ShouldBe(new[] { "x", string.Empty, "y" });
        query.ShouldBe(RouteQuery.Parse(query.RawText));
    }

    [Fact]
    public void With_UnicodeAndReservedCharacters_UsesDeterministicFormEncoding()
    {
        var query = RouteQuery.Empty.With("a b+&=#?", "AZaz09*-._ ~+é☕😀");

        query.RawText.ShouldBe("a+b%2B%26%3D%23%3F=AZaz09*-._+%7E%2B%C3%A9%E2%98%95%F0%9F%98%80");
        RouteQuery.Parse(query.RawText).ShouldBe(query);
    }

    [Fact]
    public void With_LoneManagedSurrogates_NormalizesReplacementCharactersForSymmetricEncoding()
    {
        var query = RouteQuery.Empty.With("\uD800", "before\uDC00after");

        query.GetString("\uFFFD").ShouldBe("before\uFFFDafter");
        query.RawText.ShouldBe("%EF%BF%BD=before%EF%BF%BDafter");
        RouteQuery.Parse(query.RawText).ShouldBe(query);
        RouteQuery.Parse("x=\uD800").GetString("x").ShouldBe("\uFFFD");
    }

    [Fact]
    public void Equality_EquivalentDecodedPairs_IgnoresRawSpellingAndMatchesHashCodes()
    {
        var left = RouteQuery.Parse("x=hello+world&%79=%2b&flag");
        var right = RouteQuery.Parse("%78=hello%20world&y=%2B&flag=");

        left.ShouldBe(right);
        left.Equals((object)right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.RawText.ShouldNotBe(right.RawText);
        left.Equals(null).ShouldBeFalse();
        left.Equals("x=hello world&y=+&flag=").ShouldBeFalse();
    }

    [Theory]
    [InlineData("x=1&y=2", "y=2&x=1")]
    [InlineData("x=1&x=2", "x=2&x=1")]
    [InlineData("x=1&y=2&x=3", "x=1&x=3&y=2")]
    [InlineData("x=1", "X=1")]
    [InlineData("x=one", "x=One")]
    [InlineData("x=1", "x=1&x=1")]
    [InlineData("x=", "")]
    public void Equality_DifferentDecodedPairSequences_AreDifferentValues(string first, string second)
    {
        var left = RouteQuery.Parse(first);
        var right = RouteQuery.Parse(second);

        left.Equals(right).ShouldBeFalse();
        (left == right).ShouldBeFalse();
        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void Parse_LiteralFragmentDelimiter_RejectsAnAmbiguousRawQuery()
    {
        Should.Throw<ArgumentException>(() => RouteQuery.Parse("x=1#fragment")).ParamName.ShouldBe("rawQuery");
        RouteQuery.Parse("x=%23fragment").GetString("x").ShouldBe("#fragment");
    }

    [Fact]
    public void PublicOperations_NullArguments_ThrowWithoutChangingTheSource()
    {
        var query = RouteQuery.Parse("x=1");

        Should.Throw<ArgumentNullException>(() => RouteQuery.Parse(null!));
        Should.Throw<ArgumentNullException>(() => query.GetString(null!));
        Should.Throw<ArgumentNullException>(() => query.TryGetString(null!, out _));
        Should.Throw<ArgumentNullException>(() => query.GetStrings(null!));
        Should.Throw<ArgumentNullException>(() => query.With(null!, "value"));
        Should.Throw<ArgumentNullException>(() => query.With("name", null!));
        Should.Throw<ArgumentNullException>(() => query.WithMany(null!, "value"));
        Should.Throw<ArgumentNullException>(() => query.WithMany("name", null!));
        Should.Throw<ArgumentNullException>(() => query.WithMany("name", "value", null!));
        query.RawText.ShouldBe("x=1");
    }
}
