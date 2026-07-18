using System;

using Shouldly;

using Xunit;

namespace Assimalign.Vue.Compiler;

// Direct unit tests over the embedded table and decoder (internal, via InternalsVisibleTo). The decoder
// mirrors the behaviour Vue 3.5 gets from the `entities` package's decodeHTML / DecodingMode.
public class EntityDecoderTests
{
    [Fact]
    public void Table_HasFullWhatwgEntryCount()
    {
        // The WHATWG list has 2231 named references (with and without the trailing ';').
        HtmlNamedCharacterReferences.Count.ShouldBe(2231);
        HtmlNamedCharacterReferences.Table.Count.ShouldBe(2231);
    }

    [Theory]
    [InlineData("&amp;", "&")]
    [InlineData("&lt;", "<")]
    [InlineData("&copy;", "©")]
    [InlineData("&notin;", "∉")]
    public void Decode_NamedReference_ReturnsMappedCharacters(string input, string expected)
        => HtmlEntityDecoder.Decode(input, false).ShouldBe(expected);

    [Fact]
    public void Decode_LongestMatchWins_ForNotItSequence()
    {
        // "&notit;" is not an entity, but "&not" (¬) is a legacy entity: longest match consumes only "&not".
        HtmlEntityDecoder.Decode("&notit;", false).ShouldBe("¬it;");
    }

    [Fact]
    public void Decode_LegacyNoSemicolon_DecodesInTextButNotAmbiguousAttribute()
    {
        HtmlEntityDecoder.Decode("&amp", false).ShouldBe("&");
        HtmlEntityDecoder.Decode("&amp;", true).ShouldBe("&");
        // In an attribute, "&amp" followed by '=' is the ambiguous-ampersand case: left literal.
        HtmlEntityDecoder.Decode("&amp=1", true).ShouldBe("&amp=1");
    }

    [Fact]
    public void Decode_NumericReference_HandlesDecimalHexAndAstralPlane()
    {
        HtmlEntityDecoder.Decode("&#65;", false).ShouldBe("A");
        HtmlEntityDecoder.Decode("&#x41;", false).ShouldBe("A");
        HtmlEntityDecoder.Decode("&#X41;", false).ShouldBe("A");
        HtmlEntityDecoder.Decode("&#128512;", false).ShouldBe(char.ConvertFromUtf32(0x1F600));
        HtmlEntityDecoder.Decode("&#x1F600;", false).ShouldBe(char.ConvertFromUtf32(0x1F600));
    }

    [Fact]
    public void Decode_NumericReference_AppliesWhatwgSanitization()
    {
        // NULL and surrogate code points become U+FFFD; the C1 range is Windows-1252 remapped.
        HtmlEntityDecoder.Decode("&#0;", false).ShouldBe("�");
        HtmlEntityDecoder.Decode("&#xD800;", false).ShouldBe("�");
        HtmlEntityDecoder.Decode("&#128;", false).ShouldBe("€"); // 0x80 -> euro sign
    }

    [Fact]
    public void Decode_UnknownReference_IsLeftLiteral()
    {
        HtmlEntityDecoder.Decode("&foo;", false).ShouldBe("&foo;");
        HtmlEntityDecoder.Decode("plain text", false).ShouldBe("plain text");
    }

    [Fact]
    public void Decode_MultiCodePointReference_ReturnsBothCharacters()
    {
        HtmlEntityDecoder.Decode("&NotEqualTilde;", false).ShouldBe("≂̸");
    }
}
