namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The catalog of compiler error codes. The C# port of Vue 3.5's <c>ErrorCodes</c> enum
/// (<c>@vue/compiler-core</c> <c>errors.ts</c>). The numeric values match upstream exactly so the
/// diagnostic surface ([V01.01.05.08]) and any higher-order compiler can share the codes.
/// </summary>
/// <remarks>
/// The template parser ([V01.01.05.01]) emits only the parse and Vue-specific parse codes (values
/// 0–27). The transform, generic, and same-name-argument codes are carried for numeric parity with
/// upstream and are produced by later pipeline stages. HTML parse errors follow the WHATWG catalog:
/// https://html.spec.whatwg.org/multipage/parsing.html#parse-errors.
/// </remarks>
public enum CompilerErrorCode
{
    // ---- WHATWG / HTML parse errors (emitted by the parser) ----

    /// <summary>An empty comment was closed abruptly, e.g. <c>&lt;!--&gt;</c> (upstream <c>ABRUPT_CLOSING_OF_EMPTY_COMMENT</c>).</summary>
    AbruptClosingOfEmptyComment = 0,

    /// <summary>A <c>&lt;![CDATA[</c> section appeared in HTML content (upstream <c>CDATA_IN_HTML_CONTENT</c>).</summary>
    CdataInHtmlContent = 1,

    /// <summary>An attribute name was repeated on one element (upstream <c>DUPLICATE_ATTRIBUTE</c>).</summary>
    DuplicateAttribute = 2,

    /// <summary>An end tag carried attributes (upstream <c>END_TAG_WITH_ATTRIBUTES</c>).</summary>
    EndTagWithAttributes = 3,

    /// <summary>An end tag carried a trailing <c>/</c> (upstream <c>END_TAG_WITH_TRAILING_SOLIDUS</c>).</summary>
    EndTagWithTrailingSolidus = 4,

    /// <summary>The input ended where a tag name was expected (upstream <c>EOF_BEFORE_TAG_NAME</c>).</summary>
    EofBeforeTagName = 5,

    /// <summary>The input ended inside a CDATA section (upstream <c>EOF_IN_CDATA</c>).</summary>
    EofInCdata = 6,

    /// <summary>The input ended inside a comment (upstream <c>EOF_IN_COMMENT</c>).</summary>
    EofInComment = 7,

    /// <summary>The input ended inside script comment-like text (upstream <c>EOF_IN_SCRIPT_HTML_COMMENT_LIKE_TEXT</c>).</summary>
    EofInScriptHtmlCommentLikeText = 8,

    /// <summary>The input ended inside a tag (upstream <c>EOF_IN_TAG</c>).</summary>
    EofInTag = 9,

    /// <summary>A comment was closed incorrectly (upstream <c>INCORRECTLY_CLOSED_COMMENT</c>).</summary>
    IncorrectlyClosedComment = 10,

    /// <summary>A comment was opened incorrectly, e.g. <c>&lt;!x</c> (upstream <c>INCORRECTLY_OPENED_COMMENT</c>).</summary>
    IncorrectlyOpenedComment = 11,

    /// <summary>A tag name started with an invalid character (upstream <c>INVALID_FIRST_CHARACTER_OF_TAG_NAME</c>).</summary>
    InvalidFirstCharacterOfTagName = 12,

    /// <summary>An attribute value was expected but missing (upstream <c>MISSING_ATTRIBUTE_VALUE</c>).</summary>
    MissingAttributeValue = 13,

    /// <summary>An end tag name was expected, e.g. <c>&lt;/&gt;</c> (upstream <c>MISSING_END_TAG_NAME</c>).</summary>
    MissingEndTagName = 14,

    /// <summary>Whitespace between attributes was expected (upstream <c>MISSING_WHITESPACE_BETWEEN_ATTRIBUTES</c>).</summary>
    MissingWhitespaceBetweenAttributes = 15,

    /// <summary>A <c>&lt;!--</c> appeared inside a comment (upstream <c>NESTED_COMMENT</c>).</summary>
    NestedComment = 16,

    /// <summary>An attribute name contained <c>"</c>, <c>'</c>, or <c>&lt;</c> (upstream <c>UNEXPECTED_CHARACTER_IN_ATTRIBUTE_NAME</c>).</summary>
    UnexpectedCharacterInAttributeName = 17,

    /// <summary>An unquoted attribute value contained a forbidden character (upstream <c>UNEXPECTED_CHARACTER_IN_UNQUOTED_ATTRIBUTE_VALUE</c>).</summary>
    UnexpectedCharacterInUnquotedAttributeValue = 18,

    /// <summary>An attribute name started with <c>=</c> (upstream <c>UNEXPECTED_EQUALS_SIGN_BEFORE_ATTRIBUTE_NAME</c>).</summary>
    UnexpectedEqualsSignBeforeAttributeName = 19,

    /// <summary>An unexpected U+0000 NULL character appeared (upstream <c>UNEXPECTED_NULL_CHARACTER</c>).</summary>
    UnexpectedNullCharacter = 20,

