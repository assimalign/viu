using Shouldly;

using Xunit;

namespace Assimalign.Vue.Syntax.Templates;

// Pins the DOM error-code numbering decision for [V01.01.05.03]. Vuecs merges compiler-core and
// compiler-dom into one error enum; upstream's DOMErrorCodes reuses core's __EXTEND_POINT__ value (53) as
// its first code, which one C# enum cannot do while keeping ExtendPoint == 53 (pinned by ErrorTests). The
// DOM codes are therefore appended after the preserved sentinel (each = its upstream DOMErrorCodes value + 1).
public class DomErrorCatalogTests
{
    [Fact]
    public void DomErrorCatalog_ExtendsCoreAfterThePreservedExtendPoint()
    {
        ((int)CompilerErrorCode.ExtendPoint).ShouldBe(53);
        ((int)CompilerErrorCode.XVHtmlNoExpression).ShouldBe(54);
        ((int)CompilerErrorCode.XVHtmlWithChildren).ShouldBe(55);
        ((int)CompilerErrorCode.XVModelOnInvalidElement).ShouldBe(58);
        ((int)CompilerErrorCode.XVShowNoExpression).ShouldBe(62);
        ((int)CompilerErrorCode.XIgnoredSideEffectTag).ShouldBe(64);
        ((int)CompilerErrorCode.DomExtendPoint).ShouldBe(65);
    }

    [Fact]
    public void DomErrorMessages_MatchUpstreamVerbatim()
    {
        CompilerErrorMessages.GetMessage(CompilerErrorCode.XVHtmlWithChildren)
            .ShouldBe("v-html will override element children.");
        CompilerErrorMessages.GetMessage(CompilerErrorCode.XVModelOnInvalidElement)
            .ShouldBe("v-model can only be used on <input>, <textarea> and <select> elements.");
        CompilerErrorMessages.GetMessage(CompilerErrorCode.XVShowNoExpression)
            .ShouldBe("v-show is missing expression.");
    }
}
