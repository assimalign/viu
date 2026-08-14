using Shouldly;

using Xunit;

using Assimalign.Viu.UtilityCss.VisualStudio;

namespace Assimalign.Viu.UtilityCss.VisualStudio.Tests;

public sealed class UtilityCssContentTypesTests
{
    [Fact]
    public void HtmlContentTypes_ActivationContract_UsesStandaloneAndDelegationTypes()
    {
        UtilityCssContentTypes.Html.ShouldBe("HTML");
        UtilityCssContentTypes.HtmlDelegation.ShouldBe("html-delegation");
    }
}
