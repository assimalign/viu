namespace Assimalign.Viu.Syntax.Templates;

/// <summary>
/// The catalog of compiler error codes. The numeric values are a <b>frozen contract</b> — the
/// diagnostic surface ([V01.01.05.08]), build logs, and any <c>NoWarn</c>/editorconfig entry a consumer
/// writes all key on the number, so a code may be added but never renumbered or reused. The layout is
/// banded: <b>0–52</b> the parse and directive codes, <b>53</b> the reserved
/// <see cref="ExtendPoint"/> sentinel, <b>54–65</b> the DOM directive codes, <b>1000+</b> the
/// Viu-specific analysis codes. The bands and the sentinel are pinned by the catalog tests.
/// </summary>
/// <remarks>
/// The template parser ([V01.01.05.01]) emits only the tokenizer and directive-syntax codes (values
/// 0–27); the transform-stage codes are produced by later pipeline stages, which is why the enum
/// carries values the parser alone never reports. HTML parse errors follow the WHATWG catalog:
/// https://html.spec.whatwg.org/multipage/parsing.html#parse-errors.
/// </remarks>
public enum CompilerErrorCode
{
    // ---- WHATWG / HTML parse errors (emitted by the parser) ----

    /// <summary>An empty comment was closed abruptly, e.g. <c>&lt;!--&gt;</c>.</summary>
    AbruptClosingOfEmptyComment = 0,

    /// <summary>A <c>&lt;![CDATA[</c> section appeared in HTML content.</summary>
    CdataInHtmlContent = 1,

    /// <summary>An attribute name was repeated on one element.</summary>
    DuplicateAttribute = 2,

    /// <summary>An end tag carried attributes.</summary>
    EndTagWithAttributes = 3,

    /// <summary>An end tag carried a trailing <c>/</c>.</summary>
    EndTagWithTrailingSolidus = 4,

    /// <summary>The input ended where a tag name was expected.</summary>
    EofBeforeTagName = 5,

    /// <summary>The input ended inside a CDATA section.</summary>
    EofInCdata = 6,

    /// <summary>The input ended inside a comment.</summary>
    EofInComment = 7,

    /// <summary>The input ended inside script comment-like text.</summary>
    EofInScriptHtmlCommentLikeText = 8,

    /// <summary>The input ended inside a tag.</summary>
    EofInTag = 9,

    /// <summary>A comment was closed incorrectly.</summary>
    IncorrectlyClosedComment = 10,

    /// <summary>A comment was opened incorrectly, e.g. <c>&lt;!x</c>.</summary>
    IncorrectlyOpenedComment = 11,

    /// <summary>A tag name started with an invalid character.</summary>
    InvalidFirstCharacterOfTagName = 12,

    /// <summary>An attribute value was expected but missing.</summary>
    MissingAttributeValue = 13,

    /// <summary>An end tag name was expected, e.g. <c>&lt;/&gt;</c>.</summary>
    MissingEndTagName = 14,

    /// <summary>Whitespace between attributes was expected.</summary>
    MissingWhitespaceBetweenAttributes = 15,

    /// <summary>A <c>&lt;!--</c> appeared inside a comment.</summary>
    NestedComment = 16,

    /// <summary>An attribute name contained <c>"</c>, <c>'</c>, or <c>&lt;</c>.</summary>
    UnexpectedCharacterInAttributeName = 17,

    /// <summary>An unquoted attribute value contained a forbidden character.</summary>
    UnexpectedCharacterInUnquotedAttributeValue = 18,

    /// <summary>An attribute name started with <c>=</c>.</summary>
    UnexpectedEqualsSignBeforeAttributeName = 19,

    /// <summary>An unexpected U+0000 NULL character appeared.</summary>
    UnexpectedNullCharacter = 20,

    /// <summary>A <c>&lt;?</c> appeared in HTML content.</summary>
    UnexpectedQuestionMarkInsteadOfTagName = 21,

    /// <summary>An unexpected <c>/</c> appeared in a tag.</summary>
    UnexpectedSolidusInTag = 22,

