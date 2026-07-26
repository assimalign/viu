using Shouldly;

using Xunit;

namespace Assimalign.Viu.VisualStudio;

public class ViuLanguageServerProviderTests
{
    [Fact]
    public void VueCompatibilityDocumentType_UsesRequiredCompatibilityIdentity()
    {
        var documentType =
            ViuLanguageServerProvider.VueCompatibilityDocumentType;

        documentType.Name.ShouldBe("viu-vue");
        documentType.FileExtensions.ShouldBe([".vue"]);
    }
}
