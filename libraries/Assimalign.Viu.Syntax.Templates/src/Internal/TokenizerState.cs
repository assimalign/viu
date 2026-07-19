namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The states of the WHATWG-derived tokenizer state machine. The C# port of Vue 3.5's <c>State</c>
/// enum (<c>@vue/compiler-core</c> <c>tokenizer.ts</c>), minus the <c>InEntity</c> state: this port
/// decodes character references when the parser materialises node content (see
/// <see cref="HtmlEntityDecoder"/>) rather than incrementally inside the tokenizer.
/// </summary>
internal enum TokenizerState
{
    /// <summary>Character data between tags.</summary>
    Text = 1,

    /// <summary>Matching the interpolation open delimiter (<c>{{</c>).</summary>
    InterpolationOpen,

    /// <summary>Inside an interpolation body.</summary>
    Interpolation,

    /// <summary>Matching the interpolation close delimiter (<c>}}</c>).</summary>
    InterpolationClose,

    /// <summary>Just after <c>&lt;</c>.</summary>
    BeforeTagName,

    /// <summary>Inside an open tag name.</summary>
    InTagName,

    /// <summary>After <c>/</c> in a self-closing tag.</summary>
    InSelfClosingTag,

    /// <summary>Just after <c>&lt;/</c>.</summary>
    BeforeClosingTagName,

    /// <summary>Inside a close tag name.</summary>
    InClosingTagName,

    /// <summary>After a close tag name, skipping to <c>&gt;</c>.</summary>
    AfterClosingTagName,

    /// <summary>Before an attribute name.</summary>
    BeforeAttributeName,

    /// <summary>Inside an attribute name.</summary>
    InAttributeName,

    /// <summary>Inside a directive name (<c>v-...</c> or a shorthand).</summary>
    InDirectiveName,

    /// <summary>Inside a directive argument.</summary>
    InDirectiveArgument,

    /// <summary>Inside a dynamic directive argument (<c>[...]</c>).</summary>
    InDirectiveDynamicArgument,

    /// <summary>Inside a directive modifier.</summary>
    InDirectiveModifier,

    /// <summary>After an attribute name (expecting <c>=</c>, the next attribute, or tag end).</summary>
    AfterAttributeName,

    /// <summary>Before an attribute value.</summary>
    BeforeAttributeValue,

    /// <summary>Inside a double-quoted attribute value.</summary>
    InAttributeValueDoubleQuote,

    /// <summary>Inside a single-quoted attribute value.</summary>
    InAttributeValueSingleQuote,

    /// <summary>Inside an unquoted attribute value.</summary>
    InAttributeValueNoQuote,

    /// <summary>After <c>&lt;!</c>.</summary>
    BeforeDeclaration,

    /// <summary>Inside a declaration.</summary>
    InDeclaration,

    /// <summary>Inside a processing instruction (<c>&lt;?</c>).</summary>
    InProcessingInstruction,

    /// <summary>After <c>&lt;!-</c>.</summary>
    BeforeComment,

    /// <summary>Matching the <c>&lt;![CDATA[</c> sequence.</summary>
    CdataSequence,

    /// <summary>Inside a bogus/special comment.</summary>
    InSpecialComment,

    /// <summary>Inside a comment or CDATA body (scanning for the end sequence).</summary>
    InCommentLike,

    /// <summary>Deciding between <c>&lt;script</c> and <c>&lt;style</c>.</summary>
    BeforeSpecialS,

    /// <summary>Deciding between <c>&lt;title</c> and <c>&lt;textarea</c>.</summary>
    BeforeSpecialT,

    /// <summary>Matching a raw-text/RCDATA start sequence.</summary>
    SpecialStartSequence,

    /// <summary>Inside RCDATA/RAWTEXT content, scanning for the matching end tag.</summary>
    InRcdata,

    /// <summary>Inside a single-file-component root-level tag name.</summary>
    InSfcRootTagName,
}