    // ---- Template-syntax parse errors, beyond the WHATWG set (emitted by the parser) ----

    /// <summary>An end tag matched no open element.</summary>
    XInvalidEndTag = 23,

    /// <summary>An element was missing its end tag.</summary>
    XMissingEndTag = 24,

    /// <summary>An interpolation was not closed.</summary>
    XMissingInterpolationEnd = 25,

    /// <summary>A directive shorthand had no name.</summary>
    XMissingDirectiveName = 26,

    /// <summary>A dynamic directive argument was not closed with <c>]</c>.</summary>
    XMissingDynamicDirectiveArgumentEnd = 27,

    // ---- Transform errors (later pipeline stages) ----

    /// <summary><c>v-if</c>/<c>v-else-if</c> is missing its expression.</summary>
    XVIfNoExpression = 28,

    /// <summary>Two branches of one <c>v-if</c> chain used the same key.</summary>
    XVIfSameKey = 29,

    /// <summary><c>v-else</c>/<c>v-else-if</c> has no adjacent <c>v-if</c> or <c>v-else-if</c>.</summary>
    XVElseNoAdjacentIf = 30,

    /// <summary><c>v-for</c> is missing its expression.</summary>
    XVForNoExpression = 31,

    /// <summary><c>v-for</c>'s expression did not match the <c>alias in source</c> form.</summary>
    XVForMalformedExpression = 32,

    /// <summary>A <c>&lt;template v-for&gt;</c> put its key on a child instead of the <c>&lt;template&gt;</c> tag.</summary>
    XVForTemplateKeyPlacement = 33,

    /// <summary><c>v-bind</c> is missing its expression.</summary>
    XVBindNoExpression = 34,

    /// <summary><c>v-on</c> is missing its expression and has no modifier to imply one.</summary>
    XVOnNoExpression = 35,

    /// <summary>A custom directive was placed on a <c>&lt;slot&gt;</c> outlet, which renders no element to apply it to.</summary>
    XVSlotUnexpectedDirectiveOnSlotOutlet = 36,

    /// <summary>A component mixed an implicit default slot with a named <c>v-slot</c> template.</summary>
    XVSlotMixedSlotUsage = 37,

    /// <summary>Two slot templates declared the same slot name.</summary>
    XVSlotDuplicateSlotNames = 38,

    /// <summary>A component with an explicit default-slot template also had loose children.</summary>
    XVSlotExtraneousDefaultSlotChildren = 39,

    /// <summary><c>v-slot</c> was used somewhere other than a component or a <c>&lt;template&gt;</c> tag.</summary>
    XVSlotMisplaced = 40,

    /// <summary><c>v-model</c> is missing its expression.</summary>
    XVModelNoExpression = 41,

    /// <summary><c>v-model</c>'s expression is not an assignable member expression.</summary>
    XVModelMalformedExpression = 42,

    /// <summary><c>v-model</c> targeted a template-local scope variable, which cannot be written back.</summary>
    XVModelOnScopeVariable = 43,

    /// <summary><c>v-model</c> targeted a prop, which the child may not assign.</summary>
    XVModelOnProps = 44,

    /// <summary>An expression body failed to parse.</summary>
    XInvalidExpression = 45,

    /// <summary><c>&lt;KeepAlive&gt;</c> was given other than exactly one child component.</summary>
    XKeepAliveInvalidChildren = 46,

    // ---- Generic errors (later pipeline stages) ----

    /// <summary>Identifier prefixing was requested in a build that does not support it.</summary>
    XPrefixIdNotSupported = 47,

    /// <summary>ES module output mode was requested in a build that does not support it.</summary>
    XModuleModeNotSupported = 48,

    /// <summary>Handler caching was requested without the identifier prefixing it depends on.</summary>
    XCacheHandlerNotSupported = 49,

    /// <summary>A scope id was supplied outside module mode, where it cannot be applied.</summary>
    XScopeIdNotSupported = 50,

