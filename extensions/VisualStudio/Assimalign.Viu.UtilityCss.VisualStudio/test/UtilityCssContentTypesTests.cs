using Shouldly;

using Xunit;

using Assimalign.Viu.UtilityCss.VisualStudio;

namespace Assimalign.Viu.UtilityCss.VisualStudio.Tests;

public sealed class UtilityCssContentTypesTests
{
    // The installed Web Tools provider assigns this type to top-level .html/.htm buffers. The
    // contained htmlLSPClient type is not a host document, while classic HTML also matches the
    // LegacyRazor hierarchy and would leak this client into the explicitly excluded .razor scope.
    [Fact]
    public void HtmlDelegation_RoutingContract_UsesTopLevelModernHtmlContentType()
    {
        UtilityCssContentTypes.HtmlDelegation.ShouldBe("html-delegation");
    }
}
