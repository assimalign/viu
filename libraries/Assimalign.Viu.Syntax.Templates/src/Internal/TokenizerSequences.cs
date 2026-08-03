namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The literal character sequences the tokenizer matches to delimit CDATA, comments, and raw-text /
/// RCDATA elements. Instances are compared by reference, so both the tokenizer and the parser must use
/// these singletons — a freshly constructed equal sequence would not match.
/// </summary>
internal static class TokenizerSequences
{
    /// <summary>The <c>CDATA[</c> that follows <c>&lt;![</c>.</summary>
    public static readonly char[] Cdata = "CDATA[".ToCharArray();

    /// <summary>The <c>]]&gt;</c> that ends a CDATA section.</summary>
    public static readonly char[] CdataEnd = "]]>".ToCharArray();

    /// <summary>The <c>--&gt;</c> that ends a comment.</summary>
    public static readonly char[] CommentEnd = "-->".ToCharArray();

    /// <summary>The <c>&lt;/script</c> that ends raw-text script content.</summary>
    public static readonly char[] ScriptEnd = "</script".ToCharArray();

    /// <summary>The <c>&lt;/style</c> that ends raw-text style content.</summary>
    public static readonly char[] StyleEnd = "</style".ToCharArray();

    /// <summary>The <c>&lt;/title</c> that ends RCDATA title content.</summary>
    public static readonly char[] TitleEnd = "</title".ToCharArray();

    /// <summary>The <c>&lt;/textarea</c> that ends RCDATA textarea content.</summary>
    public static readonly char[] TextareaEnd = "</textarea".ToCharArray();
}
