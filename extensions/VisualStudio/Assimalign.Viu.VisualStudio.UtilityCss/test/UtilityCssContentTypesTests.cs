using Shouldly;

using Xunit;

using Assimalign.Viu.VisualStudio.UtilityCss;

namespace Assimalign.Viu.VisualStudio.UtilityCss.Tests;

public sealed class UtilityCssContentTypesTests
{
    [Fact]
    public void HtmlContentTypes_ActivationContract_UsesStandaloneAndDelegationTypes()
    {
        UtilityCssContentTypes.Html.ShouldBe("HTML");
        UtilityCssContentTypes.HtmlDelegation.ShouldBe("html-delegation");
    }
}
