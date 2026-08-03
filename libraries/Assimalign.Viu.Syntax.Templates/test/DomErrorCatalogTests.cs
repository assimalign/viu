using Shouldly;

using Xunit;

namespace Assimalign.Viu.Syntax.Templates;

// Pins the DOM error-code numbering decision for [V01.01.05.03]. Viu keeps the core and DOM diagnostics
// in ONE enum, so a diagnostic consumer never switches on two catalog types. A single C# enum cannot give
// two members the same value, and the ExtendPoint sentinel's 53 is pinned by ErrorTests, so the DOM band
// starts at 54, immediately after the preserved sentinel. Both boundaries are frozen.
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
