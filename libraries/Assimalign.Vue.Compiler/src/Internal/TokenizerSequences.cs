namespace Assimalign.Vue.Compiler;

/// <summary>
/// The literal character sequences the tokenizer matches to delimit CDATA, comments, and raw-text /
/// RCDATA elements. The C# port of Vue 3.5's <c>Sequences</c> table (<c>@vue/compiler-core</c>
/// <c>tokenizer.ts</c>). Instances are compared by reference, so both the tokenizer and the parser use
/// these singletons.
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