    /// <summary>A <c>&lt;?</c> appeared in HTML content (upstream <c>UNEXPECTED_QUESTION_MARK_INSTEAD_OF_TAG_NAME</c>).</summary>
    UnexpectedQuestionMarkInsteadOfTagName = 21,

    /// <summary>An unexpected <c>/</c> appeared in a tag (upstream <c>UNEXPECTED_SOLIDUS_IN_TAG</c>).</summary>
    UnexpectedSolidusInTag = 22,

    // ---- Vue-specific parse errors (emitted by the parser) ----

    /// <summary>An end tag matched no open element (upstream <c>X_INVALID_END_TAG</c>).</summary>
    XInvalidEndTag = 23,

    /// <summary>An element was missing its end tag (upstream <c>X_MISSING_END_TAG</c>).</summary>
    XMissingEndTag = 24,

    /// <summary>An interpolation was not closed (upstream <c>X_MISSING_INTERPOLATION_END</c>).</summary>
    XMissingInterpolationEnd = 25,

    /// <summary>A directive shorthand had no name (upstream <c>X_MISSING_DIRECTIVE_NAME</c>).</summary>
    XMissingDirectiveName = 26,

    /// <summary>A dynamic directive argument was not closed with <c>]</c> (upstream <c>X_MISSING_DYNAMIC_DIRECTIVE_ARGUMENT_END</c>).</summary>
    XMissingDynamicDirectiveArgumentEnd = 27,

    // ---- Transform errors (later pipeline stages) ----

    /// <summary>Upstream <c>X_V_IF_NO_EXPRESSION</c>.</summary>
    XVIfNoExpression = 28,

    /// <summary>Upstream <c>X_V_IF_SAME_KEY</c>.</summary>
    XVIfSameKey = 29,

    /// <summary>Upstream <c>X_V_ELSE_NO_ADJACENT_IF</c>.</summary>
    XVElseNoAdjacentIf = 30,

    /// <summary>Upstream <c>X_V_FOR_NO_EXPRESSION</c>.</summary>
    XVForNoExpression = 31,

    /// <summary>Upstream <c>X_V_FOR_MALFORMED_EXPRESSION</c>.</summary>
    XVForMalformedExpression = 32,

    /// <summary>Upstream <c>X_V_FOR_TEMPLATE_KEY_PLACEMENT</c>.</summary>
    XVForTemplateKeyPlacement = 33,

    /// <summary>Upstream <c>X_V_BIND_NO_EXPRESSION</c>.</summary>
    XVBindNoExpression = 34,

    /// <summary>Upstream <c>X_V_ON_NO_EXPRESSION</c>.</summary>
    XVOnNoExpression = 35,

    /// <summary>Upstream <c>X_V_SLOT_UNEXPECTED_DIRECTIVE_ON_SLOT_OUTLET</c>.</summary>
    XVSlotUnexpectedDirectiveOnSlotOutlet = 36,

    /// <summary>Upstream <c>X_V_SLOT_MIXED_SLOT_USAGE</c>.</summary>
    XVSlotMixedSlotUsage = 37,

    /// <summary>Upstream <c>X_V_SLOT_DUPLICATE_SLOT_NAMES</c>.</summary>
    XVSlotDuplicateSlotNames = 38,

    /// <summary>Upstream <c>X_V_SLOT_EXTRANEOUS_DEFAULT_SLOT_CHILDREN</c>.</summary>
    XVSlotExtraneousDefaultSlotChildren = 39,

    /// <summary>Upstream <c>X_V_SLOT_MISPLACED</c>.</summary>
    XVSlotMisplaced = 40,

    /// <summary>Upstream <c>X_V_MODEL_NO_EXPRESSION</c>.</summary>
    XVModelNoExpression = 41,

    /// <summary>Upstream <c>X_V_MODEL_MALFORMED_EXPRESSION</c>.</summary>
    XVModelMalformedExpression = 42,

    /// <summary>Upstream <c>X_V_MODEL_ON_SCOPE_VARIABLE</c>.</summary>
    XVModelOnScopeVariable = 43,

    /// <summary>Upstream <c>X_V_MODEL_ON_PROPS</c>.</summary>
    XVModelOnProps = 44,

    /// <summary>Upstream <c>X_INVALID_EXPRESSION</c>.</summary>
    XInvalidExpression = 45,

    /// <summary>Upstream <c>X_KEEP_ALIVE_INVALID_CHILDREN</c>.</summary>
    XKeepAliveInvalidChildren = 46,

    // ---- Generic errors (later pipeline stages) ----

    /// <summary>Upstream <c>X_PREFIX_ID_NOT_SUPPORTED</c>.</summary>
    XPrefixIdNotSupported = 47,

    /// <summary>Upstream <c>X_MODULE_MODE_NOT_SUPPORTED</c>.</summary>
    XModuleModeNotSupported = 48,

    /// <summary>Upstream <c>X_CACHE_HANDLER_NOT_SUPPORTED</c>.</summary>
    XCacheHandlerNotSupported = 49,

    /// <summary>Upstream <c>X_SCOPE_ID_NOT_SUPPORTED</c>.</summary>
    XScopeIdNotSupported = 50,

