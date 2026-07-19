using System.Collections.Generic;

namespace Assimalign.Vue.Syntax.Templates;

/// <summary>
/// The human-readable messages for each <see cref="CompilerErrorCode"/>. The C# port of Vue 3.5's
/// <c>errorMessages</c> map (<c>@vue/compiler-core</c> <c>errors.ts</c>); the strings are kept verbatim
/// so diagnostics read identically to upstream.
/// </summary>
internal static class CompilerErrorMessages
{
    private static readonly Dictionary<CompilerErrorCode, string> Messages = new()
    {
        // parse errors
        [CompilerErrorCode.AbruptClosingOfEmptyComment] = "Illegal comment.",
        [CompilerErrorCode.CdataInHtmlContent] = "CDATA section is allowed only in XML context.",
        [CompilerErrorCode.DuplicateAttribute] = "Duplicate attribute.",
        [CompilerErrorCode.EndTagWithAttributes] = "End tag cannot have attributes.",
        [CompilerErrorCode.EndTagWithTrailingSolidus] = "Illegal '/' in tags.",
        [CompilerErrorCode.EofBeforeTagName] = "Unexpected EOF in tag.",
        [CompilerErrorCode.EofInCdata] = "Unexpected EOF in CDATA section.",
        [CompilerErrorCode.EofInComment] = "Unexpected EOF in comment.",
        [CompilerErrorCode.EofInScriptHtmlCommentLikeText] = "Unexpected EOF in script.",
        [CompilerErrorCode.EofInTag] = "Unexpected EOF in tag.",
        [CompilerErrorCode.IncorrectlyClosedComment] = "Incorrectly closed comment.",
        [CompilerErrorCode.IncorrectlyOpenedComment] = "Incorrectly opened comment.",
        [CompilerErrorCode.InvalidFirstCharacterOfTagName] = "Illegal tag name. Use '&lt;' to print '<'.",
        [CompilerErrorCode.MissingAttributeValue] = "Attribute value was expected.",
        [CompilerErrorCode.MissingEndTagName] = "End tag name was expected.",
        [CompilerErrorCode.MissingWhitespaceBetweenAttributes] = "Whitespace was expected.",
        [CompilerErrorCode.NestedComment] = "Unexpected '<!--' in comment.",
        [CompilerErrorCode.UnexpectedCharacterInAttributeName] =
            "Attribute name cannot contain U+0022 (\"), U+0027 ('), and U+003C (<).",
        [CompilerErrorCode.UnexpectedCharacterInUnquotedAttributeValue] =
            "Unquoted attribute value cannot contain U+0022 (\"), U+0027 ('), U+003C (<), U+003D (=), and U+0060 (`).",
        [CompilerErrorCode.UnexpectedEqualsSignBeforeAttributeName] = "Attribute name cannot start with '='.",
        [CompilerErrorCode.UnexpectedQuestionMarkInsteadOfTagName] = "'<?' is allowed only in XML context.",
        [CompilerErrorCode.UnexpectedNullCharacter] = "Unexpected null character.",
        [CompilerErrorCode.UnexpectedSolidusInTag] = "Illegal '/' in tags.",

        // Vue-specific parse errors
        [CompilerErrorCode.XInvalidEndTag] = "Invalid end tag.",
        [CompilerErrorCode.XMissingEndTag] = "Element is missing end tag.",
        [CompilerErrorCode.XMissingInterpolationEnd] = "Interpolation end sign was not found.",
        [CompilerErrorCode.XMissingDynamicDirectiveArgumentEnd] =
            "End bracket for dynamic directive argument was not found. " +
            "Note that dynamic directive argument cannot contain spaces.",
        [CompilerErrorCode.XMissingDirectiveName] = "Legal directive name was expected.",

        // transform errors
        [CompilerErrorCode.XVIfNoExpression] = "v-if/v-else-if is missing expression.",
        [CompilerErrorCode.XVIfSameKey] = "v-if/else branches must use unique keys.",
        [CompilerErrorCode.XVElseNoAdjacentIf] = "v-else/v-else-if has no adjacent v-if or v-else-if.",
        [CompilerErrorCode.XVForNoExpression] = "v-for is missing expression.",
        [CompilerErrorCode.XVForMalformedExpression] = "v-for has invalid expression.",
        [CompilerErrorCode.XVForTemplateKeyPlacement] = "<template v-for> key should be placed on the <template> tag.",
        [CompilerErrorCode.XVBindNoExpression] = "v-bind is missing expression.",
        [CompilerErrorCode.XVBindInvalidSameNameArgument] =
            "v-bind with same-name shorthand only allows static argument.",
        [CompilerErrorCode.XVOnNoExpression] = "v-on is missing expression.",
        [CompilerErrorCode.XVSlotUnexpectedDirectiveOnSlotOutlet] = "Unexpected custom directive on <slot> outlet.",
        [CompilerErrorCode.XVSlotMixedSlotUsage] =
            "Mixed v-slot usage on both the component and nested <template>. " +
            "When there are multiple named slots, all slots should use <template> " +
            "syntax to avoid scope ambiguity.",
        [CompilerErrorCode.XVSlotDuplicateSlotNames] = "Duplicate slot names found. ",
        [CompilerErrorCode.XVSlotExtraneousDefaultSlotChildren] =
            "Extraneous children found when component already has explicitly named " +
            "default slot. These children will be ignored.",
        [CompilerErrorCode.XVSlotMisplaced] = "v-slot can only be used on components or <template> tags.",
        [CompilerErrorCode.XVModelNoExpression] = "v-model is missing expression.",
        [CompilerErrorCode.XVModelMalformedExpression] = "v-model value must be a valid JavaScript member expression.",
        [CompilerErrorCode.XVModelOnScopeVariable] =
            "v-model cannot be used on v-for or v-slot scope variables because they are not writable.",
        [CompilerErrorCode.XVModelOnProps] =
            "v-model cannot be used on a prop, because local prop bindings are not writable.\n" +
            "Use a v-bind binding combined with a v-on listener that emits update:x event instead.",
        [CompilerErrorCode.XInvalidExpression] = "Error parsing JavaScript expression: ",
        [CompilerErrorCode.XKeepAliveInvalidChildren] = "<KeepAlive> expects exactly one child component.",

        // generic errors
        [CompilerErrorCode.XPrefixIdNotSupported] =
            "\"prefixIdentifiers\" option is not supported in this build of compiler.",
        [CompilerErrorCode.XModuleModeNotSupported] = "ES module mode is not supported in this build of compiler.",
        [CompilerErrorCode.XCacheHandlerNotSupported] =
            "\"cacheHandlers\" option is only supported when the \"prefixIdentifiers\" option is enabled.",
        [CompilerErrorCode.XScopeIdNotSupported] = "\"scopeId\" option is only supported in module mode.",
        [CompilerErrorCode.XVnodeHooks] =
            "@vnode-* hooks in templates are no longer supported. Use the vue: prefix instead. " +
            "For example, @vnode-mounted should be changed to @vue:mounted. " +
            "@vnode-* hooks support has been removed in 3.4.",

        // DOM directive transform errors (verbatim from @vue/compiler-dom errors.ts DOMErrorMessages)
        [CompilerErrorCode.XVHtmlNoExpression] = "v-html is missing expression.",
        [CompilerErrorCode.XVHtmlWithChildren] = "v-html will override element children.",
        [CompilerErrorCode.XVTextNoExpression] = "v-text is missing expression.",
        [CompilerErrorCode.XVTextWithChildren] = "v-text will override element children.",
        [CompilerErrorCode.XVModelOnInvalidElement] =
            "v-model can only be used on <input>, <textarea> and <select> elements.",
        [CompilerErrorCode.XVModelArgumentOnElement] = "v-model argument is not supported on plain elements.",
        [CompilerErrorCode.XVModelOnFileInputElement] =
            "v-model cannot be used on file inputs since they are read-only. Use a v-on:change listener instead.",
        [CompilerErrorCode.XVModelUnnecessaryValue] =
            "Unnecessary value binding used alongside v-model. It will interfere with v-model's behavior.",
        [CompilerErrorCode.XVShowNoExpression] = "v-show is missing expression.",
        [CompilerErrorCode.XTransitionInvalidChildren] =
            "<Transition> expects exactly one child element or component.",
        [CompilerErrorCode.XIgnoredSideEffectTag] =
            "Tags with side effect (<script> and <style>) are ignored in client component templates.",

        // Vuecs-specific expression/scope analysis messages ([V01.01.05.04]); the offending name is appended.
        [CompilerErrorCode.XVuecsUnresolvedIdentifier] =
            "Cannot resolve template identifier against any component binding, template scope variable, or " +
            "allowed global: ",

        // The offending "'<module>' has no member '<member>'." detail is appended by the reporter ([V01.01.05.04.01]).
        [CompilerErrorCode.XVuecsUnknownCssModuleMember] = "Unknown CSS module member: ",
    };

    /// <summary>Gets the message for <paramref name="code"/>, or an empty string when none is defined.</summary>
    /// <param name="code">The error code.</param>
    public static string GetMessage(CompilerErrorCode code)
        => Messages.TryGetValue(code, out var message) ? message : string.Empty;
}