    /// <summary>An <c>@vnode-*</c> lifecycle hook was used; the <c>vue:</c> prefix replaces it.</summary>
    XVnodeHooks = 51,

    /// <summary>A same-name <c>v-bind</c> shorthand was used with an argument that is not a plain static identifier.</summary>
    XVBindInvalidSameNameArgument = 52,

    /// <summary>
    /// The reserved value one past the last defined <b>core</b> code. It is a sentinel, never reported:
    /// it exists so the DOM band below can start immediately after the core band without a later core
    /// addition colliding with it. Its value 53 is pinned by the catalog test and must not change.
    /// </summary>
    ExtendPoint = 53,

    // ---- DOM directive transform errors ([V01.01.05.03]) ----
    //
    // Viu keeps the core and DOM diagnostics in ONE enum, because Assimalign.Viu.Syntax.Templates is a
    // single project covering both the platform-neutral template language and its DOM directive set;
    // a second enum would force every diagnostic consumer to switch on two catalog types.
    // Consequence: the DOM codes start at 54, one past the preserved ExtendPoint sentinel, rather than
    // reusing 53 — a single C# enum cannot give two members the same value, and ExtendPoint's 53 is
    // pinned. Both the sentinel and the DOM band's start are pinned by the catalog tests; neither may
    // be renumbered.

    /// <summary><c>v-html</c> is missing its expression.</summary>
    XVHtmlNoExpression = 54,

    /// <summary><c>v-html</c> will override the element's children.</summary>
    XVHtmlWithChildren = 55,

    /// <summary><c>v-text</c> is missing its expression.</summary>
    XVTextNoExpression = 56,

    /// <summary><c>v-text</c> will override the element's children.</summary>
    XVTextWithChildren = 57,

    /// <summary><c>v-model</c> used on an unsupported element.</summary>
    XVModelOnInvalidElement = 58,

    /// <summary><c>v-model</c> argument used on a plain element.</summary>
    XVModelArgumentOnElement = 59,

    /// <summary><c>v-model</c> used on a file input.</summary>
    XVModelOnFileInputElement = 60,

    /// <summary>Unnecessary <c>value</c> binding alongside <c>v-model</c>.</summary>
    XVModelUnnecessaryValue = 61,

    /// <summary><c>v-show</c> is missing its expression.</summary>
    XVShowNoExpression = 62,

    /// <summary><c>&lt;Transition&gt;</c> expects exactly one child.</summary>
    XTransitionInvalidChildren = 63,

    /// <summary>A side-effect tag (<c>&lt;script&gt;</c>/<c>&lt;style&gt;</c>) was ignored.</summary>
    XIgnoredSideEffectTag = 64,

    /// <summary>The reserved value one past the last defined DOM code.</summary>
    DomExtendPoint = 65,

    // ---- Expression/scope analysis codes ([V01.01.05.04]) ----
    //
    // These exist because there is no runtime proxy ([RCT-8]): a template identifier that resolves to
    // nothing real cannot fall back to a dynamic member lookup, so the compiler must surface it. They
    // start a reserved 1000+ band rather than continuing at 66, leaving the slots immediately after
    // DomExtendPoint free for a future stage-specific band (the server renderer, [V01.01.07]) to claim
    // contiguously.

    /// <summary>
    /// A template identifier resolved to neither a template-local, an allowed global, nor a known component
    /// binding, under strict binding metadata (<see cref="BindingMetadata.ReportsUnresolvedIdentifiers"/>).
    /// </summary>
    XViuUnresolvedIdentifier = 1000,

    /// <summary>
    /// A template expression accessed a member that does not exist on a CSS Modules accessor
    /// ([V01.01.05.04.01]) whose full class map the generator supplied
    /// (<see cref="CssModuleAccessors.ReportsUnknownMembers"/>). The accessor is a compile-time class
    /// whose members are exactly the declared classes, so an unknown member is decidably wrong and is
    /// reported on the exact template coordinate rather than failing at runtime (<c>[STY-4]</c>).
    /// </summary>
    XViuUnknownCssModuleMember = 1001,
}
