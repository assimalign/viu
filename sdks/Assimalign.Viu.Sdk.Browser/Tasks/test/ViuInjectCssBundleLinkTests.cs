using Shouldly;
using Xunit;

using Assimalign.Viu.Sdk.Browser.Tasks;

namespace Assimalign.Viu.Sdk.Browser.Tasks.Tests;

public sealed class ViuInjectCssBundleLinkTests
{
    [Fact]
    public void InjectStylesheetLink_HeadCloseTag_InsertsLinkImmediatelyBeforeTag()
    {
        const string html = "<html><head><title>Viu</title></head><body></body></html>";

        var transformed = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "App.viu.css",
            "App.viu.css",
            out var injected);

        injected.ShouldBeTrue();
        transformed.ShouldBe(
            "<html><head><title>Viu</title><link rel=\"stylesheet\" href=\"App.viu.css\" />\n" +
            "</head><body></body></html>");
    }

    [Fact]
    public void InjectStylesheetLink_ExistingHrefReference_DoesNotInjectDuplicate()
    {
        const string html =
            "<head>\n<link rel=\"stylesheet\" href=\"./assets/App.viu.css?v=1\" />\n</head>";

        var transformed = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "App.fingerprint.viu.css",
            "App.viu.css",
            out var injected);

        injected.ShouldBeFalse();
        transformed.ShouldBe(html);
    }

    [Fact]
    public void InjectStylesheetLink_SuffixCollidingLibraryBundleNames_InjectsBothLinks()
    {
        const string html = "<html><head></head><body></body></html>";

        var withContoso = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "_content/Contoso.Ui/Contoso.Ui.viu.css",
            "Contoso.Ui.viu.css",
            out var contosoInjected);
        var withBoth = CssBundleLinkInjector.InjectStylesheetLink(
            withContoso,
            "_content/Ui/Ui.viu.css",
            "Ui.viu.css",
            out var suffixBundleInjected);

        contosoInjected.ShouldBeTrue();
        suffixBundleInjected.ShouldBeTrue();
        withBoth.ShouldContain(
            "href=\"_content/Contoso.Ui/Contoso.Ui.viu.css\"");
        withBoth.ShouldContain("href=\"_content/Ui/Ui.viu.css\"");
    }

    [Fact]
    public void InjectStylesheetLink_LibraryBundleEndingInApplicationBundleName_InjectsApplicationLink()
    {
        const string html = "<html><head></head><body></body></html>";

        var withLibrary = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "_content/WebApp/WebApp.viu.css",
            "WebApp.viu.css",
            out var libraryInjected);
        var withApplication = CssBundleLinkInjector.InjectStylesheetLink(
            withLibrary,
            "App.viu.css",
            "App.viu.css",
            out var applicationInjected);

        libraryInjected.ShouldBeTrue();
        applicationInjected.ShouldBeTrue();
        withApplication.ShouldContain("href=\"_content/WebApp/WebApp.viu.css\"");
        withApplication.ShouldContain("href=\"App.viu.css\"");
    }

    [Fact]
    public void InjectStylesheetLink_HandAuthoredHref_SuppressesOnlyMatchingBundle()
    {
        const string html =
            "<head>\n<link rel=\"stylesheet\" " +
            "href=\"./assets/Contoso.Ui.viu.css?v=1\" />\n</head>";

        var matching = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "_content/Contoso.Ui/Contoso.Ui.viu.css",
            "Contoso.Ui.viu.css",
            out var matchingInjected);
        var withSuffixBundle = CssBundleLinkInjector.InjectStylesheetLink(
            matching,
            "_content/Ui/Ui.viu.css",
            "Ui.viu.css",
            out var suffixBundleInjected);

        matchingInjected.ShouldBeFalse();
        matching.ShouldBe(html);
        suffixBundleInjected.ShouldBeTrue();
        withSuffixBundle.ShouldContain("href=\"_content/Ui/Ui.viu.css\"");
    }

    [Fact]
    public void InjectStylesheetLink_BareCommentMention_StillInjectsLink()
    {
        const string html = "<head>\n<!-- App.viu.css is generated. -->\n</head>";

        var transformed = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "App.viu.css",
            "App.viu.css",
            out var injected);

        injected.ShouldBeTrue();
        transformed.ShouldContain("<link rel=\"stylesheet\" href=\"App.viu.css\" />");
    }

    [Fact]
    public void InjectStylesheetLink_LfDocument_PreservesNewlinesAndIndentation()
    {
        const string html = "<head>\n    </head>\n";

        var transformed = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "App.viu.css",
            "App.viu.css",
            out var injected);

        injected.ShouldBeTrue();
        transformed.ShouldBe(
            "<head>\n    <link rel=\"stylesheet\" href=\"App.viu.css\" />\n    </head>\n");
        transformed.ShouldNotContain("\r\n");
    }

    [Fact]
    public void InjectStylesheetLink_CrlfDocument_PreservesNewlinesAndIndentation()
    {
        const string html = "<head>\r\n\t</head>\r\n";

        var transformed = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "App.viu.css",
            "App.viu.css",
            out var injected);

        injected.ShouldBeTrue();
        transformed.ShouldBe(
            "<head>\r\n\t<link rel=\"stylesheet\" href=\"App.viu.css\" />\r\n\t</head>\r\n");
    }

    [Fact]
    public void InjectStylesheetLink_MissingHeadCloseTag_ReturnsInputUnchanged()
    {
        const string html = "<html><body>Viu</body></html>";

        var transformed = CssBundleLinkInjector.InjectStylesheetLink(
            html,
            "App.viu.css",
            "App.viu.css",
            out var injected);

        injected.ShouldBeFalse();
        transformed.ShouldBe(html);
    }
}
