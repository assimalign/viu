using Shouldly;

using Xunit;

namespace Assimalign.Viu.LanguageServer.Tests;

/// <summary>
/// Pins the snippet-to-plaintext rendering the server sends a client that advertised no snippet
/// support. Such a client inserts the text verbatim, so anything left of the snippet grammar reaches
/// the author's buffer as literal characters ([V01.01.12.07.12]).
/// <see href="https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#snippet_syntax">
/// Language Server Protocol 3.17, snippet syntax</see>.
/// </summary>
public class LanguageServerSnippetTextTests
{
    [Theory]
    // A tabstop marks where a caret would go; without carets it contributes nothing.
    [InlineData("<template$1>$0</template>", "<template></template>")]
    [InlineData("<input $1/>", "<input />")]
    [InlineData(":class=\"$1\"", ":class=\"\"")]
    [InlineData("v-for=\"$1 in $2\"", "v-for=\" in \"")]
    // An escaped dollar sign is literal text the snippet grammar was hiding.
    [InlineData("\\$event => $1", "$event => ")]
    [InlineData("async \\$event => $1", "async $event => ")]
    // A placeholder's default is the text the author would have accepted anyway.
    [InlineData("${1:name}=\"${2}\"", "name=\"\"")]
    // A dollar sign the grammar does not continue is ordinary text.
    [InlineData("total $ due", "total $ due")]
    [InlineData("@script {\n\t$0\n}", "@script {\n\t\n}")]
    public void ToPlainText_SnippetInsertText_RendersTheTextTheAuthorReceives(
        string snippet,
        string expected)
        => LanguageServerSnippetText.ToPlainText(snippet).ShouldBe(expected);
}