    /// <summary>Upstream <c>X_VNODE_HOOKS</c>.</summary>
    XVnodeHooks = 51,

    /// <summary>Upstream <c>X_V_BIND_INVALID_SAME_NAME_ARGUMENT</c>.</summary>
    XVBindInvalidSameNameArgument = 52,

    /// <summary>
    /// The reserved value one past the last defined <b>core</b> code, matching upstream
    /// <c>@vue/compiler-core</c>'s <c>__EXTEND_POINT__</c>. The DOM diagnostics below extend the catalog from
    /// here, exactly as <c>@vue/compiler-dom</c>'s <c>DOMErrorCodes</c> enum extends core's error codes.
    /// </summary>
    ExtendPoint = 53,

    // ---- DOM directive transform errors ([V01.01.05.03], the C# port of @vue/compiler-dom's DOMErrorCodes) ----
    //
    // DESIGN DECISION (flagged): Vuecs merges @vue/compiler-core and @vue/compiler-dom into a single
    // Assimalign.Vue.Syntax.Templates project, so the DOM codes live in THIS one enum rather than a separate one.
    // Upstream's DOMErrorCodes begins at core's __EXTEND_POINT__ value (53) — its first real code reuses the
    // sentinel's number because the two enums are distinct types. A single C# enum cannot give ExtendPoint the
    // value 53 (pinned by ErrorCatalog_NumericValues_MatchUpstreamErrorCodes, which must not change) AND give
    // X_V_HTML_NO_EXPRESSION the same 53. The DOM codes are therefore appended after the preserved sentinel
    // (starting at 54), so each equals its upstream DOMErrorCodes value + 1. Core codes 0..53 remain numeric-
    // exact with upstream; the +1 DOM offset is pinned by DomErrorCatalog_NumericValues below.

    /// <summary><c>v-html</c> is missing its expression (upstream DOM <c>X_V_HTML_NO_EXPRESSION</c>).</summary>
    XVHtmlNoExpression = 54,

    /// <summary><c>v-html</c> will override the element's children (upstream DOM <c>X_V_HTML_WITH_CHILDREN</c>).</summary>
    XVHtmlWithChildren = 55,

    /// <summary><c>v-text</c> is missing its expression (upstream DOM <c>X_V_TEXT_NO_EXPRESSION</c>).</summary>
    XVTextNoExpression = 56,

    /// <summary><c>v-text</c> will override the element's children (upstream DOM <c>X_V_TEXT_WITH_CHILDREN</c>).</summary>
    XVTextWithChildren = 57,

    /// <summary><c>v-model</c> used on an unsupported element (upstream DOM <c>X_V_MODEL_ON_INVALID_ELEMENT</c>).</summary>
    XVModelOnInvalidElement = 58,

    /// <summary><c>v-model</c> argument used on a plain element (upstream DOM <c>X_V_MODEL_ARG_ON_ELEMENT</c>).</summary>
    XVModelArgumentOnElement = 59,

    /// <summary><c>v-model</c> used on a file input (upstream DOM <c>X_V_MODEL_ON_FILE_INPUT_ELEMENT</c>).</summary>
    XVModelOnFileInputElement = 60,

    /// <summary>Unnecessary <c>value</c> binding alongside <c>v-model</c> (upstream DOM <c>X_V_MODEL_UNNECESSARY_VALUE</c>).</summary>
    XVModelUnnecessaryValue = 61,

    /// <summary><c>v-show</c> is missing its expression (upstream DOM <c>X_V_SHOW_NO_EXPRESSION</c>).</summary>
    XVShowNoExpression = 62,

    /// <summary><c>&lt;Transition&gt;</c> expects exactly one child (upstream DOM <c>X_TRANSITION_INVALID_CHILDREN</c>).</summary>
    XTransitionInvalidChildren = 63,

    /// <summary>A side-effect tag (<c>&lt;script&gt;</c>/<c>&lt;style&gt;</c>) was ignored (upstream DOM <c>X_IGNORED_SIDE_EFFECT_TAG</c>).</summary>
    XIgnoredSideEffectTag = 64,

    /// <summary>The reserved value one past the last defined DOM code (upstream DOM <c>__EXTEND_POINT__</c>).</summary>
    DomExtendPoint = 65,

    // ---- Vuecs-specific expression/scope analysis codes ([V01.01.05.04], no upstream counterpart) ----
    //
    // These extend the catalog past both upstream sentinels, exactly as @vue/compiler-dom's DOMErrorCodes
    // extended core's __EXTEND_POINT__. They have no vuejs/core equivalent because they encode a divergence
    // C# forces: with no runtime Proxy fallback, a template identifier that resolves to nothing real is an
    // error the compiler must surface, where Vue would silently emit a _ctx member access.

    /// <summary>
    /// A template identifier resolved to neither a template-local, an allowed global, nor a known component
    /// binding, under strict binding metadata (<see cref="BindingMetadata.ReportsUnresolvedIdentifiers"/>).
    /// Vuecs-specific; no upstream counterpart.
    /// </summary>
    XVuecsUnresolvedIdentifier = 66,
}
