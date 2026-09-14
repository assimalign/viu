using System;
using System.IO;

using Shouldly;
using Xunit;

namespace Assimalign.Viu.Browser.Router.Tests;

// Pins the browser-owned half of [RTR-10]. HTML defines scrollRestoration and its default auto
// mode (https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-history-scroll-restoration-dev);
// CSSOM View defines the scroll offsets captured in the saved-position ledger
// (https://drafts.csswg.org/cssom-view/#dom-window-scrollx).
public sealed class HistoryJavaScriptContractTests
{
    [Fact]
    public void SnapshotAndPopState_SerializedSuffix_PreservesEmptyQueryAndFragmentDelimiters()
    {
        // [RTR-12]: Location.search/hash erase bare delimiters, so the edge must read href.
        // External URL format: https://url.spec.whatwg.org/#url-parsing.
        string source = ReadHistoryModule();

        source.ShouldContain("const href = window.location.href");
        source.ShouldContain("const fragmentPosition = href.indexOf('#')");
        source.ShouldContain("const queryPosition = href.indexOf('?')");
        source.ShouldContain("queryPosition >= 0 && (fragmentPosition < 0 || queryPosition < fragmentPosition)");
        source.ShouldContain("href.slice(queryPosition, fragmentPosition < 0 ? href.length : fragmentPosition)");
        source.ShouldContain("hash: fragmentPosition >= 0 ? href.slice(fragmentPosition) : ''");
        source.ShouldNotContain("window.location.search,");
        source.ShouldNotContain("window.location.hash,");

        int snapshot = source.IndexOf("readSnapshot: () =>", StringComparison.Ordinal);
        int snapshotRead = source.IndexOf("const suffix = readLocationSuffix()", snapshot, StringComparison.Ordinal);
        int snapshotReturn = source.IndexOf("return [", snapshot, StringComparison.Ordinal);
        snapshotRead.ShouldBeGreaterThan(snapshot);
        snapshotRead.ShouldBeLessThan(snapshotReturn);

        int handler = source.IndexOf("const handler = (event) =>", StringComparison.Ordinal);
        int popRead = source.IndexOf("const suffix = readLocationSuffix()", handler, StringComparison.Ordinal);
        int dispatch = source.IndexOf("dispatchPopState(", handler, StringComparison.Ordinal);
        popRead.ShouldBeGreaterThan(handler);
        popRead.ShouldBeLessThan(dispatch);
    }

    [Fact]
    public void ScrollHandling_UsesManualRestorationUntilLastSubscriptionDisposes()
    {
        string source = ReadHistoryModule();

        source.ShouldContain("function activateScrollHandling()");
        source.ShouldContain("function deactivateScrollHandling()");
        source.ShouldContain("window.history.scrollRestoration = 'manual'");
        source.ShouldContain("window.history.scrollRestoration = previousScrollRestoration");
        source.ShouldContain("if (popstateListeners.size !== 0)");

        int subscribe = source.IndexOf("subscribe: (subscriptionIdentifier)", StringComparison.Ordinal);
        int activate = source.IndexOf("activateScrollHandling()", subscribe, StringComparison.Ordinal);
        int register = source.IndexOf(
            "popstateListeners.set(subscriptionIdentifier",
            subscribe,
            StringComparison.Ordinal);
        activate.ShouldBeGreaterThan(subscribe);
        activate.ShouldBeLessThan(register);

        int unsubscribe = source.IndexOf("unsubscribe: (subscriptionIdentifier)", StringComparison.Ordinal);
        int remove = source.IndexOf(
            "popstateListeners.delete(subscriptionIdentifier)",
            unsubscribe,
            StringComparison.Ordinal);
        int deactivate = source.IndexOf(
            "deactivateScrollHandling()",
            unsubscribe,
            StringComparison.Ordinal);
        remove.ShouldBeGreaterThan(unsubscribe);
        deactivate.ShouldBeGreaterThan(remove);
    }

    private static string ReadHistoryModule()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "src", "wwwroot", "viu-history.js");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate Browser.Router's shipping viu-history.js from the test output.");
    }
}
