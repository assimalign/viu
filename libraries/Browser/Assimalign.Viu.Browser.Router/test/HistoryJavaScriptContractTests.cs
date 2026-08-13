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
